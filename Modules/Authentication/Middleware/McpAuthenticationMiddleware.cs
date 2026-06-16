using System.Diagnostics;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.Authentication.Middleware;

/// <summary>
/// Authenticates MCP requests using opaque MCP session Bearer tokens.
/// Validation is purely local (ITokenStore lookup); HaloPSA tokens
/// are loaded into HttpContext.Items for downstream tool handlers.
/// </summary>
internal class McpAuthenticationMiddleware {
    private readonly RequestDelegate _next;
    private readonly ILogger<McpAuthenticationMiddleware> _logger;

    public McpAuthenticationMiddleware(RequestDelegate next, ILogger<McpAuthenticationMiddleware> logger) {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        McpAuthenticationService authService,
        ITokenStore tokenStorage,
        HaloPsaTokenRefresher tokenRefresher,
        AppConfig appConfig) {
        // CORS preflight must not require auth (handled by UseCors; pass through if reached here).
        if (HttpMethods.IsOptions(context.Request.Method)) {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var sw = Stopwatch.StartNew();
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        string? token = null;

        if (!string.IsNullOrEmpty(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            token = authHeader.Substring(7);
        }

        if (string.IsNullOrEmpty(token)) {
            _logger.LogWarning(
                "Auth rejected — no Bearer token | path={Path} method={Method}",
                context.Request.Path, context.Request.Method);
            await Reject401(context, appConfig, "unauthorized", "Bearer token required").ConfigureAwait(false);
            return;
        }

        var hint = SecretRedactor.Hint(token);
        var (isValid, _) = await authService.ValidateTokenWithCacheInfoAsync(token).ConfigureAwait(false);

        if (!isValid) {
            sw.Stop();
            _logger.LogWarning(
                "Auth rejected — invalid/expired session | mcp={Hint} path={Path} elapsed={ElapsedMs}ms",
                hint, context.Request.Path, sw.ElapsedMilliseconds);
            await Reject401(context, appConfig, "invalid_token", "Token is invalid or expired").ConfigureAwait(false);
            return;
        }

        sw.Stop();
        var entry = tokenStorage.GetToken(token);
        if (entry is null) {
            await Reject401(context, appConfig, "invalid_token", "Session not found").ConfigureAwait(false);
            return;
        }

        var expiresIn = TimeSpan.FromMilliseconds(
            entry.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        if (expiresIn.TotalMinutes < 0) {
            _logger.LogWarning(
                "Auth OK but HaloPSA access expired | mcp={Hint} expiresIn={ExpiresIn}m path={Path}",
                hint, (int)expiresIn.TotalMinutes, context.Request.Path);
        } else {
            _logger.LogInformation(
                "Auth OK | mcp={Hint} expiresIn={ExpiresIn}m path={Path} elapsed={ElapsedMs}ms",
                hint, (int)expiresIn.TotalMinutes,
                context.Request.Path, sw.ElapsedMilliseconds);
        }

        if (HaloPsaTokenRefresher.NeedsRefresh(entry.ExpiresAt) &&
            !string.IsNullOrWhiteSpace(entry.RefreshToken)) {
            try {
                await tokenRefresher.EnsureFreshAsync(
                    token,
                    token,
                    entry.AccessToken,
                    entry.RefreshToken,
                    entry.ExpiresAt,
                    onRefreshed: null,
                    context.RequestAborted).ConfigureAwait(false);
                entry = tokenStorage.GetToken(token) ?? entry;
            } catch (HttpRequestException ex) when (HaloPsaResponseSanitizer.IsTokenRefreshFailure(ex)) {
                sw.Stop();
                _logger.LogWarning(
                    "Auth rejected — HaloPSA refresh failed | mcp={Hint} path={Path} elapsed={ElapsedMs}ms",
                    hint, context.Request.Path, sw.ElapsedMilliseconds);
                await Reject401(
                    context, appConfig, "invalid_token",
                    "HaloPSA session expired — sign in again").ConfigureAwait(false);
                return;
            }
        }

        authService.StoreSessionInContext(
            context, token, entry.AccessToken, entry.RefreshToken, entry.ExpiresAt);

        await _next(context).ConfigureAwait(false);
    }

    private static async Task Reject401(HttpContext context, AppConfig appConfig, string err, string desc) {
        context.Response.StatusCode = 401;
        var metadata = OAuthDiscovery.ProtectedResourceMetadataUrl(appConfig, pathSuffixed: true);
        context.Response.Headers.Append("WWW-Authenticate",
            $"Bearer error=\"{err}\", error_description=\"{EscapeChallenge(desc)}\", resource_metadata=\"{metadata}\"");
        await context.Response.WriteAsJsonAsync(
            new { error = err, error_description = desc }).ConfigureAwait(false);
    }

    private static string EscapeChallenge(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
