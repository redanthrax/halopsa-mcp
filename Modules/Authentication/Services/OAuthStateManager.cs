using System.Collections.Concurrent;
using HaloPsaMcp.Modules.Authentication.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Shared state manager for OAuth endpoints
/// </summary>
internal class OAuthStateManager
{
    public static readonly ConcurrentDictionary<string, PendingAuth> PendingAuths = new();
    public static readonly ConcurrentDictionary<string, CompletedAuth> CompletedAuths = new();

    public static void CleanExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var kvp in PendingAuths.Where(x => now > x.Value.Expires))
        {
            PendingAuths.TryRemove(kvp.Key, out _);
        }

        foreach (var kvp in CompletedAuths.Where(x => now > x.Value.Expires))
        {
            CompletedAuths.TryRemove(kvp.Key, out _);
        }

        // Token cleanup is now handled by TokenStorageService
    }
}