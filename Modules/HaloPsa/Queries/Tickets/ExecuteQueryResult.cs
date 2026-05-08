namespace HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;

public record ExecuteQueryResult(int Count, List<Dictionary<string, object>> Rows, string? RawResponse = null);
