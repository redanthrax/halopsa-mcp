using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class GetTicketQueryHandler
{
    public static async Task<GetTicketResult> Handle(
        GetTicketQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.GetAsync<JsonElement>($"/api/Tickets/{query.Id}", null).ConfigureAwait(false);
        return new GetTicketResult(result);
    }
}