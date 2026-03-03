namespace HaloPsaMcp.Modules.HaloPsa.Queries.Agents;

/// <summary>
/// Query to list agents
/// </summary>
internal record ListAgentsQuery(int Count = 10, string? Search = null);
