# HaloPSA data reference

Reference for the HaloPSA SQL Server schema. **845 tables** across **15 domains**. Tenant-specific row counts have been redacted; the structure (PK, FK, columns) is what an agent needs to query.

**How to use this file as an agent:**

1. Find the domain for what you're querying (section list below).
2. Skim the domain table to identify candidate tables.
3. Open `catalog.json` and read the table's `columns` + `foreign_keys` for column types and join keys.
4. For visual context, open `erd/<domain>.md` — Mermaid ERDs that render in GitHub / VS Code markdown preview.

**FK sources** (in `catalog.json` each FK has a `kind`):
- `declared` — actual `FOREIGN KEY` constraint in the schema (~26 of them; gold).
- `convention` — exact HaloPSA-specific naming (`Areaint`→AREA, `sitenumber`→SITE, `Assignedtoint`→UNAME, `whocreated`→UNAME, etc.). High confidence.
- `convention-suffix` — table-abbreviation prefix + known PK column (e.g. INVOICEHEADER columns `IHaarea`/`IHsitenumber`/`IHuid` → AREA/SITE/USERS; APPOINTMENT.`APFaultid`→FAULTS). High confidence.
- `pk-match` — column name equals the single-column PK of exactly one other table. Lower confidence, may overmatch.

## Domains

- **tickets** (71 tables) — Tickets / requests. **FAULTS** is the central ticket table; **ACTIONS** holds each update (one row per work-log/email/status change). STDREQUEST = templates, REQUESTTYPE = ticket category.
- **crm** (36 tables) — Clients (**AREA**) and their **SITEs**, address books, opportunities, marketing, campaigns. AREA.Aarea = client id; SITE.Ssitenum = site id.
- **assets** (28 tables) — Assets / configuration items. **ITEM** is the canonical asset table (Iid). DEVICE*, plus per-platform inventory.
- **billing** (38 tables) — Invoices (**INVOICEHEADER** + INVOICEDETAIL), taxes, budgets, charges, currencies, quotes.
- **contracts** (15 tables) — Recurring contracts (**CONTRACTHEADER** + CONTRACTDETAIL), plans, schedules, billing rules.
- **kb** (12 tables) — Knowledge base articles (**KBENTRY**), FAQs, related links.
- **projects** (9 tables) — Project plans, phases, milestones, risk register.
- **comms** (30 tables) — Appointments / calendar (**APPOINTMENT**), calls, chat, meetings, SMS.
- **email** (21 tables) — Inbound (IncomingEmail) and outbound mail handling, mailboxes, templates, message bodies.
- **auth** (66 tables) — Identity & access. **UNAME** = internal agents (Unum), **USERS** = end-users / contacts (Uid), NHD_IDENTITY_* = OAuth/OpenID tables, AgentLogin = login history.
- **workflow** (32 tables) — Approval processes, auto-assign, workflows, schedules, matching rules.
- **audit** (18 tables) — Audit trail, change history, logs, traces, events. AUDIT, AUDITFAULT, *History, *Change, PORTALLOG, Trace, EventData.
- **integrations** (65 tables) — Per-vendor integration tables — RMM (Datto, NinjaOne, NCentral, Atera, Auvik, Continuum, Kaseya, Syncro, Domotz, Addigy, Automate), PSA peers (ConnectWise, Autotask, ServiceNow, Freshdesk, Zendesk, Jira), monitoring (Splunk, Sentinel, NewRelic, Pagerduty, Orion, Splunk OnCall, OpsGenie), billing (Stripe, Sage, Xero, QuickBooks, Pax8, Chargebee), comms (Twilio, Slack, Teams, RingCentral), social (Twitter, Facebook), webhooks, OAuth, vendor alert tables.
- **system** (79 tables) — Config, options, analyzer/report definitions, language packs, custom translations, PDF templates, theming.
- **other** (325 tables) — Miscellaneous tables that didn't match domain rules.

## Key entities (start here)

| Table | PK | Rows | Purpose |
|---|---|---:|---|
| `FAULTS` | Faultid | — | Tickets — incidents, changes, requests, opportunities, projects (one row per request, ~600 cols) |
| `ACTIONS` | Faultid, actionnumber | — | Each update on a ticket (work log, replies, status changes; PK = Faultid+actionnumber) |
| `AREA` | Aarea | — | Clients / customer organisations (Aarea); FAULTS.Areaint joins here |
| `SITE` | Ssitenum | — | Sites under a client (Ssitenum); FAULTS.sitenumber joins here |
| `UNAME` | Unum | — | Internal agents / staff (Unum); FAULTS.Assignedtoint, ACTIONS.Whoint join here |
| `USERS` | Uid | — | End-users / contacts / requesters (Uid); FAULTS.userid joins here |
| `ITEM` | Iid | — | Assets / configuration items (Iid) |
| `INVOICEHEADER` | IHid | — | Invoice header (IHid) |
| `INVOICEDETAIL` | IDid | — | Invoice line items |
| `CONTRACTHEADER` | CHid | — | Contract header (CHid) |
| `KBENTRY` | id | — | Knowledge base articles (id) |
| `STDREQUEST` | stdid | — | Standard request templates (stdid) |
| `REQUESTTYPE` | RTid | — | Request types (incident / change / project / opportunity / etc.) |
| `APPOINTMENT` | APid | — | Calendar appointments / scheduled work |
| `ATTACHMENT` | ATid | — | Files attached to tickets, articles, etc. |

## Common joins

```sql
-- Open tickets with client, site, agent, end-user, and request type
SELECT f.Faultid,
       f.Symptom,
       f.Status,
       f.datereported,
       a.aareadesc        AS client,
       s.SSiteName        AS site,
       u.UFullname        AS agent,
       eu.Uusername       AS end_user,
       rt.RTDesc          AS request_type
  FROM dbo.FAULTS         f
  LEFT JOIN dbo.AREA      a  ON a.Aarea     = f.Areaint
  LEFT JOIN dbo.SITE      s  ON s.Ssitenum  = f.sitenumber
  LEFT JOIN dbo.UNAME     u  ON u.Unum      = f.Assignedtoint
  LEFT JOIN dbo.USERS     eu ON eu.Uid      = f.userid
  LEFT JOIN dbo.REQUESTTYPE rt ON rt.RTid   = f.RequestTypeNew
 WHERE f.Status NOT IN (8, 9);  -- exclude closed / cancelled

-- All updates on a single ticket
SELECT a.Faultid, a.actionnumber, a.actiondate, a.who, a.note
  FROM dbo.ACTIONS a
 WHERE a.Faultid = @id
 ORDER BY a.actionnumber;

-- Invoiced amounts per client this year
SELECT a.aareadesc, SUM(ih.IHnetamount) AS net
  FROM dbo.INVOICEHEADER ih
  JOIN dbo.AREA          a  ON a.Aarea = ih.IHareaint
 WHERE ih.IHinvoicedate >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1)
 GROUP BY a.aareadesc;
```

## Tables by domain

### tickets  (71 tables)

