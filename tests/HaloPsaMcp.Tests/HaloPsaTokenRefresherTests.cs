using System.Net;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;
using HaloPsaMcp.Tests.TestDoubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HaloPsaMcp.Tests;

public class HaloPsaTokenRefresherTests {
    [Fact]
    public async Task EnsureFreshAsync_serializes_concurrent_refresh_for_same_session() {
        var refreshCalls = 0;
        var handler = new StubHttpHandler {
            Responder = req => {
                if (req.RequestUri?.AbsolutePath == "/auth/token") {
                    Interlocked.Increment(ref refreshCalls);
                    Thread.Sleep(100);
                    var body = JsonSerializer.Serialize(new {
                        access_token = "new_access",
                        refresh_token = "new_refresh",
                        expires_in = 3600
                    });
                    return new HttpResponseMessage(HttpStatusCode.OK) {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        var tempDir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-refresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tokenPath = Path.Combine(tempDir, "tokens.json");

        try {
            var config = new AppConfig {
                AuthBaseUrl = "http://localhost:3000",
                PublicBaseUrl = "http://localhost:3000",
                HttpPort = 3000,
                HaloPsa = new HaloPsaSettings {
                    Url = "https://test.halopsa.com",
                    ClientId = "test-client",
                    TokenStorePath = tokenPath
                }
            };

            using var store = new FileTokenStore(
                config,
                DataProtectionProvider.Create("HaloPsaMcp.Tests.Refresh"),
                NullLogger<FileTokenStore>.Instance);

            var expiredAccess = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
            var (mcpToken, _) = await store.CreateSessionAsync("old_access", "halo_refresh", expiredAccess);

            var baseConfig = new HaloPsaConfig {
                Url = config.HaloPsa.Url,
                ClientId = config.HaloPsa.ClientId
            };
            var httpClient = new HttpClient(handler);
            var refresher = new HaloPsaTokenRefresher(
                baseConfig,
                store,
                new SingleHttpClientFactory(httpClient),
                NullLogger<HaloPsaTokenRefresher>.Instance);

            var tasks = Enumerable.Range(0, 8).Select(_ => refresher.EnsureFreshAsync(
                mcpToken,
                mcpToken,
                "old_access",
                "halo_refresh",
                expiredAccess,
                onRefreshed: null)).ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, refreshCalls);
            Assert.All(results, r => Assert.Equal("new_access", r.AccessToken));
            Assert.All(results, r => Assert.Equal("new_refresh", r.RefreshToken));

            var stored = store.GetToken(mcpToken);
            Assert.NotNull(stored);
            Assert.Equal("new_access", stored.AccessToken);
            Assert.Equal("new_refresh", stored.RefreshToken);
        } finally {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task EnsureFreshAsync_reuses_store_after_peer_refresh() {
        var refreshCalls = 0;
        var handler = new StubHttpHandler {
            Responder = req => {
                if (req.RequestUri?.AbsolutePath == "/auth/token") {
                    Interlocked.Increment(ref refreshCalls);
                    var body = JsonSerializer.Serialize(new {
                        access_token = "refreshed_access",
                        refresh_token = "refreshed_refresh",
                        expires_in = 3600
                    });
                    return new HttpResponseMessage(HttpStatusCode.OK) {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        var tempDir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-refresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tokenPath = Path.Combine(tempDir, "tokens.json");

        try {
            var config = new AppConfig {
                AuthBaseUrl = "http://localhost:3000",
                PublicBaseUrl = "http://localhost:3000",
                HttpPort = 3000,
                HaloPsa = new HaloPsaSettings {
                    Url = "https://test.halopsa.com",
                    ClientId = "test-client",
                    TokenStorePath = tokenPath
                }
            };

            using var store = new FileTokenStore(
                config,
                DataProtectionProvider.Create("HaloPsaMcp.Tests.Refresh"),
                NullLogger<FileTokenStore>.Instance);

            var expiredAccess = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
            var (mcpToken, _) = await store.CreateSessionAsync("old_access", "halo_refresh", expiredAccess);
            var freshExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            await store.UpdateSessionTokensAsync(mcpToken, "already_fresh", "still_valid_refresh", freshExpiry);

            var baseConfig = new HaloPsaConfig {
                Url = config.HaloPsa.Url,
                ClientId = config.HaloPsa.ClientId
            };
            var httpClient = new HttpClient(handler);
            var refresher = new HaloPsaTokenRefresher(
                baseConfig,
                store,
                new SingleHttpClientFactory(httpClient),
                NullLogger<HaloPsaTokenRefresher>.Instance);

            var result = await refresher.EnsureFreshAsync(
                mcpToken,
                mcpToken,
                "old_access",
                "halo_refresh",
                expiredAccess,
                onRefreshed: null);

            Assert.Equal(0, refreshCalls);
            Assert.Equal("already_fresh", result.AccessToken);
            Assert.Equal("still_valid_refresh", result.RefreshToken);
            Assert.Equal(freshExpiry, result.ExpiresAt);
        } finally {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task EnsureFreshAsync_invalidates_session_when_halo_rejects_refresh() {
        var handler = new StubHttpHandler {
            Responder = req => {
                if (req.RequestUri?.AbsolutePath == "/auth/token") {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) {
                        Content = new StringContent("{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        var tempDir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-refresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tokenPath = Path.Combine(tempDir, "tokens.json");

        try {
            var config = new AppConfig {
                AuthBaseUrl = "http://localhost:3000",
                PublicBaseUrl = "http://localhost:3000",
                HttpPort = 3000,
                HaloPsa = new HaloPsaSettings {
                    Url = "https://test.halopsa.com",
                    ClientId = "test-client",
                    TokenStorePath = tokenPath
                }
            };

            using var store = new FileTokenStore(
                config,
                DataProtectionProvider.Create("HaloPsaMcp.Tests.Refresh"),
                NullLogger<FileTokenStore>.Instance);

            var expiredAccess = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
            var (mcpToken, _) = await store.CreateSessionAsync("old_access", "halo_refresh", expiredAccess);

            var baseConfig = new HaloPsaConfig {
                Url = config.HaloPsa.Url,
                ClientId = config.HaloPsa.ClientId
            };
            var httpClient = new HttpClient(handler);
            var refresher = new HaloPsaTokenRefresher(
                baseConfig,
                store,
                new SingleHttpClientFactory(httpClient),
                NullLogger<HaloPsaTokenRefresher>.Instance);

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                refresher.EnsureFreshAsync(
                    mcpToken,
                    mcpToken,
                    "old_access",
                    "halo_refresh",
                    expiredAccess,
                    onRefreshed: null));

            Assert.Contains("Token refresh failed (400)", ex.Message, StringComparison.Ordinal);
            Assert.False(store.IsValidSession(mcpToken));
            Assert.Null(store.GetToken(mcpToken));
        } finally {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
