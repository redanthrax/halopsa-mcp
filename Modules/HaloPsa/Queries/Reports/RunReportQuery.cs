namespace HaloPsaMcp.Modules.HaloPsa.Queries.Reports;

/// <summary>
/// Query to run a report
/// </summary>
internal record RunReportQuery(int Id, string? Parameters = null);
