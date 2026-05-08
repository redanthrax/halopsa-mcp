namespace HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;

/// <summary>
/// Query to list ticket actions
/// </summary>
public record ListActionsQuery(int TicketId, int Count = 10);
