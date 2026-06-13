using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 dynamic client registration. Public clients only (no secret).
/// Redirect URIs are canonicalized on registration (lowercase host, default port
/// stripped, trailing slash removed). Persists via ClientRegistrationStore.
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
        HttpContext http,
        [FromBody] JsonElement? body) {
        if (OAuthDiscovery.RequiresDcrInitialAccessToken()) {
            var requiredIat = OAuthDiscovery.GetDcrInitialAccessToken()!;
            var authHeader = http.Request.Headers.Authorization.ToString();
            string? presented = null;
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
                presented = authHeader.Substring(7);
            }
            if (presented is null || !FixedTimeEquals(presented, requiredIat)) {
                logger.LogWarning("DCR rejected — missing/invalid initial access token");
                return RejectDcrUnauthorized(http);
            }
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

        foreach (var u in redirectUris) {
            if (!Uri.TryCreate(u, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != "https" && !(parsed.Scheme == "http" && parsed.IsLoopback))) {
                return Results.BadRequest(new {
                    error = "invalid_redirect_uri",
                    error_description = $"Invalid redirect_uri: {u}"
                });
            }
            if (u.Contains('?', StringComparison.Ordinal) || u.Contains('#', StringComparison.Ordinal)) {
                return Results.BadRequest(new {
                    error = "invalid_redirect_uri",
                    error_description = "redirect_uri must not contain '?' or '#'"
                });
            }
        }

        redirectUris = RedirectUriNormalizer.NormalizeAll(redirectUris);

        if (!await store.TryMakeRoomAsync().ConfigureAwait(false)) {
            logger.LogWarning("DCR rejected — registration store at capacity ({Count})", store.Count);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var clientId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLower(CultureInfo.InvariantCulture);
        var registered = new RegisteredClient {
            ClientId = clientId,
            ClientSecret = null,
            RedirectUris = redirectUris,
            CreatedAt = now,
            LastUsedAt = now
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

    private static IResult RejectDcrUnauthorized(HttpContext http) {
        http.Response.Headers.Append(
            "WWW-Authenticate",
            "Bearer error=\"invalid_token\", error_description=\"initial_access_token required\"");
        return Results.Json(new {
            error = "invalid_token",
            error_description =
                "Initial access token required. Present MCP_DCR_INITIAL_ACCESS_TOKEN as a Bearer credential."
        }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static bool FixedTimeEquals(string a, string b) {
        var ab = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        if (ab.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }
}
