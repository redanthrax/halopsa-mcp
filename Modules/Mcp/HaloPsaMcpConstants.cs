using System.Text.Json;

using System.Text;
using System.Text.RegularExpressions;
using HaloPsaMcp.Modules.Common.Models;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.Mcp;

internal static class HaloPsaMcpConstants
{
    internal static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    // CompositeFormats for performance
    internal static readonly CompositeFormat LargeResponseWarningFormat = CompositeFormat.Parse(LargeResponseWarningTemplate);
    internal static readonly CompositeFormat MediumResponseWarningFormat = CompositeFormat.Parse(MediumResponseWarningTemplate);
    internal static readonly CompositeFormat QueryResultFormat = CompositeFormat.Parse(QueryResultTemplate);
    internal static readonly CompositeFormat QueryFailedFormat = CompositeFormat.Parse(QueryFailedTemplate);
    internal static readonly CompositeFormat SchemaFormat = CompositeFormat.Parse(SchemaTemplate);
    internal static readonly CompositeFormat AuthNetworkErrorFormat = CompositeFormat.Parse(AuthNetworkErrorTemplate);
    internal static readonly CompositeFormat AuthFailedFormat = CompositeFormat.Parse(AuthFailedTemplate);
    internal static readonly CompositeFormat ListLargeResponseWarningFormat = CompositeFormat.Parse(ListLargeResponseWarningTemplate);
    internal static readonly CompositeFormat ActionsLargeResponseWarningFormat = CompositeFormat.Parse(ActionsLargeResponseWarningTemplate);
    internal static readonly string[] TicketSummaryFields = [
        "id", "faultid", "summary", "symptom", "details",
        "status_id", "status", "priority_id", "priority",
        "client_id", "client_name", "site_id", "site_name",
        "agent_id", "agent", "user_id", "user_name",
        "requesttype", "category_1", "category_2", "category_3",
        "dateoccurred", "datecreated", "datelogged", "datecleared",
        "sla_id", "team"
    ];

    internal static readonly string[] ActionSummaryFields = [
        "id", "faultid", "actoutcome", "who", "whe_", "whodid",
        "note", "emailfrom", "emailto", "timetaken",
        "outcome", "actiontype", "isimportant"
    ];

    internal static readonly string[] TicketDetailFields = [
        "id", "faultid", "summary", "symptom", "details",
        "status_id", "status", "priority_id", "priority",
        "client_id", "client_name", "site_id", "site_name",
        "agent_id", "agent", "user_id", "user_name",
        "requesttype", "category_1", "category_2", "category_3",
        "dateoccurred", "datecreated", "datelogged", "datecleared",
        "sla_id", "team", "impact", "urgency",
        "deadlinedate", "startdate", "targetdate",
        "responsetargetmet", "fixbytargetmet",
        "contract_id", "project_id", "asset_id",
        "attachments", "customfields"
    ];

    internal static readonly string[] OutcomeSummaryFields = [
        "id", "name", "colour", "timetaken", "isdefault"
    ];

    internal static string GetLoginUrl(AppConfig appConfig)
    {
        return $"{appConfig.PublicBaseUrl}/login";
    }

