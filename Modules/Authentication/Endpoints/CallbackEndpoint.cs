using System.Security.Cryptography;
#pragma warning disable IDE0005 // Using directives flagged as unnecessary but are required for the code
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 callback endpoint
/// </summary>
internal static class CallbackEndpoint
{
    private static TokenStorageService? _tokenStorage;
    private static ILogger? _logger;

    public static void MapCallback(this IEndpointRouteBuilder app)
    {
        _tokenStorage = app.ServiceProvider.GetRequiredService<TokenStorageService>();
        _logger = app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(CallbackEndpoint));

        app.MapGet("/callback", Callback);
    }

    private static async Task<IResult> Callback(
        AppConfig config,
        HaloPsaConfig haloPsaConfig,
        IHttpClientFactory httpClientFactory,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Results.BadRequest("Authorization failed or missing parameters");
        }

        if (!OAuthStateManager.PendingAuths.TryRemove(state, out var pending) ||
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > pending.Expires)
        {
            return Results.BadRequest("State expired or unknown — please try connecting again");
        }

        var httpClient = httpClientFactory.CreateClient();
        var tokenUrl = $"{haloPsaConfig.Url}/auth/token?tenant={Uri.EscapeDataString(haloPsaConfig.GetTenant())}";

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = haloPsaConfig.ClientId,
            ["redirect_uri"] = $"{config.AuthBaseUrl}/callback",
            ["code"] = code,
            ["code_verifier"] = pending.HaloPsaVerifier,
            ["scope"] = "all offline_access"
        };

        if (!string.IsNullOrEmpty(haloPsaConfig.ClientSecret))
        {
            parameters["client_secret"] = haloPsaConfig.ClientSecret;
        }

        try
        {
            var response = await httpClient.PostAsync(
                tokenUrl,
                new FormUrlEncodedContent(parameters)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger?.LogError("HaloPSA token exchange failed: {StatusCode} - {Error}", response.StatusCode, errorText);
                return Results.Problem($"HaloPSA token exchange failed: {response.StatusCode} - {errorText}");
            }

            var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>().ConfigureAwait(false)
                ?? throw new InvalidOperationException("Invalid token response");

            var expiresIn = tokenData.expires_in > 0 ? tokenData.expires_in : 3600;

            if (pending.IsDirectLogin)
            {
                var entry = new UserTokenEntry
                {
                    AccessToken = tokenData.access_token,
                    RefreshToken = tokenData.refresh_token ?? string.Empty,
                    ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn - 60) * 1000
                };
                _ = _tokenStorage?.SaveTokenAsync(tokenData.access_token, entry);
                _logger?.LogInformation("Direct browser login completed, token saved");
                return Results.Redirect(pending.ClientRedirectUri);
            }

            OAuthStateManager.CompletedAuths[pending.ClientCode] = new CompletedAuth
            {
                AccessToken = tokenData.access_token,
                RefreshToken = tokenData.refresh_token ?? string.Empty,
                ExpiresIn = expiresIn,
                ClientCodeChallenge = pending.ClientCodeChallenge,
                Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10 * 60 * 1000
            };

            var redirectUri = new UriBuilder(pending.ClientRedirectUri);
            var query = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
            query["code"] = pending.ClientCode;
            if (!string.IsNullOrEmpty(pending.ClientState))
            {
                query["state"] = pending.ClientState;
            }
            redirectUri.Query = query.ToString();

            return Results.Redirect(redirectUri.ToString());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OAuth callback token exchange error");
            return Results.Problem($"Token exchange error: {ex.Message}");
        }
    }
}