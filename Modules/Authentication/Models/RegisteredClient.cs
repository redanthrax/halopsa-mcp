namespace HaloPsaMcp.Modules.Authentication.Models;

/// <summary>
/// A persisted OAuth 2.1 dynamically-registered client.
/// </summary>
public sealed record RegisteredClient {
    public required string ClientId { get; init; }
    /// <summary>Optional client_secret (we issue public clients only — null).</summary>
    public string? ClientSecret { get; init; }
    public required string[] RedirectUris { get; init; }
    public required long CreatedAt { get; init; }
    /// <summary>Unix ms when client was last used (/authorize or /token).</summary>
    public long LastUsedAt { get; init; }
}
