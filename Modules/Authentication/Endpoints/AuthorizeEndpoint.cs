using System.Globalization;
using System.Security.Cryptography;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 authorization endpoint.
/// Requires client_id (DCR-issued) and exact-match redirect_uri.
/// </summary>
internal static class AuthorizeEndpoint {
    public static void MapAuthorize(this IEndpointRouteBuilder app) {
        app.MapGet("/authorize", Authorize)
            .RequireRateLimiting("oauth");
    }

    private static IResult Authorize(
        AppConfig config,
        HaloPsaConfig haloPsaConfig,
        ClientRegistrationStore clientStore,
        ILogger<AuthorizeMarker> logger,
        [FromQuery] string? client_id,
        [FromQuery] string? redirect_uri,
        [FromQuery] string? code_challenge,
        [FromQuery] string? code_challenge_method,
        [FromQuery] string? state) {
        if (string.IsNullOrEmpty(client_id) ||
            string.IsNullOrEmpty(redirect_uri) ||
            string.IsNullOrEmpty(code_challenge)) {
            return Results.BadRequest(new {
                error = "invalid_request",
                error_description = "Missing client_id, redirect_uri or code_challenge"
            });
        }

        // Enforce S256 explicitly. RFC 7636: default is "plain" which we must reject.
        var method = code_challenge_method ?? "plain";
        if (!string.Equals(method, "S256", StringComparison.Ordinal)) {
            logger.LogWarning("Authorize rejected — non-S256 challenge method | method={Method} client={ClientId}",
                method, SecretRedactor.Hint(client_id));
            return Results.BadRequest(new {
                error = "invalid_request",
                error_description = "code_challenge_method must be S256"
            });
        }

        if (!Uri.TryCreate(redirect_uri, UriKind.Absolute, out var parsedRedirect) ||
            (parsedRedirect.Scheme != "https" && parsedRedirect.Scheme != "http")) {
            return Results.BadRequest(new {
                error = "invalid_request", error_description = "redirect_uri must be a valid absolute URL"
            });
        }
        // Allow http only for loopback (RFC 8252)
        if (parsedRedirect.Scheme == "http" && !parsedRedirect.IsLoopback) {
            return Results.BadRequest(new {
                error = "invalid_request", error_description = "redirect_uri must be HTTPS (http allowed only for loopback)"
            });
        }

        var registered = clientStore.Get(client_id);
        if (registered is null) {
            logger.LogWarning("Authorize rejected — unknown client | client={ClientId}",
                SecretRedactor.Hint(client_id));
            return Results.BadRequest(new {
                error = "invalid_client", error_description = "Unknown client_id — register first via /register"
            });
        }

        if (!clientStore.ValidateRedirectUri(client_id, redirect_uri)) {
            logger.LogWarning("Authorize rejected — redirect_uri mismatch | client={ClientId}",
                SecretRedactor.Hint(client_id));
            return Results.BadRequest(new {
                error = "invalid_request",
                error_description = "redirect_uri does not match a registered URI"
            });
        }

        OAuthStateManager.CleanExpiredEntries();

        var clientCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLower(CultureInfo.InvariantCulture);
        var haloPsaVerifier = PkceHelper.GenerateCodeVerifier();
        var haloPsaChallenge = PkceHelper.GenerateCodeChallenge(haloPsaVerifier);
        var oauthState = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLower(CultureInfo.InvariantCulture);

        OAuthStateManager.AddPending(oauthState, new PendingAuth {
            HaloPsaVerifier = haloPsaVerifier,
            ClientRedirectUri = redirect_uri,
            ClientState = state,
            ClientCodeChallenge = code_challenge,
            ClientCode = clientCode,
            ClientId = client_id,
            Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10 * 60 * 1000
        });

        var haloPsaAuthUrl = PkceHelper.BuildAuthUrl(
            haloPsaConfig.Url,
            haloPsaConfig.ClientId,
            haloPsaConfig.GetTenant(),
            AppConfigRuntime.OAuthCallbackUrl(config),
            oauthState,
            haloPsaChallenge);

        logger.LogInformation("Authorize OK | client={ClientId} state={StateHint}",
            SecretRedactor.Hint(client_id), SecretRedactor.Hint(oauthState));
        return Results.Redirect(haloPsaAuthUrl);
    }

    /// <summary>Marker class for ILogger&lt;T&gt; categorization.</summary>
    internal sealed class AuthorizeMarker { }
}
