using System.ComponentModel;
using System.Text.Json;
using HaloPsaMcp.Modules.HaloPsa.Services;
using ModelContextProtocol.Server;

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// MCP tools that expose the offline HaloPSA database catalog
/// (schema/catalog.json) to the LLM. The flow is meant to be cheap drill-down:
/// domains → tables → columns. Avoid asking for everything at once — the
/// catalog is 3 MB and FAULTS alone has 625 columns.
/// </summary>
internal partial class HaloPsaMcpTools {
    private const int DefaultTableLimit = 25;
    private const int DefaultSearchLimit = 30;
    private const int DefaultColumnLimit = 80;

    private static string SchemaUnavailable() =>
        JsonSerializer.Serialize(new {
            available = false,
            error = "Schema catalog not loaded. Confirm the schema/catalog.json file exists, " +
                    "or set HALOPSA_SCHEMA_PATH to its absolute path."
        }, HaloPsaMcpConstants.IndentedJsonOptions);

    [McpServerTool]
    [Description("List the 15 HaloPSA database domains and their table counts. " +
                 "Use this FIRST when planning a SQL query — pick a domain, then call halopsa_db_tables to drill in.")]
    public static string HalopsaDbDomains(SchemaCatalogService catalog) {
        if (!catalog.IsLoaded) return SchemaUnavailable();
        return JsonSerializer.Serialize(new {
            source = catalog.Source,
            dumped_at = catalog.DumpedAt,
            table_count = catalog.TableCount,
            domains = catalog.Domains()
        }, HaloPsaMcpConstants.IndentedJsonOptions);
    }

    [McpServerTool]
    [Description("List tables in a HaloPSA database domain. " +
                 "Returns name, row_count, primary key, column_count and the table's domain. " +
                 "Filter with `domain` (e.g. \"tickets\") and/or `search` (substring match on table name). " +
                 "Sorted by row_count descending. Use halopsa_db_columns next when you've picked tables.")]
    public static string HalopsaDbTables(
        SchemaCatalogService catalog,
        [Description("Optional domain filter (tickets, crm, billing, contracts, kb, projects, comms, email, auth, workflow, audit, integrations, system, assets, other)")] string? domain = null,
        [Description("Optional substring match on table name")] string? search = null,
        [Description("Max tables to return (default 25, max 200)")] int limit = DefaultTableLimit) {
        if (!catalog.IsLoaded) return SchemaUnavailable();

        var tables = catalog.TablesIn(
            string.IsNullOrEmpty(domain) ? null : domain,
            string.IsNullOrEmpty(search) ? null : search,
            Math.Clamp(limit, 1, 200));

        return JsonSerializer.Serialize(new {
            count = tables.Count,
            tables = tables.Select(t => new {
                name = t.Name,
                domain = t.Domain,
                row_count = t.RowCount,
                column_count = t.ColumnCount,
                primary_key = t.PrimaryKey,
                fk_targets = t.ForeignKeys.Select(fk => fk.RefTable).Distinct().OrderBy(s => s).ToArray()
            })
        }, HaloPsaMcpConstants.IndentedJsonOptions);
    }

    [McpServerTool]
    [Description("Get full column and foreign-key detail for one HaloPSA table. " +
                 "Returns each column's name, SQL type, nullable, identity, default — plus all FK relations and the table's domain/row_count. " +
                 "Wide tables (FAULTS has 625 cols) get the first N columns; pass `columnSearch` to narrow.")]
    public static string HalopsaDbColumns(
        SchemaCatalogService catalog,
        [Description("Table name (case-insensitive, e.g. FAULTS)")] string table,
        [Description("Optional substring match on column name")] string? columnSearch = null,
        [Description("Max columns to return (default 80, max 1000)")] int limit = DefaultColumnLimit) {
        if (!catalog.IsLoaded) return SchemaUnavailable();

        var t = catalog.GetTable(table);
        if (t == null) {
            return JsonSerializer.Serialize(new {
                found = false,
                error = $"Table '{table}' not found in catalog. Use halopsa_db_search to locate it."
            }, HaloPsaMcpConstants.IndentedJsonOptions);
        }

        IEnumerable<HaloPsa.Models.SchemaColumn> cols = t.Columns;
        if (!string.IsNullOrEmpty(columnSearch)) {
            cols = cols.Where(c => c.Name.Contains(columnSearch, StringComparison.OrdinalIgnoreCase));
        }
        var capped = Math.Clamp(limit, 1, 1000);
        var columnList = cols.Take(capped).ToList();

        return JsonSerializer.Serialize(new {
            found = true,
            name = t.Name,
            domain = t.Domain,
            row_count = t.RowCount,
            column_count = t.ColumnCount,
            primary_key = t.PrimaryKey,
            columns = columnList.Select(c => new {
                name = c.Name,
                type = c.Type,
                nullable = c.Nullable,
                identity = c.Identity,
                @default = c.Default
            }),
            columns_returned = columnList.Count,
            columns_truncated = t.ColumnCount > columnList.Count && string.IsNullOrEmpty(columnSearch),
            foreign_keys = t.ForeignKeys.Select(fk => new {
                column = fk.Column,
                ref_table = fk.RefTable,
                ref_column = fk.RefColumn,
                kind = fk.Kind
            })
        }, HaloPsaMcpConstants.IndentedJsonOptions);
    }

    [McpServerTool]
    [Description("Search the HaloPSA database catalog for tables and columns whose name contains `term`. " +
                 "Use this when you don't know which table holds a concept (e.g. term=\"satisfaction\" finds the FEEDBACK table). " +
                 "Returns up to `limit` hits, ranked by table row_count.")]
    public static string HalopsaDbSearch(
        SchemaCatalogService catalog,
        [Description("Substring to search for in table and column names (case-insensitive)")] string term,
        [Description("Max hits to return (default 30, max 200)")] int limit = DefaultSearchLimit) {
        if (!catalog.IsLoaded) return SchemaUnavailable();

        var hits = catalog.Search(term, Math.Clamp(limit, 1, 200));
        return JsonSerializer.Serialize(new {
            term,
            count = hits.Count,
            hits = hits.Select(h => new {
                table = h.Table,
                domain = h.Domain,
                row_count = h.RowCount,
                match_type = h.MatchType,
                matched_name = h.MatchedName
            })
        }, HaloPsaMcpConstants.IndentedJsonOptions);
    }
}
