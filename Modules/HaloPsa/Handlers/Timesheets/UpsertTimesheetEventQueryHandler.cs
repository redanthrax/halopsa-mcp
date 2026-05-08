using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Timesheets;

public static class UpsertTimesheetEventQueryHandler {
    public static async Task<UpsertTimesheetEventResult> Handle(
        UpsertTimesheetEventQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);
        var payload = new[] { query.Request };
        var result = await client.PostAsync<JsonElement>("/api/TimesheetEvent", payload).ConfigureAwait(false);

        int? eventId = null;
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0) {
            var first = result[0];
            if (first.TryGetProperty("id", out var idProp)) eventId = idProp.GetInt32();
        } else if (result.ValueKind == JsonValueKind.Object) {
            if (result.TryGetProperty("id", out var idProp)) eventId = idProp.GetInt32();
        }
        return new UpsertTimesheetEventResult(eventId);
    }
}
