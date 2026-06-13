using HaloPsaMcp.Modules.Common.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// RFC 9728 / MCP 2025-06-18 discovery URLs and metadata builders.
/// </summary>
internal static class OAuthDiscovery {
    public const string McpEndpointPath = "/mcp";
    public const string ProtectedResourcePathSuffix = "mcp";
    private const string DcrInitialAccessTokenEnv = "MCP_DCR_INITIAL_ACCESS_TOKEN";
    private const string OpenDcrEnv = "MCP_ALLOW_OPEN_DCR";

    public static string ResolveMcpResourceUrl(AppConfig config) =>
        $"{AppConfigRuntime.ResolveAuthBaseUrl(config)}{McpEndpointPath}";

    public static string ProtectedResourceMetadataUrl(AppConfig config, bool pathSuffixed) {
        var baseUrl = AppConfigRuntime.ResolveAuthBaseUrl(config);
        return pathSuffixed
            ? $"{baseUrl}/.well-known/oauth-protected-resource/{ProtectedResourcePathSuffix}"
            : $"{baseUrl}/.well-known/oauth-protected-resource";
    }

    public static string AuthorizationServerMetadataUrl(AppConfig config, bool pathSuffixed) {
        var baseUrl = AppConfigRuntime.ResolveAuthBaseUrl(config);
        return pathSuffixed
            ? $"{baseUrl}/.well-known/oauth-authorization-server/{ProtectedResourcePathSuffix}"
            : $"{baseUrl}/.well-known/oauth-authorization-server";
    }

    public static bool IsDcrInitialAccessTokenConfigured() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DcrInitialAccessTokenEnv));

    public static bool IsOpenDcrEnabled() {
        if (IsTruthy(Environment.GetEnvironmentVariable(OpenDcrEnv))) {
            return true;
        }
        return !IsDcrInitialAccessTokenConfigured();
    }

    public static bool RequiresDcrInitialAccessToken() =>
        IsDcrInitialAccessTokenConfigured() && !IsTruthy(Environment.GetEnvironmentVariable(OpenDcrEnv));

    public static object BuildProtectedResourceMetadata(AppConfig config) {
        var authBaseUrl = AppConfigRuntime.ResolveAuthBaseUrl(config);
        return new {
            resource = ResolveMcpResourceUrl(config),
            authorization_servers = new[] { authBaseUrl },
            bearer_methods_supported = new[] { "header" },
            resource_name = "HaloPSA MCP",
            scopes_supported = new[] { "all" },
            resource_documentation = "https://github.com/redanthrax/halopsa-mcp",
            resource_signing_alg_values_supported = Array.Empty<string>()
        };
    }

    public static object BuildAuthorizationServerMetadata(AppConfig config) {
        var authBaseUrl = AppConfigRuntime.ResolveAuthBaseUrl(config);
        if (RequiresDcrInitialAccessToken()) {
            return new {
                issuer = authBaseUrl,
                authorization_endpoint = $"{authBaseUrl}/authorize",
                token_endpoint = $"{authBaseUrl}/token",
                registration_endpoint = $"{authBaseUrl}/register",
                response_types_supported = new[] { "code" },
                grant_types_supported = new[] { "authorization_code", "refresh_token" },
                code_challenge_methods_supported = new[] { "S256" },
                token_endpoint_auth_methods_supported = new[] { "none" },
                registration_endpoint_auth_methods_supported = new[] { "bearer" }
            };
        }

        return new {
            issuer = authBaseUrl,
            authorization_endpoint = $"{authBaseUrl}/authorize",
            token_endpoint = $"{authBaseUrl}/token",
            registration_endpoint = $"{authBaseUrl}/register",
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            code_challenge_methods_supported = new[] { "S256" },
            token_endpoint_auth_methods_supported = new[] { "none" }
        };
    }

    public static bool IsKnownProtectedResourcePath(string? resourcePath) =>
        string.IsNullOrEmpty(resourcePath) ||
        string.Equals(resourcePath.Trim('/'), ProtectedResourcePathSuffix, StringComparison.Ordinal);

    public static bool IsKnownAuthorizationServerPath(string? resourcePath) =>
        string.IsNullOrEmpty(resourcePath) ||
        string.Equals(resourcePath.Trim('/'), ProtectedResourcePathSuffix, StringComparison.Ordinal);

    private static bool IsTruthy(string? value) =>
        value is "1" or "true" or "yes" or "on";
}
