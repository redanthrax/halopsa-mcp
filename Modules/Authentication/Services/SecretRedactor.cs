namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Centralized redaction helpers so tokens, codes, and verifiers never reach
/// logs in clear text. Always emit a short suffix-only hint, never the full value.
/// </summary>
public static class SecretRedactor {
    /// <summary>Returns "...XXXXXXXX" (last 8 chars) or "***" for short/empty input.</summary>
    public static string Hint(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return "***";
        }
        return value.Length >= 8 ? $"...{value[^8..]}" : "***";
    }
}
