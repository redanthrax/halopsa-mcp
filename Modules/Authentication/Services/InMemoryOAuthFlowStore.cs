using System.Collections.Concurrent;
using HaloPsaMcp.Modules.Authentication.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Process-local OAuth flow store for single-replica / stdio deployments.
/// </summary>
public sealed class InMemoryOAuthFlowStore : IOAuthFlowStore {
    private const int MaxPending = 10_000;
    private const int MaxCompleted = 10_000;

    private readonly ConcurrentDictionary<string, PendingAuth> _pending = new();
    private readonly ConcurrentDictionary<string, CompletedAuth> _completed = new();

    public int PendingCount => _pending.Count;
    public int CompletedCount => _completed.Count;

    internal bool HasPending(string key) => _pending.ContainsKey(key);
    internal bool HasCompleted(string key) => _completed.ContainsKey(key);

    public void AddPending(string key, PendingAuth value) {
        EnforceCap(_pending, MaxPending);
        _pending[key] = value;
    }

    public bool TryRemovePending(string key, out PendingAuth? value) =>
        _pending.TryRemove(key, out value);

    public void AddCompleted(string key, CompletedAuth value) {
        EnforceCap(_completed, MaxCompleted);
        _completed[key] = value;
    }

    public bool TryRemoveCompleted(string key, out CompletedAuth? value) =>
        _completed.TryRemove(key, out value);

    public int CleanExpiredEntries() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var removed = 0;
        foreach (var kvp in _pending.Where(x => now > x.Value.Expires).ToArray()) {
            if (_pending.TryRemove(kvp.Key, out _)) {
                removed++;
            }
        }
        foreach (var kvp in _completed.Where(x => now > x.Value.Expires).ToArray()) {
            if (_completed.TryRemove(kvp.Key, out _)) {
                removed++;
            }
        }
        return removed;
    }

    private static void EnforceCap<T>(ConcurrentDictionary<string, T> dict, int cap)
        where T : IExpiring {
        if (dict.Count < cap) {
            return;
        }
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
