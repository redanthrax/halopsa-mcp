using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Reports;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Reports;

internal static class GetReportDefinitionHandler {
    public static async Task<GetReportDefinitionResult> Handle(
        GetReportDefinitionQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.GetAsync<JsonElement>($"/api/Report/{query.Id}", null).ConfigureAwait(false);
        return new GetReportDefinitionResult(result);
    }
}