Tickets / requests. **FAULTS** is the central ticket table; **ACTIONS** holds each update (one row per work-log/email/status change). STDREQUEST = templates, REQUESTTYPE = ticket category.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `ACTIONS` | 199 | — | Faultid, actionnumber | ATTACHMENT, FAULTS |
| `FAULTS` | 625 | — | Faultid | AREA, CONTRACTHEADER, KBENTRY, REQUESTTYPE, SITE, STDREQUEST, UNAME, USERS |
| `AUDITFAULT` | 11 | — | AFid | AREA, FAULTS, SITE |
| `FaultRuleMatch` | 4 | — | FRMid | FAULTS |
| `FEEDBACK` | 14 | — | FBID | ATTACHMENT, FAULTS |
| `FaultRuleHistory` | 5 | — | FRHid | FAULTS |
| `FAULTTODO` | 12 | — | FTfaultid, FTseq | FAULTS |
| `REQUESTTYPEFIELD` | 34 | — | RTFid | — |
| `FAULTAPPROVAL` | 30 | — | FAid, FAseq | ACTIONS, ADDRESSBOOK, UNAME |
| `StdRequestScheduleOccurrence` | 13 | — | id | STDREQUEST |
| `STDTODO` | 5 | — | TDid, TDseq | — |
| `FaultMerge` | 17 | — | FMid | FAULTS |
| `STDREQUEST` | 281 | — | stdid | KBENTRY, USERS |
| `FaultAdditionalAgents` | 2 | — | FAAFaultid, FAAUnum | FAULTS, UNAME |
| `TOUTCOME` | 191 | — | Oid | — |
| `FaultBudget` | 11 | — | FBTid | BudgetType, FAULTS |
| `FaultWatch` | 4 | — | fwid | FAULTS, UNAME |
| `FaultsTimeEntry` | 6 | — | FTEID | FAULTS |
| `FaultsMileStone` | 3 | — | FMSid | FAULTS |
| `REQUESTTYPE` | 357 | — | RTid | — |
| `REQUESTTYPESTATUS` | 2 | — | RTSRTID, RTSTStatus | — |
| `ActionReaction` | 5 | — | ARfaultid, ARactionnumber, ARunum | ACTIONS, FAULTS, UNAME |
| `FAQLISTDET` | 3 | — | FAQDid, FAQDkbid | KBENTRY |
| `STDREQUESTCHILDREN` | 2 | — | TCParentId, TCChildId | — |
| `ACTYPE` | 11 | — | Actypenum | — |
| `STDREQUESTRULE` | 16 | — | TRid | — |
| `FAQLISTHEAD` | 15 | — | FAQid | — |
| `STDLIST` | 5 | — | — | STDREQUEST, USERS |
| `TicketArea` | 34 | — | — | TASK |
| `FAULTSERVICE` | 7 | — | fsid | FAULTS, STDREQUEST |
| `STDrequestbudget` | 5 | — | STBid | STDREQUEST |
| `ACT` | 5 | — | actID | — |
| `ACTARC` | 68 | — | Faultid, actionnumber | ACTIONS, FAULTS |
| `ActionRead` | 4 | — | ARid | FAULTS |
| `AiSuggestionFault` | 9 | — | AISFid | FAULTS |
| `FAQCLIENT` | 2 | — | FAQCid, FAQCarea | — |
| `FAQCompany` | 2 | — | FAQCompanycnum, FAQCompanyfaqid | — |
| `FAQOrg` | 2 | — | FOorgid, FOfaqlistid | — |
| `FaqRequestType` | 3 | — | frtid | — |
| `FAQRole` | 4 | — | FAQRid | — |
| `FAQSite` | 2 | — | FAQSid, FAQSsiteid | — |
| `FAQStd` | 2 | — | FSStdid, FSFAQid | FAQLISTHEAD, STDREQUEST |
| `FAULTARC` | 198 | — | Faultid | AREA, FAULTS, REQUESTTYPE, SITE, UNAME |
| `FAULTBOOKMARKS` | 6 | — | FBID | FAULTS, UNAME |
| `FaultChat` | 4 | — | FCid | ATTACHMENT, FAULTS |
| `FaultCommit` | 4 | — | — | FAULTS |
| `FAULTDEVICE` | 7 | — | FDfaultid, FDsiteid, FDdevnum | FAULTS |
| `FaultDraft` | 4 | — | FDid | USERS |
| `FAULTITEM` | 25 | — | FLid, FLseq | UNAME |
| `FaultKbRelation` | 2 | — | Faultid, KbId | FAULTS, KBENTRY |
| `FaultNotes` | 5 | — | FNid | FAULTS, UNAME, USERS |
| `FaultOLA` | 17 | — | FOid | FAULTS |
| `FaultOLADates` | 4 | — | FODid | — |
| `Faults_Metadata` | 1 | — | Faultid | — |
| `FaultsDateDependencies` | 6 | — | FDDID | FAULTS |
| `FaultsForecasting` | 5 | — | FFCid | FAULTS, UNAME |
| `FaultsViewLog` | 6 | — | FVLid | FAULTS, UNAME, USERS |
| `FaultVector` | 4 | — | FVid | FAULTS |
| `FaultVectorScore` | 6 | — | FVSid | FAULTS |
| `FaultVotes` | 6 | — | FVid | FAULTS, UNAME, USERS |
| `OneBillFaultItem` | 7 | — | OBFIID | — |
| `RequestTypeChildren` | 2 | — | RTCparentid, RTCchildid | — |
| `REQUESTTYPECONTACT` | 4 | — | RCid | UNAME, USERS |
| `RequestTypeFieldRestriction` | 11 | — | RTFRid | — |
| `RequestTypeGroup` | 3 | — | RTGid | — |
| `REQUESTTYPESLAOVERRIDE` | 5 | — | RTSOid | — |
| `StdKBEntryTag` | 3 | — | stdktid | KBENTRY |
| `stdkbrelation` | 2 | — | skrstdid, skrKbId | KBENTRY, STDREQUEST |
| `StdRequestCustomField` | 2 | — | STDCFstdid, STDCFfiid | STDREQUEST |
| `StdrequestDateDependencies` | 5 | — | SDDid | — |
| `STDREQUESTDEVICE` | 3 | — | SAid | STDREQUEST |

### crm  (36 tables)

Clients (**AREA**) and their **SITEs**, address books, opportunities, marketing, campaigns. AREA.Aarea = client id; SITE.Ssitenum = site id.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `MailCampaignLog` | 9 | — | MCLid | FAULTS, UNAME, USERS |
| `MarketingOpen` | 9 | — | MOid | USERS |
| `BulkEmailUser` | 8 | — | id | BulkEmail, USERS |
| `ADDRESSSTORE` | 17 | — | ASID | — |
| `SITE` | 120 | — | Ssitenum | — |
| `AREA` | 332 | — | Aarea | — |
| `AreaAzureTenant` | 11 | — | AATid | — |
| `MailCampaignEmail` | 14 | — | MCEid | — |
| `MailCampaign` | 16 | — | MCid | — |
| `BulkEmail` | 19 | — | id | ACTIONS, FAULTS |
| `OPPCLOSURECATEGORYFIELDS` | 4 | — | OCID | ITEM |
| `AREANOTE` | 22 | — | ANid | FAULTS, INVOICEHEADER, USERS |
| `MarketingUnsubscribe` | 7 | — | MUid | USERS |
| `ORGANISATION` | 36 | — | ORid | — |
| `SITECONTACT` | 4 | — | SCid | USERS |
| `ACCOUNTSINTERFACE` | 19 | — | AIAreaid, AIseq | FAULTS |
| `ADDRESSBOOK` | 12 | — | abid | UNAME |
| `AOV` | 4 | — | aovCnum, aovBudgetCode | — |
| `AreaChangeFreeze` | 3 | — | acfid | — |
| `AREAFIELD` | 4 | — | AFid | ITEM |
| `AREAITEM` | 26 | — | AMID | — |
| `AREAORGANISATION` | 2 | — | AOArea, AOORid | — |
| `AREAPOPUP` | 14 | — | APOPid | — |
| `AREAREQUESTTEMPLATE` | 3 | — | ARTID | STDREQUEST |
| `AREAREQUESTTYPE` | 2 | — | ARarea, ARRT | — |
| `AreaRequestTypeRule` | 5 | — | ARTRid | — |
| `AreaSectionDetail` | 4 | — | ASDid | — |
| `AreaSite` | 2 | — | AreaID, SiteID | SITE |
| `AreaToDO` | 3 | — | atdtdid, atdtdseq, atdarea | — |
| `CUSTOMERVERSIONHISTORY` | 4 | — | CVHid | — |
| `OrganisationField` | 3 | — | OFid | ITEM |
| `ORGANISATIONREQUESTTYPE` | 2 | — | ORTorid, ORTrtid | — |
| `SalesMailbox` | 25 | — | smid | — |
| `SalesMailboxDetail` | 16 | — | smdid | UNAME |
| `SITEBUDGET` | 6 | — | sbId | SITE |
| `SITEVISITLOCATION` | 4 | — | SVID | — |

### assets  (28 tables)

Assets / configuration items. **ITEM** is the canonical asset table (Iid). DEVICE*, plus per-platform inventory.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `stocktrace` | 8 | — | STid | UNAME |
| `DEVICECHANGE` | 20 | — | pk | CONNCHANGE, USERS |
| `DEVICE` | 115 | — | Dsite, ddevnum | Downtime |
| `ITEM` | 125 | — | Iid | — |
| `DeviceApplications` | 16 | — | DAID | USERS |
| `ASSETFIELDCOLUMN` | 5 | — | AFid | UNAME |
| `STOCKHISTORY` | 15 | — | SHid | — |
| `StockBin` | 6 | — | STBID | SITE |
| `STOCKLEVEL` | 4 | — | SLid, SLlocation | — |
| `DEVICECONTRACT` | 8 | — | DCid | — |
| `STOCKLOCATION` | 4 | — | SCid | — |
| `AssetAttachmentMaint` | 7 | — | — | — |
| `AssetMeters` | 7 | — | AMid | — |
| `Certificate` | 7 | — | Cid | — |
| `DeviceChecklist` | 11 | — | DCDID, DCSeq | — |
| `DeviceChild` | 5 | — | DCID, DCCID | CONNCHANGE |
| `DeviceEnvironments` | 3 | — | deid | — |
| `DeviceLicence` | 3 | — | DLid | — |
| `DeviceLicense` | 6 | — | DLDID, DLLHID | — |
| `DEVICEMETER` | 22 | — | DMid | — |
| `DEVICEMETERCHANGE` | 5 | — | DMCid | — |
| `DEVICEMETERREADING` | 14 | — | DMRid | — |
| `DEVICEPARTS` | 10 | — | DPid | FAULTS |
| `DeviceRelationshipRestriction` | 4 | — | drrid | — |
| `DEVICEREVIEW` | 5 | — | DRdid, DRdseq | — |
| `StockChangeTrace` | 4 | — | id | — |
| `STOCKLEVELTEMPLATE` | 3 | — | LTid, LTitem | LoginToken |
| `STOCKLEVELTEMPLATEHEADER` | 2 | — | HTid | — |

