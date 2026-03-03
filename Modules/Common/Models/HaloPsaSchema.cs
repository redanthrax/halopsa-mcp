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
        "All datetimes are UTC — convert from user's timezone",
        "Default scope: current calendar month unless user says otherwise",
        "FDeleted='False' is a STRING comparison, not integer",
        "Request type column is 'Requesttype' not 'RequestTypeID'",
        "Never use Fclosed column (broken). Closed tickets have Status=9",
        "Close date is 'datecleared' not 'Closeddate'",
        "Ticket occurrence date is 'dateoccured' not 'dateoccurred' (note: missing 'r')",
        "Type mismatches require CAST: CAST(f.Assignedtoint AS int) = u.Unum",
        "Invoice totals: (IHAmountDue + IHAmountPaid) because paid invoices zero out IHAmountDue",
        "Feedback table ratings: 1=best, 5=worst (lower score = better satisfaction)",
        "Table relationships: faults.faultid = actions.faultid = feedback.FBFaultID, faults.Requesttype = requesttype.RTid",
        "Timesheet entries track actual work time logged against tickets (TSunum = agent ID, TSstartdate/TSenddate = work time)",
        "Actions table contains all ticket updates, notes, and status changes",
        "Note: Timesheet-to-ticket relationship may be indirect (check with HaloPSA admin for specific linking column)"
    ];

    /// <summary>
    /// Dictionary of commonly used HaloPSA database tables with their descriptions and key columns.
    /// </summary>
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
            "Client sites",
            ["aarea", "asite"]
        ),
        ["users"] = new TableInfo(
            "End users",
            ["uid", "uusername", "uemail", "usite"]
        ),
        ["requesttype"] = new TableInfo(
            "Request types configuration",
            [
                "RTid", "RTSortseq", "rtdesc", "RTVisible", "RTEndUsersCanSelect", "RTIncludeInSLA", "RTDefSection", "RTDefCat2", "RTDefCat3", "RTDefCat4", "RTDefCat5",
                "RTDefSeriousness", "RTDefInitialStatus", "RTDefTech", "RTRequestType", "RTexcludefromSLA", "RTShowOnWeb", "RTcalcFixbydate", "RTAutoStartApproval", "RTtargetopen",
                "RTSLAid", "RTInformAccountManager", "RTChargeRate", "RTMailboxid", "RTcalcolour", "RTIsOpportunity", "RTStatusAfterUserUpdate", "RTClosedRequestsWithUpdates",
                "RTIncludeInMobileDBsync", "RTqr2filename", "RTWorkFlowID", "RTBccToAddress", "RTIsProject", "RTreopenedstatus", "RTMinCredit", "rtIsPurchaseRequisition",
                "RTScriptID", "RTQuoteID", "RTDeliverToUs", "RTQuoteQR2FileName", "RTcat2", "RTcat3", "RTcat4", "RTcat5", "rtGID", "rtGIDEND", "rtprintrequestdetails",
                "rtprintserviceform", "RTDefaultSendAck", "RTDefaultSendEmail", "RTIsRMA", "RTchangeseq", "rtclosedrequestsemailID", "RThiderespondbtn", "RTDefaultSubject",
                "RTQuickCaption", "RTKBonclose", "RTwebannouncement", "RTUsercanaddattachments", "RTAlwaysAvailableInSearch", "RTJiraIssueType", "RTDefaultDetails",
                "RTDontSendSLAReminders", "RTStatusAfterEndUsersRequestsClosure", "rtTechMustChoosePriority", "RTStatusAfterTechUpdate", "rtallowallactions", "rtallowallstatus",
                "rtallowallcat2", "rtallowallcat3", "rtallowallcat4", "rtallowallcat5", "rtdefproduct", "RTHideUserDefFields", "RTHideDateOccured", "RTHideSLAFields",
                "RTHideAssigned", "RTHideStatus", "RTHideReportedBy", "RTHideTimeTaken", "RTPartsLookupID", "RTAcknowledgementTemplate", "RTAcknowledgementTemplateOOH",
                "RTStatusAfterSupplierUpdate", "RTAppointmentNoteBlob", "RTAppointmentDefaultBody", "RTAppointmentNoteHTML", "rtgroupid", "rtagentscanselect", "RTClosedUserUpdateHours",
                "RTApplyDefaultsOnTypeChange", "RTdefaultestimate", "RTmustclosechildbeforeclosure", "rtdefbudgettype", "RTdefsectiontoagentsdefault", "RTDefaultSendRemoteInvite",
                "RTLogTimeInDays", "RTLogTimeInDaysIncrement", "RTUseTimeslotsForStartAndTargetTimes", "RTChildTicketColumnsOverride", "rtshowunborntab", "rtallowcustomiseunborn",
                "rtallowattachments", "rtdontusependingclosure", "rtallowtickettypetobechild", "RTShowKBPrompt", "RTAllowCSVUserUpload", "RTCSVTableID", "RTAutoForwardEmailUpdates",
                "RTpdftemplateid", "rtanonymouscanselect", "RTmaximumRestrictedPriority", "RAutoRespondLoggedManually", "RTclosedrequestreplylimit", "rtautoclosehours",
                "RTPortalCanReopen", "RToverrideWithTheFollowingTemplateWhenLoggingManually", "RTeditServStatus", "RTallowVoting", "RTsetRelatedServicesFromAssets",
                "rtdefaultmatchedkbid", "RTDefaultSendToSOC", "RTDefaultSOCTargetType", "RTDefaultSOCTargetId", "RTDefaultSOCTargetName", "RTForwardInboundUpdates",
                "rtDefaultApptSummary", "rtDefaultApptDetails", "RTDisplayOnQuickTime", "RTisSprint", "rtstatusafterapproverupdate", "rtdisplayaudittab", "RTOverwriteShowForUsers",
                "RTShowDownvote", "RTDefaultChangeInformationHtml", "RTDefaultJustification", "RTDefaultImpact", "RTDefaultImpactDescription", "RTDefaultRiskLevel",
                "RTDefaultRiskLevelDescription", "RTDefaultTestPlan", "RTDefaultBackoutPlan", "RTDefaultCommunicationPlan", "RTIsBillable", "rtAddActionsToLinked",
                "rtAddNoteOnlyToLinked", "RTdefaultResourceType", "RTShowDevOpsID", "RTdefaultListType", "RTdefaultTab", "rtpendingrequestswithupdates", "RTpendingrequestreplylimit",
                "rtpendingrequestsemailID", "RTpendingUserUpdateHours", "RTClosedRequestsWithUpdatesIncPending", "rtdefaultsynctosentinel", "RTAddUserUpdatesToChildren",
                "RTDefaultTargetDate", "rtresourcebookingtypeagent", "rtuserbookfromportal", "RTdefaultResourceTypeAgent", "rtshowopenusertickets", "rtshowopensitetickets",
                "rtshowopenclienttickets", "rtattendeesenduser", "rtupdateservicestatus", "rtdefaultservicestatusnote", "RTshowAutomationsTab", "rtshowoppcontacttab",
                "RTDefaultAgentResourceBookingDuration", "rtsubmitlabeloverride", "rtshowactivestatuskanban", "rtkanbanstatuschoice_list", "rtallowallappointmenttypes",
                "rtincludeappsscheduledhours", "rtganttgrandchildview", "rtticketlinktype", "rtalluserscanview", "rtdefaultsynctosalesforce", "rtsalesforcedefaultstage",
                "rtshowaddnote", "rtcopyattachmentstochild", "rtautodeletedata", "rtautodeletedays", "rtshowchildtaskstype", "rtpinimportantactions", "rtshowdecendantwarningoptions",
                "rtshowbillingtab", "rtstatusafterresourcebook", "rtallowlogonbehalfof", "rtparentstatusafterallchildclosed"
            ]
        ),
        ["feedback"] = new TableInfo(
            "Customer satisfaction surveys for tickets (1=best, 5=worst rating)",
            ["FBFaultID", "FBDate", "FBScore", "fbcomment"]
        ),
        ["timesheet"] = new TableInfo(
            "Timesheet entries for work logged",
            ["TSid", "TSunum", "TSdate", "TSstartdate", "TSenddate"]
        ),
        ["invoiceheader"] = new TableInfo(
            "Invoices",
            ["IHid", "IHInvoice_ID", "IHAmountDue", "IHAmountPaid"]
        )
    };

    /// <summary>
    /// Example SQL queries demonstrating common HaloPSA data retrieval patterns.
    /// </summary>
    public static readonly string[] ExampleQueries = [
        @"SELECT TOP 10 faultid, symptom, Status, datecreated FROM faults
WHERE FDeleted='False' AND datecreated >= '2026-03-01T00:00:00Z'
ORDER BY faultid DESC",

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

        @"SELECT f.faultid, f.symptom, rt.rtdesc as request_type, f.datecreated
FROM faults f
INNER JOIN requesttype rt ON f.Requesttype = rt.RTid
WHERE f.FDeleted='False' AND f.datecreated >= '2026-03-01T00:00:00Z'
ORDER BY f.faultid",

        @"SELECT DATEPART(YEAR, FBDate) AS Year, DATEPART(MONTH, FBDate) AS Month, COUNT(*) AS SurveyCount
FROM feedback
WHERE FBDate >= '2026-01-01T00:00:00Z' AND FBFaultID IS NOT NULL
GROUP BY DATEPART(YEAR, FBDate), DATEPART(MONTH, FBDate)
ORDER BY Year, Month",

        @"SELECT f.faultid, f.symptom, a.actoutcome, a.note, a.whe_ as action_date
FROM faults f
INNER JOIN actions a ON f.faultid = a.faultid
WHERE f.FDeleted='False' AND f.datecreated >= '2026-03-01T00:00:00Z'
ORDER BY f.faultid, a.whe_",

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