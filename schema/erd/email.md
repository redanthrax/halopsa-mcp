# email — ERD

Inbound (IncomingEmail) and outbound mail handling, mailboxes, templates, message bodies.

21 tables in this domain (showing up to 60 by row count). PK = primary key, FK = foreign key.

```mermaid
erDiagram
    OutgoingAttempt {
        int id PK
        int status
        datetime attemptdate
        nvarchar errorcode
    }
    IncomingEmail {
        int IEIdentity PK
        datetime IEDateCreated
        int IEStatus
        datetime IELastAttemptDate
    }
    EMAILSTORE {
        int ESID PK
        int ESFaultID FK
        int ESUid FK
        datetime esdatecreated
    }
    ESCMSG {
        int EMuserid FK
        int EMfaultid FK
        int EMactionnumber FK
        int emuid FK
        bigint pk PK
        int EMreadstatus
        datetime EMdate
        int EMemailstatus
    }
    email {
        int id PK
        datetime sentdate
    }
    FORMATTEDEMAIL {
        int FMgroup PK
        int FMid PK
    }
    EMAILRULE {
        int ERid PK
        nvarchar ERDesc
        nvarchar ERfieldname
        nvarchar ER2fieldname
    }
    MAILBOX {
        int MBid PK
        nvarchar MBusername
        nvarchar MBExchangeMailBoxDisplayName
        nvarchar MBsmtpusername
    }
    MailboxCredential {
        int MCid PK
        nvarchar MCusername
        nvarchar MCsmtpdisplayname
        nvarchar MCsmtpusername
    }
    MAILINGLISTTYPE {
        int MLTid PK
        nvarchar MLTdesc
    }
    EMAILCAMPAIGNDETAIL {
        int ECDid PK
    }
    EMAILCAMPAIGNHEADER {
        int ECHid PK
        nvarchar ECHdesc
    }
    EMAILCAMPAIGNSTATUS {
        int ECSid PK
        datetime ECSLastEmailDate
    }
    EMAILRULEFIELDMAPPING {
        int ERFid PK
    }
    MailboxSenderRestrictions {
        int MSRid PK
    }
    MAILBOXTECHNICIAN {
        int MTid PK
        int MTunum FK
    }
    MAILINGLIST {
        int MLid PK
        nvarchar MLName
    }
    MAILINGLISTHISTORY {
        int MLHid PK
    }
    MAILINGLISTRULE {
        int MLRid PK
    }
    OutgoingEmail {
        int OEID PK
    }
    ReleaseProductEmail {
        int RPErpid PK
        int RPErltid PK
    }
```
