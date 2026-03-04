using System;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class UpdateTicketQueryHandler
{
    public static async Task<UpdateTicketResult> Handle(
        UpdateTicketQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);

        var request = query.Request;

        // If userId is provided, always look up clientId and siteId from the user
        if (request.UserId.HasValue)
        {
            var userQuery = $"SELECT client_id, site_id FROM faults WHERE hdid = {request.UserId.Value}";
            var userResult = await client.ExecuteQueryAsync(userQuery).ConfigureAwait(false);
            if (userResult.Rows.Count > 0)
            {
                var row = userResult.Rows[0];
                if (row.TryGetValue("client_id", out var clientIdValue))
                {
                    request = request with { ClientId = Convert.ToInt32(clientIdValue) };
                }
                if (row.TryGetValue("site_id", out var siteIdValue))
                {
                    request = request with { SiteId = Convert.ToInt32(siteIdValue) };
                }
            }
        }

        var payload = new[] { request };
        var result = await client.PostAsync<JsonElement>("/api/Tickets", payload).ConfigureAwait(false);
        return new UpdateTicketResult(result);
    }
}