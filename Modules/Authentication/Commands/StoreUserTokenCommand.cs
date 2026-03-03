namespace HaloPsaMcp.Modules.Authentication.Commands;

/// <summary>
/// Command to store user token
/// </summary>
internal record StoreUserTokenCommand(string AccessToken, string RefreshToken, long ExpiresAt);
