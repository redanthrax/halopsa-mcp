namespace HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;

internal record ExecuteQueryResult(int Count, List<Dictionary<string, object>> Rows);
