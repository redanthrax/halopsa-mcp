using System.Collections.Concurrent;
using HaloPsaMcp.Modules.Authentication.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Shared state manager for OAuth endpoints. Caches are bounded to defend
/// against memory-exhaustion attacks on /authorize and /callback.
/// </summary>
internal static class OAuthStateManager {
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

    private static void EnforceCap<T>(ConcurrentDictionary<string, T> dict, int cap) {
        if (dict.Count < cap) {
            return;
        }
        // Evict any entry with an Expires field via reflection-free approach: remove arbitrary head
        var keysToRemove = dict.Keys.Take(dict.Count - cap + 1).ToArray();
        foreach (var k in keysToRemove) {
            dict.TryRemove(k, out _);
        }
    }
}