### billing  (38 tables)

Invoices (**INVOICEHEADER** + INVOICEDETAIL), taxes, budgets, charges, currencies, quotes.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `InvoiceChange` | 11 | — | ICid | — |
| `INVOICEDETAIL` | 123 | — | IDid | CONTRACTHEADER, FAULTS, INVOICEHEADER |
| `INVOICEHEADER` | 157 | — | IHid | AREA, CONTRACTHEADER, FAULTS, SITE, USERS |
| `QUOTATIONDETAIL` | 62 | — | QDid | — |
| `InvoicePayment` | 25 | — | IPid | INVOICEHEADER |
| `InvoiceHeaderMerge` | 7 | — | IHMid | CONTRACTHEADER, INVOICEHEADER |
| `ORDERLINE` | 64 | — | OLid, OLseq | FAULTS |
| `QUOTATIONHEADER` | 87 | — | QHid | FAULTS, UNAME, USERS |
| `Tax` | 18 | — | TaxID | — |
| `ORDERHEAD` | 59 | — | OHid | CONTRACTHEADER, FAULTS, USERS |
| `CHARGERATE` | 27 | — | CRid | — |
| `BudgetType` | 4 | — | BTid | — |
| `CURRENCY` | 6 | — | Cid | — |
| `BillingAudit` | 5 | — | BAID | UNAME |
| `BillingPlanCriteria` | 10 | — | BPCid | ITEM |
| `BILLINGREPORT` | 21 | — | AIAreaid, AIseq | FAULTS |
| `CHARGECD` | 3 | — | Chargecode | — |
| `ChargeRateArea` | 4 | — | craid | AREA |
| `CurrencyHistory` | 4 | — | CHID | — |
| `GOODSINHEAD` | 6 | — | GHid | — |
| `InvoiceCreationTrace` | 5 | — | id | — |
| `INVOICECSVLAYOUT` | 7 | — | ILID, ILSeq | IntegrationLookUp |
| `InvoiceDetail_Metadata` | 1 | — | Idid | — |
| `InvoiceDetailAssetMeters` | 3 | — | IDAMid | — |
| `InvoiceDetailComponents` | 8 | — | IDCid | INVOICEHEADER |
| `InvoiceDetailMeterTiers` | 6 | — | IDMid | — |
| `InvoiceDetailProRata` | 16 | — | IDPRid | USERS |
| `InvoiceDetailQuantity` | 15 | — | IDQid | — |
| `InvoiceDetailQuantityCriteria` | 10 | — | idqcid | — |
| `InvoiceHeader_Metadata` | 1 | — | Ihid | — |
| `QUOTATIONDETAILTEMPLATE` | 24 | — | QDid | — |
| `QuotationHeaderPdf` | 4 | — | QHPid | ATTACHMENT |
| `RECEIPTNOTEDETAIL` | 8 | — | RNDID, RNDHID | — |
| `RECEIPTNOTEHEADER` | 11 | — | RNHID | FAULTS |
| `TaxRelation` | 2 | — | TaxId1, TaxId2 | Tax |
| `TaxRule` | 4 | — | TRLID | — |
| `TaxRuleConditions` | 6 | — | TRCID | — |
| `TaxRuleResult` | 11 | — | TRRid | — |

### contracts  (15 tables)

Recurring contracts (**CONTRACTHEADER** + CONTRACTDETAIL), plans, schedules, billing rules.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `CONTRACTPLAN` | 9 | — | CPid | — |
| `CONTRACTDETAIL` | 30 | — | CDid, CDseq | — |
| `CONTRACTHEADER` | 115 | — | CHid | FAULTS, USERS |
| `CONTRACT` | 19 | — | CNid | — |
| `ContractCI` | 3 | — | CCid | — |
| `ContractHeaderContract` | 3 | — | CHCid | CONTRACTHEADER |
| `CONTRACTPERIODHISTORY` | 8 | — | CPid, CPseq | — |
| `CONTRACTPLANHISTORY` | 4 | — | PHid, PHPeriod, PHBillingPlan | — |
| `ContractRule` | 10 | — | crlid | — |
| `ContractSchedule` | 7 | — | CSCHID, CSSeq | CONTRACTHEADER, UNAME |
| `ContractSchedulePlan` | 7 | — | CSPID | CONTRACTHEADER, UNAME |
| `ContractSite` | 3 | — | CSID | CONTRACTHEADER, SITE |
| `CONTRACTTEMPLATEDETAIL` | 30 | — | CTid2 | CONTRACTHEADER |
| `CONTRACTTEMPLATEHEADER` | 23 | — | CHid | — |
| `ContractUser` | 6 | — | CUid | CONTRACTHEADER, USERS |

### kb  (12 tables)

Knowledge base articles (**KBENTRY**), FAQs, related links.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `KBSECTION` | 2 | — | KBSkbid, KBSSDID | KBENTRY |
| `KBViewLog` | 5 | — | KBVLid | KBENTRY, USERS |
| `KBENTRYTAG` | 3 | — | KTid | KBENTRY |
| `KBENTRY` | 186 | — | id | FAULTS, UNAME |
| `KBOwner` | 4 | — | KOid | KBENTRY, UNAME |
| `KBVotes` | 8 | — | KBVid | KBENTRY, UNAME, USERS |
| `KbDevice` | 3 | — | KBDid | KBENTRY |
| `KbEntryAreaAccess` | 3 | — | KEAid | AREA, KBENTRY |
| `KBEntryFavourites` | 4 | — | KBFid | KBENTRY |
| `KbEntryTopLevelAccess` | 3 | — | KETLid | KBENTRY |
| `KbRelation` | 2 | — | KbId1, KbId2 | KBENTRY |
| `KBSearchLog` | 7 | — | KBSLid | — |

### projects  (9 tables)

Project plans, phases, milestones, risk register.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `RESOURCETIMELOG` | 8 | — | ID | FAULTS, UNAME |
| `Timesheet` | 5 | — | TSid | UNAME |
| `TimesheetApproval` | 7 | — | TSAId | UNAME |
| `TimesheetEvent` | 10 | — | TSEid | UNAME |
| `EXPENSE` | 16 | — | EXid | ACTIONS, FAULTS, INVOICEHEADER, UNAME |
| `MileStone` | 17 | — | MSTid | FAULTS |
| `MileStoneDependency` | 3 | — | MSTDid | — |
| `PROJECTNOTE` | 5 | — | PNid | — |
| `ProjectSetupLines` | 10 | — | PSLID | — |

### comms  (30 tables)

Appointments / calendar (**APPOINTMENT**), calls, chat, meetings, SMS.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `CALENDAR` | 15 | — | date_id | — |
| `APPOINTMENT` | 100 | — | APid | FAULTS, UNAME, USERS |
| `CALLLOG` | 25 | — | CLid | ACTIONS, FAULTS, UNAME, USERS |
| `MESSAGECONTENT` | 16 | — | MSid | — |
| `LIVECHATMSG` | 21 | — | LCMid, LCMchatid | ATTACHMENT, FAULTS |
| `NOTIFICATIONCONTENT` | 4 | — | NCid | — |
| `LIVECHATPARTICIPANT` | 17 | — | LCPid, LCPchatid | ATTACHMENT, UNAME, USERS |
| `LIVECHATHEADER` | 34 | — | LCHid | FAULTS |
| `LIVECHATONLINESTATUS` | 6 | — | LCOSid | UNAME, USERS |
| `NOTIFICATIONCONDITIONS` | 9 | — | NCid | — |
| `LiveChatAssignment` | 4 | — | LCAid | ATTACHMENT |
| `ChatInputSuggestion` | 5 | — | CISid | — |
| `CALLOUTCOME` | 28 | — | COid | — |
| `ChatProfile` | 55 | — | CPid | — |
| `Appointment_Metadata` | 1 | — | Apid | — |
| `AppointmentReminderEmails` | 4 | — | AREID | — |
| `AppointmentReminderSetup` | 2 | — | ARSID | — |
| `AppointmentTypeRequestType` | 3 | — | — | ATTACHMENT |
| `CALLHISTORY` | 7 | — | CAid | — |
| `ChatBanner` | 6 | — | CBid | — |
| `ChatMatchingData` | 5 | — | CMDid | — |
| `CHATMESSAGE` | 7 | — | CMid | UNAME |
| `ChatPopupMessage` | 4 | — | CPMid | — |
| `ChatStartMessage` | 4 | — | CSMid | — |
| `ChatStepQuestion` | 5 | — | CSQid | — |
| `ChatWaitMessage` | 4 | — | CWMid | — |
| `MessageContentVariable` | 4 | — | MVid | — |
| `NotificationLog` | 10 | — | NLid | — |
| `NotificationOutcome` | 6 | — | noid | — |
| `NotificationTimestamp` | 4 | — | NTunid, NTFaultid, NTUnum | FAULTS, UNAME |

