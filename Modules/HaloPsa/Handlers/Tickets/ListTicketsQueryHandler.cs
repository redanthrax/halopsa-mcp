using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class ListTicketsQueryHandler
{
    public static async Task<ListTicketsResult> Handle(
        ListTicketsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);

        var queryParams = new Dictionary<string, string>
        {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (query.Status.HasValue)
        {
            queryParams["status"] = query.Status.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ClientId.HasValue)
        {
            queryParams["client_id"] = query.ClientId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.AgentId.HasValue)
        {
            queryParams["agent_id"] = query.AgentId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            queryParams["search"] = query.Search;
        }

        var result = await client.GetAsync<JsonElement>("/api/Tickets", queryParams).ConfigureAwait(false);
        return new ListTicketsResult(result);
    }
}