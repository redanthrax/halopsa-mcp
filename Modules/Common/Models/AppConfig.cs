using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace HaloPsaMcp.Modules.Common.Models;

/// <summary>
/// Application configuration loaded from environment variables
/// </summary>
public class AppConfig {
    public HaloPsaSettings HaloPsa { get; set; } = new();
    public required string AuthBaseUrl { get; init; }
    public int HttpPort { get; init; }

    public static AppConfig LoadFromEnvironment() {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(AppConfig).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        return LoadFromConfiguration(config);
    }

    public static AppConfig LoadFromConfiguration(IConfiguration config) {
        ArgumentNullException.ThrowIfNull(config);

        var haloPsaUrl = config["HALOPSA_URL"]
            ?? throw new InvalidOperationException("HALOPSA_URL environment variable is required");
        var clientId = config["HALOPSA_CLIENT_ID"]
            ?? throw new InvalidOperationException("HALOPSA_CLIENT_ID environment variable is required");
        var tokenStorePath = config["HALOPSA_TOKEN_STORE"] ?? "./data/tokens.json";
        var authBaseUrl = config["AUTH_BASE_URL"] ?? "";
        var httpPort = int.Parse(config["HTTP_PORT"] ?? "3000", CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(authBaseUrl)) {
            authBaseUrl = $"http://localhost:{httpPort}";
        }

        return new AppConfig {
            HaloPsa = new HaloPsaSettings {
                Url = haloPsaUrl,
                ClientId = clientId,
                ClientSecret = config["HALOPSA_CLIENT_SECRET"],
                TokenStorePath = tokenStorePath
            },
            AuthBaseUrl = authBaseUrl,
            HttpPort = httpPort
        };
    }
}
