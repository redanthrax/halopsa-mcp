using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HaloPsaMcp.Modules.Common;

/// <summary>
/// Common module registration — placeholder for future shared services.
/// </summary>
internal class CommonModuleRegistrar : IModuleRegistrar {
    public int Priority => 1;

    public void Register(IServiceCollection services, IConfiguration configuration) {
        // No shared services today; left as the entry point for cross-module infrastructure.
    }
}
