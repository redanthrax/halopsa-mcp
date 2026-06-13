namespace HaloPsaMcp.Modules.Common.Security;

/// <summary>
/// CORS policy for browser-based MCP clients (e.g. Claude.ai org connectors).
/// </summary>
internal static class McpCorsConfiguration {
    public const string PolicyName = "McpBrowserClients";
    private const string OriginsEnv = "MCP_CORS_ALLOWED_ORIGINS";

    private static readonly string[] DefaultOrigins = ["https://claude.ai"];

    public static void AddMcpCors(this IServiceCollection services) {
        services.AddCors(options => {
            options.AddPolicy(PolicyName, policy => {
                policy.SetIsOriginAllowed(IsOriginAllowed)
                    .WithMethods("GET", "POST", "OPTIONS")
                    .WithHeaders(
                        "Authorization",
                        "Content-Type",
                        "Mcp-Session-Id",
                        "MCP-Protocol-Version",
                        "Last-Event-ID")
                    .WithExposedHeaders("Mcp-Session-Id", "WWW-Authenticate")
                    .AllowCredentials()
                    .SetPreflightMaxAge(TimeSpan.FromSeconds(600));
            });
        });
    }

    internal static bool IsOriginAllowed(string origin) {
        if (string.IsNullOrWhiteSpace(origin)) {
            return false;
        }

        foreach (var allowed in GetAllowedOrigins()) {
            if (string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    internal static IReadOnlyList<string> GetAllowedOrigins() {
        var env = Environment.GetEnvironmentVariable(OriginsEnv);
        if (!string.IsNullOrWhiteSpace(env)) {
            return env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static origin => !string.IsNullOrWhiteSpace(origin))
                .ToArray();
        }

        return DefaultOrigins;
    }
}
