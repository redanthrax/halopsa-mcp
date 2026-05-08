using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

public static class ExecuteQueryQueryHandler
{
    public static async Task<ExecuteQueryResult> Handle(
        ExecuteQueryQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var guard = SqlGuard.Inspect(query.Sql);
        if (!guard.Ok) {
            throw new InvalidOperationException($"SQL rejected by guard: {guard.Reason}");
        }
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);
        var result = await client.ExecuteQueryAsync(query.Sql).ConfigureAwait(false);
        return new ExecuteQueryResult(result.Count, result.Rows, result.RawResponse);
    }
}