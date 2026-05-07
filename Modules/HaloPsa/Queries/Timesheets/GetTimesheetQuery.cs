namespace HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;

internal record GetTimesheetQuery(int Id, int? AgentId = null, string? Date = null);
