using System.Globalization;
using System.Security.Cryptography;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// Direct browser login endpoint (bypasses MCP client PKCE)
/// </summary>
internal static class DirectLoginEndpoint
{
    public static void MapDirectLogin(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", DirectLogin);
        app.MapGet("/success", () => Results.Ok("Authentication successful! You can close this window."));
    }

    private static IResult DirectLogin(AppConfig config, HaloPsaConfig haloPsaConfig)
    {
        OAuthStateManager.CleanExpiredEntries();

        var verifier = PkceHelper.GenerateCodeVerifier();
        var challenge = PkceHelper.GenerateCodeChallenge(verifier);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);

        OAuthStateManager.PendingAuths[state] = new PendingAuth
        {
            HaloPsaVerifier = verifier,
            ClientRedirectUri = $"{config.AuthBaseUrl}/success",
            ClientState = null,
            ClientCodeChallenge = challenge,
            ClientCode = string.Empty,
            Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10 * 60 * 1000,
            IsDirectLogin = true
        };

        var haloPsaAuthUrl = PkceHelper.BuildAuthUrl(
            haloPsaConfig.Url,
            haloPsaConfig.ClientId,
            haloPsaConfig.GetTenant(),
            $"{config.AuthBaseUrl}/callback",
            state,
            challenge
        );

        return Results.Redirect(haloPsaAuthUrl);
    }
}