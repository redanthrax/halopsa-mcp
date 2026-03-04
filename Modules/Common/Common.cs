using HaloPsaMcp.Modules.Common.Infrastructure;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HaloPsaMcp.Modules.Common;

/// <summary>
/// Common module registration - shared services and infrastructure
/// </summary>
internal class CommonModuleRegistrar : IModuleRegistrar
{
    public int Priority => 1; // Register first - shared infrastructure

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Get AppConfig from DI (registered in Program.cs)
        var serviceProvider = services.BuildServiceProvider();
        var appConfig = serviceProvider.GetRequiredService<AppConfig>();

        // Register token store
        services.AddSingleton<ITokenStore>(sp =>
            new FileTokenStore(appConfig.HaloPsa.TokenStorePath));
    }
}