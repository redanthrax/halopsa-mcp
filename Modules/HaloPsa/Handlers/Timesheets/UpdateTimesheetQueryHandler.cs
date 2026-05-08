using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Timesheets;

public static class UpdateTimesheetQueryHandler {
    public static async Task<UpdateTimesheetResult> Handle(
        UpdateTimesheetQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);

        var current = await client.GetAsync<JsonElement>($"/api/Timesheet/{query.Id}", null).ConfigureAwait(false);
        if (current.TryGetProperty("id", out var idProp) && idProp.GetInt32() == 0) {
            return new UpdateTimesheetResult(Exists: false);
        }

        var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(current.GetRawText()) ?? [];

        if (query.StartTime != null) doc["start_time"] = JsonSerializer.Deserialize<JsonElement>($"\"{query.StartTime}\"");
        if (query.EndTime != null) doc["end_time"] = JsonSerializer.Deserialize<JsonElement>($"\"{query.EndTime}\"");
        if (query.SubmitApproval) doc["_submitapproval"] = JsonSerializer.Deserialize<JsonElement>("true");
        if (query.Approve) doc["_approve"] = JsonSerializer.Deserialize<JsonElement>("true");
        if (query.Reject) doc["_reject"] = JsonSerializer.Deserialize<JsonElement>("true");
        if (query.RevertApproval) doc["_revertapproval"] = JsonSerializer.Deserialize<JsonElement>("true");

        var payload = new[] { doc };
        var queryParams = new Dictionary<string, string> {
            ["utcoffset"] = query.UtcOffset.ToString(CultureInfo.InvariantCulture)
        };
        await client.PostAsync<JsonElement>("/api/Timesheet", payload, queryParams).ConfigureAwait(false);

        return new UpdateTimesheetResult(Exists: true);
    }
}
