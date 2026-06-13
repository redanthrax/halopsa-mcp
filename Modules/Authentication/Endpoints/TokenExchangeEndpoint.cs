using System.Net.Http.Headers;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.Common.Models;
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
        AppConfig config,
        ITokenStore tokenStorage,
        IOAuthFlowStore flowStore,
        HaloPsaConfig haloPsaConfig,
        IHttpClientFactory httpClientFactory,
        ILogger<TokenMarker> logger,
        [FromForm] string? grant_type,
        [FromForm] string? code,
        [FromForm] string? code_verifier,
        [FromForm] string? refresh_token,
        [FromForm] string? client_id,
        [FromForm] string? redirect_uri,
        [FromForm] string? resource) {
        return (grant_type ?? "authorization_code") switch {
            "authorization_code" => await HandleAuthorizationCode(
                config, tokenStorage, flowStore, logger, code, code_verifier, client_id, redirect_uri, resource)
                .ConfigureAwait(false),
            "refresh_token" => await HandleRefreshToken(
                config, tokenStorage, haloPsaConfig, httpClientFactory, logger, refresh_token, resource)
                .ConfigureAwait(false),
            _ => Results.BadRequest(new {
                error = "unsupported_grant_type",
                error_description = $"grant_type '{grant_type}' is not supported"
            })
        };
    }

    private static async Task<IResult> HandleAuthorizationCode(
        AppConfig config,
        ITokenStore tokenStorage,
        IOAuthFlowStore flowStore,
        ILogger logger,
        string? code,
        string? code_verifier,
        string? client_id,
        string? redirect_uri,
        string? resource) {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(code_verifier)) {
            return Results.BadRequest(new {
                error = "invalid_request", error_description = "Missing code or code_verifier"
            });
        }

        if (!OAuthResourceValidation.IsValid(config, resource)) {
            return Results.BadRequest(new {
                error = "invalid_target",
                error_description = "Requested resource is not supported by this authorization server"
            });
        }

        if (!flowStore.TryRemoveCompleted(code, out var completed) || completed is null ||
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > completed.Expires) {
            logger.LogWarning("Token rejected — code expired/unknown | codeHint={CodeHint}",
                SecretRedactor.Hint(code));
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "Code expired or unknown"
            });
        }

        if (!string.IsNullOrEmpty(completed.ClientId)) {
            if (string.IsNullOrEmpty(client_id) ||
                !string.Equals(client_id, completed.ClientId, StringComparison.Ordinal)) {
                logger.LogWarning("Token rejected — client_id mismatch | codeHint={CodeHint}",
                    SecretRedactor.Hint(code));
                return Results.BadRequest(new {
                    error = "invalid_grant", error_description = "client_id mismatch"
                });
            }
        }

        if (!string.IsNullOrEmpty(completed.ClientRedirectUri) && !string.IsNullOrEmpty(redirect_uri) &&
            !string.Equals(
                RedirectUriNormalizer.Normalize(redirect_uri),
                completed.ClientRedirectUri,
                StringComparison.Ordinal)) {
            logger.LogWarning("Token rejected — redirect_uri mismatch | codeHint={CodeHint}",
                SecretRedactor.Hint(code));
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "redirect_uri mismatch"
            });
        }

        var boundResource = completed.Resource ?? OAuthResourceValidation.ExpectedResource(config);
        if (!string.IsNullOrEmpty(resource) &&
            !string.Equals(
                OAuthResourceValidation.NormalizeResourceUri(resource),
                OAuthResourceValidation.NormalizeResourceUri(boundResource),
                StringComparison.OrdinalIgnoreCase)) {
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "resource mismatch"
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
        var (mcpToken, mcpRefresh) = await tokenStorage.CreateSessionAsync(
            completed.AccessToken, completed.RefreshToken, expiresAt).ConfigureAwait(false);

        return Results.Ok(new {
            access_token = mcpToken,
            token_type = "Bearer",
            expires_in = completed.ExpiresIn,
            refresh_token = mcpRefresh,
            resource = boundResource
        });
    }

    private static async Task<IResult> HandleRefreshToken(
        AppConfig config,
        ITokenStore tokenStorage,
        HaloPsaConfig haloPsaConfig,
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        string? refresh_token,
        string? resource) {
        if (string.IsNullOrEmpty(refresh_token)) {
            return Results.BadRequest(new {
                error = "invalid_request", error_description = "Missing refresh_token"
            });
        }

        if (!OAuthResourceValidation.IsValid(config, resource)) {
            return Results.BadRequest(new {
                error = "invalid_target",
                error_description = "Requested resource is not supported by this authorization server"
            });
        }

        var found = tokenStorage.FindByRefreshToken(refresh_token);
        if (found is null) {
            logger.LogWarning("Refresh rejected — unknown refresh token | mcr={Hint}",
                SecretRedactor.Hint(refresh_token));
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "Unknown or already-rotated refresh token"
            });
        }
        var mcpAccessToken = found.Value.Key;
        var session = found.Value.Value;
        if (string.IsNullOrEmpty(session.RefreshToken)) {
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "Session has no upstream refresh token"
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
            logger.LogError("HaloPSA refresh failed | status={Status}", response.StatusCode);
            _ = error; // body not logged: may contain PII / token fragments
            return Results.BadRequest(new {
                error = "invalid_grant", error_description = "HaloPSA rejected the refresh token"
            });
        }

        var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid HaloPSA token response");
        var expiresIn = tokenData.expires_in > 0 ? tokenData.expires_in : 3600;
        var newExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn - 60) * 1000;
        var newRefresh = tokenData.refresh_token ?? session.RefreshToken;

        // One-time-use rotation: the supplied refresh_token is now invalid; client
        // must use the new value we return.
        var newMcpRefresh = await tokenStorage.RotateRefreshTokenAsync(
            mcpAccessToken, tokenData.access_token, newRefresh, newExpiresAt).ConfigureAwait(false);

        logger.LogInformation("Refresh OK | mcp={Hint} expiresIn={Seconds}s",
            SecretRedactor.Hint(mcpAccessToken), expiresIn);

        return Results.Ok(new {
            access_token = mcpAccessToken,
            token_type = "Bearer",
            expires_in = expiresIn,
            refresh_token = newMcpRefresh,
            resource = OAuthResourceValidation.BindResource(config, resource)
        });
    }

    internal sealed class TokenMarker { }
}
