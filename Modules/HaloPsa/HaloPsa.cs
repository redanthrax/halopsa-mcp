using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HaloPsaMcp.Modules.HaloPsa;

/// <summary>
/// HaloPSA module registration - API client and queries
/// </summary>
internal class HaloPsaModuleRegistrar : IModuleRegistrar
{
    public int Priority => 3; // Register third - depends on Common

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Get AppConfig from DI (registered in Program.cs)
        var serviceProvider = services.BuildServiceProvider();
        var appConfig = serviceProvider.GetRequiredService<AppConfig>();

        // Register HaloPSA configuration
        var haloPsaConfig = new HaloPsaConfig
        {
            Url = appConfig.HaloPsa.Url,
            ClientId = appConfig.HaloPsa.ClientId,
            ClientSecret = appConfig.HaloPsa.ClientSecret
        };
        services.AddSingleton(haloPsaConfig);

        // Register HaloPsaClientFactory as scoped (per-request)
        services.AddScoped<HaloPsaClientFactory>();
    }
}