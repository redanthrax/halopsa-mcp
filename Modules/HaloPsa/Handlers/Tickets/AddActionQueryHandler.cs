using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class AddActionQueryHandler
{
    public static async Task<AddActionResult> Handle(
        AddActionQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var payload = new[] { query.Request };
        var result = await client.PostAsync<JsonElement>("/api/Actions", payload).ConfigureAwait(false);
        return new AddActionResult(result);
    }
}