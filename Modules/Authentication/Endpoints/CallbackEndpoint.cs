using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 callback endpoint — exchanges HaloPSA code for tokens, then
/// either redirects the MCP client to its registered redirect_uri (with
/// our short-lived authorization code) or, for direct browser login,
/// creates an MCP session immediately.
/// </summary>
internal static class CallbackEndpoint {
    public static void MapCallback(this IEndpointRouteBuilder app) {
        app.MapGet("/callback", Callback)
            .RequireRateLimiting("oauth");
    }

    private static async Task<IResult> Callback(
        AppConfig config,
        HaloPsaConfig haloPsaConfig,
        IHttpClientFactory httpClientFactory,
        ITokenStore tokenStorage,
        ILogger<CallbackMarker> logger,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error) {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state)) {
            logger.LogWarning("Callback rejected — error={Error} stateHint={StateHint}",
                error, SecretRedactor.Hint(state));
            return Results.BadRequest("Authorization failed or missing parameters");
        }

        if (!OAuthStateManager.PendingAuths.TryRemove(state, out var pending) ||
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > pending.Expires) {
            logger.LogWarning("Callback rejected — state expired/unknown | stateHint={StateHint}",
                SecretRedactor.Hint(state));
            return Results.BadRequest("State expired or unknown — please try connecting again");
        }

        var httpClient = httpClientFactory.CreateClient();
        var tokenUrl = $"{haloPsaConfig.Url}/auth/token?tenant={Uri.EscapeDataString(haloPsaConfig.GetTenant())}";
        var parameters = new Dictionary<string, string> {
            ["grant_type"] = "authorization_code",
            ["client_id"] = haloPsaConfig.ClientId,
            ["redirect_uri"] = AppConfigRuntime.OAuthCallbackUrl(config),
            ["code"] = code,
            ["code_verifier"] = pending.HaloPsaVerifier,
            ["scope"] = "all offline_access"
        };
        if (!string.IsNullOrEmpty(haloPsaConfig.ClientSecret)) {
            parameters["client_secret"] = haloPsaConfig.ClientSecret;
        }

        try {
            var response = await httpClient.PostAsync(
                tokenUrl, new FormUrlEncodedContent(parameters)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                var errorBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                logger.LogError(
                    "HaloPSA token exchange failed | status={Status} bodyBytes={BodyBytes}",
                    response.StatusCode, errorBytes.Length);
                return Results.Problem(
                    detail: $"HaloPSA token exchange failed ({(int)response.StatusCode}).",
                    statusCode: (int)response.StatusCode);
            }

            var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>().ConfigureAwait(false)
                ?? throw new InvalidOperationException("Invalid token response");
            var expiresIn = tokenData.expires_in > 0 ? tokenData.expires_in : 3600;
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn - 60) * 1000;

            if (pending.IsDirectLogin) {
                // Direct browser login — create MCP session immediately
                var (mcp, _) = await tokenStorage.CreateSessionAsync(
                    tokenData.access_token, tokenData.refresh_token, expiresAt).ConfigureAwait(false);
                logger.LogInformation("Direct login OK | mcpToken={Hint}", SecretRedactor.Hint(mcp));
                return Results.Redirect(pending.ClientRedirectUri);
            }

            // MCP-client flow: stash HaloPSA tokens behind a short-lived authorization code
            OAuthStateManager.AddCompleted(pending.ClientCode, new CompletedAuth {
                AccessToken = tokenData.access_token,
                RefreshToken = tokenData.refresh_token ?? string.Empty,
                ExpiresIn = expiresIn,
                ClientCodeChallenge = pending.ClientCodeChallenge,
                Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10 * 60 * 1000
            });

            var redirectUri = new UriBuilder(pending.ClientRedirectUri);
            var query = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
            query["code"] = pending.ClientCode;
            if (!string.IsNullOrEmpty(pending.ClientState)) {
                query["state"] = pending.ClientState;
            }
            redirectUri.Query = query.ToString();

            logger.LogInformation("Callback OK | client={ClientId} codeHint={CodeHint}",
                SecretRedactor.Hint(pending.ClientId), SecretRedactor.Hint(pending.ClientCode));
            return Results.Redirect(redirectUri.ToString());
        } catch (Exception ex) {
            logger.LogError(ex, "OAuth callback token exchange error");
            return Results.Problem(detail: "Token exchange failed. Please try signing in again.", statusCode: 500);
        }
    }

    internal sealed class CallbackMarker { }
}
