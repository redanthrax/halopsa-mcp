using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.Mvc;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

/// <summary>
/// OAuth 2.1 PKCE authentication endpoints as minimal API
/// </summary>
internal static class OAuthEndpoints {
    private const int VerifierTtlMs = 10 * 60 * 1000;

    private static readonly ConcurrentDictionary<string, PendingAuth> PendingAuths = new();
    private static readonly ConcurrentDictionary<string, CompletedAuth> CompletedAuths = new();
    
    private static TokenStorageService? _tokenStorage;
    private static ILogger? _logger;

    public static void MapOAuthEndpoints(this IEndpointRouteBuilder app) {
        _tokenStorage = app.ServiceProvider.GetRequiredService<TokenStorageService>();
        _logger = app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(OAuthEndpoints));
        
        // OAuth 2.1 discovery endpoints
        app.MapGet("/.well-known/oauth-protected-resource", ProtectedResourceMetadata);
        app.MapGet("/.well-known/oauth-authorization-server", AuthorizationServerMetadata);

        // OAuth 2.1 PKCE flow
        app.MapPost("/register", DynamicClientRegistration).DisableAntiforgery();
        app.MapGet("/authorize", Authorize);
        app.MapGet("/callback", Callback);
        app.MapPost("/token", TokenExchange).DisableAntiforgery();

