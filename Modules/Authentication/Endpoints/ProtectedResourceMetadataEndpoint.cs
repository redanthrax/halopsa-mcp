using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 discovery endpoint for protected resource metadata
/// </summary>
internal static class ProtectedResourceMetadataEndpoint
{
    public static void MapProtectedResourceMetadata(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/oauth-protected-resource", (AppConfig config) =>
            ProtectedResourceMetadata(config, resourcePath: null));
        app.MapGet("/.well-known/oauth-protected-resource/{*resourcePath}", (AppConfig config, string resourcePath) =>
            ProtectedResourceMetadata(config, resourcePath));
    }

    private static IResult ProtectedResourceMetadata(AppConfig config, string? resourcePath)
    {
        if (!OAuthDiscovery.IsKnownProtectedResourcePath(resourcePath)) {
            return Results.NotFound();
        }
        return Results.Ok(OAuthDiscovery.BuildProtectedResourceMetadata(config));
    }
}
