namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Validates MCP session tokens against the local ITokenStore.
/// Validation is purely local — opaque mcp_ tokens are issued by us and
/// don't require a round-trip to HaloPSA. The underlying HaloPSA token
/// is exposed to downstream tool handlers via HttpContext.Items.
/// </summary>
public class McpAuthenticationService {
    public const string McpTokenContextKey = "mcp.session_token";
    public const string HaloPsaTokenContextKey = "mcp.halopsa_token";
    public const string HaloPsaRefreshContextKey = "mcp.halopsa_refresh";
    public const string HaloPsaExpiryContextKey = "mcp.halopsa_expiry";

    private readonly ITokenStore _storage;
    private readonly ILogger<McpAuthenticationService> _logger;

    public McpAuthenticationService(
        ITokenStore storage,
        ILogger<McpAuthenticationService> logger) {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Validates the supplied MCP session token (local lookup only).</summary>
    public Task<bool> ValidateTokenAsync(string mcpToken) {
        return Task.FromResult(_storage.IsValidSession(mcpToken));
    }

    public Task<(bool IsValid, bool FromCache)> ValidateTokenWithCacheInfoAsync(string mcpToken) {
        // Always "from cache" — local lookup, no remote call ever made.
        return Task.FromResult((_storage.IsValidSession(mcpToken), true));
    }

    /// <summary>Records the MCP session and HaloPSA tokens in the request context.</summary>
    public void StoreSessionInContext(HttpContext context, string mcpToken, string haloPsaToken, string? refreshToken, long? expiresAt) {
        context.Items[McpTokenContextKey] = mcpToken;
        context.Items[HaloPsaTokenContextKey] = haloPsaToken;
        context.Items[HaloPsaRefreshContextKey] = refreshToken;
        context.Items[HaloPsaExpiryContextKey] = expiresAt;
    }

    public string? GetMcpTokenFromContext(HttpContext context) =>
        context.Items[McpTokenContextKey] as string;

    public string? GetTokenFromContext(HttpContext context) =>
        context.Items[HaloPsaTokenContextKey] as string;

    public string? GetRefreshTokenFromContext(HttpContext context) =>
        context.Items[HaloPsaRefreshContextKey] as string;

    public long? GetTokenExpiryFromContext(HttpContext context) =>
        context.Items[HaloPsaExpiryContextKey] as long?;

    /// <summary>
    /// Update token info in context after a refresh. Note this only updates the
    /// in-flight HttpContext; persistence is done by HaloPsaClientFactory.
    /// </summary>
    public void StoreTokenInContext(HttpContext context, string newHaloAccess, string? newRefresh, long? newExpiresAt) {
        context.Items[HaloPsaTokenContextKey] = newHaloAccess;
        context.Items[HaloPsaRefreshContextKey] = newRefresh;
        context.Items[HaloPsaExpiryContextKey] = newExpiresAt;
    }

    /// <summary>Revokes an MCP session so the bearer token is no longer accepted.</summary>
    public async Task<bool> InvalidateTokenAsync(string token) {
        var removed = await _storage.InvalidateSessionAsync(token).ConfigureAwait(false);
        if (removed) {
            _logger.LogInformation("MCP session revoked | mcpToken={Hint}", SecretRedactor.Hint(token));
        } else {
            _logger.LogDebug("InvalidateToken — session not found | mcpToken={Hint}", SecretRedactor.Hint(token));
        }
        return removed;
    }
}
