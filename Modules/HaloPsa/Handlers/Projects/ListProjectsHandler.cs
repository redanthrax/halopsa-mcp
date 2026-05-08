using System.Globalization;
using System.Text;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Projects;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Projects;

/// <summary>
/// Lists project tickets via the reporting database, identifying projects
/// authoritatively by REQUESTTYPE.RTIsProject = 1 joined to FAULTS.RequestTypeNew.
/// The `/api/Projects` REST endpoint mixes project tasks (children) with
/// parent project tickets and does not filter by request type, so it cannot
/// give an accurate "list my projects" answer; SQL is the only reliable path.
/// </summary>
public static class ListProjectsHandler {
    public static async Task<ListProjectsResult> Handle(
        ListProjectsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);

        var sql = BuildSql(query);
        var guard = SqlGuard.Inspect(sql);
        if (!guard.Ok) {
            throw new InvalidOperationException("Internal projects SQL failed guard: " + guard.Reason);
        }

        var queryResult = await client.ExecuteQueryAsync(sql).ConfigureAwait(false);
        var json = JsonSerializer.SerializeToElement(new {
            count = queryResult.Rows.Count,
            source = "FAULTS via REQUESTTYPE.RTIsProject = 1",
            openOnly = query.OpenOnly,
            rows = queryResult.Rows,
        });
        return new ListProjectsResult(json);
    }

    /// <summary>
    /// Build the SELECT sent to /api/Report. Public for unit testing the
    /// "must use RTIsProject and never the legacy Requesttype column" invariant.
    /// </summary>
    public static string BuildSql(ListProjectsQuery query) {
        var count = Math.Clamp(query.Count, 1, 100);
        var sb = new StringBuilder(512);
        sb.Append(CultureInfo.InvariantCulture, $"SELECT TOP {count} ");
        sb.Append("f.Faultid, f.Symptom, f.Status, f.Areaint AS client_id, ");
        sb.Append("f.Assignedtoint AS assigned_agent_id, f.datecreated, f.datecleared, ");
        sb.Append("a.aareadesc AS client, ");
        sb.Append("u.uname AS assigned_to, ");
        sb.Append("rt.RTRequestType AS request_type ");
        sb.Append("FROM FAULTS f ");
        sb.Append("INNER JOIN REQUESTTYPE rt ON rt.RTid = f.RequestTypeNew AND rt.RTIsProject = 1 ");
        sb.Append("LEFT JOIN AREA a ON a.Aarea = f.Areaint ");
        sb.Append("LEFT JOIN UNAME u ON u.Unum = CAST(f.Assignedtoint AS int) ");
        sb.Append("WHERE f.FDeleted = 'False' ");
        if (query.OpenOnly) {
            sb.Append("AND f.Status NOT IN (8, 9) ");
        }
        if (query.ClientId.HasValue && query.ClientId.Value > 0) {
            sb.Append(CultureInfo.InvariantCulture, $"AND f.Areaint = {query.ClientId.Value} ");
        }
        if (!string.IsNullOrWhiteSpace(query.Search)) {
            // Escape single quotes for SQL string literal; SqlGuard rejects ;
            // and comments so this is the only special character we worry about.
            var safe = query.Search.Replace("'", "''", StringComparison.Ordinal);
            sb.Append(CultureInfo.InvariantCulture, $"AND f.Symptom LIKE '%{safe}%' ");
        }
        sb.Append("ORDER BY f.Faultid DESC");
        return sb.ToString();
    }
}
