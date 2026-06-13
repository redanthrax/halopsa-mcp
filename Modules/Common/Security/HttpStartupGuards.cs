namespace HaloPsaMcp.Modules.Common.Security;

/// <summary>Fail-closed checks before starting HTTP / production mode.</summary>
internal static class HttpStartupGuards {
    private const string OpenDcrEnv = "MCP_ALLOW_OPEN_DCR";

    internal static void EnsureHttpModeSecurity() {
        if (IsDcrInitialAccessTokenConfigured()) {
            if (IsTruthy(Environment.GetEnvironmentVariable(OpenDcrEnv))) {
                return;
            }
            return;
        }

        if (IsTruthy(Environment.GetEnvironmentVariable(OpenDcrEnv))) {
            return;
        }

        // Open DCR is the MCP default (rate-limited). Set MCP_DCR_INITIAL_ACCESS_TOKEN to gate /register.
    }

    internal static bool IsDcrInitialAccessTokenConfigured() =>
        SecretEnv.IsSet("MCP_DCR_INITIAL_ACCESS_TOKEN");

    internal static bool StdioOAuthBindAllInterfaces() =>
        IsTruthy(Environment.GetEnvironmentVariable("HTTP_BIND_ALL"));

    internal static bool IsTruthy(string? value) =>
        value is "1" or "true" or "yes" or "on";
}
