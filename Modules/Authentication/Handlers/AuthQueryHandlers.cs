using HaloPsaMcp.Modules.Authentication.Queries;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Handlers;

/// <summary>
/// Wolverine handlers for authentication queries
/// </summary>
internal static class AuthQueryHandlers {
    /// <summary>
    /// Handle ValidateTokenQuery - validates a Bearer token against HaloPSA API
    /// </summary>
    public static async Task<ValidateTokenResult> Handle(
        ValidateTokenQuery query,
        McpAuthenticationService authService) {
        var isValid = await authService.ValidateTokenAsync(query.Token).ConfigureAwait(false);
        return new ValidateTokenResult(isValid);
    }

    /// <summary>
    /// Handle GetUserTokenQuery - retrieves user token from HttpContext
    /// </summary>
    public static Task<GetUserTokenResult> Handle(
        GetUserTokenQuery query,
        McpAuthenticationService authService,
        IHttpContextAccessor contextAccessor) {
        var context = contextAccessor.HttpContext;
        if (context == null) {
            return Task.FromResult(new GetUserTokenResult(null, null, null));
        }

        var accessToken = authService.GetTokenFromContext(context);
        var refreshToken = authService.GetRefreshTokenFromContext(context);
        var expiresAt = authService.GetTokenExpiryFromContext(context);

        return Task.FromResult(new GetUserTokenResult(accessToken, refreshToken, expiresAt));
    }

    /// <summary>
    /// Handle GetAuthStatusQuery - checks authentication status by validating token
    /// </summary>
    public static async Task<GetAuthStatusResult> Handle(
        GetAuthStatusQuery query,
        McpAuthenticationService authService) {
        var isValid = await authService.ValidateTokenAsync(query.Token).ConfigureAwait(false);

        if (!isValid) {
            return new GetAuthStatusResult(false);
        }

        // Token is valid - could fetch agent data here if needed
        return new GetAuthStatusResult(true);
    }
}
