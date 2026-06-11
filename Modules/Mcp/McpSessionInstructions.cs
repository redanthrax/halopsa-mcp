using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.Http;

namespace HaloPsaMcp.Modules.Mcp;

internal static class McpSessionInstructions {
    internal static string Build(AppConfig config, ITokenStore tokenStore, McpHostMode mode) {
        var loginUrl = HaloPsaMcpConstants.GetLoginUrl(config);
        if (tokenStore.HasValidTokens()) {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                AuthenticatedTemplate,
                config.HaloPsa.Url);
        }

        return mode == McpHostMode.DesktopStdio
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, DesktopUnauthenticatedTemplate, loginUrl)
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, HttpUnauthenticatedTemplate, loginUrl);
    }

    internal static string BuildForHttpSession(HttpContext context, AppConfig config, ITokenStore tokenStore) {
        if (IsBearerSessionValid(context, tokenStore)) {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                AuthenticatedTemplate,
                config.HaloPsa.Url);
        }

        return Build(config, tokenStore, McpHostMode.Http);
    }

    internal static bool IsBearerSessionValid(HttpContext context, ITokenStore tokenStore) {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        var token = authHeader["Bearer ".Length..];
        return tokenStore.IsValidSession(token);
    }

    private const string AuthenticatedTemplate = """
        HaloPSA MCP session is active for {0}.
        Call halopsa_auth_status to confirm identity before sensitive operations.
        """;

    private const string DesktopUnauthenticatedTemplate = """
        AUTHENTICATION REQUIRED — no HaloPSA session is active.

        On this first connection you must help the user sign in before any other HaloPSA tool:
        1. Immediately tell the user they need to sign in to HaloPSA.
        2. Give them this login URL: {0}
        3. Ask them to open it in a browser and complete sign-in (a browser window may already have opened).
        4. After they confirm sign-in, call halopsa_auth_status; proceed only when authenticated=true.

        Do not call halopsa_query or ticket tools until authentication succeeds.
        """;

    private const string HttpUnauthenticatedTemplate = """
        AUTHENTICATION REQUIRED — authorize this MCP connection before using HaloPSA tools.
        Your MCP client should complete OAuth using the server's protected-resource metadata.
        Browser login URL: {0}
        After connecting, call halopsa_auth_status to verify authenticated=true.
        """;
}
