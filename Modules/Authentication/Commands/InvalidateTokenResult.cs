namespace HaloPsaMcp.Modules.Authentication.Commands;

/// <summary>Result of revoking an MCP session token.</summary>
/// <param name="Success">True when an existing session token was invalidated.</param>
public record InvalidateTokenResult(bool Success);
