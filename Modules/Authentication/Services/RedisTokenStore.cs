using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using StackExchange.Redis;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Redis-backed <see cref="ITokenStore"/> for multi-replica HTTP/Kubernetes deployments.
/// Session payloads are stored as JSON on a private Redis instance (TLS + network policy recommended).
/// </summary>
public sealed class RedisTokenStore : ITokenStore {
    private const int MaxSessions = 5000;
    private const string SessionKeyPrefix = "halopsa:mcp:session:";
    private const string RefreshKeyPrefix = "halopsa:mcp:refresh:";
    private const string LatestSessionKey = "halopsa:mcp:latest";
    private const string SessionIndexKey = "halopsa:mcp:session-index";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTokenStore> _logger;
    private readonly IDatabase _db;

    public string Backend => "redis";

    public RedisTokenStore(IConnectionMultiplexer redis, ILogger<RedisTokenStore> logger) {
        _redis = redis;
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public async Task<(string AccessToken, string RefreshToken)> CreateSessionAsync(
        string haloPsaAccess, string? haloPsaRefresh, long expiresAt) {
        await EnforceCapAsync().ConfigureAwait(false);

        var mcpToken = McpTokenGenerator.GenerateMcpToken();
        var mcpRefresh = McpTokenGenerator.GenerateMcpRefreshToken();
        var entry = new UserTokenEntry {
            AccessToken = haloPsaAccess,
            RefreshToken = haloPsaRefresh ?? string.Empty,
            ExpiresAt = expiresAt,
            McpRefreshToken = mcpRefresh
        };

        await WriteSessionAsync(mcpToken, mcpRefresh, entry).ConfigureAwait(false);
        await _db.StringSetAsync(LatestSessionKey, mcpToken).ConfigureAwait(false);

        _logger.LogInformation("MCP session created | mcpToken={Hint}", SecretRedactor.Hint(mcpToken));
        return (mcpToken, mcpRefresh);
    }

    public UserTokenEntry? GetToken(string mcpToken) {
        var raw = _db.StringGet(SessionKey(mcpToken));
        if (raw.IsNullOrEmpty) {
            return null;
        }
        return DeserializeEntry(raw!);
    }

    public UserTokenEntry? GetDefaultToken() {
        if (TokenStoreRuntime.DisableDefaultFallback) {
            return null;
        }
        var latest = _db.StringGet(LatestSessionKey);
        if (latest.IsNullOrEmpty) {
            return null;
        }
        return GetToken(latest!);
    }

    public bool IsValidSession(string mcpToken) {
        var entry = GetToken(mcpToken);
        if (entry is null) {
            return false;
        }
        return entry.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public async Task UpdateSessionTokensAsync(
        string mcpToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt) {
        var existing = GetToken(mcpToken);
        if (existing is null) {
            _logger.LogWarning("Refresh-update for unknown session | mcpToken={Hint}", SecretRedactor.Hint(mcpToken));
            return;
        }
        var updated = new UserTokenEntry {
            AccessToken = newHaloAccess,
            RefreshToken = newHaloRefresh,
            ExpiresAt = newExpiresAt,
            McpRefreshToken = existing.McpRefreshToken
        };
        await WriteSessionAsync(mcpToken, existing.McpRefreshToken!, updated).ConfigureAwait(false);
    }

    public KeyValuePair<string, UserTokenEntry>? FindByRefreshToken(string mcpRefresh) {
        var mcpToken = _db.StringGet(RefreshKey(mcpRefresh));
        if (mcpToken.IsNullOrEmpty) {
            return null;
        }
        var entry = GetToken(mcpToken!);
        if (entry is null) {
            return null;
        }
        return new KeyValuePair<string, UserTokenEntry>(mcpToken!, entry);
    }

    public async Task<string> RotateRefreshTokenAsync(
        string mcpAccessToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt) {
        var existing = GetToken(mcpAccessToken)
            ?? throw new InvalidOperationException("Cannot rotate refresh for unknown session");

        if (!string.IsNullOrEmpty(existing.McpRefreshToken)) {
            await _db.KeyDeleteAsync(RefreshKey(existing.McpRefreshToken)).ConfigureAwait(false);
        }

        var newMcpRefresh = McpTokenGenerator.GenerateMcpRefreshToken();
        var updated = new UserTokenEntry {
            AccessToken = newHaloAccess,
            RefreshToken = newHaloRefresh,
            ExpiresAt = newExpiresAt,
            McpRefreshToken = newMcpRefresh
        };
        await WriteSessionAsync(mcpAccessToken, newMcpRefresh, updated).ConfigureAwait(false);
        return newMcpRefresh;
    }

    public int PruneExpired() {
        // Redis TTL evicts keys; index cleanup happens opportunistically on cap enforcement.
        return 0;
    }

    public bool HasValidTokens() => ActiveSessionCount > 0;

    public int SessionCount => (int)_db.SetLength(SessionIndexKey);

    public int ActiveSessionCount {
        get {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var count = 0;
            foreach (var token in _db.SetMembers(SessionIndexKey)) {
                var entry = GetToken(token!);
                if (entry is not null && entry.ExpiresAt > now) {
                    count++;
                }
            }
            return count;
        }
    }

    public async ValueTask<bool> CheckHealthAsync(CancellationToken cancellationToken = default) {
        try {
            var latency = await _redis.GetDatabase().PingAsync().ConfigureAwait(false);
            return latency >= TimeSpan.Zero;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Redis health check failed");
            return false;
        }
    }

    public void Dispose() {
        _redis.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task WriteSessionAsync(string mcpToken, string mcpRefresh, UserTokenEntry entry) {
        var ttl = ExpiryTtl(entry.ExpiresAt);
        var payload = JsonSerializer.Serialize(entry, JsonOptions);
        var batch = _db.CreateBatch();
        var tasks = new List<Task> {
            batch.StringSetAsync(SessionKey(mcpToken), payload, ttl),
            batch.StringSetAsync(RefreshKey(mcpRefresh), mcpToken, ttl),
            batch.SetAddAsync(SessionIndexKey, mcpToken)
        };
        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task EnforceCapAsync() {
        var count = await _db.SetLengthAsync(SessionIndexKey).ConfigureAwait(false);
        if (count < MaxSessions) {
            return;
        }
        var victims = (int)(count - MaxSessions + 1);
        for (var i = 0; i < victims; i++) {
            var token = await _db.SetPopAsync(SessionIndexKey).ConfigureAwait(false);
            if (token.IsNullOrEmpty) {
                break;
            }
            await DeleteSessionAsync(token!).ConfigureAwait(false);
        }
    }

    private async Task DeleteSessionAsync(string mcpToken) {
        var entry = GetToken(mcpToken);
        var batch = _db.CreateBatch();
        var tasks = new List<Task> { batch.KeyDeleteAsync(SessionKey(mcpToken)) };
        if (entry?.McpRefreshToken is not null) {
            tasks.Add(batch.KeyDeleteAsync(RefreshKey(entry.McpRefreshToken)));
        }
        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static string SessionKey(string mcpToken) => SessionKeyPrefix + mcpToken;

    private static string RefreshKey(string mcpRefresh) => RefreshKeyPrefix + mcpRefresh;

    private static TimeSpan ExpiryTtl(long expiresAtUnixMs) {
        var remainingMs = expiresAtUnixMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (remainingMs <= 0) {
            return TimeSpan.FromMinutes(1);
        }
        return TimeSpan.FromMilliseconds(remainingMs);
    }

    private static UserTokenEntry? DeserializeEntry(string raw) {
        try {
            return JsonSerializer.Deserialize<UserTokenEntry>(raw, JsonOptions);
        } catch (JsonException) {
            return null;
        }
    }
}
