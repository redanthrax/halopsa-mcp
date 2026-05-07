namespace HaloPsaMcp.Modules.Authentication.Models;

/// <summary>
/// A persisted OAuth 2.1 dynamically-registered client.
/// </summary>
internal sealed class RegisteredClient {
    public required string ClientId { get; init; }
    /// <summary>Optional client_secret (we issue public clients only — null).</summary>
    public string? ClientSecret { get; init; }
    public required string[] RedirectUris { get; init; }
    public required long CreatedAt { get; init; }
}
