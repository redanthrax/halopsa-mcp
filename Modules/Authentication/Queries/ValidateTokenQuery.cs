namespace HaloPsaMcp.Modules.Authentication.Queries;

/// <summary>
/// Query to validate a Bearer token
/// </summary>
public record ValidateTokenQuery(string Token);
