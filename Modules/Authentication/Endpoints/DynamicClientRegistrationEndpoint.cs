using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 dynamic client registration. Public clients only (no secret).
/// Persists registrations to disk via ClientRegistrationStore so subsequent
/// /authorize calls can validate client_id + redirect_uri.
/// </summary>
internal static class DynamicClientRegistrationEndpoint {
    public static void MapDynamicClientRegistration(this IEndpointRouteBuilder app) {
        app.MapPost("/register", DynamicClientRegistration)
            .DisableAntiforgery()
            .RequireRateLimiting("register");
    }

    private static async Task<IResult> DynamicClientRegistration(
        ClientRegistrationStore store,
        ILogger<RegisterMarker> logger,
        [FromBody] JsonElement? body) {
        if (store.IsAtCapacity) {
            logger.LogWarning("DCR rejected — registration store at capacity ({Count})", store.Count);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        string[] redirectUris = Array.Empty<string>();
        if (body.HasValue && body.Value.TryGetProperty("redirect_uris", out var urisElement) &&
            urisElement.ValueKind == JsonValueKind.Array) {
            redirectUris = urisElement.EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }

        if (redirectUris.Length == 0) {
            return Results.BadRequest(new {
                error = "invalid_redirect_uri",
                error_description = "redirect_uris must contain at least one URI"
            });
        }

        // Validate every URI is a well-formed absolute URL with https or loopback http
        foreach (var u in redirectUris) {
            if (!Uri.TryCreate(u, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != "https" && !(parsed.Scheme == "http" && parsed.IsLoopback))) {
                return Results.BadRequest(new {
                    error = "invalid_redirect_uri",
                    error_description = $"Invalid redirect_uri: {u}"
                });
            }
        }

        var clientId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLower(CultureInfo.InvariantCulture);
        var registered = new RegisteredClient {
            ClientId = clientId,
            ClientSecret = null,
            RedirectUris = redirectUris,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        if (!await store.AddAsync(registered).ConfigureAwait(false)) {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        logger.LogInformation("DCR OK | client={ClientId} redirectCount={Count}",
            SecretRedactor.Hint(clientId), redirectUris.Length);

        return Results.Created(string.Empty, new {
            client_id = clientId,
            client_secret_expires_at = 0,
            redirect_uris = redirectUris,
            grant_types = new[] { "authorization_code" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "none"
        });
    }

    internal sealed class RegisterMarker { }
}
