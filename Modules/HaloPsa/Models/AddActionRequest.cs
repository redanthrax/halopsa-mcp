using System.Text.Json.Serialization;

namespace HaloPsaMcp.Modules.HaloPsa.Models;

internal record AddActionRequest(
    [property: JsonPropertyName("ticket_id")] int TicketId,
    [property: JsonPropertyName("outcome_id")] int OutcomeId,
    [property: JsonPropertyName("note_html")] string? Note = null,
    [property: JsonPropertyName("timetaken")] double? TimeTaken = null,
    [property: JsonPropertyName("hiddenfromuser")] bool? HiddenFromUser = true,
    [property: JsonPropertyName("new_status")] int? NewStatus = null,
    [property: JsonPropertyName("dont_do_rules")] bool? DontDoRules = true);