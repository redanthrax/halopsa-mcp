namespace HaloPsaMcp.Modules.Common.Models;

internal class HaloPsaSettings {
    public string Url { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string TokenStorePath { get; set; } = string.Empty;
}
