namespace HaloPsaMcp.Modules.Common.Models;

/// <summary>
/// HaloPSA schema cheat sheet rendered by `halopsa_get_schema`.
/// Source of truth: schema/reference.md and schema/catalog.json. Keep in sync.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Types need to be public for MCP framework discovery")]
public static class HaloPsaSchema
{
    /// <summary>
    /// Critical query notes. Read these before writing SQL.
    /// </summary>
    public static readonly string[] ImportantNotes = [
        "WORKFLOW: When you don't know which table or column holds a concept, ALWAYS call halopsa_db_search FIRST (e.g. db_search('elapsed'), db_search('contract hours'), db_search('satisfaction')). Do not try information_schema queries before halopsa_db_search.",
        "DEEPER SCHEMA: For full reference docs and ERDs use halopsa_db_reference and halopsa_db_erd. catalog.json column-level detail is via halopsa_db_columns.",
        "All datetimes are stored in UTC. The user is in Pacific Time (UTC-8 standard / UTC-7 DST). Always convert local times to UTC for WHERE clauses (e.g. user's 'today March 4' = UTC range 2026-03-04T08:00:00Z to 2026-03-05T07:59:59Z).",
        "Default scope: current calendar month in UTC unless the user specifies otherwise.",
        "FDeleted='False' is a STRING comparison, not integer.",
        "Closed tickets have Status=9. Never use the Fclosed column (broken). Close date is `datecleared`, not `Closeddate`.",
        "Ticket occurrence date is `dateoccured` (note: missing second 'r'), not `dateoccurred`.",
        "AGREEMENT / CONTRACT HOURS CHARGED: ACTIONS.ActionChargeHours filtered by ActionContractID < 0 (HaloPSA stores the linked contract id as a NEGATIVE integer; ABS(ActionContractID) = real contract id). DO NOT use FAULTS.Elapsedhrs for agreement utilisation — Elapsedhrs includes Projects, Alerts and non-billable T&M time and produces wildly inflated numbers vs. what is actually charged to the agreement. PREFERRED: call halopsa_get_contract_utilisation, which already encodes this join.",
        "CLIENT vs SITE — these are different concepts in HaloPSA. AREA = client/customer organisation (PK Aarea, name aareadesc). SITE = sub-location under a client (PK Ssitenum). FAULTS.Areaint joins to AREA.Aarea for client. FAULTS.sitenumber (INT FK) joins to SITE.Ssitenum for site. Do NOT use faults.sectio_ as a join key — it is NVARCHAR free text, not an FK.",
        "Client name is AREA.aareadesc. SITE.Sname is often blank in many tenants — prefer AREA.aareadesc, or fall back to the list_clients REST API for authoritative client names.",
        "Request type display name: requesttype.RTRequestType. Description: requesttype.rtdesc. FAULTS.RequestTypeNew and FAULTS.Requesttype both exist; reference.md and the canonical join uses RequestTypeNew = requesttype.RTid.",
        "Ticket sub-type / category is in the symptom field, not requesttype — for maintenance tickets use symptom LIKE '%maintenance%'.",
        "Type mismatches require CAST: CAST(f.Assignedtoint AS int) = u.Unum.",
        "Invoice totals: (IHAmountDue + IHAmountPaid) — paid invoices zero out IHAmountDue.",
        "Feedback ratings: 1=best, 5=worst (lower = better satisfaction).",
        "Key joins: faults.faultid = actions.faultid = feedback.FBFaultID; faults.Areaint = AREA.Aarea (client); faults.sitenumber = SITE.Ssitenum (site); faults.Assignedtoint = uname.Unum (agent); faults.userid = users.Uid (end user); faults.RequestTypeNew = requesttype.RTid.",
        "Use LIKE for name searches: symptom LIKE '%maintenance%' or u.uname LIKE '%john%' to allow partial matches.",
        "Actions table contains all ticket updates, notes, and status changes (one row per work-log/email/status change). Action-level billing fields: ActionChargeHours (hours charged to agreement), ActionContractID (negative = linked agreement, AContractId is unrelated and always -1 in many tenants — IGNORE it), Whe_ (action timestamp, UTC).",
        "Timesheet-to-ticket relationship may be indirect (check with HaloPSA admin for specific linking column)."
    ];

