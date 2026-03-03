using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Infrastructure;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.Common.Extensions;

/// <summary>
/// Extension methods for registering modules with dependency injection
/// </summary>
internal static class ServiceCollectionExtensions {
    /// <summary>
    /// Register shared module services (configuration, token store)
    /// </summary>
    public static IServiceCollection AddSharedModule(
        this IServiceCollection services,
        IConfiguration configuration) {
        // Get AppConfig from DI (registered in Program.cs)
        var serviceProvider = services.BuildServiceProvider();
        var appConfig = serviceProvider.GetRequiredService<AppConfig>();

        // Register token store
        services.AddSingleton<ITokenStore>(sp =>
            new FileTokenStore(appConfig.HaloPsa.TokenStorePath));

        return services;
    }

    /// <summary>
    /// Register authentication module services (OAuth, token validation)
    /// </summary>
    public static IServiceCollection AddAuthenticationModule(
        this IServiceCollection services,
        IConfiguration configuration) {
        // Register authentication services
        services.AddSingleton<McpAuthenticationService>();
        services.AddSingleton<TokenStorageService>();

        // HttpContextAccessor needed for per-user token retrieval
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Register HaloPSA module services (API client, factory)
    /// </summary>
    public static IServiceCollection AddHaloPsaModule(
        this IServiceCollection services,
        IConfiguration configuration) {
        // Get AppConfig from DI (registered in Program.cs)
        var serviceProvider = services.BuildServiceProvider();
        var appConfig = serviceProvider.GetRequiredService<AppConfig>();
        
        // Register HaloPSA configuration
        var haloPsaConfig = new HaloPsaConfig {
            Url = appConfig.HaloPsa.Url,
            ClientId = appConfig.HaloPsa.ClientId,
            ClientSecret = appConfig.HaloPsa.ClientSecret
        };
        services.AddSingleton(haloPsaConfig);

        // Register HaloPsaClientFactory as scoped (per-request)
        services.AddScoped<HaloPsaClientFactory>();

        return services;
    }
}
