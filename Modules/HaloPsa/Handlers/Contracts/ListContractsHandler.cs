using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Contracts;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Contracts;

internal static class ListContractsHandler {
    public static async Task<ListContractsResult> Handle(
        ListContractsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (query.ClientId.HasValue) {
            queryParams["client_id"] = query.ClientId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(query.Search)) {
            queryParams["search"] = query.Search;
        }

        var result = await client.GetAsync<JsonElement>("/api/SLAService", queryParams).ConfigureAwait(false);
        return new ListContractsResult(result);
    }
}