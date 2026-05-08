namespace HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;

public record UpdateTimesheetQuery(
    int Id,
    int UtcOffset,
    string? StartTime,
    string? EndTime,
    bool SubmitApproval,
    bool Approve,
    bool Reject,
    bool RevertApproval);
