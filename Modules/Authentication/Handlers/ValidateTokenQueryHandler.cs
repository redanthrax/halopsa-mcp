using HaloPsaMcp.Modules.Authentication.Queries;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Handlers;

internal static class ValidateTokenQueryHandler
{
    public static async Task<ValidateTokenResult> Handle(
        ValidateTokenQuery query,
        McpAuthenticationService authService)
    {
        var isValid = await authService.ValidateTokenAsync(query.Token).ConfigureAwait(false);
        return new ValidateTokenResult(isValid);
    }
}