using System.Text.Json;

using System.Text;
using HaloPsaMcp.Modules.Common.Models;

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
        return $"{appConfig.AuthBaseUrl}/login";
    }

    internal static string AuthErrorMessage(AppConfig appConfig)
    {
        return $"NOT AUTHENTICATED. Tell the user to open this URL in their browser to log in: {GetLoginUrl(appConfig)}";
    }

    internal const string HalopsaQueryDescription =
        "PREFERRED TOOL for all HaloPSA data questions: ticket counts, filtering, aggregation, reports, " +
        "client lookups, agent stats, status breakdowns, satisfaction surveys, timesheet hours, and date-based analysis. " +
        "Executes a SQL SELECT query against the HaloPSA reporting database. " +
        "Call halopsa_get_schema first to get table names, column names, status IDs, and example queries. " +
        "IMPORTANT: All datetimes are stored in UTC. The user is in Pacific Time (UTC-8 standard / UTC-7 DST). Always convert their local date references to a UTC range for WHERE clauses (e.g. user's 'today March 4' = UTC range 2026-03-04T08:00:00Z to 2026-03-05T07:59:59Z). " +
        "DEFAULT SCOPE: Unless the user specifies otherwise, scope queries to the current calendar month in UTC. " +
        "Returns only the columns you SELECT, keeping responses compact. " +
        "Times out after 30 seconds for slow queries. " +
        "WARN: Large responses (>500KB) may exceed context window limits - use TOP with smaller numbers or aggregation. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaGetSchemaDescription =
        "Get the HaloPSA database schema, lookup IDs, and query best practices for halopsa_query. " +
        "ALWAYS call this before writing SQL queries. Returns table/column names, " +
        "status IDs, request type IDs, and example queries. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaAuthStatusDescription =
        "Check the current authentication status with HaloPSA. " +
        "ALWAYS call this first when the user asks anything about HaloPSA data. " +
        "Times out after 10 seconds if HaloPSA server is unresponsive. " +
        "If not authenticated, show the user the login URL from the response.";

    internal const string HalopsaListTicketsDescription =
        "Search HaloPSA tickets by keyword or filters. Returns summary fields only. " +
        "Use halopsa_query for counts, date filtering, or aggregation. " +
        "Use halopsa_get_ticket for full ticket detail by ID. " +
        "WARN: Large responses (>100KB) may impact context - reduce count or add filters. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaGetTicketDescription =
        "Get full details for a specific HaloPSA ticket by ID. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaCreateTicketDescription =
        "Create new ticket. Use 0 for optional fields to use defaults.";

    internal const string HalopsaUpdateTicketDescription =
        "Update existing ticket. Use 0 for optional fields to leave unchanged.";

    internal const string HalopsaGetOutcomesDescription =
        "Get valid outcome IDs for actions. Call before halopsa_add_action.";

    internal const string HalopsaAddActionDescription =
        "Add action/note to ticket. REQUIRES outcome_id. Use halopsa_get_outcomes.";

    internal const string HalopsaListActionsDescription =
        "List actions (notes/updates) for a specific HaloPSA ticket. Returns summary fields only. " +
        "WARN: Large responses (>100KB) may impact context - reduce count or narrow ticket scope. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaGetTimesheetDescription =
        "Get a HaloPSA timesheet by ID, including all TimesheetEvent entries for that day. " +
        "Returns hours worked, break time, agent info, and all events for the day. " +
        "If id=0 is returned, no timesheet record exists for that day — use halopsa_create_timesheet to create one. " +
        "To find a timesheet ID by date, use halopsa_query: " +
        "SELECT TSid, TSunum, TSdate FROM timesheet WHERE TSunum = <agent_id> AND TSdate >= '2026-03-04T00:00:00Z' AND TSdate < '2026-03-05T00:00:00Z'. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaCreateTimesheetDescription =
        "Create a new HaloPSA timesheet day record for an agent. " +
        "Use this when no timesheet record exists yet for a given date (i.e. halopsa_get_timesheet returns id=0). " +
        "agent_id is required. date must be the start of the day in UTC ISO 8601 (e.g. 2026-03-04T00:00:00Z). " +
        "start_time and/or end_time are optional UTC ISO 8601 (e.g. 2026-03-04T15:30:00Z for 7:30 AM Pacific). " +
        "Posts as [{\"date\":\"2026-03-04T00:00:00.000Z\",\"agent_id\":5,\"start_time\":\"2026-03-04T15:30:00.000Z\"}] to POST /Timesheet?utcoffset=480. " +
        "utcoffset is minutes from UTC (Pacific Standard=480, Pacific Daylight=420). " +
        "Returns the created timesheet object including the new ID. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaUpdateTimesheetDescription =
        "Update a HaloPSA timesheet day record (shift times, submit/approve). " +
        "IMPORTANT: The record must already exist. If you get error 'does not exist', use halopsa_create_timesheet first. " +
        "Fetches the current timesheet by ID, applies your changes, and posts it back. " +
        "utcoffset is the user's timezone offset in minutes from UTC (Pacific Standard = 480, Pacific Daylight = 420). " +
        "Set submit_approval=true to submit the timesheet for manager approval. " +
        "start_time and end_time must be UTC ISO 8601 (e.g. 2026-03-02T15:30:00Z for 7:30 AM Pacific). " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaUpsertTimesheetEventDescription =
        "Create or update a timesheet event (time entry) in HaloPSA. " +
        "To create: omit id or set id=0. To update: provide the existing event id. " +
        "All datetimes must be in UTC ISO 8601 format (e.g. 2026-03-05T01:00:00Z). " +
        "The user is in Pacific Time — convert their local times to UTC before calling. " +
        "ticket_id links the entry to a ticket; timetaken is hours (e.g. 0.5 = 30 min). " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaListTimesheetEventsDescription =
        "List timesheet events (time entries) for a date range. " +
        "All datetimes in HaloPSA are UTC — convert Pacific Time to UTC before passing start_date/end_date. " +
        "Returns event id, ticket_id, timetaken, start_date, end_date, note, and subject per entry. " +
        "Use this to review logged time before creating or updating entries. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

    internal const string HalopsaDeleteTimesheetEventDescription =
        "Delete a timesheet event (time entry) by ID. " +
        "Use halopsa_list_timesheet_events or halopsa_get_timesheet to find event IDs first. " +
        "If the result says NOT AUTHENTICATED, show the user the login URL from the response.";

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