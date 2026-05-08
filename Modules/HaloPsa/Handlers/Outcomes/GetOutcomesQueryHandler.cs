using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Outcomes;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Outcomes;

public static class GetOutcomesQueryHandler {
    public static async Task<GetOutcomesResult> Handle(
        GetOutcomesQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        _ = query;
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);
        var result = await client.GetAsync<JsonElement>("/api/Outcome", null).ConfigureAwait(false);
        return new GetOutcomesResult(result);
    }
}
