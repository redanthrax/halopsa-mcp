using System.Security.Cryptography;
using System.Text;

namespace HaloPsaMcp.Modules.Authentication.Services;

internal static class PkceHelper {
    public static string GenerateCodeVerifier() {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public static string GenerateCodeChallenge(string verifier) {
        var bytes = Encoding.UTF8.GetBytes(verifier);
        var hash = SHA256.HashData(bytes);
        return Base64UrlEncode(hash);
    }

    public static string BuildAuthUrl(string baseUrl, string clientId, string tenant,
        string redirectUri, string state, string codeChallenge, string? scope = null) {
        var actualScope = scope ?? "all offline_access";
        var queryParams = new Dictionary<string, string> {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = actualScope,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var query = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{baseUrl}/auth/authorize?tenant={Uri.EscapeDataString(tenant)}&{query}";
    }

    private static string Base64UrlEncode(byte[] input) {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
