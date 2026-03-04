using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Assets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Assets;

internal static class GetAssetQueryHandler
{
    public static async Task<GetAssetResult> Handle(
        GetAssetQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.GetAsync<JsonElement>($"/api/Asset/{query.Id}", null).ConfigureAwait(false);
        return new GetAssetResult(result);
    }
}