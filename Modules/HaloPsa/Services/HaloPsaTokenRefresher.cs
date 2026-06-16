using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.HaloPsa.Models;
using StackExchange.Redis;

namespace HaloPsaMcp.Modules.HaloPsa.Services;

/// <summary>
/// Serializes HaloPSA OAuth refresh per MCP session so concurrent tool calls (or
/// overlapping MCP OAuth refresh grants) cannot burn the same single-use refresh token.
/// </summary>
public sealed class HaloPsaTokenRefresher {
    private const long RefreshSkewMs = 60_000;

    private readonly HaloPsaConfig _baseConfig;
    private readonly ITokenStore _tokenStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<HaloPsaTokenRefresher> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public HaloPsaTokenRefresher(
        HaloPsaConfig baseConfig,
        ITokenStore tokenStore,
        IHttpClientFactory httpClientFactory,
        ILogger<HaloPsaTokenRefresher> logger,
        IConnectionMultiplexer? redis = null) {
        _baseConfig = baseConfig;
        _tokenStore = tokenStore;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _redis = redis;
    }

    internal static bool NeedsRefresh(long expiresAtMs, long? nowMs = null) {
        var now = nowMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now >= expiresAtMs - RefreshSkewMs;
    }

    /// <summary>
    /// Returns fresh HaloPSA tokens for the session, refreshing upstream only when needed.
    /// </summary>
    public async Task<(string AccessToken, string RefreshToken, long ExpiresAt)> EnsureFreshAsync(
        string sessionLockKey,
        string? mcpSessionToken,
        string accessToken,
        string refreshToken,
        long expiresAt,
        Action<string, string, long>? onRefreshed,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(refreshToken)) {
            return (accessToken, refreshToken, expiresAt);
        }
        if (!NeedsRefresh(expiresAt)) {
            return (accessToken, refreshToken, expiresAt);
        }

        await using var distributedLock = await AcquireDistributedLockAsync(sessionLockKey, cancellationToken)
            .ConfigureAwait(false);
        var gate = _locks.GetOrAdd(sessionLockKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (!string.IsNullOrEmpty(mcpSessionToken)) {
                var stored = _tokenStore.GetToken(mcpSessionToken);
                if (stored is not null) {
                    accessToken = stored.AccessToken;
                    refreshToken = stored.RefreshToken;
                    expiresAt = stored.ExpiresAt;
                    if (!NeedsRefresh(expiresAt)) {
                        onRefreshed?.Invoke(accessToken, refreshToken, expiresAt);
                        return (accessToken, refreshToken, expiresAt);
                    }
                }
            }

            if (!NeedsRefresh(expiresAt)) {
                return (accessToken, refreshToken, expiresAt);
            }

            try {
                var refreshed = await RefreshWithHaloPsaAsync(refreshToken, cancellationToken).ConfigureAwait(false);
                onRefreshed?.Invoke(refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresAt);
                if (!string.IsNullOrEmpty(mcpSessionToken)) {
                    await _tokenStore.UpdateSessionTokensAsync(
                        mcpSessionToken,
                        refreshed.AccessToken,
                        refreshed.RefreshToken,
                        refreshed.ExpiresAt).ConfigureAwait(false);
                }
                return refreshed;
            } catch (HttpRequestException ex) when (HaloPsaResponseSanitizer.IsTokenRefreshFailure(ex)) {
                await InvalidateSessionAfterRefreshFailureAsync(mcpSessionToken).ConfigureAwait(false);
                throw;
            }
        } finally {
            gate.Release();
        }
    }

    private async Task InvalidateSessionAfterRefreshFailureAsync(string? mcpSessionToken) {
        if (string.IsNullOrEmpty(mcpSessionToken)) {
            return;
        }
        if (await _tokenStore.InvalidateSessionAsync(mcpSessionToken).ConfigureAwait(false)) {
            _logger.LogWarning(
                "MCP session removed after HaloPSA refresh failure | mcp={Hint}",
                SecretRedactor.Hint(mcpSessionToken));
        }
    }

    /// <summary>
    /// Refresh HaloPSA tokens for an MCP session during OAuth refresh_token grant handling.
    /// Caller is responsible for MCP refresh-token rotation after this returns.
    /// </summary>
    public Task<(string AccessToken, string RefreshToken, long ExpiresAt)> RefreshSessionAsync(
        string mcpAccessToken,
        UserTokenEntry session,
        CancellationToken cancellationToken = default) =>
        EnsureFreshAsync(
            mcpAccessToken,
            mcpAccessToken,
            session.AccessToken,
            session.RefreshToken,
            session.ExpiresAt,
            onRefreshed: null,
            cancellationToken);

    private async Task<(string AccessToken, string RefreshToken, long ExpiresAt)> RefreshWithHaloPsaAsync(
        string refreshToken,
        CancellationToken cancellationToken) {
        _logger.LogInformation("Refreshing expired HaloPSA token | refresh={Hint}", SecretRedactor.Hint(refreshToken));

        var tokenUrl = $"{_baseConfig.Url}/auth/token?tenant={Uri.EscapeDataString(_baseConfig.GetTenant())}";
        var parameters = new Dictionary<string, string> {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _baseConfig.ClientId,
            ["refresh_token"] = refreshToken
        };
        if (!string.IsNullOrEmpty(_baseConfig.ClientSecret)) {
            parameters["client_secret"] = _baseConfig.ClientSecret;
        }

        var http = _httpClientFactory.CreateClient("halopsa");
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        sw.Stop();

        if (!response.IsSuccessStatusCode) {
            var errorBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            HaloPsaResponseSanitizer.LogFailure(
                _logger, "token refresh", response.StatusCode, errorBytes.Length, sw.ElapsedMilliseconds);
            throw HaloPsaResponseSanitizer.ApiException("Token refresh", response.StatusCode);
        }

        _logger.LogInformation("Token refresh succeeded | elapsed={ElapsedMs}ms", sw.ElapsedMilliseconds);

        var tokenResponse = await JsonSerializer.DeserializeAsync<TokenResponse>(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid token response");

        var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (tokenResponse.expires_in - 60) * 1000;
        return (
            tokenResponse.access_token,
            tokenResponse.refresh_token ?? refreshToken,
            expiresAt);
    }

    private async Task<IAsyncDisposable?> AcquireDistributedLockAsync(
        string sessionKey,
        CancellationToken cancellationToken) {
        if (_redis is null) {
            return null;
        }

        var db = _redis.GetDatabase();
        var key = (RedisKey)$"halopsa:refresh-lock:{sessionKey}";
        var value = (RedisValue)Guid.NewGuid().ToString("N");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await db.LockTakeAsync(key, value, TimeSpan.FromSeconds(30)).ConfigureAwait(false)) {
                return new RedisRefreshLock(db, key, value, _logger, sessionKey);
            }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning(
            "Distributed refresh lock timeout; continuing with in-process lock | session={Hint}",
            SecretRedactor.Hint(sessionKey));
        return null;
    }

    private sealed class RedisRefreshLock(
        IDatabase db,
        RedisKey key,
        RedisValue value,
        ILogger logger,
        string sessionKey) : IAsyncDisposable {
        public async ValueTask DisposeAsync() {
            if (!await db.LockReleaseAsync(key, value).ConfigureAwait(false)) {
                logger.LogWarning(
                    "Distributed refresh lock release failed | session={Hint}",
                    SecretRedactor.Hint(sessionKey));
            }
        }
    }
}
