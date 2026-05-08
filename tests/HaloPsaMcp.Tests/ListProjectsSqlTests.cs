using HaloPsaMcp.Modules.HaloPsa.Handlers.Projects;
using HaloPsaMcp.Modules.HaloPsa.Queries.Projects;
using HaloPsaMcp.Modules.HaloPsa.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

public class ListProjectsSqlTests {
    [Fact]
    public void Default_query_uses_RTIsProject_join_not_legacy_Requesttype() {
        var sql = ListProjectsHandler.BuildSql(new ListProjectsQuery());

        Assert.Contains("rt.RTid = f.RequestTypeNew", sql, StringComparison.Ordinal);
        Assert.Contains("rt.RTIsProject = 1", sql, StringComparison.Ordinal);
        // Hard-codes the legacy `Requesttype` column would be a regression.
        Assert.DoesNotContain("f.Requesttype ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("f.Requesttype=", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestTypeNew = 5", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_query_excludes_cancelled_and_closed() {
        var sql = ListProjectsHandler.BuildSql(new ListProjectsQuery(OpenOnly: true));
        Assert.Contains("FDeleted = 'False'", sql, StringComparison.Ordinal);
        Assert.Contains("Status NOT IN (8, 9)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenOnly_false_omits_status_filter() {
        var sql = ListProjectsHandler.BuildSql(new ListProjectsQuery(OpenOnly: false));
        Assert.DoesNotContain("Status NOT IN", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_filter_inlined_when_set() {
        var sql = ListProjectsHandler.BuildSql(new ListProjectsQuery(ClientId: 42));
        Assert.Contains("f.Areaint = 42", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_term_is_escaped_for_single_quotes() {
        var sql = ListProjectsHandler.BuildSql(new ListProjectsQuery(Search: "O'Brien"));
        Assert.Contains("LIKE '%O''Brien%'", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 100)]
    [InlineData(25, 25)]
    public void Count_is_clamped_1_to_100(int requested, int expected) {
        var sql = ListProjectsHandler.BuildSql(new ListProjectsQuery(Count: requested));
        Assert.Contains($"SELECT TOP {expected} ", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_sql_passes_SqlGuard() {
        var sql = ListProjectsHandler.BuildSql(new ListProjectsQuery(
            Count: 50, ClientId: 12, Search: "rollout", OpenOnly: true));
        var r = SqlGuard.Inspect(sql);
        Assert.True(r.Ok, r.Reason);
    }
}
