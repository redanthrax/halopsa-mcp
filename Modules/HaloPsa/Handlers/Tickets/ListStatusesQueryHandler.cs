using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class ListStatusesQueryHandler
{
    public static async Task<ListStatusesResult> Handle(
        ListStatusesQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string>();
        if (query.Type.HasValue)
        {
            queryParams["type"] = query.Type.Value.ToString(CultureInfo.InvariantCulture);
        }

        var result = await client.GetAsync<JsonElement>("/api/Status", queryParams).ConfigureAwait(false);
        return new ListStatusesResult(result);
    }
}