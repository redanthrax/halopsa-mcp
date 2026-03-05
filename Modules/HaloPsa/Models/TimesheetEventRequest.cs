using System.Text.Json.Serialization;

namespace HaloPsaMcp.Modules.HaloPsa.Models;

internal record TimesheetEventRequest(
    [property: JsonPropertyName("id")] int Id = 0,
    [property: JsonPropertyName("ticket_id")] int? TicketId = null,
    [property: JsonPropertyName("agent_id")] int? AgentId = null,
    [property: JsonPropertyName("start_date")] string? StartDate = null,
    [property: JsonPropertyName("end_date")] string? EndDate = null,
    [property: JsonPropertyName("timetaken")] double? TimeTaken = null,
    [property: JsonPropertyName("note")] string? Note = null,
    [property: JsonPropertyName("subject")] string? Subject = null,
    [property: JsonPropertyName("event_type")] int? EventType = null,
    [property: JsonPropertyName("client_id")] int? ClientId = null,
    [property: JsonPropertyName("site_id")] int? SiteId = null);
