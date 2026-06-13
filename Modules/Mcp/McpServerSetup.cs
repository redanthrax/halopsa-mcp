using System.Diagnostics;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.Common.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace HaloPsaMcp.Modules.Mcp;

internal static class McpServerSetup {
    internal static IServiceCollection AddMcpSessionInstructions(this IServiceCollection services) {
        services.AddSingleton<IConfigureOptions<McpServerOptions>, McpServerInstructionsConfigurer>();
        services.AddSingleton<IConfigureOptions<McpServerOptions>, McpToolAllowlistConfigurer>();
        return services;
    }

    internal static IMcpServerBuilder AddMcpToolPolicies(this IMcpServerBuilder builder) =>
        builder.WithRequestFilters(filters => {
            filters.AddCallToolFilter(ToolAuditFilter);
        });

    internal static void ConfigureHttpSessionInstructions(HttpServerTransportOptions options) {
        options.ConfigureSessionOptions = (context, mcpOptions, cancellationToken) => {
            var config = context.RequestServices.GetRequiredService<AppConfig>();
            var tokenStore = context.RequestServices.GetRequiredService<ITokenStore>();
            mcpOptions.ServerInstructions = McpSessionInstructions.BuildForHttpSession(context, config, tokenStore);
            return Task.CompletedTask;
        };
    }

    private static McpRequestFilter<CallToolRequestParams, CallToolResult> ToolAuditFilter =>
        next => async (context, cancellationToken) => {
            var request = context.Params!;
            var tool = request.Name ?? "unknown";
            if (!McpToolAllowlist.IsAllowed(tool)) {
                return new CallToolResult {
                    IsError = true,
                    Content = [new TextContentBlock { Text = $"Tool '{tool}' is not enabled on this deployment." }]
                };
            }

            var services = context.Services ?? context.Server.Services;
            var logger = services.GetRequiredService<ILogger<ToolAuditMarker>>();
            var tokenStore = services.GetRequiredService<ITokenStore>();
            var httpAccessor = services.GetService<IHttpContextAccessor>();
            var user = ToolAuditHelper.ResolveUserHint(httpAccessor?.HttpContext, tokenStore);
            var argsHash = ToolAuditHelper.HashArgs(request.Arguments);
            var traceId = Activity.Current?.TraceId.ToString() ?? "none";
            var started = DateTimeOffset.UtcNow;

            try {
                var result = await next(context, cancellationToken).ConfigureAwait(false);
                var status = result.IsError == true ? "error" : "ok";
                logger.LogInformation(
                    "tool_audit user={User} tool={Tool} args_hash={ArgsHash} ts={Ts} traceId={TraceId} status={Status}",
                    user, tool, argsHash, started.ToString("O"), traceId, status);
                Activity.Current?.SetTag("mcp.tool", tool);
                Activity.Current?.SetTag("mcp.tool.args_hash", argsHash);
                Activity.Current?.SetTag("mcp.tool.status", status);
                Activity.Current?.SetTag("mcp.user", user);
                return result;
            } catch (Exception ex) {
                logger.LogWarning(
                    ex,
                    "tool_audit user={User} tool={Tool} args_hash={ArgsHash} ts={Ts} traceId={TraceId} status={Status}",
                    user, tool, argsHash, started.ToString("O"), traceId, "exception");
                Activity.Current?.SetTag("mcp.tool.status", "exception");
                throw;
            }
        };

    private sealed class McpServerInstructionsConfigurer(
        AppConfig config,
        ITokenStore tokenStore) : IConfigureOptions<McpServerOptions> {
        public void Configure(McpServerOptions options) {
            options.ServerInstructions = McpSessionInstructions.Build(config, tokenStore, McpRuntime.HostMode);
        }
    }

    private sealed class McpToolAllowlistConfigurer : IConfigureOptions<McpServerOptions> {
        public void Configure(McpServerOptions options) => McpToolAllowlist.FilterToolCollection(options);
    }

    private sealed class ToolAuditMarker { }
}