### email  (21 tables)

Inbound (IncomingEmail) and outbound mail handling, mailboxes, templates, message bodies.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `OutgoingAttempt` | 8 | — | id | — |
| `IncomingEmail` | 22 | — | IEIdentity | — |
| `EMAILSTORE` | 31 | — | ESID | FAULTS, USERS |
| `ESCMSG` | 58 | — | pk | ACTIONS, FAULTS, USERS |
| `email` | 6 | — | id | — |
| `FORMATTEDEMAIL` | 3 | — | FMgroup, FMid | FaultMerge |
| `EMAILRULE` | 61 | — | ERid | — |
| `MAILBOX` | 101 | — | MBid | — |
| `MailboxCredential` | 38 | — | MCid | — |
| `MAILINGLISTTYPE` | 3 | — | MLTid | — |
| `EMAILCAMPAIGNDETAIL` | 4 | — | ECDid | — |
| `EMAILCAMPAIGNHEADER` | 3 | — | ECHid | — |
| `EMAILCAMPAIGNSTATUS` | 8 | — | ECSid | — |
| `EMAILRULEFIELDMAPPING` | 9 | — | ERFid | — |
| `MailboxSenderRestrictions` | 4 | — | MSRid | — |
| `MAILBOXTECHNICIAN` | 5 | — | MTid | UNAME |
| `MAILINGLIST` | 3 | — | MLid | — |
| `MAILINGLISTHISTORY` | 7 | — | MLHid | — |
| `MAILINGLISTRULE` | 10 | — | MLRid | — |
| `OutgoingEmail` | 15 | — | OEID | — |
| `ReleaseProductEmail` | 5 | — | RPErpid, RPErltid | — |

### auth  (66 tables)

Identity & access. **UNAME** = internal agents (Unum), **USERS** = end-users / contacts (Uid), NHD_IDENTITY_* = OAuth/OpenID tables, AgentLogin = login history.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `AgentLogin` | 5 | — | ALid | UNAME |
| `NHD_IDENTITY_Authorization` | 9 | — | Id | NHD_IDENTITY_Application |
| `USERS` | 216 | — | Uid | — |
| `NHD_IDENTITY_Token` | 12 | — | Id | NHD_IDENTITY_Application, NHD_IDENTITY_Authorization |
| `NHD_UserClaims` | 4 | — | Id | NHD_User |
| `NHD_RoleClaims` | 4 | — | Id | NHD_Roles |
| `AccessControl` | 11 | — | ACid | UNAME, USERS |
| `NHD_User` | 24 | — | ID | UNAME, USERS |
| `AgentCheckIn` | 4 | — | ACIid | UNAME |
| `ImpersonationRequest` | 9 | — | IRid | UNAME, USERS |
| `UNAMEANALYZER` | 3 | — | UAPUnum, UAPAPID | UNAME |
| `UserRoleLink` | 4 | — | URLid | USERS |
| `unamenotificationlink` | 5 | — | — | UNAME |
| `UNAMEFIELD` | 6 | — | ufid | UNAME |
| `UnameDepartment` | 6 | — | UDid | UNAME |
| `UNAMESECTION` | 14 | — | USID | UNAME |
| `NHD_Identity_ApplicationScope` | 3 | — | Id | — |
| `UNAMENOTIFICATION` | 38 | — | UNid | UNAME, USERS |
| `NHD_Tokens` | 5 | — | UserId, LoginProvider, Name | USERS |
| `NHD_UserRoles` | 3 | — | UserId, RoleId | NHD_Roles, NHD_User |
| `NHD_IDENTITY_Application` | 53 | — | Id | — |
| `UNAMETICKETHISTORY` | 5 | — | UTHID | FAULTS, UNAME |
| `UNAME` | 369 | — | Unum | — |
| `COMPANY` | 109 | — | Cnum | — |
| `UnameEventSubscription` | 7 | — | UESid | UNAME |
| `NHD_DeviceInfo` | 13 | — | Id | USERS |
| `NHD_Roles` | 8 | — | Id | — |
| `UnameAppointment` | 6 | — | upid | UNAME |
| `UnameCustom` | 9 | — | ucid | UNAME |
| `UNAMECALENDARFILTERS` | 8 | — | UCFID | UNAME |
| `PORTALRESETPASSWORD` | 6 | — | PRID | USERS |
| `UNAMEREQUESTTYPE` | 12 | — | URTid | UNAME |
| `UNAMETEMPLATE` | 139 | — | UTid | — |
| `NHD_Identity_ApplicationRole` | 3 | — | Id | — |
| `UserCompany` | 2 | — | company_id, user_id | COMPANY, USERS |
| `USERHASH` | 6 | — | UHID | USERS |
| `AccessToken` | 10 | — | Tid | CONTRACTHEADER, USERS |
| `ADFSIDS` | 4 | — | ADFSID | — |
| `AgentStatusChangeLog` | 6 | — | ASGLid | UNAME |
| `AgentStatusReassignMapping` | 4 | — | ASRMid | — |
| `APIAPPLICATION` | 3 | — | APIAid | — |
| `APIToken` | 4 | — | Id | — |
| `NHD_IDENTITY_Scope` | 7 | — | Id | — |
| `NHD_UserLogins` | 4 | — | LoginProvider, ProviderKey | NHD_User |
| `UNAMEACTIVITY` | 3 | — | UAnum, UAid | UserAction |
| `UNAMEAREA` | 2 | — | UAunum, UAPrefArea | UNAME |
| `UNAMEAREAPOPUP` | 5 | — | UAPOPid | UNAME |
| `UnameAreaRestriction` | 4 | — | — | UNAME |
| `UnameButton` | 5 | — | UBid | UNAME |
| `UnameCostTracking` | 6 | — | uctId | — |
| `UNAMEEXPENSERESTRICTION` | 2 | — | UEUnum, UERUnum | UNAME |
| `UNAMEHASH` | 6 | — | UNHID | UNAME |
| `UNAMEHIDEFIELD` | 3 | — | UHid | ITEM, UNAME |
| `UnameHolidayAllowance` | 5 | — | UHAid | — |
| `UnameIntegration` | 9 | — | UIid | UNAME |
| `UnameLoadBalanceLimit` | 5 | — | ULBLid | UNAME |
| `UNAMEORGANISATION` | 2 | — | UOUnum, UOORid | UNAME |
| `UnamePresenceRule` | 6 | — | UPRid | — |
| `UnamePresenceSubscription` | 13 | — | UPSid | UNAME |
| `UNAMEQUALIFICATION` | 5 | — | UQid | UNAME |
| `UNAMEREQUESTTYPEMOBILE` | 3 | — | URTMID | UNAME |
| `UnameSite` | 5 | — | USid | UNAME |
| `UnameStatusChange` | 3 | — | USCunum | — |
| `UnameStockLocation` | 2 | — | USLUnum, USLSCid | UNAME |
| `UNAMETOUTCOME` | 2 | — | UTUnum, UTOid | UNAME |
| `UNAMEXTYPE` | 8 | — | UXid | UNAME |

### workflow  (32 tables)

Approval processes, auto-assign, workflows, schedules, matching rules.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `TaskTrace` | 4 | — | id | — |
| `Automation` | 39 | — | id | FAULTS |
| `BackgroundTask` | 14 | — | id | — |
| `FLOWSUBDETAIL` | 22 | — | FSDID | — |
| `FLOWDETAIL` | 49 | — | FDFHID, FDSEQ, FDChatProfileId | — |
| `RaceCheck` | 4 | — | RCid | — |
| `FlowStages` | 6 | — | FSid | — |
| `AUTOASSIGNCRITERIA` | 31 | — | AACid | STDREQUEST, USERS |
| `AUTOASSIGN` | 52 | — | auid | UNAME |
| `FLOWHEADER` | 8 | — | FHID | — |
| `APPROVALPROCESSRULE` | 20 | — | ARid | — |
| `APPROVALPROCESS` | 5 | — | APid | — |
| `APPROVALPROCESSRULECRITERIA` | 10 | — | ARCid | — |
| `APPROVALPROCESSSTEP` | 55 | — | ASid | — |
| `AutomationVariable` | 6 | — | AVid | — |
| `Schedule` | 32 | — | SCHid | — |
| `AutoAssignOutcome` | 9 | — | AAOid | USERS |
| `ApprovalItems` | 8 | — | AISeq, AIFaultID | FAULTS |
| `ApprovalItemsStatus` | 8 | — | AISSeq, AISFaultID, AISFASeq, AISStatus | FAULTS |
| `ApprovalStore` | 9 | — | ASID | FAULTS |
| `ASSIGNSCHEDULE` | 11 | — | ASSid | — |
| `AutomationIteration` | 9 | — | AIid | — |
| `AutomationTicketCreationAudit` | 5 | — | ATCASTDID, ATCAID | STDREQUEST |
| `FlowDetailStatus` | 4 | — | fdsid | — |
| `FlowSubDetailRestriction` | 17 | — | fsdrid | — |
| `WORKFLOWACTION` | 4 | — | WAid | — |
| `WORKFLOWHEADER` | 4 | — | WFid | — |
| `WORKFLOWSTEP` | 4 | — | WSid | — |
| `WorkflowTarget` | 10 | — | WTid | — |
| `WorkflowTargetPriority` | 3 | — | wtpid | — |
| `WorkflowTargetStep` | 4 | — | WTsid | — |
| `WorkflowTargetTeam` | 3 | — | wttid | — |

