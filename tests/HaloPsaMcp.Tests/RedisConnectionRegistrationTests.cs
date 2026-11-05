using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa;
using HaloPsaMcp.Modules.HaloPsa.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace HaloPsaMcp.Tests;

/// <summary>
/// Regression: on the file backend, IConnectionMultiplexer used to be registered with a
/// factory that threw, so optional GetService consumers (HaloPsaTokenRefresher) crashed
/// and every authenticated-path /mcp request returned 500.
/// </summary>
public class RedisConnectionRegistrationTests {
    private static IConfiguration Config(string? backend) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(backend is null
                ? []
                : new Dictionary<string, string?> { ["HALOPSA_TOKEN_STORE_BACKEND"] = backend })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("file")]
    public void Non_redis_backend_does_not_register_multiplexer(string? backend) {
        var services = new ServiceCollection();
        services.AddRedisConnectionIfNeeded(Config(backend));

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IConnectionMultiplexer));
        Assert.Null(services.BuildServiceProvider().GetService<IConnectionMultiplexer>());
    }

    [Theory]
    [InlineData("redis")]
    [InlineData("Redis")]
    public void Redis_backend_registers_multiplexer(string backend) {
        var services = new ServiceCollection();
        services.AddRedisConnectionIfNeeded(Config(backend));

        Assert.Contains(services, d => d.ServiceType == typeof(IConnectionMultiplexer));
    }

    [Fact]
    public void File_backend_resolves_HaloPsaTokenRefresher() {
        var configuration = Config("file");
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig {
            AuthBaseUrl = "http://localhost:3000",
            PublicBaseUrl = "http://localhost:3000",
            HttpPort = 3000,
            HaloPsa = new HaloPsaSettings {
                Url = "https://test.halopsa.com",
                ClientId = "test",
                TokenStoreBackend = "file",
                TokenStorePath = Path.Combine(Path.GetTempPath(), "halopsa-redis-reg-test-" + Guid.NewGuid().ToString("N"), "tokens.json")
            }
        });
        services.AddDataProtection().SetApplicationName("HaloPsaMcp.Tests");
        services.AddLogging();
        services.AddRedisConnectionIfNeeded(configuration);
        services.AddTokenStore();
        new HaloPsaModuleRegistrar().Register(services, configuration);

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<HaloPsaTokenRefresher>());
    }
}
