using System.Text.Json.Serialization;

namespace HaloPsaMcp.Modules.Authentication.Models;

/// <summary>
/// Per-session token record. AccessToken/RefreshToken are the upstream HaloPSA
/// pair (held server-side, never exposed). McpRefreshToken is the opaque
/// rotating refresh credential we hand out to the MCP client; it is distinct
/// from the bearer access token so a stolen bearer cannot be replayed at /token.
/// </summary>
public class UserTokenEntry {
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("expires_at")]
    public required long ExpiresAt { get; init; }

    [JsonPropertyName("mcp_refresh_token")]
    public string? McpRefreshToken { get; init; }
}
