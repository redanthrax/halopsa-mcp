using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Projects;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Projects;

internal static class ListProjectsHandler {
    public static async Task<ListProjectsResult> Handle(
        ListProjectsQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (query.ClientId.HasValue) {
            queryParams["client_id"] = query.ClientId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(query.Search)) {
            queryParams["search"] = query.Search;
        }

        var result = await client.GetAsync<JsonElement>("/api/Projects", queryParams).ConfigureAwait(false);
        return new ListProjectsResult(result);
    }
}