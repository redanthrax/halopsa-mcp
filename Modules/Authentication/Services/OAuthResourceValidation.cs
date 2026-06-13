using HaloPsaMcp.Modules.Common.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>RFC 8707 resource indicator validation for the MCP protected resource.</summary>
internal static class OAuthResourceValidation {
    internal static string ExpectedResource(AppConfig config) =>
        OAuthDiscovery.ResolveMcpResourceUrl(config);

    internal static bool IsValid(AppConfig config, string? resource) {
        if (string.IsNullOrWhiteSpace(resource)) {
            return true;
        }

        return string.Equals(
            NormalizeResourceUri(resource),
            NormalizeResourceUri(ExpectedResource(config)),
            StringComparison.OrdinalIgnoreCase);
    }

    internal static string BindResource(AppConfig config, string? requestedResource) {
        if (string.IsNullOrWhiteSpace(requestedResource)) {
            return ExpectedResource(config);
        }

        return NormalizeResourceUri(requestedResource);
    }

    internal static string NormalizeResourceUri(string resource) {
        var trimmed = resource.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) && trimmed.Length > 1
            ? trimmed.TrimEnd('/')
            : trimmed;
    }
}
