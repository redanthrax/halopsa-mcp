namespace HaloPsaMcp.Modules.Common.Models;

/// <summary>
/// Static class containing HaloPSA database schema information, important notes, and example queries.
/// Used by MCP tools to provide context about available tables and columns.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Types need to be public for MCP framework discovery")]
public static class HaloPsaSchema
{
    /// <summary>
    /// Important notes about working with HaloPSA data, including datetime handling, filtering, and table relationships.
    /// </summary>
    public static readonly string[] ImportantNotes = [
        "All datetimes in HaloPSA are stored in UTC",
        "The user is in Pacific Time (America/Los_Angeles, UTC-8 standard / UTC-7 daylight saving). Always convert their local date/time references to UTC before building WHERE clauses. Example: if the user says 'today' and it is March 4th in Pacific time, the UTC range to search is '2026-03-04T08:00:00Z' to '2026-03-05T07:59:59Z'",
        "Default scope: current calendar month in UTC unless user says otherwise",
        "FDeleted='False' is a STRING comparison, not integer",
        "Request type display name is in RTRequestType column (not rtdesc which is description)",
        "Requesttype column in faults is NUMERIC RTid — join with requesttype table for names",
        "Ticket type is identified by symptom field, not requesttype — use symptom LIKE '%maintenance%' for maintenance tickets",
        "Never use Fclosed column (broken). Closed tickets have Status=9",
        "Close date is 'datecleared' not 'Closeddate'",
        "Ticket occurrence date is 'dateoccured' not 'dateoccurred' (note: missing 'r')",
        "Type mismatches require CAST: CAST(f.Assignedtoint AS int) = u.Unum",
        "Invoice totals: (IHAmountDue + IHAmountPaid) because paid invoices zero out IHAmountDue",
        "Feedback table ratings: 1=best, 5=worst (lower score = better satisfaction)",
        "Table relationships: faults.faultid = actions.faultid = feedback.FBFaultID, faults.Requesttype = requesttype.RTid",
        "Use LIKE for name searches: symptom LIKE '%maintenance%' or u.uname LIKE '%john%' to allow partial matches",
        "Actions table contains all ticket updates, notes, and status changes",
        "Note: Timesheet-to-ticket relationship may be indirect (check with HaloPSA admin for specific linking column)"
    ];

    /// <summary>
    /// Dictionary of commonly used HaloPSA database tables with their descriptions and key columns.
    /// </summary>
#pragma warning disable CA2244 // Allow redundant element initialization for schema clarity
    public static readonly Dictionary<string, TableInfo> CommonTables = new()
    {
        ["faults"] = new TableInfo(
            "Tickets table",
            ["faultid", "symptom", "Status", "Assignedtoint", "sectio_", "category2", "category3", "Requesttype", "FDeleted", "dateoccured", "datecreated", "datecleared"]
        ),
        ["uname"] = new TableInfo(
            "Agents/Users table",
            ["Unum", "uname", "uemail"]
        ),
        ["site"] = new TableInfo(
            "Clients table",
            ["Ssitenum", "Sname", "sdeleted"]
        ),
        ["aareadex"] = new TableInfo(
            "Client sites/locations",
            ["aarea", "asite", "adeleted", "acontact", "aaddress1", "aaddress2", "acity", "astate", "azip"]
        ),
        ["users"] = new TableInfo(
            "End users",
            ["uid", "uusername", "uemail", "usite"]
        ),
        ["requesttype"] = new TableInfo(
            "Request types. Name is 'RTRequestType' column, description is 'rtdesc'",
            ["RTid", "RTRequestType", "rtdesc", "RTVisible", "RTSLAid", "RTDefInitialStatus", "RTDefSection", "RTDefCat2", "RTDefCat3", "RTIsProject", "RTIsOpportunity"]
        ),
        ["feedback"] = new TableInfo(
            "Customer satisfaction surveys for tickets (1=best, 5=worst rating)",
            ["FBFaultID", "FBDate", "FBScore", "fbcomment"]
        ),
        ["timesheet"] = new TableInfo(
            "Timesheet entries for work logged",
            ["TSid", "TSunum", "TSdate", "TSstartdate", "TSenddate"]
        ),
        ["actions"] = new TableInfo(
            "Ticket actions, notes, updates, and status changes",
            ["actid", "faultid", "actoutcome", "who", "whe_", "note", "timetaken", "outcome", "actiontype", "isimportant"]
        )
    };

