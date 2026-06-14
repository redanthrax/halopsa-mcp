using HaloPsaMcp.Modules.Mcp;
using Xunit;

namespace HaloPsaMcp.Tests;

public class McpHttpTransportConfigurationTests : IDisposable {
    private readonly string? _priorStateless;
    private readonly string? _priorBackend;

    public McpHttpTransportConfigurationTests() {
        _priorStateless = Environment.GetEnvironmentVariable("MCP_HTTP_STATELESS");
        _priorBackend = Environment.GetEnvironmentVariable("HALOPSA_TOKEN_STORE_BACKEND");
    }

    public void Dispose() {
        Environment.SetEnvironmentVariable("MCP_HTTP_STATELESS", _priorStateless);
        Environment.SetEnvironmentVariable("HALOPSA_TOKEN_STORE_BACKEND", _priorBackend);
    }

    [Fact]
    public void ResolveStateless_defaults_true_when_redis_backend() {
        Environment.SetEnvironmentVariable("MCP_HTTP_STATELESS", null);
        Environment.SetEnvironmentVariable("HALOPSA_TOKEN_STORE_BACKEND", "redis");
        Assert.True(McpHttpTransportConfiguration.ResolveStateless());
    }

    [Fact]
    public void ResolveStateless_defaults_false_for_file_backend() {
        Environment.SetEnvironmentVariable("MCP_HTTP_STATELESS", null);
        Environment.SetEnvironmentVariable("HALOPSA_TOKEN_STORE_BACKEND", "file");
        Assert.False(McpHttpTransportConfiguration.ResolveStateless());
    }

    [Fact]
    public void ResolveStateless_honors_explicit_zero() {
        Environment.SetEnvironmentVariable("HALOPSA_TOKEN_STORE_BACKEND", "redis");
        Environment.SetEnvironmentVariable("MCP_HTTP_STATELESS", "0");
        Assert.False(McpHttpTransportConfiguration.ResolveStateless());
    }
}
