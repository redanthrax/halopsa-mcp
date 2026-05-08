using HaloPsaMcp.Modules.HaloPsa.Models;

namespace HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;

/// <summary>
/// Query to create a new ticket in HaloPSA
/// </summary>
public record CreateTicketQuery(CreateTicketRequest Request);