using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Assets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Assets;

internal static class ListAssetsQueryHandler
{
    public static async Task<ListAssetsResult> Handle(
        ListAssetsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string>
        {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (query.ClientId.HasValue)
        {
            queryParams["client_id"] = query.ClientId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            queryParams["search"] = query.Search;
        }

        var result = await client.GetAsync<JsonElement>("/api/Asset", queryParams).ConfigureAwait(false);
        return new ListAssetsResult(result);
    }
}