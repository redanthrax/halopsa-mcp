using HaloPsaMcp.Modules.Authentication.Endpoints;
using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Middleware;

/// <summary>
/// Middleware to authenticate MCP requests using Bearer tokens
/// Matches the TypeScript implementation's authentication flow
/// 
/// OAuth 2.1 Flow: Unauthenticated requests receive 401 with WWW-Authenticate header
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

    public async Task InvokeAsync(HttpContext context, McpAuthenticationService authService) {
        // Extract Bearer token from Authorization header
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        string? token = null;

        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            token = authHeader.Substring(7);
        }

        if (string.IsNullOrEmpty(token)) {
            _logger.LogWarning("MCP request rejected: no Bearer token on {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            context.Response.Headers.Append("WWW-Authenticate",
                $"Bearer resource_metadata=\"{context.Request.Scheme}://" +
                $"{context.Request.Host}/.well-known/oauth-protected-resource\"");
            await context.Response.WriteAsJsonAsync(
                new { error = "unauthorized", error_description = "Bearer token required" })
                .ConfigureAwait(false);
            return;
        }

        var isValid = await authService.ValidateTokenAsync(token).ConfigureAwait(false);
        if (!isValid) {
            _logger.LogWarning("MCP request rejected: invalid token on {Path}", context.Request.Path);
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

        // Get user token entry from OAuthEndpoints (if it exists)
        var userEntry = OAuthEndpoints.GetUserToken(token);

        authService.StoreTokenInContext(
            context,
            token,
            userEntry?.RefreshToken,
            userEntry?.ExpiresAt
        );

        await _next(context).ConfigureAwait(false);
    }
}
