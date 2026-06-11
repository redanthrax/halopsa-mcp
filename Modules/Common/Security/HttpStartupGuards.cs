namespace HaloPsaMcp.Modules.Common.Security;

/// <summary>Fail-closed checks before starting HTTP / production mode.</summary>
internal static class HttpStartupGuards {
    private const string OpenDcrEnv = "MCP_ALLOW_OPEN_DCR";

    internal static void EnsureHttpModeSecurity() {
        var iat = Environment.GetEnvironmentVariable("MCP_DCR_INITIAL_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(iat)) {
            return;
        }

        if (IsTruthy(Environment.GetEnvironmentVariable(OpenDcrEnv))) {
            return;
        }

        throw new InvalidOperationException(
            "HTTP mode requires MCP_DCR_INITIAL_ACCESS_TOKEN so /register (DCR) is not open to the internet. "
            + $"For local Docker testing only, set {OpenDcrEnv}=1.");
    }

    internal static bool StdioOAuthBindAllInterfaces() =>
        IsTruthy(Environment.GetEnvironmentVariable("HTTP_BIND_ALL"));

    private static bool IsTruthy(string? value) =>
        value is "1" or "true" or "yes" or "on";
}
