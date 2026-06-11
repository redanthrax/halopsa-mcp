using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.Mcp;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

internal static class DesktopStatusEndpoint {
    public static void MapDesktopStatus(this IEndpointRouteBuilder app) {
        app.MapGet("/", DesktopStatus);
    }

    private static IResult DesktopStatus(AppConfig config, ITokenStore tokenStore) {
        var loginUrl = HaloPsaMcpConstants.GetLoginUrl(config);
        var authenticated = tokenStore.HasValidTokens();
        return LoginPages.DesktopStatus(config, authenticated, loginUrl);
    }
}
