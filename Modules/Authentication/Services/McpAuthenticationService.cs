using System.Collections.Concurrent;
using HaloPsaMcp.Modules.HaloPsa.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Service to manage user token storage and validation for MCP authentication
/// </summary>
internal class McpAuthenticationService {
    private const string UserTokenKey = "mcp.user_token";
    private const string UserRefreshTokenKey = "mcp.user_refresh_token";
    private const string UserTokenExpiryKey = "mcp.user_token_expiry";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HaloPsaConfig _haloPsaConfig;
    private readonly ILogger<McpAuthenticationService> _logger;
    private static readonly ConcurrentDictionary<string, long> ValidatedTokenCache = new();
    private const int TokenValidationCacheTtlMs = 5 * 60 * 1000;

    public McpAuthenticationService(
        IHttpClientFactory httpClientFactory,
        HaloPsaConfig haloPsaConfig,
        ILogger<McpAuthenticationService> logger) {
        _httpClientFactory = httpClientFactory;
        _haloPsaConfig = haloPsaConfig;
        _logger = logger;
    }

    /// <summary>
    /// Validate a token against HaloPSA API (with 5-minute caching).
    /// </summary>
    public async Task<bool> ValidateTokenAsync(string token) {
        var (isValid, _) = await ValidateTokenWithCacheInfoAsync(token).ConfigureAwait(false);
        return isValid;
    }

    /// <summary>
    /// Validate a token and return whether the result came from the local cache.
    /// </summary>
    public async Task<(bool IsValid, bool FromCache)> ValidateTokenWithCacheInfoAsync(string token) {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (ValidatedTokenCache.TryGetValue(token, out var cachedExpiry) && now < cachedExpiry) {
            return (true, true);
        }

        try {
            var httpClient = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_haloPsaConfig.Url}/api/Agent/me?tenant={Uri.EscapeDataString(_haloPsaConfig.GetTenant())}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            if (response.IsSuccessStatusCode) {
                ValidatedTokenCache[token] = now + TokenValidationCacheTtlMs;
                _logger.LogDebug("Token validated against HaloPSA (live check)");
                return (true, false);
            }

            _logger.LogWarning("Token validation failed — HaloPSA returned {StatusCode}", response.StatusCode);
            ValidatedTokenCache.TryRemove(token, out _);
            return (false, false);
        } catch (Exception ex) {
            _logger.LogError(ex, "Token validation request failed");
            ValidatedTokenCache.TryRemove(token, out _);
            return (false, false);
        }
    }

    /// <summary>
    /// Store user token information in HttpContext
    /// </summary>
    public void StoreTokenInContext(HttpContext context, string accessToken, string? refreshToken, long? expiresAt) {
        context.Items[UserTokenKey] = accessToken;
        context.Items[UserRefreshTokenKey] = refreshToken;
        context.Items[UserTokenExpiryKey] = expiresAt;
    }

    /// <summary>
    /// Retrieve user token from HttpContext
    /// </summary>
    public string? GetTokenFromContext(HttpContext context) {
        return context.Items[UserTokenKey] as string;
    }

    /// <summary>
    /// Retrieve user refresh token from HttpContext
    /// </summary>
    public string? GetRefreshTokenFromContext(HttpContext context) {
        return context.Items[UserRefreshTokenKey] as string;
    }

    /// <summary>
    /// Retrieve user token expiry from HttpContext
    /// </summary>
    public long? GetTokenExpiryFromContext(HttpContext context) {
        return context.Items[UserTokenExpiryKey] as long?;
    }

    /// <summary>
    /// Invalidate a token in the cache
    /// </summary>
    public void InvalidateToken(string token) {
        ValidatedTokenCache.TryRemove(token, out _);
    }
}
