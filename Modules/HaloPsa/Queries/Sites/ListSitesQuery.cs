namespace HaloPsaMcp.Modules.HaloPsa.Queries.Sites;

/// <summary>
/// Query to list sites
/// </summary>
internal record ListSitesQuery(int ClientId, int Count = 10);
