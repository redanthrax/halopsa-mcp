namespace HaloPsaMcp.Modules.HaloPsa.Services;

internal class QueryResult {
    public List<Dictionary<string, object>> Rows { get; set; } = new();
    public int Count { get; set; }
    public List<ReportColumn> Columns { get; set; } = new();
    public string? RawResponse { get; set; }
}
