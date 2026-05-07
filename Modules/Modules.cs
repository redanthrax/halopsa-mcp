using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HaloPsaMcp.Modules;

/// <summary>
/// Discovers and invokes IModuleRegistrar implementations in priority order.
/// </summary>
internal static class Modules {
    public static IServiceCollection AddAllModules(
        this IServiceCollection services,
        IConfiguration configuration) {
        var registrars = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IModuleRegistrar).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => (IModuleRegistrar)Activator.CreateInstance(t)!)
            .OrderBy(r => r.Priority)
            .ToList();

        foreach (var registrar in registrars) {
            registrar.Register(services, configuration);
        }

        return services;
    }
}
