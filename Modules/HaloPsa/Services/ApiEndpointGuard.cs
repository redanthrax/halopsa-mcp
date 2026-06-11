namespace HaloPsaMcp.Modules.HaloPsa.Services;

/// <summary>Validates halopsa_api_call paths before they reach HaloPSA.</summary>
internal static class ApiEndpointGuard {
    private static readonly HashSet<string> AllowedMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "POST", "PUT" };

    internal static void Validate(string endpoint, string method) {
        if (string.IsNullOrWhiteSpace(endpoint)) {
            throw new ArgumentException("endpoint is required");
        }

        if (!endpoint.StartsWith('/')) {
            throw new ArgumentException("endpoint must be a relative path starting with /");
        }

        if (endpoint.Contains("://", StringComparison.Ordinal)) {
            throw new ArgumentException("absolute URLs are not allowed");
        }

        if (endpoint.Contains("..", StringComparison.Ordinal)) {
            throw new ArgumentException("path traversal is not allowed");
        }

        if (!endpoint.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("endpoint must start with /api/");
        }

        if (!AllowedMethods.Contains(method)) {
            throw new ArgumentException("method must be GET, POST, or PUT");
        }
    }
}
