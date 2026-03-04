using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 token exchange endpoint
/// </summary>
internal static class TokenExchangeEndpoint
{
    private static TokenStorageService? _tokenStorage;
    private static ILogger? _logger;

    public static void MapTokenExchange(this IEndpointRouteBuilder app)
    {
        _tokenStorage = app.ServiceProvider.GetRequiredService<TokenStorageService>();
        _logger = app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(TokenExchangeEndpoint));

        app.MapPost("/token", TokenExchange).DisableAntiforgery();
    }

    private static IResult TokenExchange(
        [FromForm] string? code,
        [FromForm] string? code_verifier)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(code_verifier))
        {
            return Results.BadRequest(
                new { error = "invalid_request", error_description = "Missing code or code_verifier" });
        }

        if (!OAuthStateManager.CompletedAuths.TryRemove(code, out var completed) ||
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > completed.Expires)
        {
            return Results.BadRequest(new { error = "invalid_grant", error_description = "Code expired or unknown" });
        }

        var challenge = PkceHelper.GenerateCodeChallenge(code_verifier);
        if (challenge != completed.ClientCodeChallenge)
        {
            return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE verification failed" });
        }

        var entry = new UserTokenEntry
        {
            AccessToken = completed.AccessToken,
            RefreshToken = completed.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (completed.ExpiresIn - 60) * 1000
        };

        _ = _tokenStorage?.SaveTokenAsync(completed.AccessToken, entry);
        _logger?.LogInformation("Token exchange completed, issued access token");

        return Results.Ok(new
        {
            access_token = completed.AccessToken,
            token_type = "Bearer",
            expires_in = completed.ExpiresIn,
            refresh_token = completed.RefreshToken
        });
    }
}