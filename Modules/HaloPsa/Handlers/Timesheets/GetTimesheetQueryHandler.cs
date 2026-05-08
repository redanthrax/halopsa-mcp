using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Timesheets;

public static class GetTimesheetQueryHandler {
    public static async Task<GetTimesheetResult> Handle(
        GetTimesheetQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string>();
        if (query.AgentId.HasValue) {
            queryParams["agent_id"] = query.AgentId.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (!string.IsNullOrEmpty(query.Date)) {
            queryParams["date"] = query.Date;
        }
        var result = await client.GetAsync<JsonElement>(
            $"/api/Timesheet/{query.Id}",
            queryParams.Count > 0 ? queryParams : null).ConfigureAwait(false);
        return new GetTimesheetResult(result);
    }
}
