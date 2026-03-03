namespace HaloPsaMcp.Modules.HaloPsa.Queries.Projects;

/// <summary>
/// Query to list opportunities
/// </summary>
internal record ListOpportunitiesQuery(int Count = 10, int? ClientId = null, string? Search = null);
