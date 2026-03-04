using HaloPsaMcp.Modules.Authentication.Queries;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Handlers;

internal static class GetUserTokenQueryHandler
{
    public static Task<GetUserTokenResult> Handle(
        GetUserTokenQuery query,
        McpAuthenticationService authService,
        IHttpContextAccessor contextAccessor)
    {
        var context = contextAccessor.HttpContext;
        if (context == null)
        {
            return Task.FromResult(new GetUserTokenResult(null, null, null));
        }

        var accessToken = authService.GetTokenFromContext(context);
        var refreshToken = authService.GetRefreshTokenFromContext(context);
        var expiresAt = authService.GetTokenExpiryFromContext(context);

        return Task.FromResult(new GetUserTokenResult(accessToken, refreshToken, expiresAt));
    }
}