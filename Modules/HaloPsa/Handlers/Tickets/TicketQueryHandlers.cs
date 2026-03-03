using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

/// <summary>
/// Handlers for ticket-related queries
/// </summary>
internal static class TicketQueryHandlers {
    public static async Task<ListTicketsResult> Handle(
        ListTicketsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);

        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (query.Status.HasValue) {
            queryParams["status"] = query.Status.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ClientId.HasValue) {
            queryParams["client_id"] = query.ClientId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.AgentId.HasValue) {
            queryParams["agent_id"] = query.AgentId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(query.Search)) {
            queryParams["search"] = query.Search;
        }

        var result = await client.GetAsync<JsonElement>("/api/Tickets", queryParams).ConfigureAwait(false);
        return new ListTicketsResult(result);
    }

    public static async Task<GetTicketResult> Handle(
        GetTicketQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.GetAsync<JsonElement>($"/api/Tickets/{query.Id}", null).ConfigureAwait(false);
        return new GetTicketResult(result);
    }

    public static async Task<ListStatusesResult> Handle(
        ListStatusesQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string>();
        if (query.Type.HasValue) {
            queryParams["type"] = query.Type.Value.ToString(CultureInfo.InvariantCulture);
        }

        var result = await client.GetAsync<JsonElement>("/api/Status", queryParams).ConfigureAwait(false);
        return new ListStatusesResult(result);
    }

    public static async Task<ListRequestTypesResult> Handle(
        ListRequestTypesQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string>();
        if (query.VisibleOnly) {
            queryParams["visible_only"] = "true";
        }

        var result = await client.GetAsync<JsonElement>("/api/RequestType", queryParams).ConfigureAwait(false);
        return new ListRequestTypesResult(result);
    }

    public static async Task<ListActionsResult> Handle(
        ListActionsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string> {
            ["ticket_id"] = query.TicketId.ToString(CultureInfo.InvariantCulture),
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        var result = await client.GetAsync<JsonElement>("/api/Actions", queryParams).ConfigureAwait(false);
        return new ListActionsResult(result);
    }

    public static async Task<ExecuteQueryResult> Handle(
        ExecuteQueryQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.ExecuteQueryAsync(query.Sql).ConfigureAwait(false);
        return new ExecuteQueryResult(result.Count, result.Rows);
    }

    public static async Task<ApiCallResult> Handle(
        ApiCallQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        object? bodyObj = null;
        if (!string.IsNullOrEmpty(query.Body)) {
            bodyObj = JsonSerializer.Deserialize<object>(query.Body);
        }

        var result = await client.MakeApiCallAsync(query.Endpoint, query.Method, bodyObj).ConfigureAwait(false);
        return new ApiCallResult(result);
    }
}
