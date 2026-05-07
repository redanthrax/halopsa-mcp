namespace HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;

internal record ListTimesheetEventsQuery(string? StartDate, string? EndDate, int? AgentId);
