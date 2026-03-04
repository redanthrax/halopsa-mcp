using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Reports;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Reports;

internal static class ListReportsHandler {
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
}