using System.Net;
using System.Text;
using HaloPsaMcp.Modules.Authentication.Services;
using HaloPsaMcp.Modules.HaloPsa.Handlers.Tickets;
using Microsoft.AspNetCore.WebUtilities;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Tests.TestDoubles;
using Xunit;

namespace HaloPsaMcp.Tests;

[Collection("TokenStoreRuntime")]
public class ListTicketsQueryHandlerTests {
    [Fact]
    public async Task Builds_query_string_from_filters() {
        var handler = new StubHttpHandler {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            }
        };
        await using var fixture = await HaloPsaTestHelpers.Fixture.CreateAsync(handler);

        await ListTicketsQueryHandler.Handle(
            new ListTicketsQuery(
                Count: 200,
                Status: 3,
                ClientId: 42,
                AgentId: 7,
                Search: "printer"),
            fixture.Factory,
            fixture.ContextAccessor);

        Assert.NotNull(handler.LastRequest);
        var query = QueryHelpers.ParseQuery(handler.LastRequest!.RequestUri!.Query);

        Assert.Equal("100", query["count"].ToString());
        Assert.Equal("3", query["status"].ToString());
        Assert.Equal("42", query["client_id"].ToString());
        Assert.Equal("7", query["agent_id"].ToString());
        Assert.Equal("printer", query["search"].ToString());
        Assert.Contains("/api/Tickets", handler.LastRequest.RequestUri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Throws_when_no_authenticated_session() {
        using var _ = TokenStoreRuntimeTestReset.Scope.WithDefaultFallbackDisabled();
        await using var fixture = await HaloPsaTestHelpers.Fixture.CreateAsync(new StubHttpHandler());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            ListTicketsQueryHandler.Handle(
                new ListTicketsQuery(),
                fixture.Factory,
                fixture.ContextAccessor));
    }
}
