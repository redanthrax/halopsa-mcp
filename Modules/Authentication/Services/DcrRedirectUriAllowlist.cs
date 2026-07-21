namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Optional Dynamic Client Registration redirect URI allowlist.
/// When MCP_DCR_ALLOWED_REDIRECT_URIS is set, every registered redirect_uri
/// must exactly match one normalized URI from that list.
/// </summary>
internal static class DcrRedirectUriAllowlist {
    private const string EnvName = "MCP_DCR_ALLOWED_REDIRECT_URIS";

    internal sealed record Policy(
        bool Enabled,
        HashSet<string> AllowedUris,
        int InvalidEntryCount);

    internal static Policy Resolve() {
        var raw = Environment.GetEnvironmentVariable(EnvName);
        if (string.IsNullOrWhiteSpace(raw)) {
            return new Policy(false, [], 0);
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        var invalid = 0;

        foreach (var candidate in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed)) {
                invalid++;
                continue;
            }

            var isHttps = string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase);
            var isLoopbackHttp = string.Equals(parsed.Scheme, "http", StringComparison.OrdinalIgnoreCase) && parsed.IsLoopback;
            if (!isHttps && !isLoopbackHttp) {
                invalid++;
                continue;
            }

            if (candidate.Contains('?', StringComparison.Ordinal) || candidate.Contains('#', StringComparison.Ordinal)) {
                invalid++;
                continue;
            }

            allowed.Add(RedirectUriNormalizer.Normalize(candidate));
        }

        return new Policy(true, allowed, invalid);
    }
}
