using HaloPsaMcp.Modules.Common.Models;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 discovery endpoint for authorization server metadata
/// </summary>
internal static class AuthorizationServerMetadataEndpoint
{
    public static void MapAuthorizationServerMetadata(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/oauth-authorization-server", AuthorizationServerMetadata);
    }

    private static IResult AuthorizationServerMetadata(AppConfig config)
    {
        var authBaseUrl = AppConfigRuntime.ResolveAuthBaseUrl(config);
        return Results.Ok(new
        {
            issuer = authBaseUrl,
            authorization_endpoint = $"{authBaseUrl}/authorize",
            token_endpoint = $"{authBaseUrl}/token",
            registration_endpoint = $"{authBaseUrl}/register",
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code" },
            code_challenge_methods_supported = new[] { "S256" },
            token_endpoint_auth_methods_supported = new[] { "none" }
        });
    }
}