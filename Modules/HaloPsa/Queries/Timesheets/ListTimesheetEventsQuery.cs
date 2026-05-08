namespace HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;

public record ListTimesheetEventsQuery(string? StartDate, string? EndDate, int? AgentId);
