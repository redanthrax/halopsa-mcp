namespace HaloPsaMcp.Modules.Common.Infrastructure;

internal interface ITokenStore {
    Task<string?> GetTokenAsync(string sessionId);
    Task<string?> GetRefreshTokenAsync(string sessionId);
    Task SaveTokenAsync(string sessionId, string accessToken, string? refreshToken, long expiresAt);
}
