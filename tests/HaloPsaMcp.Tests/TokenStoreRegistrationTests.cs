using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HaloPsaMcp.Tests;

public class TokenStoreRegistrationTests {
    [Fact]
    public void File_backend_resolves_FileTokenStore() {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig {
            AuthBaseUrl = "http://localhost:3000",
            PublicBaseUrl = "http://localhost:3000",
            HttpPort = 3000,
            HaloPsa = new HaloPsaSettings {
                Url = "https://test.halopsa.com",
                ClientId = "test",
                TokenStoreBackend = "file",
                TokenStorePath = Path.Combine(Path.GetTempPath(), "halopsa-reg-test-" + Guid.NewGuid().ToString("N"), "tokens.json")
            }
        });
        services.AddDataProtection().SetApplicationName("HaloPsaMcp.Tests");
        services.AddLogging();
        services.AddTokenStore();

        var store = services.BuildServiceProvider().GetRequiredService<ITokenStore>();
        Assert.IsType<FileTokenStore>(store);
        Assert.Equal("file", store.Backend);
        store.Dispose();
    }

    [Fact]
    public void Redis_backend_without_connection_throws() {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig {
            AuthBaseUrl = "http://localhost:3000",
            PublicBaseUrl = "http://localhost:3000",
            HttpPort = 3000,
            HaloPsa = new HaloPsaSettings {
                Url = "https://test.halopsa.com",
                ClientId = "test",
                TokenStoreBackend = "redis"
            }
        });
        services.AddLogging();
        services.AddTokenStore();

        Assert.Throws<InvalidOperationException>(() =>
            services.BuildServiceProvider().GetRequiredService<ITokenStore>());
    }
}
