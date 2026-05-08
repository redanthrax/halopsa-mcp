namespace HaloPsaMcp.Modules.HaloPsa.Queries.KnowledgeBase;

/// <summary>
/// Query to list KB articles
/// </summary>
public record ListKbArticlesQuery(int Count = 10, string? Search = null);
