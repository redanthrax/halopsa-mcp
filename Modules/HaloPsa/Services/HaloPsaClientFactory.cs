using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.HaloPsa.Models;

namespace HaloPsaMcp.Modules.HaloPsa.Services;

/// <summary>
/// Factory for creating HaloPsaClient instances with per-user tokens.
/// Resolves the active session from HttpContext (HTTP mode) or
/// TokenStorageService.GetDefaultToken (stdio mode). On refresh both the
/// in-flight HttpContext and the persistent TokenStorageService are updated.
/// </summary>
internal class HaloPsaClientFactory {
    private const string HttpClientName = "halopsa";

    private readonly HaloPsaConfig _baseConfig;
    private readonly McpAuthenticationService _authService;
    private readonly TokenStorageService _tokenStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HaloPsaClientFactory> _logger;

    public HaloPsaClientFactory(
        HaloPsaConfig baseConfig,
        McpAuthenticationService authService,
        TokenStorageService tokenStorage,
        IHttpClientFactory httpClientFactory,
        ILogger<HaloPsaClientFactory> logger) {
        _baseConfig = baseConfig;
        _authService = authService;
        _tokenStorage = tokenStorage;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Build a client for the active session. Resolves the user's HaloPSA
    /// access/refresh tokens from the HTTP context if present, otherwise
    /// falls back to the stdio default session in TokenStorageService.
    /// Returns null when no authenticated session is available.
    /// </summary>
    public HaloPsaClient? CreateClient(HttpContext? context) {
        string? userToken = null;
        string? refreshToken = null;
        long? expiresAt = null;
        string? mcpSessionToken = null;

        if (context != null) {
            userToken = _authService.GetTokenFromContext(context);
            refreshToken = _authService.GetRefreshTokenFromContext(context);
            expiresAt = _authService.GetTokenExpiryFromContext(context);
            mcpSessionToken = _authService.GetMcpTokenFromContext(context);

            if (string.IsNullOrEmpty(userToken)) {
                var authHeader = context.Request.Headers.Authorization.ToString();
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
                    mcpSessionToken = authHeader.Substring(7);
                    var entry = _tokenStorage.GetToken(mcpSessionToken);
                    if (entry != null) {
                        userToken = entry.AccessToken;
                        refreshToken = entry.RefreshToken;
                        expiresAt = entry.ExpiresAt;
                    }
                }
            }
        } else {
            var entry = _tokenStorage.GetDefaultToken();
            if (entry != null) {
                userToken = entry.AccessToken;
                refreshToken = entry.RefreshToken;
                expiresAt = entry.ExpiresAt;
            }
        }

        if (string.IsNullOrEmpty(userToken)) {
            return null;
        }

        var capturedMcpToken = mcpSessionToken;
        var capturedContext = context;
        var userConfig = new HaloPsaConfig {
            Url = _baseConfig.Url,
            ClientId = _baseConfig.ClientId,
            ClientSecret = _baseConfig.ClientSecret,
            DirectToken = userToken,
            DirectRefreshToken = refreshToken,
            DirectTokenExpiresAt = expiresAt,
            OnTokenRefreshed = (newToken, newRefreshToken, newExpiresAt) => {
                if (capturedContext != null) {
                    _authService.StoreTokenInContext(capturedContext, newToken, newRefreshToken, newExpiresAt);
                }
                if (!string.IsNullOrEmpty(capturedMcpToken)) {
                    _ = _tokenStorage.UpdateSessionTokensAsync(
                        capturedMcpToken, newToken, newRefreshToken, newExpiresAt);
                } else {
                    _logger.LogDebug("Refresh occurred without MCP session token — skipping persist");
                }
            }
        };

        return new HaloPsaClient(userConfig, _httpClientFactory.CreateClient(HttpClientName));
    }

    /// <summary>
    /// Like <see cref="CreateClient"/> but throws <see cref="UnauthorizedAccessException"/>
    /// when no authenticated session is available. Used by Wolverine handlers, where
    /// the exception propagates back to the MCP tool as a "not authenticated" response.
    /// </summary>
    public HaloPsaClient CreateClientOrThrow(HttpContext? context) =>
        CreateClient(context) ?? throw new UnauthorizedAccessException("No authenticated HaloPSA session");
}
