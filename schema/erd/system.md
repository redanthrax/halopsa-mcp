# system — ERD

Config, options, analyzer/report definitions, language packs, custom translations, PDF templates, theming.

79 tables in this domain (showing up to 60 by row count). PK = primary key, FK = foreign key.

```mermaid
erDiagram
    LANGUAGEPACKTRANSLATIONS {
        int LPTid PK
    }
    ATTACHMENT {
        int ATid PK
        int aaunum FK
        int aauserid FK
        nvarchar ATFilename
        datetime ATDateCreated
        nvarchar ATDesc
    }
    DistributionListsLog {
        int DLLid PK
        int DLLuid FK
        int DLLunum FK
        int DLLfaultid FK
    }
    LOOKUP {
        int fid PK
        int fcode PK
    }
    INFO {
        nvarchar Ikind PK
        int Isite PK
        float Inum PK
        float Iseq PK
    }
    DocumentCreation {
        int DCid PK
        int DCunum FK
    }
    SigningRequest {
        int SRid PK
        int SRuserId FK
        datetime SRrequestdate
        nvarchar SRsignedbyname
        nvarchar SRsignedbyemail
    }
    ATTACHMENTACTION {
        int AAATID FK
        int AAfaultid FK
        int AAActionNumber FK
        bigint pk PK
    }
    FieldDataExtra {
        int Id PK
    }
    DistributionListsUser {
        int DLUid PK
        int DLUuid FK
        int DLUfaultid FK
    }
    ANALYZERPROFILECOLUMN {
        int APCid PK
        nvarchar APCname
        int APCgroupbystatus
        bit APCSortDesc
    }
    FIELDDISPLAY {
        int FDid PK
    }
    UCOLUMN {
        int UCUserID PK
        int UCviewid PK
        int UCfieldid PK
    }
    FIELDINFO {
        int FIid PK
        nvarchar FIName
        nvarchar FIJiraFieldName
        bit ficopytochildonupdate
    }
    MODULESETUP {
        int MSid PK
        nvarchar MSName
        int MSstatus
        int MSScreenName
    }
    ANALYZERPROFILE {
        int APid PK
        nvarchar APTitle
        datetime APReportingPeriodstartdate
        datetime APReportingPeriodenddate
    }
    KWORD {
        nvarchar Kword PK
        int Kwordid PK
    }
    KINDEX {
        int Kiwordid PK
        int Kiid PK
    }
    CriteriaGroup {
        int cgid PK
        nvarchar cgdesc
    }
    TabConfig {
        int TCid PK
        int TCtabid FK
    }
    Widget {
        int Wid PK
        nvarchar Wtitle
        nvarchar WcolumnName
        nvarchar Wdatefilterfieldname
    }
    PdfTemplatePage {
        int PDFTPid PK
        nvarchar PDFTPname
    }
    ViewColumnsDetails {
        int VCDid PK
        bit VCDorderdesc
        int vcgroupbystatus
    }
    CUSTOMFIELDVISIBILITY {
        int CFVid FK
        int cfvpk PK
    }
    PdfTemplateDetail {
        int PDFTDid PK
        nvarchar PDFTDtitle
    }
    TYPEINFO {
        nvarchar Xkind PK
        float Xnum PK
        float Xseq PK
        nvarchar XGroupName
    }
    ViewFilterDetails {
        int VFDid PK
        nvarchar VFDfiltername
    }
    HOLIDAYS {
        uniqueidentifier ID PK
        datetime Hdate
        nvarchar hdesc
        datetime henddate
    }
    FIELDLIST {
        int FLtype PK
        int FLid PK
        nvarchar FLdbname
        nvarchar FLdisplayname
        nvarchar FLlookupdesc
    }
    FIELD {
        nvarchar Ykind PK
        int Yseq PK
        nvarchar Yname
        nvarchar Yvalidate
        nvarchar Ysqldatabasename
    }
    PdfTemplate {
        int PDFTid PK
        nvarchar PDFTname
        nvarchar pdftDesc
        nvarchar pdftlicencename
    }
    AnalyzerProfileSeries {
        int APSid PK
    }
    FIELDGROUP {
        int FGid PK
        nvarchar FGname
        nvarchar FGsectiondesc
        nvarchar FGtitle
    }
    PdfTemplateReport {
        int PDFTRid PK
    }
    XTYPE {
        smallint TTypenum PK
        nvarchar tdesc
        bit Thidebmpdesc
        nvarchar TItemCode
    }
    AnalyzerBookmark {
        int ABid PK
        int ABunum FK
    }
    TAG {
        int TAGid PK
        nvarchar TAGname
    }
    ViewColumns {
        int VCid PK
        int VCunum FK
        nvarchar VCdesc
        nvarchar vccalendareventtitle
    }
    LANGUAGEPACK {
        int LPid PK
        nvarchar LPFullName
        nvarchar LPShortName
        nvarchar LPLinkedPackCode
    }
    ViewFilter {
        int VFid PK
        int VFunum FK
        nvarchar VFdesc
    }
    ANALYZERSUMMARYGROUP {
        int asgid PK
        nvarchar asgname
        nvarchar asgfieldname
    }
    ANALYZERFILTER {
        int AFid PK
        nvarchar AFFieldName
    }
    DistributionLists {
        int DLid PK
        nvarchar DLname
        nvarchar DLdesc
    }
    CUSTOMTRANSLATION {
        int ctlid PK
    }
    CUSTOMFIELDVALUERESTRICTIONS {
        int CFRid PK
    }
    ANALYZERSUMMARYCOL {
        int ascid PK
    }
    ANALYZERSUMMARYROW {
        int asrid PK
    }
    AnalyzerProfileColour {
        int APCid PK
        nvarchar APCName
    }
    Attachment_Metadata {
        bigint atid PK
    }
    CUSTOMFIELDUPDATE {
        int CFUid PK
        nvarchar CFUfiid FK
    }
    CustomFieldValidation {
        int CFVid PK
        int CFVFIid FK
    }
    FieldData {
        int fdId PK
        datetime fdLastUpdated
    }
    FieldRoleRestriction {
        int FRRid PK
    }
    LanguagePackTranslationsCustom {
        int LPTCid PK
        nvarchar LPTCfieldname
        datetime LPTCtranslationDate
    }
    ReportBuilderElement {
        int RBEID PK
        nvarchar RBETitle
    }
    ReportBuilderElementFilters {
        int RBEFID PK
    }
    ReportBuilderElementSeries {
        int RBESID PK
    }
    ReportBuilderElementSeriesFilters {
        int RBESFID PK
    }
    ReportBuilderGroup {
        int RBGID PK
        nvarchar RBGTitle
    }
    ReportBuilderGroupAnalyzerProfile {
        int APID PK
        int RBGID PK
        int RBGAPType PK
        nvarchar RBGAPTitle
    }
    ATTACHMENT ||--o{ ATTACHMENTACTION : "AAATID"
    CustomFieldValidation ||--o{ CUSTOMFIELDVISIBILITY : "CFVid"
    ReportBuilderGroup ||--o{ ReportBuilderGroupAnalyzerProfile : "RBGID"
```
