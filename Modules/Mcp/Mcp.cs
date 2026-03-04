using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// MCP module registration - Model Context Protocol server and tools
/// </summary>
internal class McpModuleRegistrar : IModuleRegistrar
{
    public int Priority => 4; // Register last - depends on others

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // MCP server registration is handled in Program.cs
        // This module file is for any future MCP-specific services
    }
}