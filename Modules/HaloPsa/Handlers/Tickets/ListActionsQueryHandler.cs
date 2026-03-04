using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class ListActionsQueryHandler
{
    public static async Task<ListActionsResult> Handle(
        ListActionsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string>
        {
            ["ticket_id"] = query.TicketId.ToString(CultureInfo.InvariantCulture),
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        var result = await client.GetAsync<JsonElement>("/api/Actions", queryParams).ConfigureAwait(false);
        return new ListActionsResult(result);
    }
}