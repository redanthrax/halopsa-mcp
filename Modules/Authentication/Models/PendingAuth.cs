namespace HaloPsaMcp.Modules.Authentication.Models;

/// <summary>
/// Tracks a pending OAuth authorization flow, including PKCE verifier and client redirect info.
/// </summary>
internal class PendingAuth {
    public required string HaloPsaVerifier { get; init; }
    public required string ClientRedirectUri { get; init; }
    public string? ClientState { get; init; }
    public required string ClientCodeChallenge { get; init; }
    public required string ClientCode { get; init; }
    public required long Expires { get; init; }
    public bool IsDirectLogin { get; init; }
}
