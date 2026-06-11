using System.Security.Cryptography;

namespace HaloPsaMcp.Modules.Authentication.Services;

internal static class McpTokenGenerator {
    private const string McpTokenPrefix = "mcp_";
    private const string McpRefreshPrefix = "mcr_";

    public static string GenerateMcpToken() => GenerateOpaque(McpTokenPrefix);

    public static string GenerateMcpRefreshToken() => GenerateOpaque(McpRefreshPrefix);

    private static string GenerateOpaque(string prefix) {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var b64 = Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return prefix + b64;
    }
}
