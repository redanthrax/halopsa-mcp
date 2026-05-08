namespace HaloPsaMcp.Modules.Authentication.Queries;

public record GetUserTokenResult(string? AccessToken, string? RefreshToken, long? ExpiresAt);