        // Direct browser login (bypasses MCP client PKCE)
        app.MapGet("/login", DirectLogin);
        app.MapGet("/success", () => Results.Ok("Authentication successful! You can close this window."));
    }

    private static IResult ProtectedResourceMetadata(AppConfig config) {
        return Results.Ok(new {
            resource = config.AuthBaseUrl,
            authorization_servers = new[] { config.AuthBaseUrl },
            bearer_methods_supported = new[] { "header" },
            resource_name = "HaloPSA MCP"
        });
    }

    private static IResult AuthorizationServerMetadata(AppConfig config) {
        return Results.Ok(new {
            issuer = config.AuthBaseUrl,
            authorization_endpoint = $"{config.AuthBaseUrl}/authorize",
            token_endpoint = $"{config.AuthBaseUrl}/token",
            registration_endpoint = $"{config.AuthBaseUrl}/register",
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code" },
            code_challenge_methods_supported = new[] { "S256" },
            token_endpoint_auth_methods_supported = new[] { "none" }
        });
    }

    private static IResult DynamicClientRegistration([FromBody] JsonElement? body) {
        var clientId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);
        
        string[] redirectUris = Array.Empty<string>();
        if (body.HasValue && body.Value.TryGetProperty("redirect_uris", out var urisElement) && 
            urisElement.ValueKind == JsonValueKind.Array) {
            redirectUris = urisElement.EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }
        
        return Results.Created(string.Empty, new {
            client_id = clientId,
            client_secret_expires_at = 0,
            redirect_uris = redirectUris,
            grant_types = new[] { "authorization_code" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "none"
        });
    }

    private static IResult DirectLogin(AppConfig config, HaloPsaConfig haloPsaConfig) {
        CleanExpiredEntries();

        var verifier = PkceHelper.GenerateCodeVerifier();
        var challenge = PkceHelper.GenerateCodeChallenge(verifier);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);

        PendingAuths[state] = new PendingAuth {
            HaloPsaVerifier = verifier,
            ClientRedirectUri = $"{config.AuthBaseUrl}/success",
            ClientState = null,
            ClientCodeChallenge = challenge,
            ClientCode = string.Empty,
            Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + VerifierTtlMs,
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

    private static IResult Authorize(
        AppConfig config,
        HaloPsaConfig haloPsaConfig,
        [FromQuery] string? redirect_uri,
        [FromQuery] string? code_challenge,
        [FromQuery] string? state) {
        if (string.IsNullOrEmpty(redirect_uri) || string.IsNullOrEmpty(code_challenge)) {
            return Results.BadRequest("Missing redirect_uri or code_challenge");
        }

        if (!Uri.TryCreate(redirect_uri, UriKind.Absolute, out var parsedRedirect) ||
            parsedRedirect.Scheme != "https") {
            return Results.BadRequest("redirect_uri must be a valid HTTPS URL");
        }

        CleanExpiredEntries();

        var clientCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);
        var haloPsaVerifier = PkceHelper.GenerateCodeVerifier();
        var haloPsaChallenge = PkceHelper.GenerateCodeChallenge(haloPsaVerifier);
        var oauthState = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);

        PendingAuths[oauthState] = new PendingAuth {
            HaloPsaVerifier = haloPsaVerifier,
            ClientRedirectUri = redirect_uri,
            ClientState = state,
            ClientCodeChallenge = code_challenge,
            ClientCode = clientCode,
            Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + VerifierTtlMs
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

    private static async Task<IResult> Callback(
        AppConfig config,
        HaloPsaConfig haloPsaConfig,
        IHttpClientFactory httpClientFactory,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error) {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state)) {
            return Results.BadRequest("Authorization failed or missing parameters");
        }

        if (!PendingAuths.TryRemove(state, out var pending) ||
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > pending.Expires) {
            return Results.BadRequest("State expired or unknown — please try connecting again");
        }

        var httpClient = httpClientFactory.CreateClient();
        var tokenUrl = $"{haloPsaConfig.Url}/auth/token?tenant={Uri.EscapeDataString(haloPsaConfig.GetTenant())}";

        var parameters = new Dictionary<string, string> {
            ["grant_type"] = "authorization_code",
            ["client_id"] = haloPsaConfig.ClientId,
            ["redirect_uri"] = $"{config.AuthBaseUrl}/callback",
            ["code"] = code,
            ["code_verifier"] = pending.HaloPsaVerifier,
            ["scope"] = "all offline_access"
        };

        if (!string.IsNullOrEmpty(haloPsaConfig.ClientSecret)) {
            parameters["client_secret"] = haloPsaConfig.ClientSecret;
        }

        try {
            var response = await httpClient.PostAsync(
                tokenUrl,
                new FormUrlEncodedContent(parameters)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                var errorText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger?.LogError("HaloPSA token exchange failed: {StatusCode} - {Error}", response.StatusCode, errorText);
                return Results.Problem($"HaloPSA token exchange failed: {response.StatusCode} - {errorText}");
            }

            var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>().ConfigureAwait(false)
                ?? throw new InvalidOperationException("Invalid token response");

            var expiresIn = tokenData.expires_in > 0 ? tokenData.expires_in : 3600;

            if (pending.IsDirectLogin) {
                var entry = new UserTokenEntry {
                    AccessToken = tokenData.access_token,
                    RefreshToken = tokenData.refresh_token ?? string.Empty,
                    ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn - 60) * 1000
                };
                _ = _tokenStorage?.SaveTokenAsync(tokenData.access_token, entry);
                _logger?.LogInformation("Direct browser login completed, token saved");
                return Results.Redirect(pending.ClientRedirectUri);
            }

            CompletedAuths[pending.ClientCode] = new CompletedAuth {
                AccessToken = tokenData.access_token,
                RefreshToken = tokenData.refresh_token ?? string.Empty,
                ExpiresIn = expiresIn,
                ClientCodeChallenge = pending.ClientCodeChallenge,
                Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + VerifierTtlMs
            };

            var redirectUri = new UriBuilder(pending.ClientRedirectUri);
            var query = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
            query["code"] = pending.ClientCode;
            if (!string.IsNullOrEmpty(pending.ClientState)) {
                query["state"] = pending.ClientState;
            }
            redirectUri.Query = query.ToString();

            return Results.Redirect(redirectUri.ToString());
        } catch (Exception ex) {
            _logger?.LogError(ex, "OAuth callback token exchange error");
            return Results.Problem($"Token exchange error: {ex.Message}");
        }
    }

    private static IResult TokenExchange(
        [FromForm] string? code,
        [FromForm] string? code_verifier) {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(code_verifier)) {
            return Results.BadRequest(
                new { error = "invalid_request", error_description = "Missing code or code_verifier" });
        }

        if (!CompletedAuths.TryRemove(code, out var completed) ||
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > completed.Expires) {
            return Results.BadRequest(new { error = "invalid_grant", error_description = "Code expired or unknown" });
        }

        var challenge = PkceHelper.GenerateCodeChallenge(code_verifier);
        if (challenge != completed.ClientCodeChallenge) {
            return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE verification failed" });
        }

        var entry = new UserTokenEntry {
            AccessToken = completed.AccessToken,
            RefreshToken = completed.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (completed.ExpiresIn - 60) * 1000
        };
        
        _ = _tokenStorage?.SaveTokenAsync(completed.AccessToken, entry);
        _logger?.LogInformation("Token exchange completed, issued access token");

        return Results.Ok(new {
            access_token = completed.AccessToken,
            token_type = "Bearer",
            expires_in = completed.ExpiresIn,
            refresh_token = completed.RefreshToken
        });
    }

    private static void CleanExpiredEntries() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var kvp in PendingAuths.Where(x => now > x.Value.Expires)) {
            PendingAuths.TryRemove(kvp.Key, out _);
        }

        foreach (var kvp in CompletedAuths.Where(x => now > x.Value.Expires)) {
            CompletedAuths.TryRemove(kvp.Key, out _);
        }
        
        // Token cleanup is now handled by TokenStorageService
    }

    /// <summary>
    /// Get user token entry by access token (used by middleware)
    /// </summary>
    public static UserTokenEntry? GetUserToken(string accessToken) {
        return _tokenStorage?.GetToken(accessToken);
    }

    /// <summary>
    /// Get the most recent user token for stdio transport mode
    /// Returns null if no tokens available
    /// </summary>
    public static UserTokenEntry? GetDefaultUserToken() {
        return _tokenStorage?.GetDefaultToken();
    }
}
