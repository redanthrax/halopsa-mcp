using System.Collections.Concurrent;
using HaloPsaMcp.Modules.Authentication.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Shared state manager for OAuth endpoints. Caches are bounded to defend
/// against memory-exhaustion attacks on /authorize and /callback.
/// </summary>
public static class OAuthStateManager {
    private const int MaxPending = 10_000;
    private const int MaxCompleted = 10_000;

    public static readonly ConcurrentDictionary<string, PendingAuth> PendingAuths = new();
    public static readonly ConcurrentDictionary<string, CompletedAuth> CompletedAuths = new();

    public static void AddPending(string key, PendingAuth value) {
        EnforceCap(PendingAuths, MaxPending);
        PendingAuths[key] = value;
    }

    public static void AddCompleted(string key, CompletedAuth value) {
        EnforceCap(CompletedAuths, MaxCompleted);
        CompletedAuths[key] = value;
    }

    public static void CleanExpiredEntries() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var kvp in PendingAuths.Where(x => now > x.Value.Expires).ToArray()) {
            PendingAuths.TryRemove(kvp.Key, out _);
        }
        foreach (var kvp in CompletedAuths.Where(x => now > x.Value.Expires).ToArray()) {
            CompletedAuths.TryRemove(kvp.Key, out _);
        }
    }

    private static void EnforceCap<T>(ConcurrentDictionary<string, T> dict, int cap)
        where T : IExpiring {
        if (dict.Count < cap) {
            return;
        }
        // Evict oldest-expiring entries first so non-expired flows in progress
        // are preserved as long as possible under load.
        var keysToRemove = dict
            .OrderBy(kvp => kvp.Value.Expires)
            .Take(dict.Count - cap + 1)
            .Select(kvp => kvp.Key)
            .ToArray();
        foreach (var k in keysToRemove) {
            dict.TryRemove(k, out _);
        }
    }
}

public interface IExpiring {
    long Expires { get; }
}
