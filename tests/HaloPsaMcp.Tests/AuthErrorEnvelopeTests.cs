using System.Text.Json;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.Mcp;
using Xunit;

namespace HaloPsaMcp.Tests;

public class AuthErrorEnvelopeTests {
    private static AppConfig MakeConfig() => new() {
        AuthBaseUrl = "https://mcp.example.com",
        PublicBaseUrl = "https://mcp.example.com",
        HttpPort = 3000,
        HaloPsa = new HaloPsaSettings {
            Url = "https://example.halopsa.com",
            ClientId = "test-client",
            TokenStorePath = "./data/tokens.json"
        }
    };

    [Fact]
    public void AuthErrorMessage_returns_json_with_login_url_for_localhost() {
        var config = new AppConfig {
            AuthBaseUrl = "http://localhost:3000",
            PublicBaseUrl = "http://localhost:3000",
            HttpPort = 3000,
            HaloPsa = MakeConfig().HaloPsa
        };
        var doc = JsonDocument.Parse(HaloPsaMcpConstants.AuthErrorMessage(config));
        Assert.False(doc.RootElement.GetProperty("authenticated").GetBoolean());
        Assert.Equal("http://localhost:3000/login", doc.RootElement.GetProperty("login_url").GetString());
    }

    [Fact]
    public void AuthErrorMessage_uses_effective_public_base_url_when_port_fallback_active() {
        AppConfigRuntime.EffectivePublicBaseUrl = "http://localhost:45678";
        try {
            var config = MakeConfig();
            var doc = JsonDocument.Parse(HaloPsaMcpConstants.AuthErrorMessage(config));
            Assert.Equal("http://localhost:45678/login", doc.RootElement.GetProperty("login_url").GetString());
        } finally {
            AppConfigRuntime.EffectivePublicBaseUrl = null;
            AppConfigRuntime.PortFallbackActive = false;
        }
    }

    [Fact]
    public void AuthErrorMessage_contains_no_imperative_phrasing() {
        var message = HaloPsaMcpConstants.AuthErrorMessage(MakeConfig());
        Assert.DoesNotContain("MUST", message, StringComparison.Ordinal);
        Assert.DoesNotContain("verbatim", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXACTLY", message, StringComparison.Ordinal);
    }

    [Fact]
    public void OAuthCallbackUrl_matches_login_base_when_port_fallback_active() {
        AppConfigRuntime.EffectivePublicBaseUrl = "http://localhost:61596";
        AppConfigRuntime.PortFallbackActive = true;
        try {
            var config = MakeConfig();
            Assert.Equal("http://localhost:61596/login", HaloPsaMcpConstants.GetLoginUrl(config));
            Assert.Equal("http://localhost:61596/callback", AppConfigRuntime.OAuthCallbackUrl(config));
        } finally {
            AppConfigRuntime.EffectivePublicBaseUrl = null;
            AppConfigRuntime.PortFallbackActive = false;
        }
    }
}
