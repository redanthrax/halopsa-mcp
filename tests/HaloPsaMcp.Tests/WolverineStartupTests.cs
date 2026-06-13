using HaloPsaMcp.Modules.HaloPsa.Middleware;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Xunit;

namespace HaloPsaMcp.Tests;

public class WolverineStartupTests {
    [Fact]
    public void Host_builds_with_ToolAuditMiddleware_registered() {
        var builder = Host.CreateApplicationBuilder();
        builder.UseWolverine(opts => {
            opts.Discovery.IncludeAssembly(typeof(ToolAuditMiddleware).Assembly);
            opts.Policies.AddMiddleware(typeof(ToolAuditMiddleware), chain =>
                chain.MessageType.Namespace?.StartsWith("HaloPsaMcp.Modules.HaloPsa", StringComparison.Ordinal) == true);
        });

        using var host = builder.Build();
    }
}
