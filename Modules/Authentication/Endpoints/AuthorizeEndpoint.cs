using System.Globalization;
using System.Security.Cryptography;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 authorization endpoint
/// </summary>
internal static class AuthorizeEndpoint
{
    public static void MapAuthorize(this IEndpointRouteBuilder app)
    {
        app.MapGet("/authorize", Authorize);
    }

    private static IResult Authorize(
        AppConfig config,
        HaloPsaConfig haloPsaConfig,
        [FromQuery] string? redirect_uri,
        [FromQuery] string? code_challenge,
        [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(redirect_uri) || string.IsNullOrEmpty(code_challenge))
        {
            return Results.BadRequest("Missing redirect_uri or code_challenge");
        }

        if (!Uri.TryCreate(redirect_uri, UriKind.Absolute, out var parsedRedirect) ||
            parsedRedirect.Scheme != "https")
        {
            return Results.BadRequest("redirect_uri must be a valid HTTPS URL");
        }

        OAuthStateManager.CleanExpiredEntries();

        var clientCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);
        var haloPsaVerifier = PkceHelper.GenerateCodeVerifier();
        var haloPsaChallenge = PkceHelper.GenerateCodeChallenge(haloPsaVerifier);
        var oauthState = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);

        OAuthStateManager.PendingAuths[oauthState] = new PendingAuth
        {
            HaloPsaVerifier = haloPsaVerifier,
            ClientRedirectUri = redirect_uri,
            ClientState = state,
            ClientCodeChallenge = code_challenge,
            ClientCode = clientCode,
            Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10 * 60 * 1000
        };

        var haloPsaAuthUrl = PkceHelper.BuildAuthUrl(
            haloPsaConfig.Url,
            haloPsaConfig.ClientId,
            haloPsaConfig.GetTenant(),
            $"{config.AuthBaseUrl}/callback",
            oauthState,
            haloPsaChallenge
        );

        return Results.Redirect(haloPsaAuthUrl);
    }
}