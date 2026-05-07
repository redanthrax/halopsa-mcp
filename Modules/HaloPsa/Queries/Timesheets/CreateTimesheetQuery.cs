namespace HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;

internal record CreateTimesheetQuery(
    int AgentId,
    string Date,
    string? StartTime,
    string? EndTime,
    int UtcOffset);
