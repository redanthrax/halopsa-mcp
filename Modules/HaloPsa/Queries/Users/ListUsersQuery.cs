namespace HaloPsaMcp.Modules.HaloPsa.Queries.Users;

/// <summary>
/// Query to list users
/// </summary>
internal record ListUsersQuery(int Count = 10, int? ClientId = null, string? Search = null);
