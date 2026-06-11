using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace HaloPsaMcp.Modules.HaloPsa.Services;

/// <summary>
/// Prevents HaloPSA upstream response bodies (PII, SQL errors, token fragments)
/// from reaching logs, browser pages, or MCP tool output.
/// </summary>
internal static class HaloPsaResponseSanitizer {
    internal static string SafeFailureMessage(string operation, HttpStatusCode status) =>
        $"{operation} failed ({(int)status}).";

    internal static HttpRequestException ApiException(string operation, HttpStatusCode status) =>
        new(SafeFailureMessage(operation, status));

    internal static void LogFailure(
        ILogger? logger,
        string operation,
        HttpStatusCode status,
        int bodyBytes,
        long elapsedMs) {
        logger?.LogError(
            "HaloPSA {Operation} failed | status={StatusCode} bodyBytes={BodyBytes} elapsed={ElapsedMs}ms",
            operation, (int)status, bodyBytes, elapsedMs);
    }

    internal static string SqlLogFingerprint(string sql) {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
