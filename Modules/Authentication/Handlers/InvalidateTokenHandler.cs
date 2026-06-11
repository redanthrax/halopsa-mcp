using HaloPsaMcp.Modules.Authentication.Commands;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Handlers;

/// <summary>
/// Wolverine handlers for token management commands
/// </summary>
public static class InvalidateTokenHandler {
    /// <summary>
    /// Handle InvalidateTokenCommand - removes token from validation cache
    /// </summary>
    public static async Task<InvalidateTokenResult> Handle(
        InvalidateTokenCommand command,
        McpAuthenticationService authService) {
        var removed = await authService.InvalidateTokenAsync(command.Token).ConfigureAwait(false);
        return new InvalidateTokenResult(removed);
    }
}