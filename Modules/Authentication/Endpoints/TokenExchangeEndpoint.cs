using System.Net.Http.Headers;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 token endpoint. Supports two grants:
/// - authorization_code: completes the PKCE flow and issues an opaque MCP session token (mcp_xxx).
/// - refresh_token: refreshes the underlying HaloPSA tokens behind an existing MCP session and
///   returns the same mcp_ token with a renewed expires_in.
/// In both cases the HaloPSA access/refresh tokens stay server-side.
/// </summary>
internal static class TokenExchangeEndpoint {
    public static void MapTokenExchange(this IEndpointRouteBuilder app) {
        app.MapPost("/token", TokenExchange)
            .DisableAntiforgery()
            .RequireRateLimiting("oauth");
    }

    private static async Task<IResult> TokenExchange(
        TokenStorageService tokenStorage,
        HaloPsaConfig haloPsaConfig,
        IHttpClientFactory httpClientFactory,
        ILogger<TokenMarker> logger,
        [FromForm] string? grant_type,
        [FromForm] string? code,
        [FromForm] string? code_verifier,
        [FromForm] string? refresh_token) {
        return (grant_type ?? "authorization_code") switch {
            "authorization_code" => await HandleAuthorizationCode(tokenStorage, logger, code, code_verifier).ConfigureAwait(false),
            "refresh_token" => await HandleRefreshToken(tokenStorage, haloPsaConfig, httpClientFactory, logger, refresh_token).ConfigureAwait(false),
            _ => Results.BadRequest(new {
                error = "unsupported_grant_type",
                error_description = $"grant_type '{grant_type}' is not supported"
            })
        };
    }

    private static async Task<IResult> HandleAuthorizationCode(
        TokenStorageService tokenStorage,
        ILogger logger,
        string? code,
        string? code_verifier) {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(code_verifier)) {
            return Results.BadRequest(new {
                error = "invalid_request", error_description = "Missing code or code_verifier"
            });
        }

        if (!OAuthStateManager.CompletedAuths.TryRemove(code, out var completed) ||
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > completed.Expires) {
            logger.LogWarning("Token rejected — code expired/unknown | codeHint={CodeHint}",
                SecretRedactor.Hint(code));
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "Code expired or unknown"
            });
        }

        var challenge = PkceHelper.GenerateCodeChallenge(code_verifier);
        if (challenge != completed.ClientCodeChallenge) {
            logger.LogWarning("Token rejected — PKCE mismatch | codeHint={CodeHint}",
                SecretRedactor.Hint(code));
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "PKCE verification failed"
            });
        }

        var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (completed.ExpiresIn - 60) * 1000;
        var mcpToken = await tokenStorage.CreateSessionAsync(
            completed.AccessToken, completed.RefreshToken, expiresAt).ConfigureAwait(false);

        return Results.Ok(new {
            access_token = mcpToken,
            token_type = "Bearer",
            expires_in = completed.ExpiresIn,
            refresh_token = mcpToken
        });
    }

    private static async Task<IResult> HandleRefreshToken(
        TokenStorageService tokenStorage,
        HaloPsaConfig haloPsaConfig,
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        string? refresh_token) {
        if (string.IsNullOrEmpty(refresh_token)) {
            return Results.BadRequest(new {
                error = "invalid_request", error_description = "Missing refresh_token"
            });
        }

        var session = tokenStorage.GetToken(refresh_token);
        if (session == null || string.IsNullOrEmpty(session.RefreshToken)) {
            logger.LogWarning("Refresh rejected — unknown session | mcp={Hint}", SecretRedactor.Hint(refresh_token));
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "Unknown or already-revoked refresh token"
            });
        }

        var tokenUrl = $"{haloPsaConfig.Url}/auth/token?tenant={Uri.EscapeDataString(haloPsaConfig.GetTenant())}";
        var parameters = new Dictionary<string, string> {
            ["grant_type"] = "refresh_token",
            ["client_id"] = haloPsaConfig.ClientId,
            ["refresh_token"] = session.RefreshToken
        };
        if (!string.IsNullOrEmpty(haloPsaConfig.ClientSecret)) {
            parameters["client_secret"] = haloPsaConfig.ClientSecret;
        }

        var http = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await http.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            logger.LogError("HaloPSA refresh failed | status={Status} body={Body}", response.StatusCode, error);
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "HaloPSA rejected the refresh token"
            });
        }

        var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid HaloPSA token response");
        var expiresIn = tokenData.expires_in > 0 ? tokenData.expires_in : 3600;
        var newExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn - 60) * 1000;
        var newRefresh = tokenData.refresh_token ?? session.RefreshToken;

        await tokenStorage.UpdateSessionTokensAsync(refresh_token, tokenData.access_token, newRefresh, newExpiresAt).ConfigureAwait(false);

        logger.LogInformation("Refresh OK | mcp={Hint} expiresIn={Seconds}s",
            SecretRedactor.Hint(refresh_token), expiresIn);

        return Results.Ok(new {
            access_token = refresh_token,
            token_type = "Bearer",
            expires_in = expiresIn,
            refresh_token
        });
    }

    internal sealed class TokenMarker { }
}
