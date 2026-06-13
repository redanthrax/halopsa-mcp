using System.Globalization;
using HaloPsaMcp.Modules.Common.Security;
using Microsoft.Extensions.Configuration;

namespace HaloPsaMcp.Modules.Common.Models;

/// <summary>
/// Application configuration loaded from environment variables
/// </summary>
public class AppConfig {
    public HaloPsaSettings HaloPsa { get; set; } = new();
    public required string AuthBaseUrl { get; init; }
    /// <summary>
    /// Public base URL used for login links and other user-facing URLs.
    /// Defaults to AuthBaseUrl. Set HALOPSA_PUBLIC_URL (or PUBLIC_BASE_URL) to
    /// override when the externally reachable URL differs from the OAuth redirect base.
    /// </summary>
    public string PublicBaseUrl { get; init; } = "";
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
        var tokenStoreBackend = config["HALOPSA_TOKEN_STORE_BACKEND"] ?? "file";
        var tokenStorePath = config["HALOPSA_TOKEN_STORE"] ?? "./data/tokens.json";
        var redisConnection = SecretEnv.Get("HALOPSA_REDIS_CONNECTION");
        var authBaseUrl = config["AUTH_BASE_URL"] ?? "";
        var publicBaseUrl = config["HALOPSA_PUBLIC_URL"]
            ?? config["PUBLIC_BASE_URL"]
            ?? authBaseUrl;
        var httpPort = int.Parse(config["HTTP_PORT"] ?? "3000", CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(authBaseUrl)) {
            authBaseUrl = $"http://localhost:{httpPort}";
        }
        if (string.IsNullOrEmpty(publicBaseUrl)) {
            publicBaseUrl = authBaseUrl;
        }

        return new AppConfig {
            HaloPsa = new HaloPsaSettings {
                Url = haloPsaUrl,
                ClientId = clientId,
                ClientSecret = SecretEnv.Get("HALOPSA_CLIENT_SECRET"),
                TokenStoreBackend = tokenStoreBackend,
                TokenStorePath = tokenStorePath,
                RedisConnection = redisConnection
            },
            AuthBaseUrl = authBaseUrl,
            PublicBaseUrl = publicBaseUrl,
            HttpPort = httpPort
        };
    }
}
