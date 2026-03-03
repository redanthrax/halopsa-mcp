namespace HaloPsaMcp.Modules.Authentication.Queries;

/// <summary>
/// Query to validate a Bearer token
/// </summary>
internal record ValidateTokenQuery(string Token);
