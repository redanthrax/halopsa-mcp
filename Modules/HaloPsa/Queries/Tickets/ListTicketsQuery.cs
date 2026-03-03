namespace HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;

/// <summary>
/// Query to list tickets from HaloPSA
/// </summary>
internal record ListTicketsQuery(
    int Count = 10,
    int? Status = null,
    int? ClientId = null,
    int? AgentId = null,
    string? Search = null
);