    /// <summary>
    /// Canonical tables an agent should reach for first. Names match catalog.json (uppercase).
    /// </summary>
#pragma warning disable CA2244 // Allow redundant element initialization for schema clarity
    public static readonly Dictionary<string, TableInfo> CommonTables = new()
    {
        ["FAULTS"] = new TableInfo(
            "Tickets — incidents, requests, changes, projects, opportunities. ~600 columns; use halopsa_db_columns to drill in.",
            ["Faultid", "Symptom", "Status", "Areaint", "sitenumber", "Assignedtoint", "userid", "RequestTypeNew", "Elapsedhrs", "FDeleted", "dateoccured", "datecreated", "datecleared"]
        ),
        ["ACTIONS"] = new TableInfo(
            "Each update on a ticket (work log, replies, status changes). PK = Faultid + actionnumber.",
            ["actid", "faultid", "actionnumber", "actoutcome", "who", "Whoint", "Whe_", "note", "timetaken", "outcome", "actiontype", "isimportant", "ActionChargeHours", "ActionContractID", "ActionChargeAmount", "ActionPrePayHours"]
        ),
        ["AREA"] = new TableInfo(
            "Clients / customer organisations. Aarea = client id. aareadesc = client name (preferred over SITE.Sname).",
            ["Aarea", "aareadesc", "Achargehours", "AHourlyRate", "Aenddate", "Astartdate"]
        ),
        ["SITE"] = new TableInfo(
            "Sub-sites under a client. NOT the client itself — for client info join AREA. Sname is often blank in many tenants.",
            ["Ssitenum", "Sname", "sdeleted"]
        ),
        ["UNAME"] = new TableInfo(
            "Internal agents / staff. FAULTS.Assignedtoint = uname.Unum (cast int).",
            ["Unum", "uname", "uemail"]
        ),
        ["USERS"] = new TableInfo(
            "End users / contacts / requesters. FAULTS.userid = users.Uid.",
            ["Uid", "Uusername", "uemail", "usite"]
        ),
        ["REQUESTTYPE"] = new TableInfo(
            "Ticket categories. FAULTS.RequestTypeNew = requesttype.RTid. Display name in RTRequestType, description in rtdesc.",
            ["RTid", "RTRequestType", "rtdesc", "RTVisible", "RTSLAid", "RTDefInitialStatus", "RTIsProject", "RTIsOpportunity"]
        ),
        ["FEEDBACK"] = new TableInfo(
            "Customer satisfaction surveys (1=best, 5=worst). FBFaultID joins to FAULTS.faultid.",
            ["FBFaultID", "FBDate", "FBScore", "fbcomment"]
        ),
        ["TIMESHEET"] = new TableInfo(
            "Timesheet day records. SQL columns differ from API: TSid=id, TSunum=agent_id, TSdate=date, TSstartdate=start_time, TSenddate=end_time.",
            ["TSid", "TSunum", "TSdate", "TSstartdate", "TSenddate"]
        ),
        ["CONTRACTHEADER"] = new TableInfo(
            "Recurring contracts. CHid = contract id. For per-client/contract utilisation use halopsa_get_contract_utilisation, or aggregate ACTIONS.ActionChargeHours WHERE ActionContractID < 0 GROUP BY ABS(ActionContractID). Do NOT use faults.Elapsedhrs for agreement utilisation.",
            ["CHid", "CHChargeHoursPerPeriod", "CHOutOfHoursMultiplier", "chprepayrecurringhours"]
        ),
        ["INVOICEHEADER"] = new TableInfo(
            "Invoice header. Total = IHAmountDue + IHAmountPaid (paid invoices zero out IHAmountDue).",
            ["IHid", "IHareaint", "IHinvoicedate", "IHAmountDue", "IHAmountPaid", "IHnetamount"]
        )
    };

