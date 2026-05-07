# assets — ERD

Assets / configuration items. **ITEM** is the canonical asset table (Iid). DEVICE*, plus per-platform inventory.

28 tables in this domain (showing up to 60 by row count). PK = primary key, FK = foreign key.

```mermaid
erDiagram
    stocktrace {
        int STid PK
        int STunum FK
        datetime STdate
    }
    DEVICECHANGE {
        int DCID FK
        int dcuserid FK
        bigint pk PK
        nvarchar dcfieldname
    }
    DEVICE {
        int Dsite PK
        int ddevnum PK
        int Did FK
        datetime Dlastchangeofvaluedate
        datetime DWarrantyStartDate
        datetime DWarrantyEndDate
    }
    ITEM {
        int Iid PK
        int Istatus
        nvarchar idesc
        int Itaxcode
    }
    DeviceApplications {
        int DAID PK
        int dauserid FK
        nvarchar DADesc
        nvarchar DABundleDesc
        datetime DAInstalledDate
    }
    ASSETFIELDCOLUMN {
        int AFid PK
        int AFUnum FK
    }
    STOCKHISTORY {
        int SHid PK
        datetime SHdate
    }
    StockBin {
        int STBID PK
        int STBSsitenum FK
        nvarchar STBName
    }
    STOCKLEVEL {
        int SLid PK
        int SLlocation PK
    }
    DEVICECONTRACT {
        int DCid PK
        datetime DClastchangeofvaluedate
        datetime dcenddate
    }
    STOCKLOCATION {
        int SCid PK
        nvarchar SCDesc
    }
    AssetAttachmentMaint {
        nvarchar ATMdesc
        nvarchar ATMfilename
    }
    AssetMeters {
        int AMid PK
        nvarchar AMmetername
    }
    Certificate {
        int Cid PK
        nvarchar Cname
    }
    DeviceChecklist {
        int DCDID PK
        int DCSeq PK
        int DCStatus
    }
    DeviceChild {
        int DCID PK
        int DCCID PK
    }
    DeviceEnvironments {
        int deid PK
    }
    DeviceLicence {
        int DLid PK
    }
    DeviceLicense {
        int DLDID PK
        int DLLHID PK
        int DLStatus
        datetime DLInstalldate
    }
    DEVICEMETER {
        int DMid PK
        nvarchar DMname
        datetime DMinstalldate
        datetime DMlastreadingdate
    }
    DEVICEMETERCHANGE {
        int DMCid PK
        nvarchar DMCdesc
    }
    DEVICEMETERREADING {
        int DMRid PK
        nvarchar DMRmetername
    }
    DEVICEPARTS {
        int DPid PK
        int DPfaultid FK
        int DPmetercurramount
        datetime DPstartdate
        datetime DPenddate
    }
    DeviceRelationshipRestriction {
        int drrid PK
    }
    DEVICEREVIEW {
        int DRdid PK
        int DRdseq PK
    }
    StockChangeTrace {
        int id PK
    }
    STOCKLEVELTEMPLATE {
        int LTid PK
        int LTitem PK
    }
    STOCKLEVELTEMPLATEHEADER {
        int HTid PK
        nvarchar HTdesc
    }
```
