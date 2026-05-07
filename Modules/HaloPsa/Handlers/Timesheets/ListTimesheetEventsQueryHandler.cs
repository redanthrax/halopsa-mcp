using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Timesheets;

internal static class ListTimesheetEventsQueryHandler {
    public static async Task<ListTimesheetEventsResult> Handle(
        ListTimesheetEventsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);

        var queryParams = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(query.StartDate)) queryParams["start_date"] = query.StartDate;
        if (!string.IsNullOrEmpty(query.EndDate)) queryParams["end_date"] = query.EndDate;
        if (query.AgentId.HasValue) {
            queryParams["agent_id"] = query.AgentId.Value.ToString(CultureInfo.InvariantCulture);
        }

        var result = await client.GetAsync<JsonElement>("/api/TimesheetEvent", queryParams).ConfigureAwait(false);
        return new ListTimesheetEventsResult(result);
    }
}
