using System.Text.Json;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.Mcp;
using Xunit;

namespace HaloPsaMcp.Tests;

public class AuthErrorEnvelopeTests {
    private static AppConfig MakeConfig() => new() {
        AuthBaseUrl = "https://mcp.example.com",
        HttpPort = 3000,
    };

    [Fact]
    public void AuthErrorMessage_returns_plain_text_with_url() {
        var message = HaloPsaMcpConstants.AuthErrorMessage(MakeConfig());
        Assert.Equal("HaloPSA session is not authenticated. Sign in at: https://mcp.example.com/login", message);
    }

    [Fact]
    public void AuthErrorMessage_contains_no_imperative_phrasing() {
        var message = HaloPsaMcpConstants.AuthErrorMessage(MakeConfig());
        // Imperative phrasing trips host MCP client prompt-injection defenses.
        Assert.DoesNotContain("MUST", message, StringComparison.Ordinal);
        Assert.DoesNotContain("verbatim", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXACTLY", message, StringComparison.Ordinal);
        Assert.DoesNotContain("do not substitute", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign in via the URL in login_url", message, StringComparison.Ordinal);
    }
}
