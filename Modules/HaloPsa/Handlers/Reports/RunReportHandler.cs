using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Reports;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Reports;

internal static class RunReportHandler {
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