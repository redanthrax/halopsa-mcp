namespace HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;

/// <summary>
/// Query to make a direct API call
/// </summary>
public record ApiCallQuery(string Endpoint, string Method = "GET", string? Body = null);
