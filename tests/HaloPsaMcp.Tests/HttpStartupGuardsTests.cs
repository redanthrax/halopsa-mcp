using HaloPsaMcp.Modules.Common.Security;
using Xunit;

namespace HaloPsaMcp.Tests;

public class HttpStartupGuardsTests {
    [Fact]
    public void EnsureHttpModeSecurity_throws_when_iat_missing_and_open_dcr_disabled() {
        Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
        Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", null);
        try {
            var ex = Assert.Throws<InvalidOperationException>(HttpStartupGuards.EnsureHttpModeSecurity);
            Assert.Contains("MCP_DCR_INITIAL_ACCESS_TOKEN", ex.Message, StringComparison.Ordinal);
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
            Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", null);
        }
    }

    [Fact]
    public void EnsureHttpModeSecurity_allows_open_dcr_when_explicitly_enabled() {
        Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
        Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", "1");
        try {
            HttpStartupGuards.EnsureHttpModeSecurity();
        } finally {
            Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", null);
        }
    }

    [Fact]
    public void EnsureHttpModeSecurity_passes_when_iat_set() {
        Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", "secret");
        Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", null);
        try {
            HttpStartupGuards.EnsureHttpModeSecurity();
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
        }
    }
}
