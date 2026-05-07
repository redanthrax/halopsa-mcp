using System.Text.Json;
using System.Text.Json.Serialization;
using HaloPsaMcp.Modules.HaloPsa.Models;

namespace HaloPsaMcp.Modules.HaloPsa.Services;

/// <summary>
/// Reads JSON null as 0 for non-nullable integer columns. Necessary because
/// catalog.json was generated from a source where some numeric fields are
/// "null" (e.g. tables with no row_count recorded).
/// </summary>
internal sealed class NullSafeLongConverter : JsonConverter<long> {
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? 0L : reader.GetInt64();
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

internal sealed class NullSafeIntConverter : JsonConverter<int> {
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? 0 : reader.GetInt32();
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// Loads schema/catalog.json once at startup and serves it to MCP discovery tools.
/// The catalog is the dump of HaloPSA's reporting database — 845 tables across
/// 15 domains. Lookups are in-memory and case-insensitive.
/// </summary>
internal sealed class SchemaCatalogService {
    public static readonly IReadOnlyDictionary<string, string> DomainDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        ["tickets"] = "Tickets / requests. FAULTS is the central ticket table; ACTIONS holds each update. STDREQUEST = templates, REQUESTTYPE = ticket category.",
        ["crm"] = "Clients (AREA) and their SITEs, address books, opportunities, marketing, campaigns. AREA.Aarea = client id; SITE.Ssitenum = site id.",
        ["assets"] = "Assets / configuration items. ITEM is the canonical asset table (Iid). DEVICE*, plus per-platform inventory.",
        ["billing"] = "Invoices (INVOICEHEADER + INVOICEDETAIL), taxes, budgets, charges, currencies, quotes.",
        ["contracts"] = "Recurring contracts (CONTRACTHEADER + CONTRACTDETAIL), plans, schedules, billing rules.",
        ["kb"] = "Knowledge base articles (KBENTRY), FAQs, related links.",
        ["projects"] = "Project plans, phases, milestones, risk register.",
        ["comms"] = "Appointments / calendar (APPOINTMENT), calls, chat, meetings, SMS.",
        ["email"] = "Inbound (IncomingEmail) and outbound mail handling, mailboxes, templates, message bodies.",
        ["auth"] = "Identity & access. UNAME = internal agents (Unum), USERS = end-users / contacts (Uid), NHD_IDENTITY_* = OAuth/OpenID, AgentLogin = login history.",
        ["workflow"] = "Approval processes, auto-assign, workflows, schedules, matching rules.",
        ["audit"] = "Audit trail, change history, logs, traces, events. AUDIT, AUDITFAULT, *History, *Change, PORTALLOG, Trace, EventData.",
        ["integrations"] = "Per-vendor integration tables (RMM, PSA peers, monitoring, billing, comms, social, webhooks, OAuth).",
        ["system"] = "Config, options, analyzer/report definitions, language packs, custom translations, PDF templates, theming.",
        ["other"] = "Miscellaneous tables that didn't match any domain rule."
    };

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        Converters = { new NullSafeLongConverter(), new NullSafeIntConverter() }
    };

    private readonly ILogger<SchemaCatalogService> _logger;
    private readonly Dictionary<string, SchemaTable> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SchemaTable>> _byDomain = new(StringComparer.OrdinalIgnoreCase);

    public string? Source { get; }
    public string? DumpedAt { get; }
    public int TableCount => _byName.Count;
    public bool IsLoaded => _byName.Count > 0;

    public SchemaCatalogService(ILogger<SchemaCatalogService> logger) {
        _logger = logger;

        var path = Environment.GetEnvironmentVariable("HALOPSA_SCHEMA_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "schema", "catalog.json");

        if (!File.Exists(path)) {
            // Fall back to the working directory copy (dotnet run picks this up
            // when the binary is run from the repo root).
            var fallback = Path.Combine(Directory.GetCurrentDirectory(), "schema", "catalog.json");
            if (File.Exists(fallback)) {
                path = fallback;
            } else {
                _logger.LogWarning(
                    "Schema catalog not found at {Path} (set HALOPSA_SCHEMA_PATH or copy the schema folder beside the binary). " +
                    "halopsa_db_* tools will report 'schema unavailable'.", path);
                return;
            }
        }

        try {
            using var stream = File.OpenRead(path);
            var file = JsonSerializer.Deserialize<SchemaCatalogFile>(stream, JsonOptions);
            if (file?.Tables == null) {
                _logger.LogWarning("Schema catalog at {Path} parsed empty", path);
                return;
            }

            Source = file.Source;
            DumpedAt = file.DumpedAt;

            foreach (var (name, table) in file.Tables) {
                table.Name = name;
                _byName[name] = table;
                if (!_byDomain.TryGetValue(table.Domain, out var list)) {
                    list = new List<SchemaTable>();
                    _byDomain[table.Domain] = list;
                }
                list.Add(table);
            }

            // Sort each domain's tables by row count descending (most useful first).
            foreach (var list in _byDomain.Values) {
                list.Sort((a, b) => b.RowCount.CompareTo(a.RowCount));
            }

            _logger.LogInformation(
                "Loaded schema catalog | tables={TableCount} domains={DomainCount} dumpedAt={DumpedAt}",
                _byName.Count, _byDomain.Count, DumpedAt);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to load schema catalog from {Path}", path);
        }
    }

    public IReadOnlyList<DomainSummary> Domains() =>
        _byDomain
            .Select(kvp => new DomainSummary(
                Name: kvp.Key,
                TableCount: kvp.Value.Count,
                Description: DomainDescriptions.TryGetValue(kvp.Key, out var d) ? d : ""))
            .OrderByDescending(d => d.TableCount)
            .ToList();

    public IReadOnlyList<SchemaTable> TablesIn(string? domain, string? search, int limit) {
        IEnumerable<SchemaTable> source =
            !string.IsNullOrEmpty(domain) && _byDomain.TryGetValue(domain, out var list)
                ? list
                : _byName.Values;

        if (!string.IsNullOrEmpty(search)) {
            source = source.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return source
            .OrderByDescending(t => t.RowCount)
            .Take(limit)
            .ToList();
    }

    public SchemaTable? GetTable(string name) =>
        _byName.GetValueOrDefault(name);

    public IReadOnlyList<SearchHit> Search(string term, int limit) {
        if (string.IsNullOrWhiteSpace(term) || !IsLoaded) {
            return Array.Empty<SearchHit>();
        }

        var hits = new List<SearchHit>();
        foreach (var table in _byName.Values) {
            if (table.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) {
                hits.Add(new SearchHit(table.Name, table.Domain, table.RowCount, "table", table.Name));
            }
            foreach (var col in table.Columns) {
                if (col.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) {
                    hits.Add(new SearchHit(table.Name, table.Domain, table.RowCount, "column", col.Name));
                }
            }
            if (hits.Count >= limit * 4) break;
        }

        return hits
            .OrderByDescending(h => h.RowCount)
            .Take(limit)
            .ToList();
    }
}

internal record DomainSummary(string Name, int TableCount, string Description);
internal record SearchHit(string Table, string Domain, long RowCount, string MatchType, string MatchedName);
