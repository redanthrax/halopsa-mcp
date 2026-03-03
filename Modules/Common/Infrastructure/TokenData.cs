namespace HaloPsaMcp.Modules.Common.Infrastructure;

internal class TokenData {
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public long ExpiresAt { get; set; }
}