### audit  (18 tables)

Audit trail, change history, logs, traces, events. AUDIT, AUDITFAULT, *History, *Change, PORTALLOG, Trace, EventData.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `PORTALLOG` | 10 | — | PLID | — |
| `TaskMonitorEvent` | 7 | — | id | — |
| `AUDIT` | 15 | — | ID | FAULTS, UNAME, USERS |
| `Trace` | 6 | — | id | — |
| `Feed` | 21 | — | Fid | AREA |
| `WorkflowHistory` | 15 | — | whid | ACTIONS, FAULTS |
| `EventData` | 4 | — | id | Event |
| `UserChange` | 12 | — | UCid | USERS |
| `Event` | 15 | — | id | UNAME, USERS |
| `ConfigCommit` | 19 | — | CCid | UNAME |
| `AUDITEVENT` | 7 | — | aid | — |
| `LicenceChange` | 8 | — | LCid | USERS |
| `PREPAYHISTORY` | 12 | — | ppid | AREA |
| `AuditPasswordField` | 6 | — | apfid | — |
| `CONNCHANGE` | 8 | — | DCID | — |
| `ItemStockHistory` | 16 | — | ISHid | CONTRACTHEADER, FAULTS, ITEM |
| `LICENCEHISTORY` | 12 | — | LHid | AREA |
| `TaskMonitor` | 5 | — | id | — |

### integrations  (65 tables)

Per-vendor integration tables — RMM (Datto, NinjaOne, NCentral, Atera, Auvik, Continuum, Kaseya, Syncro, Domotz, Addigy, Automate), PSA peers (ConnectWise, Autotask, ServiceNow, Freshdesk, Zendesk, Jira), monitoring (Splunk, Sentinel, NewRelic, Pagerduty, Orion, Splunk OnCall, OpsGenie), billing (Stripe, Sage, Xero, QuickBooks, Pax8, Chargebee), comms (Twilio, Slack, Teams, RingCentral), social (Twitter, Facebook), webhooks, OAuth, vendor alert tables.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `IntegratorTrace` | 3 | — | id | — |
| `IntegrationRequest` | 18 | — | IRID | — |
| `IncomingWebhookAttempt` | 6 | — | id | — |
| `Outgoing` | 14 | — | id | — |
| `WebhookEvent` | 17 | — | WHEid | — |
| `IncomingWebhook` | 17 | — | id | — |
| `AzureLicences` | 3 | — | ALid | — |
| `OutboundIntegrationMethodValue` | 18 | — | OIMVid | — |
| `IntegrationError` | 9 | — | IEid | — |
| `IntegrationConfiguration` | 23 | — | ICid | — |
| `AzureDelta` | 7 | — | ADid | — |
| `IntegrationFieldMapping` | 19 | — | IFMid | ITEM |
| `AzureADMapping` | 25 | — | AMid | — |
| `OutboundIntegrationMethod` | 15 | — | OIMid | ITEM |
| `Webhook` | 48 | — | WHid | — |
| `OutboundIntegrationMethodValueMapping` | 5 | — | OIMVMid | — |
| `OutboundIntegration` | 33 | — | OIid | — |
| `AzureADConnection` | 104 | — | ACid | — |
| `AddigyDetails` | 25 | — | AdgID | — |
| `DattoRmmDetails` | 31 | — | DRDid | — |
| `NCentralDetails` | 30 | — | NCDid | — |
| `PagerDutyMapping` | 9 | — | PMid | — |
| `Pax8Details` | 27 | — | PA8ID | — |
| `QuickBooksDetails` | 78 | — | QDid | — |
| `TwilioDetails` | 3 | — | tdid | — |
| `AdobeAcrobatDetails` | 13 | — | aadid | — |
| `AdobeCommerceDetails` | 10 | — | ACid | — |
| `AWSDetails` | 29 | — | Aid | — |
| `AzureADFilter` | 6 | — | AFid | — |
| `AzureADGrouping` | 4 | — | AGid | — |
| `AzureADMappingOld` | 14 | — | AMid | — |
| `AzureDevOpsDetails` | 34 | — | ADOid | — |
| `CiscoStates` | 3 | — | CSID | — |
| `CiscoTimestamps` | 4 | — | CTUnum, CTDatetime | UNAME |
| `DattoCommerceDetails` | 11 | — | DCDid | — |
| `FACEBOOKDETAILS` | 56 | — | FDid | UNAME, USERS |
| `GoogleBusinessDetails` | 15 | — | GBDid | — |
| `GoogleWorkplaceMapping` | 10 | — | GMid | — |
| `IntegrationDelta` | 7 | — | id | — |
| `IntegrationExport` | 12 | — | IEid | — |
| `IntegrationExportData` | 5 | — | IEdid | — |
| `IntegrationFieldData` | 6 | — | IFDid | — |
| `IntegrationFilter` | 12 | — | IFid | — |
| `IntegrationLookUp` | 5 | — | ILID | — |
| `IntegrationSiteMapping` | 11 | — | ISMid | — |
| `JiraDetails` | 26 | — | JDid | — |
| `JiraMappings` | 9 | — | JMid | — |
| `KaseyaVSAXDetails` | 30 | — | KVXid | — |
| `MicrosoftSubscriptionMapping` | 14 | — | MSMid | — |
| `MicrosoftTeamsDetails` | 8 | — | MTDid | UNAME |
| `MicrosoftTeamsMapping` | 7 | — | MTMid | — |
| `OutboundIntegrationCredential` | 6 | — | OICid | ITEM |
| `SageBusinessCloudDetails` | 18 | — | SBCDid | — |
| `SentinelOneDetails` | 26 | — | SODId | — |
| `SharePointFileLog` | 9 | — | SFLid | — |
| `ShopifyDetails` | 6 | — | SDid | — |
| `SlackChatApp` | 6 | — | SCAid | — |
| `SlackChatBlock` | 5 | — | SCBid | ATTACHMENT |
| `SLACKDETAILS` | 16 | — | SDid | UNAME |
| `SophosDetails` | 17 | — | SDid | — |
| `TeamsChatMessage` | 10 | — | TCMid | ATTACHMENT |
| `TwilioWhatsAppDetails` | 18 | — | TWADid | ADDRESSSTORE |
| `TwitterDetails` | 21 | — | TDid | USERS |
| `WebhookMapping` | 8 | — | WHMid | ITEM |
| `XeroDetails` | 53 | — | XDid | — |

### system  (79 tables)

