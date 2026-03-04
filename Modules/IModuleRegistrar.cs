#pragma warning disable IDE0005 // Using directives flagged as unnecessary but are required for the code
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HaloPsaMcp.Modules;

/// <summary>
/// Interface for module registrars that can be discovered and invoked dynamically
/// </summary>
internal interface IModuleRegistrar
{
    /// <summary>
    /// Register module services with the dependency injection container
    /// </summary>
    void Register(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Priority for module registration order (lower numbers register first)
    /// </summary>
    int Priority { get; }
}