namespace HaloPsaMcp.Modules.Authentication.Models;

/// <summary>
/// OAuth token response from HaloPSA
/// </summary>
internal class TokenResponse {
    public required string access_token { get; init; }
    public string? refresh_token { get; init; }
    public int expires_in { get; init; }
    public string? token_type { get; init; }
}
