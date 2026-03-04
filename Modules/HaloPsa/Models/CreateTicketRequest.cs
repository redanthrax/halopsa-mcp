using System.Text.Json.Serialization;

namespace HaloPsaMcp.Modules.HaloPsa.Models;

/// <summary>
/// Request model for creating a new ticket
/// </summary>
internal class CreateTicketRequest
{
    /// <summary>
    /// The ticket summary or title.
    /// </summary>
    public required string Summary { get; set; }
    /// <summary>
    /// The ticket details or description.
    /// </summary>
    public string? Details { get; set; }
    /// <summary>
    /// The client ID associated with the ticket.
    /// </summary>
    [JsonPropertyName("client_id")]
    public int? ClientId { get; set; }
    /// <summary>
    /// The agent ID assigned to the ticket.
    /// </summary>
    [JsonPropertyName("agent_id")]
    public int? AgentId { get; set; }
    /// <summary>
    /// The status ID of the ticket.
    /// </summary>
    [JsonPropertyName("status_id")]
    public int? StatusId { get; set; }
    /// <summary>
    /// The priority ID of the ticket.
    /// </summary>
    [JsonPropertyName("priority_id")]
    public int? PriorityId { get; set; }
    /// <summary>
    /// The ticket type ID.
    /// </summary>
    [JsonPropertyName("tickettype_id")]
    public int? TicketTypeId { get; set; }
    /// <summary>
    /// The site ID associated with the ticket.
    /// </summary>
    [JsonPropertyName("site_id")]
    public int? SiteId { get; set; }
}