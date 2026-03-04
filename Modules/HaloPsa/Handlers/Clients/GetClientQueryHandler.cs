using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Clients;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Clients;

internal static class GetClientQueryHandler
{
    public static async Task<GetClientResult> Handle(
        GetClientQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.GetAsync<JsonElement>($"/api/Client/{query.Id}", null).ConfigureAwait(false);
        return new GetClientResult(result);
    }
}