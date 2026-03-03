namespace HaloPsaMcp.Modules.HaloPsa.Queries.KnowledgeBase;

/// <summary>
/// Query to list KB articles
/// </summary>
internal record ListKbArticlesQuery(int Count = 10, string? Search = null);
