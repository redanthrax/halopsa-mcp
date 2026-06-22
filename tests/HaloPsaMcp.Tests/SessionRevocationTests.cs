using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HaloPsaMcp.Tests;

[Collection("TokenStoreRuntime")]
public class SessionRevocationTests {
    [Fact]
    public async Task FileTokenStore_InvalidateSessionAsync_removes_session() {
        var dir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-revoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "tokens.json");
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

        try {
            using var store = new FileTokenStore(
                config,
                DataProtectionProvider.Create("HaloPsaMcp.Tests.Revoke"),
                NullLogger<FileTokenStore>.Instance);

            var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            var (mcpToken, _) = await store.CreateSessionAsync("access", "refresh", expiry);
            Assert.True(store.IsValidSession(mcpToken));

            var removed = await store.InvalidateSessionAsync(mcpToken);
            Assert.True(removed);
            Assert.False(store.IsValidSession(mcpToken));

            var missing = await store.InvalidateSessionAsync(mcpToken);
            Assert.False(missing);
        } finally {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DirectLogin_replaces_prior_default_session() {
        var dir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-revoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "tokens.json");
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

        try {
            using var store = new FileTokenStore(
                config,
                DataProtectionProvider.Create("HaloPsaMcp.Tests.Revoke"),
                NullLogger<FileTokenStore>.Instance);

            var expiry = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeMilliseconds();
            var (oldToken, _) = await store.CreateSessionAsync("old_access", "old_refresh", expiry);
            Assert.True(store.IsValidSession(oldToken));

            var prior = store.GetDefaultSession();
            Assert.NotNull(prior);
            await store.InvalidateSessionAsync(prior.Value.Key);

            var newExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            var (newToken, _) = await store.CreateSessionAsync("new_access", "new_refresh", newExpiry);

            Assert.False(store.IsValidSession(oldToken));
            Assert.True(store.IsValidSession(newToken));
            Assert.Equal(newToken, store.GetDefaultSession()?.Key);
        } finally {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