    /// <summary>
    /// Worked example queries. Joins follow reference.md / catalog.json — do NOT change without verifying both.
    /// </summary>
    public static readonly string[] ExampleQueries = [
        @"-- Open projects (authoritative — uses RTIsProject, not /api/Projects)
SELECT TOP 50 f.Faultid, f.Symptom, f.Status,
              a.aareadesc      AS client,
              u.uname          AS assigned_to,
              rt.RTRequestType AS request_type
  FROM FAULTS f
  INNER JOIN REQUESTTYPE rt ON rt.RTid = f.RequestTypeNew AND rt.RTIsProject = 1
  LEFT  JOIN AREA  a ON a.Aarea = f.Areaint
  LEFT  JOIN UNAME u ON u.Unum  = CAST(f.Assignedtoint AS int)
 WHERE f.FDeleted = 'False' AND f.Status NOT IN (8, 9)
 ORDER BY f.Faultid DESC",

        @"-- Open tickets with client, site, agent, end-user, and request type
SELECT TOP 25 f.Faultid, f.Symptom, f.Status, f.datereported,
       a.aareadesc        AS client,
       s.Sname            AS site,
       u.uname            AS agent,
       eu.Uusername       AS end_user,
       rt.RTRequestType   AS request_type
  FROM FAULTS f
  LEFT JOIN AREA        a  ON a.Aarea     = f.Areaint
  LEFT JOIN SITE        s  ON s.Ssitenum  = f.sitenumber
  LEFT JOIN UNAME       u  ON u.Unum      = CAST(f.Assignedtoint AS int)
  LEFT JOIN USERS       eu ON eu.Uid      = f.userid
  LEFT JOIN REQUESTTYPE rt ON rt.RTid     = f.RequestTypeNew
 WHERE f.FDeleted='False' AND f.Status NOT IN (8, 9)
 ORDER BY f.Faultid DESC",

        @"-- Tickets created today (UTC), with names
SELECT TOP 25 f.Faultid, f.Symptom, a.aareadesc AS client, f.datecreated
FROM FAULTS f
LEFT JOIN AREA a ON a.Aarea = f.Areaint
WHERE f.FDeleted='False'
  AND f.datecreated >= '2026-03-04T08:00:00Z'
  AND f.datecreated <  '2026-03-05T08:00:00Z'
ORDER BY f.Faultid DESC",

        @"-- Closed maintenance tickets today
SELECT f.Faultid, f.Symptom, rt.RTRequestType, f.datecleared
FROM FAULTS f
INNER JOIN REQUESTTYPE rt ON f.RequestTypeNew = rt.RTid
WHERE f.FDeleted='False' AND f.Status=9
  AND f.Symptom LIKE '%maintenance%'
  AND f.datecleared >= CAST(GETDATE() AS DATE)
  AND f.datecleared <  DATEADD(DAY, 1, CAST(GETDATE() AS DATE))
ORDER BY f.Faultid",

        @"-- Total closed tickets this month
SELECT COUNT(*) AS total
FROM FAULTS
WHERE FDeleted='False' AND Status=9
  AND datecleared >= '2026-03-01T00:00:00Z'",

        @"-- Tickets per agent this month
SELECT TOP 25 u.uname, COUNT(*) AS ticket_count
FROM FAULTS f
INNER JOIN UNAME u ON CAST(f.Assignedtoint AS int) = u.Unum
WHERE f.FDeleted='False' AND f.datecreated >= '2026-03-01T00:00:00Z'
GROUP BY u.uname
ORDER BY ticket_count DESC",

        @"-- AGREEMENT HOURS CHARGED PER CLIENT/CONTRACT this month (CORRECT source for contract utilisation).
-- Prefer the halopsa_get_contract_utilisation tool, which also joins entitlement from /api/ClientContract.
SELECT TOP 100
    f.Areaint                       AS client_id,
    a.aareadesc                     AS client_name,
    ABS(ac.ActionContractID)        AS contract_id,
    ROUND(SUM(ac.ActionChargeHours), 2) AS charge_hours,
    COUNT(DISTINCT ac.Faultid)      AS ticket_count
FROM ACTIONS ac
INNER JOIN FAULTS f ON ac.Faultid = f.Faultid
INNER JOIN AREA   a ON a.Aarea     = f.Areaint
WHERE ac.Whe_ >= '2026-04-01T00:00:00Z'
  AND ac.Whe_ <  '2026-05-01T00:00:00Z'
  AND ac.ActionContractID < 0   -- negative = linked agreement
  AND ac.ActionChargeHours > 0
  AND f.FDeleted = 'False'
GROUP BY f.Areaint, a.aareadesc, ac.ActionContractID
ORDER BY charge_hours DESC",

        @"-- Survey volume by month
SELECT DATEPART(YEAR, FBDate) AS Year, DATEPART(MONTH, FBDate) AS Month, COUNT(*) AS SurveyCount
FROM FEEDBACK
WHERE FBDate >= '2026-01-01T00:00:00Z' AND FBFaultID IS NOT NULL
GROUP BY DATEPART(YEAR, FBDate), DATEPART(MONTH, FBDate)
ORDER BY Year, Month",

        @"-- All actions on tickets created this month
SELECT f.faultid, f.Symptom, a.actoutcome, a.note, a.whe_ AS action_date, a.timetaken
FROM FAULTS f
INNER JOIN ACTIONS a ON f.faultid = a.faultid
WHERE f.FDeleted='False' AND f.datecreated >= '2026-03-01T00:00:00Z'
ORDER BY f.faultid, a.whe_",

        @"-- Find a timesheet by agent and date (TSdate is UTC, TSunum is agent id)
SELECT TSid, TSunum, TSdate, TSstartdate, TSenddate
FROM TIMESHEET
WHERE TSunum = <agent_id>
  AND TSdate >= '2026-03-02T00:00:00Z'
  AND TSdate <  '2026-03-03T00:00:00Z'
ORDER BY TSdate DESC"
    ];

    /// <summary>
    /// Record describing a table for the cheat sheet.
    /// </summary>
    /// <param name="Description">Human-readable description.</param>
    /// <param name="KeyColumns">Important columns.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Types need to be public for MCP framework discovery")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Do not nest type", Justification = "Simple data record paired with schema data")]
    public record TableInfo(string Description, string[] KeyColumns);
}
