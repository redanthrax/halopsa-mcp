using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class ExecuteQueryQueryHandler
{
    public static async Task<ExecuteQueryResult> Handle(
        ExecuteQueryQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        var result = await client.ExecuteQueryAsync(query.Sql).ConfigureAwait(false);
        return new ExecuteQueryResult(result.Count, result.Rows);
    }
}