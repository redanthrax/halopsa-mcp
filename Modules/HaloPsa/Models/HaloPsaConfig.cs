namespace HaloPsaMcp.Modules.HaloPsa.Models;

/// <summary>
/// HaloPSA API configuration
/// </summary>
internal class HaloPsaConfig {
    public required string Url { get; set; }
    public required string ClientId { get; set; }
    public string? ClientSecret { get; set; }

    // Direct token mode (per-user authentication)
    public string? DirectToken { get; set; }
    public string? DirectRefreshToken { get; set; }
    public long? DirectTokenExpiresAt { get; set; }
    public Action<string, string, long>? OnTokenRefreshed { get; set; }

    /// <summary>
    /// Extracts the tenant from the HaloPSA URL (subdomain)
    /// </summary>
    public string GetTenant() {
        var uri = new Uri(Url);
        var host = uri.Host;
        var parts = host.Split('.');
        if (parts.Length < 2) {
            throw new InvalidOperationException($"Cannot extract tenant from URL: {Url}");
        }
        return parts[0]; // Return subdomain (e.g., "acme" from "acme.halopsa.com")
    }
}
