#pragma warning disable IDE0005
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
#pragma warning restore IDE0005

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// MCP module registration - Model Context Protocol server and tools
/// </summary>
internal class McpModuleRegistrar : IModuleRegistrar {
    public int Priority => 4;

    public void Register(IServiceCollection services, IConfiguration configuration) {
        var backend = configuration["HALOPSA_TOKEN_STORE_BACKEND"] ?? "file";
        var stateless = McpHttpTransportConfiguration.ResolveStateless();
        if (!stateless &&
            string.Equals(backend, "redis", StringComparison.OrdinalIgnoreCase)) {
            services.AddSingleton<ISessionMigrationHandler, RedisMcpSessionMigrationHandler>();
        }
    }
}
