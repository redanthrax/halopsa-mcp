using HaloPsaMcp.Modules.Authentication.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// MCP sessions stay usable while the access token is valid or a HaloPSA refresh token remains.
/// </summary>
internal static class SessionValidity {
    internal static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    internal static bool IsUsable(UserTokenEntry entry, long? nowMs = null) {
        var now = nowMs ?? NowMs();
        if (entry.ExpiresAt > now) {
            return true;
        }
        return !string.IsNullOrWhiteSpace(entry.RefreshToken);
    }
}
