using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.AspNetCore.Http;
using HaloPsaMcp.Modules.HaloPsa.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;
using HaloPsaMcp.Tests.TestDoubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HaloPsaMcp.Tests;

internal static class HaloPsaTestHelpers {
    internal sealed class Fixture : IAsyncDisposable {
        private readonly string _tempDir;
        private readonly TokenStorageService _tokenStorage;

        public StubHttpHandler Handler { get; }
        public HaloPsaClientFactory Factory { get; }
        public HttpContextAccessor ContextAccessor { get; } = new();

        private Fixture(
            string tempDir,
            TokenStorageService tokenStorage,
            StubHttpHandler handler,
            HaloPsaClientFactory factory) {
            _tempDir = tempDir;
            _tokenStorage = tokenStorage;
            Handler = handler;
            Factory = factory;
        }

        public static async Task<Fixture> CreateAsync(StubHttpHandler handler) {
            var tempDir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tokenPath = Path.Combine(tempDir, "tokens.json");

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

            var dp = DataProtectionProvider.Create("HaloPsaMcp.Tests");
            var tokenStorage = new TokenStorageService(
                config, dp, NullLogger<TokenStorageService>.Instance);

            var futureExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            await tokenStorage.CreateSessionAsync("halo_access_test", "halo_refresh_test", futureExpiry);

            var httpClient = new HttpClient(handler) {
                BaseAddress = new Uri("https://test.halopsa.com")
            };

            var baseConfig = new HaloPsaConfig {
                Url = config.HaloPsa.Url,
                ClientId = config.HaloPsa.ClientId
            };

            var factory = new HaloPsaClientFactory(
                baseConfig,
                new McpAuthenticationService(tokenStorage, NullLogger<McpAuthenticationService>.Instance),
                tokenStorage,
                new SingleHttpClientFactory(httpClient),
                NullLogger<HaloPsaClientFactory>.Instance);

            return new Fixture(tempDir, tokenStorage, handler, factory);
        }

        public async ValueTask DisposeAsync() {
            _tokenStorage.Dispose();
            await Task.Run(() => {
                try {
                    Directory.Delete(_tempDir, recursive: true);
                } catch {
                    // Best-effort cleanup for temp test dirs.
                }
            }).ConfigureAwait(false);
        }
    }
}
