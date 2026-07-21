namespace HaloPsaMcp.Modules.Authentication.Commands;

/// <summary>Result of persisting the authenticated user's token session.</summary>
/// <param name="Success">True when token storage succeeded.</param>
public record StoreUserTokenResult(bool Success);
