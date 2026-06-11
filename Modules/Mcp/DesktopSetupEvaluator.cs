using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.Common.Models;

namespace HaloPsaMcp.Modules.Mcp;

internal static class DesktopSetupEvaluator {
    internal sealed record SetupCheck(string Id, bool Ok, string Detail);

    internal sealed record SetupStatus(
        string Mode,
        bool ReadyForTools,
        bool Authenticated,
        string LoginUrl,
        string? NextStep,
        IReadOnlyList<SetupCheck> Checks,
        string DesktopSetupHint);

    internal static SetupStatus Evaluate(AppConfig config, ITokenStore? tokenStore) {
        var loginUrl = HaloPsaMcpConstants.GetLoginUrl(config);
        var authenticated = tokenStore?.HasValidTokens() ?? false;
        var checks = new List<SetupCheck> {
            new("halopsa_url", !string.IsNullOrWhiteSpace(config.HaloPsa.Url),
                string.IsNullOrWhiteSpace(config.HaloPsa.Url) ? "HALOPSA_URL is not set" : config.HaloPsa.Url),
            new("client_id", !string.IsNullOrWhiteSpace(config.HaloPsa.ClientId),
                string.IsNullOrWhiteSpace(config.HaloPsa.ClientId) ? "HALOPSA_CLIENT_ID is not set" : "configured"),
            new("token_store", Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(config.HaloPsa.TokenStorePath)) ?? "./data"),
                $"sessions stored at {config.HaloPsa.TokenStorePath}"),
            new("oauth_listener", !AppConfigRuntime.PortFallbackActive,
                AppConfigRuntime.PortFallbackActive
                    ? $"Port {config.HttpPort} was busy — using {AppConfigRuntime.EffectivePublicBaseUrl}. Set AUTH_BASE_URL to match or free the port and restart."
                    : $"OAuth server on {AppConfigRuntime.ResolvePublicBaseUrl(config)}"),
            new("session", authenticated,
                authenticated ? "active HaloPSA session found" : "no session — browser login required")
        };

        string? nextStep = authenticated
            ? null
            : $"Ask the user to open {loginUrl} in a browser, sign in to HaloPSA, then retry.";

        var desktopHint =
            "Desktop stdio: put HALOPSA_* vars in .env (not the MCP host config env block when using WSL). "
            + "Restart the MCP host after changing .env or the MCP binary.";

        return new SetupStatus(
            Mode: "desktop_stdio",
            ReadyForTools: checks.Where(c => c.Id is "halopsa_url" or "client_id").All(c => c.Ok),
            Authenticated: authenticated,
            LoginUrl: loginUrl,
            NextStep: nextStep,
            Checks: checks,
            DesktopSetupHint: desktopHint);
    }
}