    private static readonly Regex InvalidColumnRegex = new(
        @"Invalid column name '([^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InvalidObjectRegex = new(
        @"Invalid object name '([^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FromTableRegex = new(
        @"\b(?:FROM|JOIN|UPDATE|INTO)\s+\[?([A-Za-z_][A-Za-z0-9_]*)\]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// On a SQL failure, parse "Invalid column name 'X'" / "Invalid object name 'X'"
    /// out of the error message and return a fuzzy-matched suggestion using the
    /// in-memory schema catalog. Collapses the typical 3-call probe loop into
    /// a single retry by giving the agent the correct identifier verbatim.
    /// </summary>
    internal static string BuildColumnHint(string errorMessage, string sql, SchemaCatalogService catalog)
    {
        if (string.IsNullOrEmpty(errorMessage) || !catalog.IsLoaded) return string.Empty;

        var objMatch = InvalidObjectRegex.Match(errorMessage);
        if (objMatch.Success) {
            var bad = objMatch.Groups[1].Value;
            var hits = catalog.Search(bad, 8);
            var tableHits = hits.Where(h => h.MatchType == "table").Select(h => h.Table).Distinct().Take(5).ToList();
            if (tableHits.Count > 0) {
                return $"\ndid_you_mean_table={string.Join(",", tableHits)}\nhint=Call halopsa_db_search('{bad}') or halopsa_db_tables(search='{bad}') to find the right table.";
            }
            return $"\nhint=Table '{bad}' not in catalog. Call halopsa_db_search('{bad}') to find similar names.";
        }

        var colMatch = InvalidColumnRegex.Match(errorMessage);
        if (!colMatch.Success) return string.Empty;

        var badCol = colMatch.Groups[1].Value;
        var tablesInQuery = FromTableRegex.Matches(sql)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var suggestions = new List<string>();
        var tablesChecked = new List<string>();
        foreach (var tableName in tablesInQuery) {
            var table = catalog.GetTable(tableName);
            if (table == null) continue;
            tablesChecked.Add(table.Name);
            foreach (var col in table.Columns) {
                if (FuzzyMatch(col.Name, badCol)) {
                    suggestions.Add($"{table.Name}.{col.Name}");
                }
            }
        }

        if (suggestions.Count == 0) {
            // Fall back to global search across the catalog.
            var hits = catalog.Search(badCol, 5);
            suggestions.AddRange(hits
                .Where(h => h.MatchType == "column")
                .Select(h => $"{h.Table}.{h.MatchedName}"));
        }

        if (suggestions.Count == 0) {
            var checkedHint = tablesChecked.Count > 0 ? $" Checked tables: {string.Join(",", tablesChecked)}." : "";
            return $"\nhint=Column '{badCol}' not found.{checkedHint} Call halopsa_db_columns('<table>') to list the real columns, or halopsa_db_search('{badCol}') to find it elsewhere.";
        }

        return $"\ndid_you_mean={string.Join(",", suggestions.Take(5))}\nhint=Use one of the suggested columns or call halopsa_db_columns('<table>') to see the full list.";
    }

    /// <summary>
    /// Loose case-insensitive match: equal, prefix, suffix, or contained.
    /// Catches typical guess errors like fstatus->Status, FAreaint->Areaint,
    /// AssignedTo->Assignedtoint, occured->dateoccured.
    /// </summary>
    private static bool FuzzyMatch(string actual, string guess)
    {
        if (string.Equals(actual, guess, StringComparison.OrdinalIgnoreCase)) return true;
        if (actual.Contains(guess, StringComparison.OrdinalIgnoreCase)) return true;
        if (guess.Contains(actual, StringComparison.OrdinalIgnoreCase)) return true;
        // Strip a single leading char (commonly the F/U/A prefix the agent guessed)
        // and re-check against the actual column name, so 'fstatus' matches 'Status'.
        if (guess.Length > 2 && actual.Equals(guess[1..], StringComparison.OrdinalIgnoreCase)) return true;
        if (actual.Length > 2 && guess.Equals(actual[1..], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static string AuthErrorMessage(AppConfig appConfig)
    {
        // Return markdown-formatted text with a clickable link to ensure LLMs render it properly,
        // as plain URLs may be treated as untrusted and suppressed.
        var url = GetLoginUrl(appConfig);
        return $"HaloPSA session is not authenticated. [Sign in here]({url})";
    }

    private const string AuthNotice =
        "If a tool result includes login_url with authenticated=false, surface that URL to the user as the sign-in link.";

    internal const string HalopsaQueryDescription = $"""
        PREFERRED TOOL for all HaloPSA data questions: ticket counts, filtering, aggregation, reports, client lookups, agent stats, status breakdowns, satisfaction surveys, timesheet hours, and date-based analysis. Executes a SQL SELECT query against the HaloPSA reporting database.

        REQUIRED: if you do not already have the exact column name from THIS conversation, call halopsa_get_schema (cheat sheet + status IDs) or halopsa_db_columns(table) BEFORE writing SQL. Do NOT guess column names from memory — HaloPSA columns use unusual casing (e.g. Status, FDeleted, Areaint, Assignedtoint, dateoccured) and a wrong guess wastes a round trip.

        QUICK FACTS for the FAULTS (tickets) table: open tickets filter = `FDeleted='False' AND Status NOT IN (8,9)`; closed = Status=9; cancelled = Status=8; client FK = Areaint -> AREA.Aarea; site FK = sitenumber -> SITE.Ssitenum; assigned agent = Assignedtoint -> UNAME.Unum; created date = datecreated; closed date = datecleared (NOT Closeddate); request type = RequestTypeNew -> REQUESTTYPE.RTid (NEVER use the legacy `Requesttype` column — it is unreliably populated). For projects, join REQUESTTYPE on RTIsProject = 1; for opportunities, RTIsOpportunity = 1.

        WORKFLOW for unfamiliar fields: when you don't know which table or column holds a concept, call halopsa_db_search BEFORE writing information_schema queries (e.g. db_search('elapsed'), db_search('contract hours')). For canonical joins or cross-domain mapping, call halopsa_db_reference. For visual ERD of one domain, call halopsa_db_erd.

        FOR PER-CLIENT BILLABLE HOURS / CONTRACT UTILISATION: do NOT hand-write SQL — call halopsa_get_contract_utilisation, which collapses the workflow into one tool call.

        IMPORTANT: All datetimes are stored in UTC. The user is in Pacific Time (UTC-8 standard / UTC-7 DST). Always convert their local date references to a UTC range for WHERE clauses (e.g. user's 'today March 4' = UTC range 2026-03-04T08:00:00Z to 2026-03-05T07:59:59Z).

        DEFAULT SCOPE: Unless the user specifies otherwise, scope queries to the current calendar month in UTC.

        Returns only the columns you SELECT, keeping responses compact. Times out after 30 seconds for slow queries.

        WARN: Large responses (>500KB) may exceed context window limits — use TOP with smaller numbers or aggregation.

        {AuthNotice}
        """;

    internal const string HalopsaGetSchemaDescription = $"""
        Get the HaloPSA database schema cheat sheet plus live lookup IDs (statuses, agents). ALWAYS call this before writing SQL. Returns key tables, common joins, and example queries.

        For deeper schema browsing — full column lists, foreign keys, or finding tables by keyword — use the catalog tools: halopsa_db_domains, halopsa_db_tables, halopsa_db_columns, halopsa_db_search.

        For the canonical reference doc (key entities, common joins, per-domain notes) call halopsa_db_reference. For per-domain Mermaid ERDs call halopsa_db_erd.

        {AuthNotice}
        """;

    internal const string HalopsaAuthStatusDescription = $"""
        Check the current authentication status with HaloPSA. ALWAYS call this first when the user asks anything about HaloPSA data. Times out after 10 seconds if HaloPSA server is unresponsive.

        {AuthNotice}
        """;

    internal const string HalopsaListTicketsDescription = $"""
        Search HaloPSA tickets by keyword or filters. Returns summary fields only.

        Use halopsa_query for counts, date filtering, or aggregation. Use halopsa_get_ticket for full ticket detail by ID.

        WARN: Large responses (>100KB) may impact context — reduce count or add filters.

        {AuthNotice}
        """;

    internal const string HalopsaGetTicketDescription = $"""
        Get full details for a specific HaloPSA ticket by ID.

        {AuthNotice}
        """;

    internal const string HalopsaCreateTicketDescription =
        "Create new ticket. Use 0 for optional fields to use defaults.";

    internal const string HalopsaUpdateTicketDescription =
        "Update existing ticket. Use 0 for optional fields to leave unchanged.";

    internal const string HalopsaGetOutcomesDescription =
        "Get valid outcome IDs for actions. Call before halopsa_add_action.";

    internal const string HalopsaAddActionDescription =
        "Add action/note to ticket. REQUIRES outcome_id. Use halopsa_get_outcomes.";

    internal const string HalopsaListActionsDescription = $"""
        List actions (notes/updates) for a specific HaloPSA ticket. Returns summary fields only.

        WARN: Large responses (>100KB) may impact context — reduce count or narrow ticket scope.

        {AuthNotice}
        """;

    internal const string HalopsaGetTimesheetDescription = $"""
        Get a HaloPSA timesheet by ID, including all TimesheetEvent entries for that day. Returns hours worked, break time, agent info, and all events for the day.

        If id=0 is returned, no timesheet record exists for that day — use halopsa_create_timesheet to create one.

        To find a timesheet ID by date, use halopsa_query:
        SELECT TSid, TSunum, TSdate FROM timesheet WHERE TSunum = <agent_id> AND TSdate >= '2026-03-04T00:00:00Z' AND TSdate < '2026-03-05T00:00:00Z'

        {AuthNotice}
        """;

    internal const string HalopsaCreateTimesheetDescription = $$"""
        Create a new HaloPSA timesheet day record for an agent. Use this when no timesheet record exists yet for a given date (i.e. halopsa_get_timesheet returns id=0).

        agent_id is required. date must be the start of the day in UTC ISO 8601 (e.g. 2026-03-04T00:00:00Z). start_time and/or end_time are optional UTC ISO 8601 (e.g. 2026-03-04T15:30:00Z for 7:30 AM Pacific).

        Posts as [{"date":"2026-03-04T00:00:00.000Z","agent_id":5,"start_time":"2026-03-04T15:30:00.000Z"}] to POST /Timesheet?utcoffset=480. utcoffset is minutes from UTC (Pacific Standard=480, Pacific Daylight=420).

        Returns the created timesheet object including the new ID.

        {{AuthNotice}}
        """;

    internal const string HalopsaUpdateTimesheetDescription = $"""
        Update a HaloPSA timesheet day record (shift times, submit/approve).

        IMPORTANT: The record must already exist. If you get error 'does not exist', use halopsa_create_timesheet first.

        Fetches the current timesheet by ID, applies your changes, and posts it back. utcoffset is the user's timezone offset in minutes from UTC (Pacific Standard = 480, Pacific Daylight = 420). Set submit_approval=true to submit the timesheet for manager approval. start_time and end_time must be UTC ISO 8601 (e.g. 2026-03-02T15:30:00Z for 7:30 AM Pacific).

        {AuthNotice}
        """;

    internal const string HalopsaUpsertTimesheetEventDescription = $"""
        Create or update a timesheet event (time entry) in HaloPSA.

        To create: omit id or set id=0. To update: provide the existing event id.

        All datetimes must be in UTC ISO 8601 format (e.g. 2026-03-05T01:00:00Z). The user is in Pacific Time — convert their local times to UTC before calling. ticket_id links the entry to a ticket; timetaken is hours (e.g. 0.5 = 30 min).

        {AuthNotice}
        """;

    internal const string HalopsaListTimesheetEventsDescription = $"""
        List timesheet events (time entries) for a date range.

        All datetimes in HaloPSA are UTC — convert Pacific Time to UTC before passing start_date/end_date.

        Returns event id, ticket_id, timetaken, start_date, end_date, note, and subject per entry. Use this to review logged time before creating or updating entries.

        {AuthNotice}
        """;

    internal const string HalopsaDeleteTimesheetEventDescription = $"""
        Delete a timesheet event (time entry) by ID. Use halopsa_list_timesheet_events or halopsa_get_timesheet to find event IDs first.

        {AuthNotice}
        """;

    internal static readonly string[] TimesheetEventSummaryFields = [
        "id", "event_type", "subject", "start_date", "end_date",
        "timetaken", "agent_id", "ticket_id", "note", "customer",
        "site_id", "user_name", "charge_rate", "client_id",
        "category1", "category2", "category3", "traveltime"
    ];

    internal static readonly string[] TimesheetSummaryFields = [
        "id", "agent_id", "agent_name", "date", "start_time", "end_time",
        "target_hours", "actual_hours", "break_hours", "work_hours",
        "chargeable_hours", "unlogged_hours", "events"
    ];

    // Error messages and responses
    internal const string QueryZeroRowsMessage = "Query returned 0 rows.\nRaw API response (for debugging):\n";
    internal const string QueryResultTemplate = "Query returned {0}:{1}\n{2}";
    internal const string QueryTimeoutMessage = "Query timed out after 30 seconds. Try simplifying your query or " +
                                                "reducing the date range.";
    internal const string QueryFailedTemplate = "Query failed: {0}";
    internal const string LargeResponseWarningTemplate = "WARNING: Large response ({0:F1}KB) may exceed context window limits. " +
                                                         "Consider using TOP with a smaller number, adding more specific WHERE conditions, " +
                                                         "or focusing on aggregated results (COUNT, SUM, AVG) instead of " +
                                                         "detailed rows.";
    internal const string MediumResponseWarningTemplate = "Response size: {0:F1}KB. " +
                                                          "For very large datasets, consider aggregation queries or " +
                                                          "smaller TOP limits.";
    internal const string ListLargeResponseWarningTemplate = "WARNING: Large response ({0:F1}KB). " +
                                                              "Consider reducing count or adding filters.";
    internal const string ActionsLargeResponseWarningTemplate = "WARNING: Large response ({0:F1}KB). " +
                                                               "Consider reducing count or narrowing the ticket scope.";
    internal const string UnknownAgentName = "Unknown";

    // Auth status property names
    internal const string AuthenticatedProperty = "authenticated";
    internal const string AgentNameProperty = "agent_name";
    internal const string AgentEmailProperty = "agent_email";
    internal const string MessageProperty = "message";
    internal const string ErrorProperty = "error";
    internal const string LoginUrlProperty = "login_url";

    // Auth status messages
    internal const string AuthenticatedMessage = "Authenticated. All tools available.";
    internal const string AuthTimeoutError = "Authentication check timed out after 10 seconds. " +
                                             "HaloPSA server may be unresponsive.";
    internal const string AuthNetworkErrorTemplate = "Network error during authentication: {0}";
    internal const string AuthFailedTemplate = "Authentication check failed: {0}";

    // Schema template
    internal const string SchemaTemplate = @"# HaloPSA Reporting Database Schema and Best Practices

## Important Notes
{0}

## Status IDs
{1}

## Agent IDs
{2}

## Common Tables
{3}

## Example Queries
{4}";
}