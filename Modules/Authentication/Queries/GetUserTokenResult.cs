namespace HaloPsaMcp.Modules.Authentication.Queries;

internal record GetUserTokenResult(string? AccessToken, string? RefreshToken, long? ExpiresAt);
