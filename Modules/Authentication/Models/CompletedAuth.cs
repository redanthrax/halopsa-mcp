using HaloPsaMcp.Modules.Authentication.Services;

namespace HaloPsaMcp.Modules.Authentication.Models;

/// <summary>
/// OAuth completed authorization with tokens
/// </summary>
public class CompletedAuth : IExpiring {
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
    public required string ClientCodeChallenge { get; init; }
    public required long Expires { get; init; }
    public string? ClientId { get; init; }
    public string? ClientRedirectUri { get; init; }
    /// <summary>RFC 8707 resource indicator bound at authorize time.</summary>
    public string? Resource { get; init; }
}
