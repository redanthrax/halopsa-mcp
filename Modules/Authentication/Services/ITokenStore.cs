using HaloPsaMcp.Modules.Authentication.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Pluggable session store for opaque MCP tokens (mcp_*) and their upstream HaloPSA pairs.
/// File-backed for single-instance deployments; Redis for multi-replica HTTP/Kubernetes.
/// </summary>
public interface ITokenStore : IDisposable {
    /// <summary>Human-readable backend id for logs and health checks (e.g. "file", "redis").</summary>
    string Backend { get; }

    Task<(string AccessToken, string RefreshToken)> CreateSessionAsync(
        string haloPsaAccess, string? haloPsaRefresh, long expiresAt);

    UserTokenEntry? GetToken(string mcpToken);
    UserTokenEntry? GetDefaultToken();
    bool IsValidSession(string mcpToken);

    Task UpdateSessionTokensAsync(
        string mcpToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt);

    KeyValuePair<string, UserTokenEntry>? FindByRefreshToken(string mcpRefresh);

    Task<string> RotateRefreshTokenAsync(
        string mcpAccessToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt);

    Task<bool> InvalidateSessionAsync(string mcpToken);

    int PruneExpired();
    bool HasValidTokens();
    int SessionCount { get; }
    int ActiveSessionCount { get; }

    ValueTask<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
