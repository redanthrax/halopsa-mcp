namespace HaloPsaMcp.Modules.HaloPsa.Queries.Appointments;

/// <summary>
/// Query to list appointments
/// </summary>
internal record ListAppointmentsQuery(
    int Count = 10,
    int? AgentId = null,
    string? StartDate = null,
    string? EndDate = null);
