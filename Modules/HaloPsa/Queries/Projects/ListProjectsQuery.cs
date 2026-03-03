namespace HaloPsaMcp.Modules.HaloPsa.Queries.Projects;

/// <summary>
/// Query to list projects
/// </summary>
internal record ListProjectsQuery(int Count = 10, int? ClientId = null, string? Search = null);
