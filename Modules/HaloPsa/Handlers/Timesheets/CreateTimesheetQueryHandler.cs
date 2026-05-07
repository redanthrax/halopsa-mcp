using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Timesheets;

internal static class CreateTimesheetQueryHandler {
    public static async Task<CreateTimesheetResult> Handle(
        CreateTimesheetQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);

        var entry = new Dictionary<string, object> {
            ["date"] = query.Date,
            ["agent_id"] = query.AgentId
        };
        if (query.StartTime != null) entry["start_time"] = query.StartTime;
        if (query.EndTime != null) entry["end_time"] = query.EndTime;

        var payload = new[] { entry };
        var queryParams = new Dictionary<string, string> {
            ["utcoffset"] = query.UtcOffset.ToString(CultureInfo.InvariantCulture)
        };
        var result = await client.PostAsync<JsonElement>("/api/Timesheet", payload, queryParams).ConfigureAwait(false);
        return new CreateTimesheetResult(result);
    }
}
