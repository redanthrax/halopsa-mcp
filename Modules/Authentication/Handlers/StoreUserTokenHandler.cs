using HaloPsaMcp.Modules.Authentication.Commands;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Handlers;

/// <summary>
/// Wolverine handlers for token management commands
/// </summary>
internal static class StoreUserTokenHandler {
    /// <summary>
    /// Handle StoreUserTokenCommand - stores user token in HttpContext
    /// </summary>
    public static Task<StoreUserTokenResult> Handle(
        StoreUserTokenCommand command,
        McpAuthenticationService authService,
        IHttpContextAccessor contextAccessor) {
        var context = contextAccessor.HttpContext;
        if (context == null) {
            return Task.FromResult(new StoreUserTokenResult(false));
        }

        authService.StoreTokenInContext(
            context,
            command.AccessToken,
            command.RefreshToken,
            command.ExpiresAt);

        return Task.FromResult(new StoreUserTokenResult(true));
    }
}