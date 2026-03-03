namespace HaloPsaMcp.Modules.Authentication.Models;

/// <summary>
/// OAuth completed authorization with tokens
/// </summary>
internal class CompletedAuth {
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
    public required string ClientCodeChallenge { get; init; }
    public required long Expires { get; init; }
}
