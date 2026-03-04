using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class CreateTicketQueryHandler
{
    public static async Task<CreateTicketResult> Handle(
        CreateTicketQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.PostAsync<JsonElement>("/api/Tickets", query.Request).ConfigureAwait(false);
        return new CreateTicketResult(result);
    }
}