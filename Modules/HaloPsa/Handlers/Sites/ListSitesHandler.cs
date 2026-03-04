using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Sites;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Sites;

internal static class ListSitesHandler {
    public static async Task<ListSitesResult> Handle(
        ListSitesQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string> {
            ["client_id"] = query.ClientId.ToString(CultureInfo.InvariantCulture),
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        var result = await client.GetAsync<JsonElement>("/api/Site", queryParams).ConfigureAwait(false);
        return new ListSitesResult(result);
    }
}