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
    public void AuthErrorMessage_returns_structured_json() {
        var json = HaloPsaMcpConstants.AuthErrorMessage(MakeConfig());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("authenticated").GetBoolean());
        Assert.Equal("NOT_AUTHENTICATED", root.GetProperty("error").GetString());
        Assert.Equal("https://mcp.example.com/login", root.GetProperty("login_url").GetString());
        Assert.True(root.TryGetProperty("message", out _));
    }

    [Fact]
    public void AuthErrorMessage_contains_no_imperative_phrasing() {
        var json = HaloPsaMcpConstants.AuthErrorMessage(MakeConfig());
        // Imperative phrasing trips host LLM prompt-injection defenses.
        Assert.DoesNotContain("MUST", json, StringComparison.Ordinal);
        Assert.DoesNotContain("verbatim", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXACTLY", json, StringComparison.Ordinal);
        Assert.DoesNotContain("do not substitute", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign in via the URL in login_url", json, StringComparison.Ordinal);
    }
}
