using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.KnowledgeBase;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.KnowledgeBase;

internal static class GetKbArticleHandler {
    public static async Task<GetKbArticleResult> Handle(
        GetKbArticleQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.GetAsync<JsonElement>($"/api/KBArticle/{query.Id}", null).ConfigureAwait(false);
        return new GetKbArticleResult(result);
    }
}