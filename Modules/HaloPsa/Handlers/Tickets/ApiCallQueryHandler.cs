using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;

internal static class ApiCallQueryHandler
{
    public static async Task<ApiCallResult> Handle(
        ApiCallQuery query,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor)
    {
        var client = factory.CreateClient(contextAccessor.HttpContext);
        object? bodyObj = null;
        if (!string.IsNullOrEmpty(query.Body))
        {
            bodyObj = JsonSerializer.Deserialize<object>(query.Body);
        }

        var result = await client.MakeApiCallAsync(query.Endpoint, query.Method, bodyObj).ConfigureAwait(false);
        return new ApiCallResult(result);
    }
}