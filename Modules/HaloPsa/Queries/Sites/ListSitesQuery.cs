namespace HaloPsaMcp.Modules.HaloPsa.Queries.Sites;

/// <summary>
/// Query to list sites
/// </summary>
public record ListSitesQuery(int ClientId, int Count = 10);
