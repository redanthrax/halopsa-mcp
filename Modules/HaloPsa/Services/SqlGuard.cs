using System.Text.RegularExpressions;

namespace HaloPsaMcp.Modules.HaloPsa.Services;

/// <summary>
/// Defence-in-depth allowlist for SQL forwarded to HaloPSA's reporting endpoint.
/// HaloPSA itself enforces tenant scoping and read-only on the report role, but
/// this MCP server should refuse anything that isn't a single SELECT/CTE+SELECT
/// before the request crosses the wire. Rejects multiple statements,
/// stored-procedure execution, file/extended procs, INSERT/UPDATE/DELETE/MERGE,
/// schema mutation, and bulk export.
/// </summary>
public static class SqlGuard {
    private const int MaxLength = 8000;

    private static readonly Regex DangerousTokens = new(
        @"(?ix)
          \b(?:
              EXEC | EXECUTE | xp_\w+ | sp_\w+
            | INSERT | UPDATE | DELETE | MERGE | TRUNCATE
            | DROP   | CREATE | ALTER  | GRANT | REVOKE | DENY
            | BACKUP | RESTORE | SHUTDOWN | RECONFIGURE
            | OPENROWSET | OPENQUERY | OPENDATASOURCE | BULK
            | INTO\s+(?:OUTFILE|DUMPFILE)
            | SELECT\s+[^\(;]*\bINTO\b
          )\b",
        RegexOptions.Compiled);

    private static readonly Regex CommentOrSemi = new(@"--|/\*|\*/|;", RegexOptions.Compiled);

    private static readonly Regex StartingKeyword = new(
        @"^\s*(?:WITH\b|SELECT\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public sealed record GuardResult(bool Ok, string? Reason);

    public static GuardResult Inspect(string? sql) {
        if (string.IsNullOrWhiteSpace(sql)) {
            return new(false, "SQL is empty.");
        }
        if (sql.Length > MaxLength) {
            return new(false, $"SQL exceeds {MaxLength} characters.");
        }
        if (!StartingKeyword.IsMatch(sql)) {
            return new(false, "Only single SELECT (or WITH … SELECT) statements are allowed.");
        }
        if (CommentOrSemi.IsMatch(sql)) {
            return new(false, "SQL must not contain comments (-- /* */) or statement terminators (;).");
        }
        var bad = DangerousTokens.Match(sql);
        if (bad.Success) {
            return new(false, $"SQL contains disallowed token '{bad.Value.Trim()}'. Only read-only SELECT queries are permitted.");
        }
        return new(true, null);
    }
}
