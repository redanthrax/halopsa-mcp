using System.Text.Json.Serialization;

namespace HaloPsaMcp.Modules.Authentication.Models;

/// <summary>
/// User token entry for tracking
/// </summary>
internal class UserTokenEntry {
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("expires_at")]
    public required long ExpiresAt { get; init; }
}
