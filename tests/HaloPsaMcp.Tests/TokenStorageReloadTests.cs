using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HaloPsaMcp.Tests;

public class TokenStorageReloadTests
{
    private static AppConfig MakeConfig(string tokenPath) => new()
    {
        AuthBaseUrl = "http://localhost:3000",
        PublicBaseUrl = "http://localhost:3000",
        HttpPort = 3000,
        HaloPsa = new HaloPsaSettings
        {
            Url = "https://example.halopsa.com",
            ClientId = "test-client",
            TokenStorePath = tokenPath
        }
    };

    [Fact]
    public async Task Reloads_session_written_by_another_process()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halopsa-mcp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "tokens.json");
        var dp = DataProtectionProvider.Create("HaloPsaMcp.Tests");

        try
        {
            // Process A: empty store at startup.
            using var a = new FileTokenStore(MakeConfig(path), dp, NullLogger<FileTokenStore>.Instance);
            Assert.Null(a.GetDefaultToken());

            // Process B: writes a session and persists to disk.
            using (var b = new FileTokenStore(MakeConfig(path), dp, NullLogger<FileTokenStore>.Instance))
            {
                var futureExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
                await b.CreateSessionAsync("halo_access_xyz", "halo_refresh_xyz", futureExpiry);
            }

            // Wait for FileSystemWatcher debounce + reload.
            UserTokenEntry? observed = null;
            for (var i = 0; i < 50; i++)
            {
                await Task.Delay(100);
                observed = a.GetDefaultToken();
                if (observed is not null) break;
            }

            Assert.NotNull(observed);
            Assert.Equal("halo_access_xyz", observed!.AccessToken);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