Config, options, analyzer/report definitions, language packs, custom translations, PDF templates, theming.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `LANGUAGEPACKTRANSLATIONS` | 6 | — | LPTid | — |
| `ATTACHMENT` | 26 | — | ATid | UNAME, USERS |
| `DistributionListsLog` | 9 | — | DLLid | FAULTS, UNAME, USERS |
| `LOOKUP` | 18 | — | fid, fcode | — |
| `INFO` | 6 | — | Ikind, Isite, Inum, Iseq | — |
| `DocumentCreation` | 6 | — | DCid | UNAME |
| `SigningRequest` | 14 | — | SRid | USERS |
| `ATTACHMENTACTION` | 8 | — | pk | ACTIONS, ATTACHMENT, FAULTS |
| `FieldDataExtra` | 6 | — | Id | — |
| `DistributionListsUser` | 4 | — | DLUid | FAULTS, USERS |
| `ANALYZERPROFILECOLUMN` | 16 | — | APCid | — |
| `FIELDDISPLAY` | 9 | — | FDid | — |
| `UCOLUMN` | 6 | — | UCUserID, UCviewid, UCfieldid | USERS |
| `FIELDINFO` | 94 | — | FIid | — |
| `MODULESETUP` | 15 | — | MSid | — |
| `ANALYZERPROFILE` | 99 | — | APid | — |
| `KWORD` | 3 | — | Kword, Kwordid | — |
| `KINDEX` | 2 | — | Kiwordid, Kiid | ITEM |
| `CriteriaGroup` | 9 | — | cgid | — |
| `TabConfig` | 9 | — | TCid | ADDRESSBOOK |
| `Widget` | 69 | — | Wid | — |
| `PdfTemplatePage` | 11 | — | PDFTPid | — |
| `ViewColumnsDetails` | 10 | — | VCDid | — |
| `CUSTOMFIELDVISIBILITY` | 12 | — | cfvpk | CustomFieldValidation |
| `PdfTemplateDetail` | 7 | — | PDFTDid | — |
| `TYPEINFO` | 16 | — | Xkind, Xnum, Xseq | — |
| `ViewFilterDetails` | 10 | — | VFDid | — |
| `HOLIDAYS` | 18 | — | ID | — |
| `FIELDLIST` | 9 | — | FLtype, FLid | — |
| `FIELD` | 44 | — | Ykind, Yseq | QVFIELD |
| `PdfTemplate` | 47 | — | PDFTid | — |
| `AnalyzerProfileSeries` | 6 | — | APSid | — |
| `FIELDGROUP` | 19 | — | FGid | — |
| `PdfTemplateReport` | 3 | — | PDFTRid | — |
| `XTYPE` | 140 | — | TTypenum | — |
| `AnalyzerBookmark` | 3 | — | ABid | UNAME |
| `TAG` | 3 | — | TAGid | — |
| `ViewColumns` | 13 | — | VCid | UNAME |
| `LANGUAGEPACK` | 12 | — | LPid | — |
| `ViewFilter` | 8 | — | VFid | UNAME |
| `ANALYZERSUMMARYGROUP` | 4 | — | asgid | — |
| `ANALYZERFILTER` | 10 | — | AFid | — |
| `DistributionLists` | 11 | — | DLid | — |
| `CUSTOMTRANSLATION` | 4 | — | ctlid | — |
| `CUSTOMFIELDVALUERESTRICTIONS` | 6 | — | CFRid | — |
| `ANALYZERSUMMARYCOL` | 7 | — | ascid | — |
| `ANALYZERSUMMARYROW` | 6 | — | asrid | — |
| `AnalyzerProfileColour` | 6 | — | APCid | — |
| `Attachment_Metadata` | 1 | — | atid | — |
| `CUSTOMFIELDUPDATE` | 6 | — | CFUid | ITEM |
| `CustomFieldValidation` | 7 | — | CFVid | ITEM |
| `FieldData` | 15 | — | fdId | — |
| `FieldRoleRestriction` | 4 | — | FRRid | — |
| `LanguagePackTranslationsCustom` | 11 | — | LPTCid | — |
| `ReportBuilderElement` | 5 | — | RBEID | — |
| `ReportBuilderElementFilters` | 9 | — | RBEFID | — |
| `ReportBuilderElementSeries` | 9 | — | RBESID | — |
| `ReportBuilderElementSeriesFilters` | 9 | — | RBESFID | — |
| `ReportBuilderGroup` | 3 | — | RBGID | — |
| `ReportBuilderGroupAnalyzerProfile` | 4 | — | APID, RBGID, RBGAPType | ReportBuilderGroup |
| `ReportBuilderGroupFilters` | 9 | — | RBGFID | — |
| `ReportBuilderReport` | 3 | — | RBRID | — |
| `ReportBuilderReportLayout` | 9 | — | RBRLID | — |
| `REPORTDASHBOARD` | 4 | — | RDid | — |
| `REPORTDASHBOARDCONTENT` | 10 | — | RDCid | — |
| `ReportEvent` | 13 | — | REid | UNAME, USERS |
| `REPORTHEADER` | 5 | — | RHid | — |
| `REPORTPOSITION` | 7 | — | rpid, rpseq | releasepipeline |
| `TranslationLog` | 9 | — | id | — |
| `WidgetField` | 10 | — | WFID | ITEM |
| `WidgetOutcome` | 3 | — | WOID | — |
| `XTypeButton` | 3 | — | xtbid | — |
| `XTypeChecklist` | 9 | — | TCID | — |
| `XTYPEITEM` | 2 | — | TITypenum, TIiid | ITEM |
| `XTypeMapping` | 16 | — | XMid | — |
| `XTypeMappingCriteria` | 10 | — | xmcid | — |
| `XTypeRole` | 3 | — | xrid | — |
| `XTypeStatus` | 4 | — | xtsid | — |
| `XTypeStatusRestrictions` | 2 | — | xtsrxtsid, xtsrdstatus | — |

### other  (325 tables)

Miscellaneous tables that didn't match domain rules.

