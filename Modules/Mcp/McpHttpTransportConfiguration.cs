using ModelContextProtocol.AspNetCore;

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// Streamable HTTP transport options for production deployments.
/// </summary>
internal static class McpHttpTransportConfiguration {
    private const string StatelessEnv = "MCP_HTTP_STATELESS";
    private const string TokenBackendEnv = "HALOPSA_TOKEN_STORE_BACKEND";

    internal static void Configure(HttpServerTransportOptions options) {
        McpServerSetup.ConfigureHttpSessionInstructions(options);
        options.Stateless = ResolveStateless();
        if (!options.Stateless) {
            options.IdleTimeout = ResolveIdleTimeout();
        }
    }

    /// <summary>
    /// When true, each POST /mcp is handled independently (no Mcp-Session-Id affinity).
    /// Required for multi-replica deployments where callers (e.g. Claude.ai) do not
    /// stick to one pod.
    /// </summary>
    internal static bool ResolveStateless() {
        var raw = Environment.GetEnvironmentVariable(StatelessEnv);
        if (string.IsNullOrWhiteSpace(raw)) {
            return IsRedisBackend();
        }
        if (raw is "0" or "false" or "no" or "off") {
            return false;
        }
        if (raw is "auto") {
            return IsRedisBackend();
        }
        return raw is "1" or "true" or "yes" or "on";
    }

    internal static bool IsRedisBackend() =>
        string.Equals(
            Environment.GetEnvironmentVariable(TokenBackendEnv),
            "redis",
            StringComparison.OrdinalIgnoreCase);

    private static TimeSpan ResolveIdleTimeout() {
        var raw = Environment.GetEnvironmentVariable("MCP_HTTP_IDLE_TIMEOUT_MINUTES");
        if (int.TryParse(raw, out var minutes) && minutes > 0) {
            return TimeSpan.FromMinutes(minutes);
        }
        return TimeSpan.FromHours(2);
    }
}
