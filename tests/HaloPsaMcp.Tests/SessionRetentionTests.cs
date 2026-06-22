using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HaloPsaMcp.Tests;

[Collection("TokenStoreRuntime")]
public class SessionRetentionTests {
    private static FileTokenStore CreateStore(string path) {
        var config = new AppConfig {
            AuthBaseUrl = "http://localhost:3000",
            PublicBaseUrl = "http://localhost:3000",
            HttpPort = 3000,
            HaloPsa = new HaloPsaSettings {
                Url = "https://example.halopsa.com",
                ClientId = "test",
                TokenStorePath = path
            }
        };
        return new FileTokenStore(
            config,
            DataProtectionProvider.Create("HaloPsaMcp.Tests.Retention"),
            NullLogger<FileTokenStore>.Instance);
    }

    [Fact]
    public async Task IsValidSession_true_when_access_expired_but_refresh_token_present() {
        var dir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "tokens.json");

        try {
            using var store = CreateStore(path);
            var expiredAccess = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
            var (mcpToken, _) = await store.CreateSessionAsync("access", "halo_refresh", expiredAccess);

            Assert.True(store.IsValidSession(mcpToken));
            Assert.True(store.HasValidTokens());
            Assert.NotNull(store.GetDefaultSession());
        } finally {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task PruneExpired_keeps_refresh_capable_sessions() {
        var dir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "tokens.json");

        try {
            using var store = CreateStore(path);
            var expiredAccess = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeMilliseconds();
            var (mcpToken, _) = await store.CreateSessionAsync("access", "halo_refresh", expiredAccess);

            var pruned = store.PruneExpired();

            Assert.Equal(0, pruned);
            Assert.True(store.IsValidSession(mcpToken));
        } finally {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task PruneExpired_removes_session_without_refresh_token() {
        var dir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "tokens.json");

        try {
            using var store = CreateStore(path);
            var expiredAccess = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeMilliseconds();
            var (mcpToken, _) = await store.CreateSessionAsync("access", null, expiredAccess);

            var pruned = store.PruneExpired();

            Assert.Equal(1, pruned);
            Assert.False(store.IsValidSession(mcpToken));
            Assert.False(store.HasValidTokens());
        } finally {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task UpdateSessionTokensAsync_extends_usable_session() {
        var dir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "tokens.json");

        try {
            using var store = CreateStore(path);
            var expiredAccess = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
            var (mcpToken, _) = await store.CreateSessionAsync("old_access", "halo_refresh", expiredAccess);
            var newExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();

            await store.UpdateSessionTokensAsync(mcpToken, "new_access", "new_refresh", newExpiry);
            var entry = store.GetToken(mcpToken);

            Assert.NotNull(entry);
            Assert.Equal("new_access", entry.AccessToken);
            Assert.Equal("new_refresh", entry.RefreshToken);
            Assert.Equal(newExpiry, entry.ExpiresAt);
            Assert.True(store.IsValidSession(mcpToken));
        } finally {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void SessionValidity_false_when_access_and_refresh_missing() {
        var entry = new UserTokenEntry {
            AccessToken = "access",
            RefreshToken = "",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds()
        };

        Assert.False(SessionValidity.IsUsable(entry));
    }
}
