namespace HaloPsaMcp.Modules.Common.Security;

/// <summary>
/// Reads configuration secrets from environment variables or Docker-style
/// <c>{NAME}_FILE</c> siblings (mounted from CSI/Vault) so values never
/// appear in <c>/proc/&lt;pid&gt;/environ</c>.
/// </summary>
internal static class SecretEnv {
    public static string? Get(string name) {
        var filePath = Environment.GetEnvironmentVariable(name + "_FILE");
        if (!string.IsNullOrWhiteSpace(filePath)) {
            try {
                return File.ReadAllText(filePath).TrimEnd('\r', '\n', ' ');
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                throw new InvalidOperationException(
                    $"Failed to read secret from {name}_FILE at '{filePath}'.", ex);
            }
        }
        return Environment.GetEnvironmentVariable(name);
    }

    public static bool IsSet(string name) => !string.IsNullOrWhiteSpace(Get(name));
}
