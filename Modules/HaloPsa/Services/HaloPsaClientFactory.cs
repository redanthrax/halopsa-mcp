using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Infrastructure;
using HaloPsaMcp.Modules.HaloPsa.Models;

namespace HaloPsaMcp.Modules.HaloPsa.Services;

/// <summary>
/// Factory for creating HaloPsaClient instances with per-user tokens from HttpContext
/// </summary>
internal class HaloPsaClientFactory {
    private readonly HaloPsaConfig _baseConfig;
    private readonly McpAuthenticationService _authService;
    private readonly ITokenStore? _tokenStore;

    public HaloPsaClientFactory(
        HaloPsaConfig baseConfig,
        McpAuthenticationService authService,
        ITokenStore? tokenStore = null) {
        _baseConfig = baseConfig;
        _authService = authService;
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Creates a HaloPsaClient with the current user's token from HttpContext
    /// </summary>
    public HaloPsaClient CreateClient(HttpContext? context) {
        if (context == null) {
            // Fall back to base config (for background jobs or non-HTTP contexts)
            return new HaloPsaClient(_baseConfig, _tokenStore);
        }

        var userToken = _authService.GetTokenFromContext(context);
        var refreshToken = _authService.GetRefreshTokenFromContext(context);
        var expiresAt = _authService.GetTokenExpiryFromContext(context);

        if (string.IsNullOrEmpty(userToken)) {
            // No user token in context, fall back to base config
            return new HaloPsaClient(_baseConfig, _tokenStore);
        }

        // Create per-user config with DirectToken
        var userConfig = new HaloPsaConfig {
            Url = _baseConfig.Url,
            ClientId = _baseConfig.ClientId,
            ClientSecret = _baseConfig.ClientSecret,
            DirectToken = userToken,
            DirectRefreshToken = refreshToken,
            DirectTokenExpiresAt = expiresAt,
            OnTokenRefreshed = (newToken, newRefreshToken, newExpiresAt) => {
                // Update token in HttpContext when refreshed
                _authService.StoreTokenInContext(
                    context,
                    newToken,
                    newRefreshToken,
                    newExpiresAt);
            }
        };

        return new HaloPsaClient(userConfig, _tokenStore);
    }

    /// <summary>
    /// Creates a HaloPsaClient with the base configuration (for system/admin operations)
    /// </summary>
    public HaloPsaClient CreateSystemClient() {
        return new HaloPsaClient(_baseConfig, _tokenStore);
    }
}
