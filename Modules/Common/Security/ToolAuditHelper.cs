using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.AspNetCore.Http;

namespace HaloPsaMcp.Modules.Common.Security;

internal static class ToolAuditHelper {
    public static string HashArgs(object? args) {
        if (args is null) {
            return "none";
        }
        try {
            var json = JsonSerializer.Serialize(args);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hash)[..16].ToLowerInvariant();
        } catch {
            return "unhashable";
        }
    }

    public static string? ResolveUserHint(HttpContext? httpContext, ITokenStore tokenStore) {
        if (httpContext is null) {
            return "stdio";
        }
        var auth = httpContext.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            return SecretRedactor.Hint(auth["Bearer ".Length..]);
        }
        return "anonymous";
    }
}
