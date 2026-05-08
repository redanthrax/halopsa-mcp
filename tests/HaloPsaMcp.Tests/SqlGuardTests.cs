using HaloPsaMcp.Modules.HaloPsa.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

public class SqlGuardTests {
    [Theory]
    [InlineData("SELECT TOP 10 id FROM tickets")]
    [InlineData("  select id from tickets where status = 1")]
    [InlineData("WITH cte AS (SELECT id FROM tickets) SELECT * FROM cte")]
    public void Allows_simple_read_only_queries(string sql) {
        var r = SqlGuard.Inspect(sql);
        Assert.True(r.Ok, r.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_empty(string? sql) {
        Assert.False(SqlGuard.Inspect(sql).Ok);
    }

    [Theory]
    [InlineData("UPDATE tickets SET status=2")]
    [InlineData("DELETE FROM tickets")]
    [InlineData("INSERT INTO tickets (id) VALUES (1)")]
    [InlineData("MERGE tickets USING staging ON 1=1 WHEN MATCHED THEN UPDATE SET status=1")]
    [InlineData("DROP TABLE tickets")]
    [InlineData("CREATE TABLE foo (id int)")]
    [InlineData("ALTER TABLE tickets ADD col int")]
    [InlineData("TRUNCATE TABLE tickets")]
    [InlineData("GRANT SELECT ON tickets TO PUBLIC")]
    [InlineData("EXEC sp_who")]
    [InlineData("EXECUTE xp_cmdshell 'dir'")]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI','x','SELECT 1')")]
    [InlineData("SELECT * INTO newtbl FROM tickets")]
    [InlineData("BACKUP DATABASE foo TO DISK='x'")]
    [InlineData("SHUTDOWN")]
    public void Rejects_dangerous_keywords(string sql) {
        var r = SqlGuard.Inspect(sql);
        Assert.False(r.Ok);
        Assert.NotNull(r.Reason);
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE tickets")]
    [InlineData("SELECT 1 -- comment")]
    [InlineData("SELECT /* hidden */ 1")]
    public void Rejects_comments_and_semicolons(string sql) {
        Assert.False(SqlGuard.Inspect(sql).Ok);
    }

    [Fact]
    public void Rejects_non_select_start() {
        Assert.False(SqlGuard.Inspect("DECLARE @x int; SELECT @x").Ok);
    }

    [Fact]
    public void Rejects_oversize() {
        var sql = "SELECT '" + new string('a', 9000) + "'";
        Assert.False(SqlGuard.Inspect(sql).Ok);
    }
}
