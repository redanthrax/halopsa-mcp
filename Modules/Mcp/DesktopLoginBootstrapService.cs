using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;
using Microsoft.Extensions.Hosting;

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// desktop MCP client / stdio: when no HaloPSA session exists, open the login page once Kestrel is up.
/// </summary>
internal sealed class DesktopLoginBootstrapService(
    AppConfig config,
    ITokenStore tokenStore,
    ILogger<DesktopLoginBootstrapService> logger) : IHostedService {
    internal static bool AutoOpenEnabled() {
        var value = Environment.GetEnvironmentVariable("HALOPSA_AUTO_OPEN_LOGIN");
        if (string.IsNullOrWhiteSpace(value)) {
            return true;
        }

        return value is not ("0" or "false" or "no" or "off");
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        if (McpRuntime.HostMode != McpHostMode.DesktopStdio) {
            return;
        }

        if (!AutoOpenEnabled()) {
            logger.LogDebug("HALOPSA_AUTO_OPEN_LOGIN disabled — skipping browser sign-in prompt");
            return;
        }

        if (tokenStore.HasValidTokens()) {
            return;
        }

        // Let the OAuth listener bind before opening the browser.
        await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);

        var loginUrl = HaloPsaMcpConstants.GetLoginUrl(config);
        logger.LogInformation("No HaloPSA session — opening sign-in page: {LoginUrl}", loginUrl);
        BrowserLauncher.TryOpen(loginUrl, logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
