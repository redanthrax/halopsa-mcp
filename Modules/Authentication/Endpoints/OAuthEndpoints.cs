using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 PKCE authentication endpoints as minimal API
/// </summary>
internal static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // OAuth 2.1 discovery endpoints
        app.MapProtectedResourceMetadata();
        app.MapAuthorizationServerMetadata();

        // OAuth 2.1 PKCE flow
        app.MapDynamicClientRegistration();
        app.MapAuthorize();
        app.MapCallback();
        app.MapTokenExchange();

        // Direct browser login (bypasses MCP client PKCE)
        app.MapDirectLogin();
    }
}
