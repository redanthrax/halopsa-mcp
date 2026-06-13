using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 discovery endpoint for authorization server metadata
/// </summary>
internal static class AuthorizationServerMetadataEndpoint
{
    public static void MapAuthorizationServerMetadata(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/oauth-authorization-server", (AppConfig config) =>
            AuthorizationServerMetadata(config, resourcePath: null));
        app.MapGet("/.well-known/oauth-authorization-server/{*resourcePath}", (AppConfig config, string resourcePath) =>
            AuthorizationServerMetadata(config, resourcePath));
    }

    private static IResult AuthorizationServerMetadata(AppConfig config, string? resourcePath)
    {
        if (!OAuthDiscovery.IsKnownAuthorizationServerPath(resourcePath)) {
            return Results.NotFound();
        }
        return Results.Ok(OAuthDiscovery.BuildAuthorizationServerMetadata(config));
    }
}
