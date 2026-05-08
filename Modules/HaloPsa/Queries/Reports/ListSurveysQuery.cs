namespace HaloPsaMcp.Modules.HaloPsa.Queries.Reports;

/// <summary>
/// Query to list surveys
/// </summary>
public record ListSurveysQuery(int Count = 10, int? TicketId = null);
