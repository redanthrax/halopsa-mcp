using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.KnowledgeBase;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.KnowledgeBase;

internal static class ListKbArticlesHandler {
    public static async Task<ListKbArticlesResult> Handle(
        ListKbArticlesQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var queryParams = new Dictionary<string, string> {
            ["count"] = Math.Min(query.Count, 100).ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrEmpty(query.Search)) {
            queryParams["search"] = query.Search;
        }

        var result = await client.GetAsync<JsonElement>("/api/KBArticle", queryParams).ConfigureAwait(false);
        return new ListKbArticlesResult(result);
    }
}