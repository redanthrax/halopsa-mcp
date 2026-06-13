using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using Xunit;

namespace HaloPsaMcp.Tests;

public class OAuthResourceValidationTests {
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
    public void ExpectedResource_uses_mcp_endpoint_path() {
        Assert.Equal(
            "https://halopsa-mcp.example.com/mcp",
            OAuthResourceValidation.ExpectedResource(MakeConfig()));
    }

    [Fact]
    public void IsValid_accepts_matching_resource_with_or_without_trailing_slash() {
        var config = MakeConfig();
        Assert.True(OAuthResourceValidation.IsValid(config, "https://halopsa-mcp.example.com/mcp"));
        Assert.True(OAuthResourceValidation.IsValid(config, "https://halopsa-mcp.example.com/mcp/"));
    }

    [Fact]
    public void IsValid_accepts_missing_resource() {
        Assert.True(OAuthResourceValidation.IsValid(MakeConfig(), null));
        Assert.True(OAuthResourceValidation.IsValid(MakeConfig(), ""));
    }

    [Fact]
    public void IsValid_rejects_wrong_resource() {
        Assert.False(OAuthResourceValidation.IsValid(
            MakeConfig(), "https://halopsa-mcp.example.com/other"));
    }

    [Fact]
    public void BindResource_defaults_to_expected_when_unspecified() {
        Assert.Equal(
            OAuthResourceValidation.ExpectedResource(MakeConfig()),
            OAuthResourceValidation.BindResource(MakeConfig(), null));
    }
}
