using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

namespace HaloPsaMcp.Modules.Mcp;

internal static class McpServerSetup {
    internal static IServiceCollection AddMcpSessionInstructions(this IServiceCollection services) {
        services.AddSingleton<IConfigureOptions<McpServerOptions>, McpServerInstructionsConfigurer>();
        return services;
    }

    internal static void ConfigureHttpSessionInstructions(HttpServerTransportOptions options) {
        options.ConfigureSessionOptions = (context, mcpOptions, cancellationToken) => {
            var config = context.RequestServices.GetRequiredService<AppConfig>();
            var tokenStore = context.RequestServices.GetRequiredService<ITokenStore>();
            mcpOptions.ServerInstructions = McpSessionInstructions.BuildForHttpSession(context, config, tokenStore);
            return Task.CompletedTask;
        };
    }

    private sealed class McpServerInstructionsConfigurer(
        AppConfig config,
        ITokenStore tokenStore) : IConfigureOptions<McpServerOptions> {
        public void Configure(McpServerOptions options) {
            options.ServerInstructions = McpSessionInstructions.Build(config, tokenStore, McpRuntime.HostMode);
        }
    }
}
