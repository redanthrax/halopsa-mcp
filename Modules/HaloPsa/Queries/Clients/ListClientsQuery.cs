namespace HaloPsaMcp.Modules.HaloPsa.Queries.Clients;

/// <summary>
/// Query to list clients
/// </summary>
internal record ListClientsQuery(int Count = 10, string? Search = null);
