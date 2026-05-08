namespace HaloPsaMcp.Modules.HaloPsa.Queries.Clients;

/// <summary>
/// Query to list clients
/// </summary>
public record ListClientsQuery(int Count = 10, string? Search = null);
