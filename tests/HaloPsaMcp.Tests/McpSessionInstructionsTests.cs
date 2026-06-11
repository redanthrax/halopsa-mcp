using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.Mcp;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HaloPsaMcp.Tests;

public class McpSessionInstructionsTests {
    private static AppConfig MakeConfig() => new() {
        AuthBaseUrl = "http://localhost:3000",
        PublicBaseUrl = "http://localhost:3000",
        HttpPort = 3000,
        HaloPsa = new HaloPsaSettings {
            Url = "https://example.halopsa.com",
            ClientId = "test-client",
            TokenStorePath = "./data/tokens.json"
        }
    };

    [Fact]
    public void Build_desktop_unauthenticated_includes_login_url_and_auth_required() {
        var store = new StubTokenStore(authenticated: false);
        var text = McpSessionInstructions.Build(MakeConfig(), store, McpHostMode.DesktopStdio);

        Assert.Contains("AUTHENTICATION REQUIRED", text, StringComparison.Ordinal);
        Assert.Contains("http://localhost:3000/login", text, StringComparison.Ordinal);
        Assert.Contains("halopsa_auth_status", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_http_unauthenticated_mentions_oauth() {
        var store = new StubTokenStore(authenticated: false);
        var text = McpSessionInstructions.Build(MakeConfig(), store, McpHostMode.Http);

        Assert.Contains("OAuth", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("http://localhost:3000/login", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_authenticated_mentions_tenant_not_login_gate() {
        var store = new StubTokenStore(authenticated: true);
        var text = McpSessionInstructions.Build(MakeConfig(), store, McpHostMode.DesktopStdio);

        Assert.Contains("https://example.halopsa.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AUTHENTICATION REQUIRED", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildForHttpSession_uses_bearer_when_present() {
        var store = new StubTokenStore(authenticated: false) {
            ValidBearer = "mcp_test_token"
        };
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer mcp_test_token";

        var text = McpSessionInstructions.BuildForHttpSession(context, MakeConfig(), store);

        Assert.DoesNotContain("AUTHENTICATION REQUIRED", text, StringComparison.Ordinal);
    }

    private sealed class StubTokenStore(bool authenticated) : ITokenStore {
        internal string? ValidBearer { get; init; }

        public string Backend => "stub";
        public bool HasValidTokens() => authenticated;
        public int SessionCount => authenticated ? 1 : 0;
        public int ActiveSessionCount => authenticated ? 1 : 0;

        public bool IsValidSession(string mcpToken) =>
            authenticated || (ValidBearer is not null && mcpToken == ValidBearer);

        public Task<(string AccessToken, string RefreshToken)> CreateSessionAsync(
            string haloPsaAccess, string? haloPsaRefresh, long expiresAt) =>
            throw new NotImplementedException();

        public UserTokenEntry? GetToken(string mcpToken) => null;
        public UserTokenEntry? GetDefaultToken() => null;

        public Task UpdateSessionTokensAsync(
            string mcpToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt) =>
            throw new NotImplementedException();

        public KeyValuePair<string, UserTokenEntry>? FindByRefreshToken(string mcpRefresh) => null;

        public Task<string> RotateRefreshTokenAsync(
            string mcpAccessToken, string newHaloAccess, string newHaloRefresh, long newExpiresAt) =>
            throw new NotImplementedException();

        public int PruneExpired() => 0;
        public ValueTask<bool> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);

        public void Dispose() { }
    }
}
