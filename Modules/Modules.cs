using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HaloPsaMcp.Modules;

/// <summary>
/// Module registration system that automatically discovers and registers all modules using dependency injection
/// </summary>
internal static class Modules
{
    /// <summary>
    /// Register all modules with the service collection by dynamically discovering IModuleRegistrar implementations
    /// </summary>
    public static IServiceCollection AddAllModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Find all types that implement IModuleRegistrar
        var registrarTypes = assembly.GetTypes()
            .Where(t => typeof(IModuleRegistrar).IsAssignableFrom(t) &&
                       !t.IsInterface && !t.IsAbstract)
            .OrderBy(t => GetRegistrarPriority(t))
            .ToList();

        // Register each registrar type as transient and then resolve and execute
        foreach (var registrarType in registrarTypes)
        {
            services.AddTransient(typeof(IModuleRegistrar), registrarType);
        }

        // Create a temporary provider to resolve the registrars
        var tempProvider = services.BuildServiceProvider();

        // Get all registrars and sort by priority
        var registrars = tempProvider.GetServices<IModuleRegistrar>()
            .OrderBy(r => r.Priority)
            .ToList();

        // Execute each registrar
        foreach (var registrar in registrars)
        {
            registrar.Register(services, configuration);
        }

        return services;
    }

    /// <summary>
    /// Get the priority of a registrar type based on its name
    /// </summary>
    private static int GetRegistrarPriority(Type registrarType)
    {
        var name = registrarType.Name.Replace("ModuleRegistrar", "");
        return name switch
        {
            "Common" => 1,        // Register first - shared infrastructure
            "Authentication" => 2, // Register second - depends on Common
            "HaloPsa" => 3,       // Register third - depends on Common
            "Mcp" => 4,           // Register last - depends on others
            _ => 99               // Unknown modules register last
        };
    }
}