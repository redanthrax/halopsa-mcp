using HaloPsaMcp.Modules.Common.Security;
using Xunit;

namespace HaloPsaMcp.Tests;

public class McpCorsConfigurationTests {
    [Fact]
    public void IsOriginAllowed_accepts_default_claude_origin() {
        Environment.SetEnvironmentVariable("MCP_CORS_ALLOWED_ORIGINS", null);
        Assert.True(McpCorsConfiguration.IsOriginAllowed("https://claude.ai"));
    }

    [Fact]
    public void IsOriginAllowed_rejects_unknown_origin_by_default() {
        Environment.SetEnvironmentVariable("MCP_CORS_ALLOWED_ORIGINS", null);
        Assert.False(McpCorsConfiguration.IsOriginAllowed("https://evil.example"));
    }

    [Fact]
    public void IsOriginAllowed_honors_env_override() {
        Environment.SetEnvironmentVariable("MCP_CORS_ALLOWED_ORIGINS", "https://app.example,https://claude.ai");
        try {
            Assert.True(McpCorsConfiguration.IsOriginAllowed("https://app.example"));
            Assert.True(McpCorsConfiguration.IsOriginAllowed("https://claude.ai"));
        } finally {
            Environment.SetEnvironmentVariable("MCP_CORS_ALLOWED_ORIGINS", null);
        }
    }

    [Fact]
    public void IsOriginAllowed_is_case_insensitive() {
        Environment.SetEnvironmentVariable("MCP_CORS_ALLOWED_ORIGINS", null);
        Assert.True(McpCorsConfiguration.IsOriginAllowed("HTTPS://CLAUDE.AI"));
    }
}
