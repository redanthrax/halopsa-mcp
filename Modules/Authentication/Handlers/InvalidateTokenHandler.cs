using HaloPsaMcp.Modules.Authentication.Commands;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Handlers;

/// <summary>
/// Wolverine handlers for token management commands
/// </summary>
internal static class InvalidateTokenHandler {
    /// <summary>
    /// Handle InvalidateTokenCommand - removes token from validation cache
    /// </summary>
    public static Task<InvalidateTokenResult> Handle(
        InvalidateTokenCommand command,
        McpAuthenticationService authService) {
        authService.InvalidateToken(command.Token);
        return Task.FromResult(new InvalidateTokenResult(true));
    }
}