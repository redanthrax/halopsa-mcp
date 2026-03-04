using System.Diagnostics;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Middleware;

/// <summary>
/// Middleware to authenticate MCP requests using Bearer tokens.
/// OAuth 2.1 Flow: unauthenticated requests receive 401 with WWW-Authenticate header
/// pointing to /.well-known/oauth-protected-resource for OAuth discovery.
/// Claude Desktop will automatically follow the OAuth flow.
/// </summary>
internal class McpAuthenticationMiddleware {
    private readonly RequestDelegate _next;
    private readonly ILogger<McpAuthenticationMiddleware> _logger;

    public McpAuthenticationMiddleware(RequestDelegate next, ILogger<McpAuthenticationMiddleware> logger) {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, McpAuthenticationService authService, TokenStorageService tokenStorage) {
        var sw = Stopwatch.StartNew();

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        string? token = null;

        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            token = authHeader.Substring(7);
        }

        if (string.IsNullOrEmpty(token)) {
            _logger.LogWarning(
                "Auth rejected — no Bearer token | path={Path} method={Method}",
                context.Request.Path, context.Request.Method);

            context.Response.StatusCode = 401;
            context.Response.Headers.Append("WWW-Authenticate",
                $"Bearer resource_metadata=\"{context.Request.Scheme}://" +
                $"{context.Request.Host}/.well-known/oauth-protected-resource\"");
            await context.Response.WriteAsJsonAsync(
                new { error = "unauthorized", error_description = "Bearer token required" })
                .ConfigureAwait(false);
            return;
        }

        var tokenHint = TokenHint(token);
        var (isValid, fromCache) = await authService.ValidateTokenWithCacheInfoAsync(token).ConfigureAwait(false);

        if (!isValid) {
            sw.Stop();
            _logger.LogWarning(
                "Auth rejected — invalid/expired token | token={TokenHint} path={Path} elapsed={ElapsedMs}ms",
                tokenHint, context.Request.Path, sw.ElapsedMilliseconds);

            authService.InvalidateToken(token);

            context.Response.StatusCode = 401;
            context.Response.Headers.Append("WWW-Authenticate",
                $"Bearer resource_metadata=\"{context.Request.Scheme}://" +
                $"{context.Request.Host}/.well-known/oauth-protected-resource\"");
            await context.Response.WriteAsJsonAsync(
                new { error = "invalid_token", error_description = "Token is invalid or expired" })
                .ConfigureAwait(false);
            return;
        }

        sw.Stop();
        var userEntry = tokenStorage.GetToken(token);
        var expiresIn = userEntry?.ExpiresAt != null
            ? TimeSpan.FromMilliseconds(userEntry.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            : (TimeSpan?)null;

        _logger.LogInformation(
            "Auth OK | token={TokenHint} cached={FromCache} expiresIn={ExpiresIn} path={Path} elapsed={ElapsedMs}ms",
            tokenHint,
            fromCache,
            expiresIn.HasValue ? $"{(int)expiresIn.Value.TotalMinutes}m" : "unknown",
            context.Request.Path,
            sw.ElapsedMilliseconds);

        authService.StoreTokenInContext(
            context,
            token,
            userEntry?.RefreshToken,
            userEntry?.ExpiresAt
        );

        await _next(context).ConfigureAwait(false);
    }

    private static string TokenHint(string token) =>
        token.Length >= 8 ? $"...{token[^8..]}" : "***";
}