    /// <summary>
    /// Example SQL queries demonstrating common HaloPSA data retrieval patterns.
    /// </summary>
    public static readonly string[] ExampleQueries = [
        @"SELECT TOP 10 faultid, symptom, Status, datecreated FROM faults
WHERE FDeleted='False' AND datecreated >= '2026-03-01T00:00:00Z'
ORDER BY faultid DESC",

        @"SELECT TOP 10 faultid, symptom, Status FROM faults
WHERE FDeleted='False' AND symptom LIKE '%maintenance%'
ORDER BY faultid DESC",

        @"SELECT f.faultid, f.symptom, rt.RTRequestType as request_type_name, f.datecleared
FROM faults f
INNER JOIN requesttype rt ON f.Requesttype = rt.RTid
WHERE f.FDeleted='False' AND f.Status=9 AND f.symptom LIKE '%maintenance%'
AND f.datecleared >= CAST(GETDATE() AS DATE) AND f.datecleared < DATEADD(DAY, 1, CAST(GETDATE() AS DATE))
ORDER BY f.faultid",

        @"SELECT COUNT(*) as total FROM faults
WHERE FDeleted='False' AND Status=9
AND datecleared >= '2026-03-01T00:00:00Z'",

        @"SELECT TOP 10 u.uname, COUNT(*) as ticket_count FROM faults f
INNER JOIN uname u ON CAST(f.Assignedtoint AS int) = u.Unum
WHERE f.FDeleted='False' AND f.datecreated >= '2026-03-01T00:00:00Z'
GROUP BY u.uname ORDER BY ticket_count DESC",

        @"SELECT TOP 10 s.Sname, COUNT(*) as ticket_count FROM faults f
INNER JOIN site s ON f.sectio_ = s.Ssitenum
WHERE f.FDeleted='False' AND f.datecreated >= '2026-03-01T00:00:00Z'
GROUP BY s.Sname ORDER BY ticket_count DESC",

        @"SELECT f.faultid, f.symptom, rt.RTRequestType as request_type_name, rt.rtdesc as description, f.datecreated
FROM faults f
INNER JOIN requesttype rt ON f.Requesttype = rt.RTid
WHERE f.FDeleted='False' AND f.datecreated >= '2026-03-01T00:00:00Z'
ORDER BY f.faultid",

        @"SELECT DATEPART(YEAR, FBDate) AS Year, DATEPART(MONTH, FBDate) AS Month, COUNT(*) AS SurveyCount
FROM feedback
WHERE FBDate >= '2026-01-01T00:00:00Z' AND FBFaultID IS NOT NULL
GROUP BY DATEPART(YEAR, FBDate), DATEPART(MONTH, FBDate)
ORDER BY Year, Month",

        @"SELECT f.faultid, f.symptom, a.actoutcome, a.note, a.whe_ as action_date, a.timetaken
FROM faults f
INNER JOIN actions a ON f.faultid = a.faultid
WHERE f.FDeleted='False' AND f.datecreated >= '2026-03-01T00:00:00Z'
ORDER BY f.faultid, a.whe_",

        @"SELECT TOP 10 s.Sname as client_name, a.asite as site_name, a.aaddress1, a.acity, a.astate, a.azip
FROM site s
INNER JOIN aareadex a ON s.Ssitenum = a.aarea
WHERE s.sdeleted='False' AND a.adeleted='False'
ORDER BY s.Sname",

        @"SELECT TOP 10 u.uusername, u.uemail, s.Sname as client_name, a.asite as site_name
FROM users u
INNER JOIN site s ON u.usite = s.Ssitenum
LEFT JOIN aareadex a ON u.usite = a.aarea
WHERE u.udeleted='False' AND s.sdeleted='False'
ORDER BY u.uusername",

        @"SELECT TSid, TSunum, TSdate, TSstartdate, TSenddate
FROM timesheet
WHERE TSstartdate >= '2026-03-01T00:00:00Z'
ORDER BY TSstartdate"
    ];

    /// <summary>
    /// Record containing information about a database table including its description and key columns.
    /// </summary>
    /// <param name="Description">Human-readable description of what the table contains.</param>
    /// <param name="KeyColumns">Array of important column names for this table.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Types need to be public for MCP framework discovery")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Do not nest type", Justification = "TableInfo is a simple data record that belongs with the schema data")]
    public record TableInfo(string Description, string[] KeyColumns);
}