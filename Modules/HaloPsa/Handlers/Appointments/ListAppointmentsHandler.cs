using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Appointments;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Appointments;

internal static class ListAppointmentsHandler {
    public static async Task<ListAppointmentsResult> Handle(
        ListAppointmentsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (query.AgentId.HasValue) {
            queryParams["agent_id"] = query.AgentId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(query.StartDate)) {
            queryParams["start_date"] = query.StartDate;
        }

        if (!string.IsNullOrEmpty(query.EndDate)) {
            queryParams["end_date"] = query.EndDate;
        }

        var result = await client.GetAsync<JsonElement>("/api/Appointment", queryParams).ConfigureAwait(false);
        return new ListAppointmentsResult(result);
    }
}