using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using StackExchange.Redis;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Redis-backed OAuth flow store for multi-replica HTTP deployments.
/// </summary>
public sealed class RedisOAuthFlowStore : IOAuthFlowStore {
    private const string PendingPrefix = "halopsa:oauth:pending:";
    private const string CompletedPrefix = "halopsa:oauth:completed:";
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IDatabase _db;
    private readonly ILogger<RedisOAuthFlowStore> _logger;

    public RedisOAuthFlowStore(IConnectionMultiplexer redis, ILogger<RedisOAuthFlowStore> logger) {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public int PendingCount => (int)_db.SetLength("halopsa:oauth:pending:index");
    public int CompletedCount => (int)_db.SetLength("halopsa:oauth:completed:index");

    public void AddPending(string key, PendingAuth value) {
        var redisKey = PendingPrefix + key;
        var ttl = ExpiryTtl(value.Expires);
        _db.StringSet(redisKey, JsonSerializer.Serialize(value, JsonOptions), ttl);
        _db.SetAdd("halopsa:oauth:pending:index", key);
    }

    public bool TryRemovePending(string key, out PendingAuth? value) {
        var redisKey = PendingPrefix + key;
        var raw = _db.StringGetDelete(redisKey);
        _db.SetRemove("halopsa:oauth:pending:index", key);
        if (raw.IsNullOrEmpty) {
            value = null;
            return false;
        }
        value = JsonSerializer.Deserialize<PendingAuth>((string)raw!, JsonOptions);
        return value is not null;
    }

    public void AddCompleted(string key, CompletedAuth value) {
        var redisKey = CompletedPrefix + key;
        var ttl = ExpiryTtl(value.Expires);
        _db.StringSet(redisKey, JsonSerializer.Serialize(value, JsonOptions), ttl);
        _db.SetAdd("halopsa:oauth:completed:index", key);
    }

    public bool TryRemoveCompleted(string key, out CompletedAuth? value) {
        var redisKey = CompletedPrefix + key;
        var raw = _db.StringGetDelete(redisKey);
        _db.SetRemove("halopsa:oauth:completed:index", key);
        if (raw.IsNullOrEmpty) {
            value = null;
            return false;
        }
        value = JsonSerializer.Deserialize<CompletedAuth>((string)raw!, JsonOptions);
        return value is not null;
    }

    public int CleanExpiredEntries() {
        // Redis TTL handles expiry; prune stale index members.
        var removed = 0;
        removed += PruneIndex("halopsa:oauth:pending:index", PendingPrefix);
        removed += PruneIndex("halopsa:oauth:completed:index", CompletedPrefix);
        if (removed > 0) {
            _logger.LogDebug("OAuth flow index prune | removed={Count}", removed);
        }
        return removed;
    }

    private int PruneIndex(RedisKey indexKey, string valuePrefix) {
        var removed = 0;
        foreach (var member in _db.SetMembers(indexKey)) {
            if (!_db.KeyExists(valuePrefix + member.ToString())) {
                _db.SetRemove(indexKey, member);
                removed++;
            }
        }
        return removed;
    }

    private static TimeSpan ExpiryTtl(long expiresUnixMs) {
        var remaining = expiresUnixMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return remaining > 0
            ? TimeSpan.FromMilliseconds(remaining)
            : TimeSpan.FromMinutes(1);
    }
}
