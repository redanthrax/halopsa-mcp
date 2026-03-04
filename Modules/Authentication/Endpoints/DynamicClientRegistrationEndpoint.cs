using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 dynamic client registration endpoint
/// </summary>
internal static class DynamicClientRegistrationEndpoint
{
    public static void MapDynamicClientRegistration(this IEndpointRouteBuilder app)
    {
        app.MapPost("/register", DynamicClientRegistration).DisableAntiforgery();
    }

    private static IResult DynamicClientRegistration([FromBody] JsonElement? body)
    {
        var clientId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);

        string[] redirectUris = Array.Empty<string>();
        if (body.HasValue && body.Value.TryGetProperty("redirect_uris", out var urisElement) &&
            urisElement.ValueKind == JsonValueKind.Array)
        {
            redirectUris = urisElement.EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }

        return Results.Created(string.Empty, new
        {
            client_id = clientId,
            client_secret_expires_at = 0,
            redirect_uris = redirectUris,
            grant_types = new[] { "authorization_code" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "none"
        });
    }
}