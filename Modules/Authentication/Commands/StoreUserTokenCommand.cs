namespace HaloPsaMcp.Modules.Authentication.Commands;

/// <summary>
/// Command to store user token
/// </summary>
public record StoreUserTokenCommand(string AccessToken, string RefreshToken, long ExpiresAt);
