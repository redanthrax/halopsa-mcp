namespace HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;

public record CreateTimesheetQuery(
    int AgentId,
    string Date,
    string? StartTime,
    string? EndTime,
    int UtcOffset);