| Table | Cols | Rows | PK | FK targets |
|---|---:|---:|---|---|
| `SQLTime` | 6 | — | TID | — |
| `AIAssistantRequest` | 11 | — | AARid | — |
| `CATEGORYDETAIL` | 14 | — | CDid | — |
| `LoginScreenConfig` | 6 | — | LSCid | — |
| `QVQUERYFIELDS` | 7 | — | Qseq, Qkind, Qid | — |
| `GEOCOORD` | 5 | — | GCid | UNAME |
| `ViewLists` | 19 | — | VLid | UNAME |
| `QVFIELD` | 12 | — | Yseq | — |
| `SQLIMPORTFIELD` | 5 | — | SIFid | ITEM |
| `UserDepartment` | 7 | — | UDid | USERS |
| `QVLOOKUP` | 5 | — | fid, fcode | — |
| `TABNAME` | 14 | — | TNid | — |
| `RESTRICTION` | 5 | — | RSid | — |
| `TSTATUS` | 32 | — | Tstatus | — |
| `CUSTOMTABLE` | 23 | — | CTid2 | — |
| `CTNinjaOneScriptLibrary` | 5 | — | CTNinjaOneScriptLibraryid | — |
| `TIME` | 2 | — | Time1 | — |
| `NEWREQUESTCONFIG` | 6 | — | NRRequestType, NRFieldId | — |
| `SERVSTATUS` | 20 | — | ssuniqueid | FAULTS, SERVERSTATUS, SITE |
| `ServiceCategoryMapping` | 2 | — | svcmserviceid, svcmcategoryid | — |
| `SERVSITE` | 117 | — | starea, stsitenum, stid | — |
| `OidcImplicitState` | 7 | — | id | — |
| `ItemGroup` | 18 | — | IGid | — |
| `dashboardlinks` | 28 | — | DBLid | — |
| `LDAPNAME` | 6 | — | LDAPid | — |
| `TABLEDEFINITION` | 7 | — | tbid | — |
| `ServiceRequestDetails` | 16 | — | SRDid | — |
| `UserDashboardButtons` | 29 | — | UDBid | — |
| `Control5` | 8 | — | SettingId | — |
| `QVQUERY` | 21 | — | Srcseq | QVQUERYSQL |
| `SERVICEUSER` | 12 | — | SUid | USERS |
| `GENERIC` | 42 | — | Ggeneric | — |
| `TEMPDATA` | 2 | — | TDID | — |
| `POLICY` | 26 | — | ID | — |
| `RICHTEXTDATA` | 4 | — | RTXid | — |
| `JOURNEY` | 19 | — | JOid | ACTIONS, FAULTS, UNAME |
| `Licence` | 38 | — | LID | — |
| `PORTALLOGTYPE` | 2 | — | PLTID | — |
| `SERVICERESTRICTION` | 8 | — | SVRid | — |
| `USERANALYZER` | 9 | — | — | AREA, SITE, USERS |
| `SECURITYQUESTION` | 3 | — | SQid | — |
| `SERV` | 2 | — | svid | — |
| `SECTIONDETAIL` | 50 | — | SDid | — |
| `ViewListGroup` | 5 | — | VLGid | — |
| `SERVICECATALOG` | 20 | — | SGid | — |
| `QVQUERYSQL` | 4 | — | QSQLid | — |
| `TREE` | 22 | — | Treeid | — |
| `UserDashboardRestrictions` | 5 | — | — | — |
| `UVIEWDESC` | 7 | — | UVUserID, UVviewid | USERS |
| `CTNCentralScriptLibrary` | 8 | — | CTNCentralScriptLibraryid | — |
| `SetupTab` | 11 | — | STid | — |
| `WORKDAYS` | 29 | — | Wdid | — |
| `AiSuggestion` | 16 | — | AISid | — |
| `Mention` | 11 | — | Mid | ACTIONS, FAULTS, UNAME, USERS |
| `SERVICECATEGORY` | 13 | — | SVCid | — |
| `UserPrefs` | 3 | — | id | — |
| `DataProtectionKeys` | 3 | — | — | — |
| `RELEASETYPE` | 5 | — | RLTid | — |
| `SQLIMPORT` | 50 | — | SIid | — |
| `COSTCENTRES` | 7 | — | CCID | — |
| `QUALIFICATIONCATEGORY` | 9 | — | QCid | — |
| `SCRIPTLINE` | 11 | — | SLid | — |
| `SERVICECATALOGTYPE` | 2 | — | SCTid | — |
| `SLAHEAD` | 23 | — | Slid | — |
| `VirtualAgent` | 12 | — | VAid | — |
| `BookingType` | 11 | — | BTid | — |
| `CABMEMBER` | 9 | — | BMkey | UNAME, USERS |
| `CANNEDTEXT` | 14 | — | CTid2 | — |
| `CTAIAssessment_deleted_20230406094400` | 6 | — | CTAIAssessmentid | — |
| `DashboardFilter` | 7 | — | dfid | — |
| `FILES` | 10 | — | FKind, Fnum, Fseq, fnum2 | — |
| `GenericAccountsMappings` | 10 | — | GAMID | — |
| `METERTYPE` | 16 | — | MTid | — |
| `PARTSLOOKUPMAPPING` | 5 | — | PLMid | — |
| `QUALIFICATION` | 8 | — | QLid | — |
| `ScreenLayout` | 36 | — | SLid | — |
| `SERVERSTATUS` | 13 | — | ssid | — |
| `SQLTEXT` | 5 | — | SQLid | — |
| `UserRoles` | 25 | — | URid | — |
| `CABHEADER` | 9 | — | BHid | — |
| `CabRole` | 6 | — | CRid | — |
| `CANNEDTEXTPERMISSION` | 4 | — | CPid | — |
| `CHANGESEQ` | 40 | — | id | REQUESTTYPE |
| `CONTROL` | 808 | — | Rseq | — |
| `CONTROL2` | 827 | — | rseq | — |
| `CONTROL3` | 879 | — | rseq | — |
| `Control4` | 669 | — | rseq | — |
| `DashboardRestriction` | 5 | — | drid | — |
| `DlFilterDetails` | 6 | — | DLFDid | — |
| `Dynamics365CRMDetails` | 19 | — | DCDid | — |
| `GWorkspaceDetails` | 41 | — | GWDID | — |
| `HaloNewsRead` | 3 | — | HNRid | UNAME |
| `JamfDetails` | 24 | — | JDId | — |
| `LASTUSED` | 18 | — | Onerecord | — |
| `LDAPConnection` | 30 | — | LCID | — |
| `MOBILEINFO` | 20 | — | MBid | USERS |
| `NHServerConfig` | 66 | — | id | — |
| `PARTSLOOKUP` | 38 | — | PLID | — |
| `PartsLookupField` | 4 | — | plfid | — |
| `PrtgDetails` | 18 | — | PRTId | — |
| `RelatedItems` | 3 | — | RIid | — |
| `RELEASEPRODUCT` | 15 | — | RPid | — |
| `SCRIPTHEADER` | 4 | — | SCid | — |
| `SERVERSETTING` | 34 | — | SSTid | — |
| `SetupTabGroup` | 3 | — | STGid | — |
| `SnowDetails` | 24 | — | SDid | — |
| `USERADDRESS` | 2 | — | UAuid, UAasid | ADDRESSSTORE, USERS |
| `AmazonSellerDetails` | 11 | — | ASDid | — |
| `AnonRequestLog` | 5 | — | ARLid | USERS |
| `ArmisDetails` | 26 | — | amdid | — |
| `ArrowSphereDetails` | 11 | — | ASDid | — |
| `AuthGuid` | 4 | — | ID | — |
| `AvalaraDetails` | 21 | — | AVAid | — |
| `Bookmark` | 4 | — | Bid | UNAME |
| `BusinessCentralDetails` | 35 | — | BCDid | — |
| `BusinessCentralDimensions` | 19 | — | BCDID | — |
| `CannedTextFavourites` | 3 | — | CTFid | UNAME |
| `CannedTextTag` | 3 | — | cttid | — |
| `CannedTextUsage` | 6 | — | CTUid | ACTIONS, FAULTS, UNAME |
| `CATEGORYRESTRICTION` | 12 | — | CATRid | ATTACHMENT |
| `CHANGELG` | 19 | — | Chid | — |
| `CHECKIN` | 6 | — | CIid | FAULTS, SITE, UNAME |
| `ChildRequestType` | 3 | — | crtid | — |
| `ClaimLookup` | 2 | — | Id | — |
| `ConfirmClosure` | 7 | — | CCid | FAULTS |
| `ConfluenceDetails` | 10 | — | CDid | — |
| `CONN` | 25 | — | Dsite, Ddevnum | — |
| `ConnectedInstance` | 7 | — | Id | — |
| `CONSIGNMENTDETAIL` | 16 | — | CDid | — |
| `CONSIGNMENTHEADER` | 11 | — | CSid | FAULTS |
| `CONTACTGROUP` | 5 | — | CGid | — |
| `CONTACTGROUPCONTACTS` | 6 | — | CGCid | UNAME, USERS |
| `CONTRIBUTORS` | 7 | — | cbid | FAULTS, UNAME, USERS |
| `CSPConsumptionData` | 75 | — | haloid | USERS |
| `CSPInvoice` | 11 | — | haloid | — |
| `CSPSubscriptionPricing` | 21 | — | CSPSPid | USERS |
| `CSVTemplate` | 4 | — | CSVTid | — |
| `CSVTemplateDetail` | 7 | — | CSVTDid | — |
| `CustomButton` | 17 | — | CBid | — |
| `CustomButtonAudit` | 6 | — | CBAid | UNAME |
| `CustomQuery` | 3 | — | CQid | — |
| `DASHBOARD` | 11 | — | dbid | UNAME |
| `DASHBOARDPROFILE` | 4 | — | DPid | — |
| `DASHBOARDPROFILECOUNTER` | 5 | — | DPCid | — |
| `datatable` | 7 | — | DTID | — |
| `datefieldnotification` | 6 | — | dfnid | FAULTS, ITEM, UNAME |
| `DCRM` | 5 | — | — | — |
| `DISTRIBUTIONLIST` | 6 | — | DLid | — |
| `Downtime` | 9 | — | Did | FAULTS |
| `Dynamics365CRMFieldMapping` | 9 | — | DCFMId | — |
| `Dynamics365CRMSubTable` | 9 | — | dcstid | — |
| `DynatraceDetails` | 28 | — | DDid | — |
| `EcommerceOrder` | 12 | — | EOid | FAULTS |
| `EcommerceOrderFault` | 3 | — | EOFid | FAULTS |
| `EracentDetails` | 27 | — | EDid | — |
| `ETHADDR` | 6 | — | EAddress | — |
| `EventMapping` | 4 | — | EMid | ITEM |
| `EventRule` | 32 | — | ERid | STDREQUEST, USERS |
| `ExactDetails` | 21 | — | EXDid | — |
| `ExternalLink` | 28 | — | ELid | — |
| `FederatedCredential` | 7 | — | FCid | — |
| `ForecastDetails` | 31 | — | FDid | — |
| `ForecastEventData` | 6 | — | FEDid | — |
| `ForecastingBudget` | 3 | — | FBFaultid, FBDatetime | FAULTS |
| `ForecastOutputValue` | 5 | — | FOVid | — |
| `ForethoughtConfigItem` | 5 | — | FCIid | — |
| `ForethoughtDetails` | 3 | — | FDid | — |
| `FortnoxDetails` | 15 | — | FTNId | — |
| `GeoLocationRestriction` | 6 | — | GLRid | — |
| `GLCODES` | 2 | — | — | — |
| `GOODINLINE` | 6 | — | GLid, GLseq | — |
| `HaloDeviceInfo` | 11 | — | Id | USERS |
| `HaloNews` | 26 | — | HNid | — |
| `HistoricalTicketVolumeConfig` | 3 | — | HTVCid | — |
| `HistoricalTicketVolumes` | 5 | — | HTVid | — |
| `HTVConfig` | 3 | — | HTVCid | — |
| `HVUP` | 5 | — | HVid | — |
| `IframeTabRequestType` | 3 | — | itrid | — |
| `ImportCsv` | 5 | — | ICSid | — |
| `IncomingEvent` | 15 | — | IEid | — |
| `IndexFieldFilters` | 9 | — | iffid | — |
| `IngramMicroDetails` | 17 | — | IMDid | — |
| `IngramMicroResellerDetails` | 30 | — | IMRDid | — |
| `ItemAccountsLink` | 17 | — | IALId | — |
| `ITEMASSEMBLY` | 6 | — | IAid | — |
| `ItemBundleRestriction` | 3 | — | IBRid | — |
| `ItemDeviceDefaults` | 5 | — | IDDid | ITEM |
| `ItemGroupRestriction` | 3 | — | IGRid | — |
| `ITEMPRICE` | 4 | — | IPid | ITEM |
| `ITEMRESERVE` | 8 | — | IRid | ITEM |
| `ItemRestriction` | 3 | — | IRid | ITEM |
| `ItemsIssuedDetails` | 9 | — | IIDid | UNAME |
| `ItemStock` | 13 | — | ISid | FAULTS, ITEM |
| `ITEMSUPPLIER` | 10 | — | ISID | ITEM |
| `ItemTieredPricing` | 7 | — | itpid | ITEM |
| `KandjiDetails` | 19 | — | KDid | — |
| `KashflowDetails` | 17 | — | KDid | — |
| `KeyVault` | 5 | — | kvid | — |
| `LDAPSTRING` | 15 | — | LDid | — |
| `LicenceMatch` | 4 | — | LMid | — |
| `LicenceRole` | 4 | — | LRid | — |
| `LicenceUsage` | 5 | — | LUid | — |
| `LOCATION` | 4 | — | LID | FAULTS |
| `LoginToken` | 9 | — | LTid | — |
| `ManageEngineDetails` | 35 | — | MEid | — |
| `MAPPING` | 4 | — | mID | — |
| `MappingContinuum` | 5 | — | MCID | — |
| `MattermostChannelDetails` | 10 | — | MMCid | — |
| `MattermostDetails` | 8 | — | MMid | — |
| `METERPACK` | 10 | — | MPid | — |
| `MYOBDetails` | 38 | — | MYDid | — |
| `NHServerLogs` | 5 | — | NLID | — |
| `nhservertoken` | 4 | — | id | Attachment_Metadata |
| `ObjectMappingProfile` | 5 | — | Ompid | — |
| `OktaMapping` | 10 | — | Oid | — |
| `OnlineStatusSnapshot` | 4 | — | OSSid | — |
| `PartslookupAutoAssign` | 3 | — | PAid | USERS |
| `PartsLookupConfirmation` | 10 | — | plcid | FAULTS, USERS |
| `Permalinks` | 6 | — | pid | — |
| `PHONECALLLOG` | 3 | — | PLid | — |
| `PlanningToolHours` | 4 | — | PTHID | FAULTS |
| `PortalAuth` | 8 | — | PAguid | PartslookupAutoAssign |
| `PowerShellScript` | 15 | — | PSSID | — |
| `PowerShellScriptCriteria` | 7 | — | PSCPSSID, PSCID | ITEM |
| `PowerShellScriptProcessing` | 10 | — | PSPID | FAULTS |
| `PRICE` | 3 | — | Pid, Pband | — |
| `PRODUCTNUMBERS` | 2 | — | — | — |
| `PublishProfileLink` | 3 | — | pplid | — |
| `PublishProfiles` | 8 | — | ppid | — |
| `QUERY` | 27 | — | Srcseq | — |
| `RaynetDetails` | 24 | — | RNid | — |
| `RelatedFaults` | 3 | — | rfid | — |
| `RELEASE` | 20 | — | RLid | — |
| `ReleaseBranch` | 4 | — | RBid | — |
| `RELEASECOMPONENT` | 3 | — | RCid | — |
| `ReleaseNoteGroup` | 3 | — | — | — |
| `releasepipeline` | 8 | — | rpid | — |
| `REMOTEMAPPING` | 4 | — | RMid | — |
| `REMOTESESSIONDATA` | 35 | — | RSDid | ACTIONS, FAULTS, UNAME, USERS |
| `RemoteSessionTeams` | 7 | — | RSTid | — |
| `REMOTESESSIONTECHQUEUE` | 4 | — | RSTQid | — |
| `RES00046` | 24 | — | — | — |
| `RES00047` | 24 | — | — | — |
| `RES00049` | 25 | — | — | — |
| `RFQDETAIL` | 10 | — | RDid | — |
| `RFQHEADER` | 4 | — | RFid | — |
| `RMAHEADER` | 9 | — | RMid | FAULTS |
| `RMALINE` | 7 | — | RLid | — |
| `RoundRobinLog` | 6 | — | RRLid | FAULTS |
| `RuleOLA` | 3 | — | ROid | USERS |
| `SailPointDetails` | 25 | — | id | — |
| `SailPointRoleMapping` | 5 | — | id | — |
| `SailPointUserMapping` | 13 | — | id | — |
| `SavedForecast` | 9 | — | SFid | — |
| `SCRIPTCHOICE` | 5 | — | SXid | — |
| `SECTIONEMAILTEMPLATEGROUPS` | 2 | — | SETGSection, SETGFCode | — |
| `SectionLoadBalanceLimit` | 4 | — | SLBLid | — |
| `SectionRequestType` | 4 | — | SRid | — |
| `SecureSecretLink` | 8 | — | sslid | — |
| `SERVERERROR` | 7 | — | SEID | ITEM |
| `ServiceAvailability` | 9 | — | SAid | — |
| `SERVICECATALOGLINES` | 4 | — | SLid | ITEM |
| `ServiceCategoryRestriction` | 3 | — | SCRID | — |
| `SERVICEDEVICE` | 3 | — | SDid | StdrequestDateDependencies |
| `SERVICELINKS` | 3 | — | SVLid | — |
| `ServiceMapping` | 7 | — | SMid | — |
| `ServiceOption` | 8 | — | SOid | — |
| `ServSiteTag` | 3 | — | sstid | — |
| `SingleSignOnApplication` | 29 | — | id | — |
| `SingleSignOnAttempt` | 8 | — | id | — |
| `SnipeITDetails` | 21 | — | SDid | — |
| `SPOTLIGHT` | 3 | — | spotID | FAULTS |
| `SQLREPORT` | 6 | — | SQid | — |
| `STATUSMAPPING` | 4 | — | SMid | — |
| `StreamOneIonDetails` | 14 | — | SOIDid | — |
| `StyleProfile` | 6 | — | SPid | — |
| `StyleProfileLink` | 4 | — | SPLid | — |
| `StyleProfileRule` | 5 | — | SPRid | — |
| `SUPPLIERORDERDETAIL` | 36 | — | SDid | — |
| `SUPPLIERORDERHEADER` | 71 | — | SHid | FAULTS, USERS |
| `SUPPLIERORDERINVOICEDETAIL` | 11 | — | SOIDID | INVOICEHEADER |
| `SUPPLIERORDERINVOICEHEADER` | 7 | — | SOIHID | UNAME |
| `SuppLog` | 6 | — | SLid | — |
| `SuppToken` | 8 | — | STid | — |
| `SynnexDetails` | 16 | — | SYDid | — |
| `TabOrder` | 4 | — | TOTabID, TOType, TOTypeID, TOSeq | ADDRESSBOOK |
| `TaniumDetails` | 31 | — | TDid | — |
| `TASK` | 10 | — | TAid | FAULTS, UNAME |
| `TechDataResellerDetails` | 11 | — | TDRDid | — |
| `TECHPARTSLOOKUP` | 4 | — | TPLID | UNAME |
| `TEMPREP1` | 4 | — | rsite, ritemno | — |
| `TEMPREPORTS` | 9 | — | — | AREA, USERS |
| `TenableDetails` | 21 | — | TDid | — |
| `TIMER` | 7 | — | TMID | ACTIONS, FAULTS, UNAME |
| `Timeslot` | 8 | — | TSid | — |
| `ToDoGroup` | 4 | — | TDGid | — |
| `TranscriptionStore` | 6 | — | TSid | ATTACHMENT |
| `TreeRequestType` | 2 | — | TRTTreeID, TRTRTID | — |
| `Tweets` | 14 | — | Tid | USERS |
| `TYPEPARTS` | 4 | — | TPid | — |
| `UNDERTAKINGOFWORK` | 5 | — | UWfaultid | — |
| `UnsubEmailServiceUsers` | 3 | — | UESUid | USERS |
| `UPDATES` | 24 | — | Rseq | — |
| `UserAction` | 8 | — | UAid | ACTIONS |
| `USERDEVICE` | 6 | — | — | USERS |
| `UserDeviceRole` | 5 | — | udrid | USERS |
| `USERMAILINGLISTTYPE` | 3 | — | UMLTid | USERS |
| `USERPOLICY` | 3 | — | upuid, upslaid, uppolicy | USERS |
| `UserRestriction` | 2 | — | URuid, URRestrictionUID | USERS |
| `UserRoleMapping` | 5 | — | id | — |
| `UserRoleRules` | 7 | — | URRId | — |
| `UserThirdPartyGroup` | 3 | — | UTGid | — |
| `VariableError` | 6 | — | ID | — |
| `ViewLog` | 5 | — | VLid | — |
| `VirimaDetails` | 27 | — | VDid | — |
| `VirtualAgentFunction` | 15 | — | VAFid | — |
| `VirtualAgentFunctionParameter` | 6 | — | VAFPid | — |
| `VMWorkspaceDetails` | 22 | — | VMWid | — |
| `WORDDOC` | 6 | — | WDid | — |
| `WORDDOCSECTION` | 2 | — | WDSwdid, WDSsection | — |
| `WordDocXType` | 2 | — | WDTWDid, WDTTTypenum | — |
| `WordpressDetails` | 10 | — | WDid | — |
| `WordpressOrgDetails` | 14 | — | WDOid | — |
| `WorkdayBreak` | 12 | — | WBid | — |
| `XMLInvoiceLayout` | 10 | — | XILID | — |
