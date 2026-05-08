namespace HaloPsaMcp.Modules.HaloPsa.Queries.Projects;

/// <summary>
/// Query to list opportunities
/// </summary>
public record ListOpportunitiesQuery(int Count = 10, int? ClientId = null, string? Search = null);
