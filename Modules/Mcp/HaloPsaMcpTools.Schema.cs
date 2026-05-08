using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Queries.Contracts;
using HaloPsaMcp.Modules.HaloPsa.Queries.Tickets;
using HaloPsaMcp.Modules.HaloPsa.Services;
using ModelContextProtocol.Server;
using Wolverine;

namespace HaloPsaMcp.Modules.Mcp;

/// <summary>
/// MCP tools that expose the offline HaloPSA database catalog
/// (schema/catalog.json) to the MCP client. The flow is meant to be cheap drill-down:
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

    private static readonly string[] s_validErdDomains = [
        "tickets", "crm", "assets", "billing", "contracts", "kb", "projects",
        "comms", "email", "auth", "workflow", "audit", "integrations", "system", "other"
    ];

    private static string? ResolveSchemaFile(string relative) {
        var candidates = new[] {
            Path.Combine(AppContext.BaseDirectory, "schema", relative),
            Path.Combine(Directory.GetCurrentDirectory(), "schema", relative)
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    [McpServerTool]
    [Description("Read the canonical HaloPSA reference doc (schema/reference.md). " +
                 "Returns key entity descriptions, common joins, and per-domain table notes — the authoritative high-level guide. " +
                 "Call this BEFORE writing complex SQL involving joins across clients/sites/agents/tickets, or when halopsa_get_schema's cheat sheet leaves a question open.")]
    public static string HalopsaDbReference() {
        var path = ResolveSchemaFile("reference.md");
        if (path == null) {
            return JsonSerializer.Serialize(new {
                available = false,
                error = "schema/reference.md not found beside the binary."
            }, HaloPsaMcpConstants.IndentedJsonOptions);
        }
        var text = File.ReadAllText(path);
        return JsonSerializer.Serialize(new {
            available = true,
            path = "schema/reference.md",
            length = text.Length,
            content = text
        }, HaloPsaMcpConstants.IndentedJsonOptions);
    }

    [McpServerTool]
    [Description("Read a HaloPSA Mermaid ERD for a domain (schema/erd/<domain>.md). " +
                 "Use to get a visual map of tables and their FK relationships within a domain before joining across many tables. " +
                 "Valid domains: tickets, crm, assets, billing, contracts, kb, projects, comms, email, auth, workflow, audit, integrations, system, other.")]
    public static string HalopsaDbErd(
        [Description("Domain name (lowercase) — see description for valid values")] string domain) {
        var d = (domain ?? "").Trim().ToLowerInvariant();
        if (!s_validErdDomains.Contains(d)) {
            return JsonSerializer.Serialize(new {
                available = false,
                error = $"Unknown domain '{domain}'. Valid: {string.Join(", ", s_validErdDomains)}"
            }, HaloPsaMcpConstants.IndentedJsonOptions);
        }
        var path = ResolveSchemaFile(Path.Combine("erd", $"{d}.md"));
        if (path == null) {
            return JsonSerializer.Serialize(new {
                available = false,
                error = $"schema/erd/{d}.md not found beside the binary."
            }, HaloPsaMcpConstants.IndentedJsonOptions);
        }
        var text = File.ReadAllText(path);
        return JsonSerializer.Serialize(new {
            available = true,
            domain = d,
            path = $"schema/erd/{d}.md",
            length = text.Length,
            content = text
        }, HaloPsaMcpConstants.IndentedJsonOptions);
    }

    [McpServerTool]
    [Description("Per-client contract/agreement hours utilisation for a UTC date range. " +
                 "Aggregates ACTIONS.ActionChargeHours (the hours actually charged against an agreement) " +
                 "joined to FAULTS->AREA for client identity. Filters ActionContractID < 0 (HaloPSA stores the " +
                 "linked contract ID as a NEGATIVE integer; ABS(ActionContractID) gives the real contract id). " +
                 "Cross-references /api/ClientContract for entitlement (numberofunitsfree). " +
                 "Returns one row per (client, contract) with charge_hours, contracted_hours, overage_hours, over_budget. " +
                 "Defaults to the current UTC calendar month. " +
                 "DO NOT use FAULTS.Elapsedhrs for agreement utilisation — it includes Projects, Alerts, and non-billable T&M time " +
                 "and produces wildly inflated numbers vs. what is actually charged to the agreement.")]
    public static async Task<string> HalopsaGetContractUtilisation(
        AppConfig appConfig,
        IMessageBus bus,
        [Description("UTC ISO 8601 start (inclusive). Defaults to first of current UTC month.")] string? startDate = null,
        [Description("UTC ISO 8601 end (exclusive). Defaults to first of next UTC month.")] string? endDate = null,
        [Description("Optional client_id filter (0 = all clients with activity)")] int clientId = 0,
        [Description("Max client rows to return (1-500)")] int limit = 100) {
        try {
            var nowUtc = DateTime.UtcNow;
            var defaultStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var defaultEnd = defaultStart.AddMonths(1);
            var start = string.IsNullOrEmpty(startDate)
                ? defaultStart.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                : startDate;
            var end = string.IsNullOrEmpty(endDate)
                ? defaultEnd.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                : endDate;
            var rowLimit = Math.Clamp(limit, 1, 500);
            var clientFilter = clientId > 0 ? $" AND f.Areaint = {clientId}" : "";

            var sql = $@"SELECT TOP {rowLimit}
    f.Areaint                       AS client_id,
    a.aareadesc                     AS client_name,
    ABS(ac.ActionContractID)        AS contract_id,
    ROUND(SUM(ac.ActionChargeHours), 2) AS charge_hours,
    COUNT(DISTINCT ac.Faultid)      AS ticket_count,
    COUNT(*)                        AS action_count
FROM ACTIONS ac
INNER JOIN FAULTS f ON ac.Faultid = f.Faultid
INNER JOIN AREA   a ON a.Aarea     = f.Areaint
WHERE ac.Whe_ >= '{start}'
  AND ac.Whe_ <  '{end}'
  AND ac.ActionContractID < 0
  AND ac.ActionChargeHours > 0
  AND f.FDeleted = 'False'{clientFilter}
GROUP BY f.Areaint, a.aareadesc, ac.ActionContractID
ORDER BY charge_hours DESC";

            var hoursTask = bus.InvokeAsync<ExecuteQueryResult>(new ExecuteQueryQuery(sql));
            var contractsTask = bus.InvokeAsync<ListContractsResult>(
                new ListContractsQuery(100, clientId > 0 ? clientId : null, null));

            await Task.WhenAll(hoursTask, contractsTask).ConfigureAwait(false);

            var hoursResult = await hoursTask.ConfigureAwait(false);
            var contractsResult = await contractsTask.ConfigureAwait(false);

            // Build entitlement lookups: by contract_id (preferred) and by client_id (fallback).
            var entitlementByContract = new Dictionary<int, double>();
            var entitlementByClient = new Dictionary<int, double>();
            if (contractsResult?.Data.ValueKind == JsonValueKind.Array) {
                foreach (var c in contractsResult.Data.EnumerateArray()) {
                    var hrs = 0.0;
                    if (c.TryGetProperty("numberofunitsfree", out var u) && u.ValueKind == JsonValueKind.Number) {
                        hrs = u.GetDouble();
                    }
                    if (c.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number) {
                        entitlementByContract[idEl.GetInt32()] = hrs;
                    }
                    if (c.TryGetProperty("client_id", out var cid) && cid.ValueKind == JsonValueKind.Number) {
                        var id = cid.GetInt32();
                        entitlementByClient[id] = entitlementByClient.TryGetValue(id, out var prev) ? prev + hrs : hrs;
                    }
                }
            }

            var rows = new List<object>();
            if (hoursResult?.Rows != null) {
                foreach (var row in hoursResult.Rows) {
                    int cid = row.TryGetValue("client_id", out var cidVal) && cidVal != null
                        ? Convert.ToInt32(cidVal, CultureInfo.InvariantCulture) : 0;
                    int contractId = row.TryGetValue("contract_id", out var ctrVal) && ctrVal != null
                        ? Convert.ToInt32(ctrVal, CultureInfo.InvariantCulture) : 0;
                    string name = row.TryGetValue("client_name", out var nVal) ? nVal?.ToString() ?? "" : "";
                    double charged = row.TryGetValue("charge_hours", out var hVal) && hVal != null
                        ? Convert.ToDouble(hVal, CultureInfo.InvariantCulture) : 0;
                    int tickets = row.TryGetValue("ticket_count", out var tVal) && tVal != null
                        ? Convert.ToInt32(tVal, CultureInfo.InvariantCulture) : 0;
                    int actions = row.TryGetValue("action_count", out var aVal) && aVal != null
                        ? Convert.ToInt32(aVal, CultureInfo.InvariantCulture) : 0;

                    double? entitled = null;
                    if (entitlementByContract.TryGetValue(contractId, out var ec)) entitled = ec;
                    else if (entitlementByClient.TryGetValue(cid, out var eb)) entitled = eb;

                    var overage = entitled.HasValue ? Math.Max(0, charged - entitled.Value) : (double?)null;
                    rows.Add(new {
                        client_id = cid,
                        client_name = name,
                        contract_id = contractId,
                        charge_hours = Math.Round(charged, 2),
                        contracted_hours = entitled.HasValue ? Math.Round(entitled.Value, 2) : (double?)null,
                        overage_hours = overage.HasValue ? Math.Round(overage.Value, 2) : (double?)null,
                        over_budget = overage.HasValue && overage.Value > 0,
                        ticket_count = tickets,
                        action_count = actions,
                        has_contract = entitled.HasValue
                    });
                }
            }

            return JsonSerializer.Serialize(new {
                period_start_utc = start,
                period_end_utc = end,
                source = "ACTIONS.ActionChargeHours WHERE ActionContractID < 0 (negative = linked agreement)",
                row_count = rows.Count,
                over_budget_count = rows.Count(r => (bool)r.GetType().GetProperty("over_budget")!.GetValue(r)!),
                rows
            }, HaloPsaMcpConstants.IndentedJsonOptions);
        } catch (UnauthorizedAccessException) {
            return HaloPsaMcpConstants.AuthErrorMessage(appConfig);
        } catch (Exception ex) {
            return JsonSerializer.Serialize(new {
                error = $"Contract utilisation failed: {ex.Message}",
                login_url = HaloPsaMcpConstants.GetLoginUrl(appConfig)
            }, HaloPsaMcpConstants.IndentedJsonOptions);
        }
    }
}
