namespace HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;

/// <summary>
/// Query to make a direct API call
/// </summary>
internal record ApiCallQuery(string Endpoint, string Method = "GET", string? Body = null);
