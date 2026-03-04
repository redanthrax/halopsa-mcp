using HaloPsaMcp.Modules.Authentication.Queries;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Handlers;

internal static class GetAuthStatusQueryHandler
{
    public static async Task<GetAuthStatusResult> Handle(
        GetAuthStatusQuery query,
        McpAuthenticationService authService)
    {
        var isValid = await authService.ValidateTokenAsync(query.Token).ConfigureAwait(false);

        if (!isValid)
        {
            return new GetAuthStatusResult(false);
        }

        // Token is valid - could fetch agent data here if needed
        return new GetAuthStatusResult(true);
    }
}