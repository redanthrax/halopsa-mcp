namespace HaloPsaMcp.Modules.HaloPsa.Queries.Assets;

/// <summary>
/// Query to list assets
/// </summary>
public record ListAssetsQuery(int Count = 10, int? ClientId = null, string? Search = null);
