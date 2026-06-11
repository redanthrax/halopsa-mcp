using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.Mcp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HaloPsaMcp.Tests;

public class DesktopSetupEvaluatorTests {
    [Fact]
    public void Evaluate_reports_unauthenticated_without_session() {
        var dir = Path.Combine(Path.GetTempPath(), "halopsa-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var config = new AppConfig {
            AuthBaseUrl = "http://localhost:3000",
            PublicBaseUrl = "http://localhost:3000",
            HttpPort = 3000,
            HaloPsa = new HaloPsaSettings {
                Url = "https://example.halopsa.com",
                ClientId = "client",
                TokenStorePath = Path.Combine(dir, "tokens.json")
            }
        };

        using var store = new FileTokenStore(
            config,
            DataProtectionProvider.Create("HaloPsaMcp.Tests"),
            NullLogger<FileTokenStore>.Instance);

        var status = DesktopSetupEvaluator.Evaluate(config, store);

        Assert.False(status.Authenticated);
        Assert.Equal("http://localhost:3000/login", status.LoginUrl);
        Assert.Contains("browser", status.NextStep!, StringComparison.OrdinalIgnoreCase);
        Assert.True(status.ReadyForTools);
    }
}
