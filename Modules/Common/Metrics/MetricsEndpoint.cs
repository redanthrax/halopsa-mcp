using System.Globalization;
using System.Text;
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.AspNetCore.Builder;

namespace HaloPsaMcp.Modules.Common.Metrics;

/// <summary>
/// Gated Prometheus-style metrics endpoint. Enable with MCP_METRICS_ENABLED=1.
/// Optional bearer gate via MCP_METRICS_TOKEN.
/// </summary>
internal static class MetricsEndpoint {
    public static void MapMetrics(this WebApplication app, DateTime startedAt) {
        if (!string.Equals(Environment.GetEnvironmentVariable("MCP_METRICS_ENABLED"), "1", StringComparison.Ordinal)) {
            return;
        }

        app.MapGet("/metrics", (
            ITokenStore tokens,
            IOAuthFlowStore oauth,
            ClientRegistrationStore clients,
            HttpContext http) => {
            if (!IsAuthorized(http)) {
                return Results.Unauthorized();
            }

            var uptime = (long)(DateTime.UtcNow - startedAt).TotalSeconds;
            var sb = new StringBuilder(512);
            AppendGauge(sb, "halopsa_mcp_uptime_seconds", uptime);
            AppendGauge(sb, "halopsa_mcp_sessions_total", tokens.SessionCount);
            AppendGauge(sb, "halopsa_mcp_sessions_active", tokens.ActiveSessionCount);
            AppendGauge(sb, "halopsa_mcp_oauth_pending", oauth.PendingCount);
            AppendGauge(sb, "halopsa_mcp_oauth_completed", oauth.CompletedCount);
            AppendGauge(sb, "halopsa_mcp_dcr_clients", clients.Count);
            return Results.Text(sb.ToString(), "text/plain; version=0.0.4; charset=utf-8");
        });
    }

    private static bool IsAuthorized(HttpContext http) {
        var required = Environment.GetEnvironmentVariable("MCP_METRICS_TOKEN");
        if (string.IsNullOrWhiteSpace(required)) {
            return true;
        }
        var auth = http.Request.Headers.Authorization.ToString();
        return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && string.Equals(auth["Bearer ".Length..], required, StringComparison.Ordinal);
    }

    private static void AppendGauge(StringBuilder sb, string name, long value) {
        sb.Append("# TYPE ").Append(name).AppendLine(" gauge");
        sb.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).AppendLine();
    }
}
