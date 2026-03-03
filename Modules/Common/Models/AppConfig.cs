using System.Globalization;

namespace HaloPsaMcp.Modules.Common.Models;

/// <summary>
/// Application configuration loaded from environment variables
/// </summary>
internal class AppConfig {
    public HaloPsaSettings HaloPsa { get; set; } = new();
    public required string AuthBaseUrl { get; init; }
    public int McpPort { get; init; }
    public int HttpPort { get; init; }

    public static AppConfig LoadFromEnvironment() {
        var haloPsaUrl = Environment.GetEnvironmentVariable("HALOPSA_URL")
            ?? throw new InvalidOperationException("HALOPSA_URL environment variable is required");
        var clientId = Environment.GetEnvironmentVariable("HALOPSA_CLIENT_ID")
            ?? throw new InvalidOperationException("HALOPSA_CLIENT_ID environment variable is required");
        var tokenStorePath = Environment.GetEnvironmentVariable("HALOPSA_TOKEN_STORE") ?? "./data/tokens.json";
        var authBaseUrl = Environment.GetEnvironmentVariable("AUTH_BASE_URL") ?? "";
        var mcpPort = int.Parse(
            Environment.GetEnvironmentVariable("MCP_PORT") ?? "8000",
            CultureInfo.InvariantCulture);
        var httpPort = int.Parse(
            Environment.GetEnvironmentVariable("HTTP_PORT") ?? "3000",
            CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(authBaseUrl)) {
            authBaseUrl = $"http://localhost:{httpPort}";
        }

        return new AppConfig {
            HaloPsa = new HaloPsaSettings {
                Url = haloPsaUrl,
                ClientId = clientId,
                ClientSecret = Environment.GetEnvironmentVariable("HALOPSA_CLIENT_SECRET"),
                TokenStorePath = tokenStorePath
            },
            AuthBaseUrl = authBaseUrl,
            McpPort = mcpPort,
            HttpPort = httpPort
        };
    }
}
