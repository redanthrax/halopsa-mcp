namespace HaloPsaMcp.Modules.HaloPsa.Queries.Assets;

/// <summary>
/// Query to list assets
/// </summary>
internal record ListAssetsQuery(int Count = 10, int? ClientId = null, string? Search = null);
