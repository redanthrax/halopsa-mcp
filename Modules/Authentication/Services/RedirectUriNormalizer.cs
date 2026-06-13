using System.Globalization;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Canonicalizes OAuth redirect URIs so equivalent URLs match during DCR
/// registration and /authorize validation (RFC 8252 loopback rules preserved).
/// </summary>
internal static class RedirectUriNormalizer {
    /// <summary>
    /// Normalizes a redirect URI: lowercase host, strip default port,
    /// remove trailing slash, reject fragments. Query strings are rejected
    /// at registration time.
    /// </summary>
    public static string Normalize(string uri) {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) {
            return uri;
        }

        var scheme = parsed.Scheme.ToLowerInvariant();
        var host = parsed.Host.ToLowerInvariant();
        var port = parsed.Port;
        var isDefaultPort =
            (scheme == "https" && port == 443) ||
            (scheme == "http" && port == 80);
        var portPart = isDefaultPort ? string.Empty : $":{port.ToString(CultureInfo.InvariantCulture)}";
        var path = parsed.AbsolutePath;
        if (path.Length > 1 && path.EndsWith('/')) {
            path = path.TrimEnd('/');
        }
        if (path.Length == 0) {
            path = "/";
        }

        return $"{scheme}://{host}{portPart}{path}";
    }

    public static string[] NormalizeAll(IEnumerable<string> uris) =>
        uris.Select(Normalize).Distinct(StringComparer.Ordinal).ToArray();
}
