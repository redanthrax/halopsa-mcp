namespace HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;

public record GetTimesheetQuery(int Id, int? AgentId = null, string? Date = null);
