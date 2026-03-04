using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Reports;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Reports;

internal static class ListSurveysHandler {
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
}