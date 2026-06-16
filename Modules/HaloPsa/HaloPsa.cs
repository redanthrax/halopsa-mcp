using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HaloPsaMcp.Modules.HaloPsa;

/// <summary>
/// HaloPSA module registration — API client and queries.
/// </summary>
internal class HaloPsaModuleRegistrar : IModuleRegistrar {
    public int Priority => 3;

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddSingleton<HaloPsaConfig>(sp => {
            var appConfig = sp.GetRequiredService<AppConfig>();
            return new HaloPsaConfig {
                Url = appConfig.HaloPsa.Url,
                ClientId = appConfig.HaloPsa.ClientId,
                ClientSecret = appConfig.HaloPsa.ClientSecret
            };
        });

        services.AddHttpClient("halopsa", c => c.Timeout = TimeSpan.FromSeconds(60));
        services.AddSingleton<HaloPsaTokenRefresher>(sp => new HaloPsaTokenRefresher(
            sp.GetRequiredService<HaloPsaConfig>(),
            sp.GetRequiredService<ITokenStore>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<HaloPsaTokenRefresher>>(),
            sp.GetService<StackExchange.Redis.IConnectionMultiplexer>()));
        services.AddScoped<HaloPsaClientFactory>();
        services.AddSingleton<SchemaCatalogService>();
    }
}
