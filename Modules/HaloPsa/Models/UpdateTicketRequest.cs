using System.Text.Json.Serialization;

namespace HaloPsaMcp.Modules.HaloPsa.Models;

internal record UpdateTicketRequest(
    int Id,
    string Summary,
    string? Details = null,
    [property: JsonPropertyName("client_id")] int? ClientId = null,
    [property: JsonPropertyName("agent_id")] int? AgentId = null,
    [property: JsonPropertyName("status_id")] int? StatusId = null,
    [property: JsonPropertyName("priority_id")] int? PriorityId = null,
    [property: JsonPropertyName("tickettype_id")] int? TicketTypeId = null,
    [property: JsonPropertyName("site_id")] int? SiteId = null,
    [property: JsonPropertyName("user_id")] int? UserId = null);