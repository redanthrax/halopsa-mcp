using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Reports;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Reports;

internal static class ReportQueryHandlers {
    public static async Task<ListSurveysResult> Handle(
        ListSurveysQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (query.TicketId.HasValue) {
            queryParams["ticket_id"] = query.TicketId.Value.ToString(CultureInfo.InvariantCulture);
        }

        var result = await client.GetAsync<JsonElement>("/api/CustomerSatisfaction", queryParams).ConfigureAwait(false);
        return new ListSurveysResult(result);
    }

    public static async Task<ListReportsResult> Handle(
        ListReportsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        var result = await client.GetAsync<JsonElement>("/api/Report", queryParams).ConfigureAwait(false);
        return new ListReportsResult(result);
    }

    public static async Task<GetReportDefinitionResult> Handle(
        GetReportDefinitionQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.GetAsync<JsonElement>($"/api/Report/{query.Id}", null).ConfigureAwait(false);
        return new GetReportDefinitionResult(result);
    }

    public static async Task<RunReportResult> Handle(
        RunReportQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var bodyObj = new Dictionary<string, object> { ["id"] = query.Id };

        if (!string.IsNullOrEmpty(query.Parameters)) {
            var paramObj = JsonSerializer.Deserialize<Dictionary<string, object>>(query.Parameters);
            if (paramObj != null) {
                bodyObj["parameters"] = paramObj;
            }
        }

        var result = await client.PostAsync<JsonElement>("/api/Report/run", bodyObj).ConfigureAwait(false);
        return new RunReportResult(result);
    }
}
