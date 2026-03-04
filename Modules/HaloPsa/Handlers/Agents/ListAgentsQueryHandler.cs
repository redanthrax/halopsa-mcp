using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Agents;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Agents;

internal static class ListAgentsQueryHandler
{
    public static async Task<ListAgentsResult> Handle(
        ListAgentsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string>
        {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrEmpty(query.Search))
        {
            queryParams["search"] = query.Search;
        }

        var result = await client.GetAsync<JsonElement>("/api/Agent", queryParams).ConfigureAwait(false);
        return new ListAgentsResult(result);
    }
}