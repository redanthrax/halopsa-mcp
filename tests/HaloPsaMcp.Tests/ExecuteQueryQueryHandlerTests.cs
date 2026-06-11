using System.Net;
using System.Text;
using HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;
using HaloPsaMcp.Tests.TestDoubles;
using Xunit;

namespace HaloPsaMcp.Tests;

public class ExecuteQueryQueryHandlerTests {
    [Theory]
    [InlineData("DROP TABLE tickets")]
    [InlineData("SELECT 1; DELETE FROM tickets")]
    public async Task Rejects_sql_before_calling_halo(string sql) {
        await using var fixture = await HaloPsaTestHelpers.Fixture.CreateAsync(new StubHttpHandler());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteQueryQueryHandler.Handle(
                new ExecuteQueryQuery(sql),
                fixture.Factory,
                fixture.ContextAccessor));

        Assert.StartsWith("SQL rejected by guard:", ex.Message, StringComparison.Ordinal);
        Assert.Null(fixture.Handler.LastRequest);
    }

    [Fact]
    public async Task Forwards_allowed_sql_to_report_api() {
        var handler = new StubHttpHandler {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    """[{"rows":[{"id":1}],"available_columns":[{"name":"id"}]}]""",
                    Encoding.UTF8,
                    "application/json")
            }
        };
        await using var fixture = await HaloPsaTestHelpers.Fixture.CreateAsync(handler);

        var result = await ExecuteQueryQueryHandler.Handle(
            new ExecuteQueryQuery("SELECT TOP 1 id FROM faults"),
            fixture.Factory,
            fixture.ContextAccessor);

        Assert.Equal(1, result.Count);
        Assert.Single(result.Rows);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("/api/Report", handler.LastRequest.RequestUri!.ToString(), StringComparison.Ordinal);

        Assert.Contains("_loadreportonly", handler.LastRequestBody!, StringComparison.Ordinal);
        Assert.Contains("SELECT TOP 1 id FROM faults", handler.LastRequestBody!, StringComparison.Ordinal);
    }
}
