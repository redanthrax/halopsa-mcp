using HaloPsaMcp.Modules.Authentication.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

public class DcrRedirectUriAllowlistTests {
    [Fact]
    public void Resolve_is_disabled_when_env_is_unset() {
        Environment.SetEnvironmentVariable("MCP_DCR_ALLOWED_REDIRECT_URIS", null);
        try {
            var policy = DcrRedirectUriAllowlist.Resolve();
            Assert.False(policy.Enabled);
            Assert.Empty(policy.AllowedUris);
            Assert.Equal(0, policy.InvalidEntryCount);
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_ALLOWED_REDIRECT_URIS", null);
        }
    }

    [Fact]
    public void Resolve_normalizes_and_keeps_valid_entries() {
        Environment.SetEnvironmentVariable(
            "MCP_DCR_ALLOWED_REDIRECT_URIS",
            "https://Claude.ai/api/mcp/auth_callback/, http://127.0.0.1:80/callback");
        try {
            var policy = DcrRedirectUriAllowlist.Resolve();
            Assert.True(policy.Enabled);
            Assert.Contains("https://claude.ai/api/mcp/auth_callback", policy.AllowedUris);
            Assert.Contains("http://127.0.0.1/callback", policy.AllowedUris);
            Assert.Equal(0, policy.InvalidEntryCount);
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_ALLOWED_REDIRECT_URIS", null);
        }
    }

    [Fact]
    public void Resolve_counts_invalid_entries() {
        Environment.SetEnvironmentVariable(
            "MCP_DCR_ALLOWED_REDIRECT_URIS",
            "https://claude.ai/api/mcp/auth_callback,bad-uri,ftp://example.com/cb,https://ok.example/cb?x=1");
        try {
            var policy = DcrRedirectUriAllowlist.Resolve();
            Assert.True(policy.Enabled);
            Assert.Single(policy.AllowedUris);
            Assert.Equal(3, policy.InvalidEntryCount);
        } finally {
            Environment.SetEnvironmentVariable("MCP_DCR_ALLOWED_REDIRECT_URIS", null);
        }
    }
}
