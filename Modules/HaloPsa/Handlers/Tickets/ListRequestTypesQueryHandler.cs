using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class ListRequestTypesQueryHandler
{
    public static async Task<ListRequestTypesResult> Handle(
        ListRequestTypesQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string>();
        if (query.VisibleOnly)
        {
            queryParams["visible_only"] = "true";
        }

        var result = await client.GetAsync<JsonElement>("/api/RequestType", queryParams).ConfigureAwait(false);
        return new ListRequestTypesResult(result);
    }
}