using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.Common.Security;
using Xunit;

namespace HaloPsaMcp.Tests;

public class OAuthDiscoveryTests {
    private static AppConfig MakeConfig() => new() {
        AuthBaseUrl = "https://halopsa-mcp.example.com",
        PublicBaseUrl = "https://halopsa-mcp.example.com",
        HttpPort = 3000,
        HaloPsa = new HaloPsaSettings {
            Url = "https://tenant.halopsa.com",
            ClientId = "test",
            TokenStorePath = "./data/tokens.json"
        }
    };

    [Fact]
    public void ProtectedResourceMetadata_uses_mcp_endpoint_as_resource() {
        var metadata = OAuthDiscovery.BuildProtectedResourceMetadata(MakeConfig());
        var json = System.Text.Json.JsonSerializer.Serialize(metadata);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(
            "https://halopsa-mcp.example.com/mcp",
            doc.RootElement.GetProperty("resource").GetString());
    }

    [Fact]
    public void Path_suffixed_well_known_urls_include_mcp_suffix() {
        var config = MakeConfig();
        Assert.Equal(
            "https://halopsa-mcp.example.com/.well-known/oauth-protected-resource/mcp",
            OAuthDiscovery.ProtectedResourceMetadataUrl(config, pathSuffixed: true));
        Assert.Equal(
            "https://halopsa-mcp.example.com/.well-known/oauth-authorization-server/mcp",
            OAuthDiscovery.AuthorizationServerMetadataUrl(config, pathSuffixed: true));
    }

    [Fact]
    public void AuthorizationServerMetadata_advertises_bearer_when_iat_required() {
        Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", "secret");
        Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", null);
        try {
            var metadata = OAuthDiscovery.BuildAuthorizationServerMetadata(MakeConfig());
            var json = System.Text.Json.JsonSerializer.Serialize(metadata);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            Assert.True(doc.RootElement.TryGetProperty("registration_endpoint_auth_methods_supported", out var methods));
            Assert.Contains("bearer", methods.EnumerateArray().Select(m => m.GetString()));
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public void Open_dcr_enabled_when_iat_not_configured() {
        Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
        Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", null);
        try {
            Assert.True(OAuthDiscovery.IsOpenDcrEnabled());
            Assert.False(OAuthDiscovery.RequiresDcrInitialAccessToken());
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public void Open_dcr_enabled_when_allow_open_flag_set_even_with_iat() {
        Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", "secret");
        Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", "1");
        try {
            Assert.True(OAuthDiscovery.IsOpenDcrEnabled());
            Assert.False(OAuthDiscovery.RequiresDcrInitialAccessToken());
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
            Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", null);
        }
    }

    [Fact]
    public void EnsureHttpModeSecurity_does_not_throw_without_iat() {
        Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
        Environment.SetEnvironmentVariable("MCP_ALLOW_OPEN_DCR", null);
        try {
            HttpStartupGuards.EnsureHttpModeSecurity();
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN", null);
        }
    }
}
