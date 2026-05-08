namespace HaloPsaMcp.Modules.Authentication.Commands;

/// <summary>
/// Command to invalidate a token in the cache
/// </summary>
public record InvalidateTokenCommand(string Token);
