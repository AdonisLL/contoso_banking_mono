-- Contoso Legacy Bank - core schema
-- All business rules that touch money live in stored procedures (StoredProcedures_*.sql),
-- not in application code. Tables are intentionally simple record stores.
USE ContosoLegacyBank;
GO

IF OBJECT_ID(N'dbo.LoanPayments', N'U') IS NOT NULL DROP TABLE dbo.LoanPayments;
IF OBJECT_ID(N'dbo.LoanSchedules', N'U') IS NOT NULL DROP TABLE dbo.LoanSchedules;
IF OBJECT_ID(N'dbo.Loans', N'U') IS NOT NULL DROP TABLE dbo.Loans;
IF OBJECT_ID(N'dbo.LoanProducts', N'U') IS NOT NULL DROP TABLE dbo.LoanProducts;
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NOT NULL DROP TABLE dbo.Notifications;
IF OBJECT_ID(N'dbo.NotificationTemplates', N'U') IS NOT NULL DROP TABLE dbo.NotificationTemplates;
IF OBJECT_ID(N'dbo.BatchImportRows', N'U') IS NOT NULL DROP TABLE dbo.BatchImportRows;
IF OBJECT_ID(N'dbo.BatchImportBatches', N'U') IS NOT NULL DROP TABLE dbo.BatchImportBatches;
IF OBJECT_ID(N'dbo.Statements', N'U') IS NOT NULL DROP TABLE dbo.Statements;
IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL DROP TABLE dbo.Transactions;
IF OBJECT_ID(N'dbo.Accounts', N'U') IS NOT NULL DROP TABLE dbo.Accounts;
IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL DROP TABLE dbo.Customers;
GO

CREATE TABLE dbo.Customers
(
    CustomerId      INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    CustomerNumber  VARCHAR(10)         NOT NULL,
    FirstName       NVARCHAR(100)       NOT NULL,
    LastName        NVARCHAR(100)       NOT NULL,
    Email           NVARCHAR(256)       NOT NULL,
    Phone           VARCHAR(20)         NULL,
    DateOfBirth     DATE                NOT NULL,
    CreatedUtc      DATETIME2(0)        NOT NULL CONSTRAINT DF_Customers_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Customers_CustomerNumber UNIQUE (CustomerNumber)
);
GO

CREATE TABLE dbo.Accounts
(
    AccountId       INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    AccountNumber   VARCHAR(12)         NOT NULL,
    CustomerId      INT                 NOT NULL CONSTRAINT FK_Accounts_Customers REFERENCES dbo.Customers (CustomerId),
    AccountType     VARCHAR(20)         NOT NULL, -- Checking | Savings
    Status          VARCHAR(20)         NOT NULL CONSTRAINT DF_Accounts_Status DEFAULT ('Active'), -- Active | Frozen | Closed
    Balance         DECIMAL(18,2)       NOT NULL CONSTRAINT DF_Accounts_Balance DEFAULT (0),
    OverdraftLimit  DECIMAL(18,2)       NOT NULL CONSTRAINT DF_Accounts_OverdraftLimit DEFAULT (0),
    InterestRate    DECIMAL(9,6)        NOT NULL CONSTRAINT DF_Accounts_InterestRate DEFAULT (0),
    OpenedUtc       DATETIME2(0)        NOT NULL CONSTRAINT DF_Accounts_OpenedUtc DEFAULT (SYSUTCDATETIME()),
    ClosedUtc       DATETIME2(0)        NULL,
    RowVersion      ROWVERSION          NOT NULL,
    CONSTRAINT UQ_Accounts_AccountNumber UNIQUE (AccountNumber)
);
GO

CREATE TABLE dbo.Transactions
(
    TransactionId        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Transactions PRIMARY KEY,
    AccountId            INT                   NOT NULL CONSTRAINT FK_Transactions_Accounts REFERENCES dbo.Accounts (AccountId),
    TransactionType      VARCHAR(20)           NOT NULL, -- Deposit | Withdrawal | TransferIn | TransferOut | Fee | Interest
    Amount               DECIMAL(18,2)         NOT NULL,
    BalanceAfter          DECIMAL(18,2)         NOT NULL,
    Description          NVARCHAR(200)         NULL,
    ExternalId           VARCHAR(50)           NULL, -- batch/dedupe correlation id
    RelatedTransactionId BIGINT                NULL CONSTRAINT FK_Transactions_Related REFERENCES dbo.Transactions (TransactionId),
    CreatedUtc           DATETIME2(0)          NOT NULL CONSTRAINT DF_Transactions_CreatedUtc DEFAULT (SYSUTCDATETIME())
);
GO
CREATE INDEX IX_Transactions_AccountId_CreatedUtc ON dbo.Transactions (AccountId, CreatedUtc);
CREATE UNIQUE INDEX UQ_Transactions_ExternalId ON dbo.Transactions (ExternalId) WHERE ExternalId IS NOT NULL;
GO

CREATE TABLE dbo.Statements
(
    StatementId     INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Statements PRIMARY KEY,
    AccountId       INT                 NOT NULL CONSTRAINT FK_Statements_Accounts REFERENCES dbo.Accounts (AccountId),
    PeriodStart     DATE                NOT NULL,
    PeriodEnd       DATE                NOT NULL,
    Status          VARCHAR(20)         NOT NULL CONSTRAINT DF_Statements_Status DEFAULT ('Requested'), -- Requested|Processing|Completed|Failed
    RequestedUtc    DATETIME2(0)        NOT NULL CONSTRAINT DF_Statements_RequestedUtc DEFAULT (SYSUTCDATETIME()),
    CompletedUtc    DATETIME2(0)        NULL,
    FilePath        NVARCHAR(400)       NULL,
    FailureReason   NVARCHAR(400)       NULL
);
GO

CREATE TABLE dbo.BatchImportBatches
(
    BatchId         INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_BatchImportBatches PRIMARY KEY,
    FileName        NVARCHAR(260)       NOT NULL,
    ImportedUtc     DATETIME2(0)        NOT NULL CONSTRAINT DF_BatchImportBatches_ImportedUtc DEFAULT (SYSUTCDATETIME()),
    TotalRows       INT                 NOT NULL CONSTRAINT DF_BatchImportBatches_TotalRows DEFAULT (0),
    SuccessCount    INT                 NOT NULL CONSTRAINT DF_BatchImportBatches_SuccessCount DEFAULT (0),
    DuplicateCount  INT                 NOT NULL CONSTRAINT DF_BatchImportBatches_DuplicateCount DEFAULT (0),
    ErrorCount      INT                 NOT NULL CONSTRAINT DF_BatchImportBatches_ErrorCount DEFAULT (0),
    ReportPath      NVARCHAR(400)       NULL
);
GO

CREATE TABLE dbo.BatchImportRows
(
    BatchImportRowId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BatchImportRows PRIMARY KEY,
    BatchId           INT                   NOT NULL CONSTRAINT FK_BatchImportRows_Batches REFERENCES dbo.BatchImportBatches (BatchId),
    RowNumber         INT                   NOT NULL,
    ExternalId        VARCHAR(50)           NOT NULL,
    AccountNumber     VARCHAR(12)           NOT NULL,
    Amount            DECIMAL(18,2)         NOT NULL,
    TransactionType   VARCHAR(20)           NOT NULL,
    Status            VARCHAR(20)           NOT NULL, -- Posted | Duplicate | Error
    ErrorMessage      NVARCHAR(400)         NULL
);
GO

CREATE TABLE dbo.LoanProducts
(
    LoanProductId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoanProducts PRIMARY KEY,
    ProductCode         VARCHAR(20)        NOT NULL,
    Name                NVARCHAR(100)      NOT NULL,
    AnnualInterestRate  DECIMAL(9,6)       NOT NULL,
    MinTermMonths       INT                NOT NULL,
    MaxTermMonths       INT                NOT NULL,
    MinAmount           DECIMAL(18,2)      NOT NULL,
    MaxAmount           DECIMAL(18,2)      NOT NULL,
    CONSTRAINT UQ_LoanProducts_ProductCode UNIQUE (ProductCode)
);
GO

CREATE TABLE dbo.Loans
(
    LoanId                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Loans PRIMARY KEY,
    LoanNumber            VARCHAR(12)        NOT NULL,
    CustomerId            INT                NOT NULL CONSTRAINT FK_Loans_Customers REFERENCES dbo.Customers (CustomerId),
    LoanProductId         INT                NOT NULL CONSTRAINT FK_Loans_LoanProducts REFERENCES dbo.LoanProducts (LoanProductId),
    PrincipalAmount       DECIMAL(18,2)      NOT NULL,
    AnnualInterestRate    DECIMAL(9,6)       NOT NULL,
    TermMonths            INT                NOT NULL,
    Status                VARCHAR(20)        NOT NULL CONSTRAINT DF_Loans_Status DEFAULT ('PendingApproval'), -- PendingApproval|Active|PaidOff|Defaulted
    OriginatedUtc         DATETIME2(0)       NOT NULL CONSTRAINT DF_Loans_OriginatedUtc DEFAULT (SYSUTCDATETIME()),
    MaturityDate          DATE               NOT NULL,
    OutstandingPrincipal  DECIMAL(18,2)      NOT NULL,
    CONSTRAINT UQ_Loans_LoanNumber UNIQUE (LoanNumber)
);
GO

CREATE TABLE dbo.LoanSchedules
(
    LoanScheduleId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoanSchedules PRIMARY KEY,
    LoanId           INT                   NOT NULL CONSTRAINT FK_LoanSchedules_Loans REFERENCES dbo.Loans (LoanId),
    InstallmentNumber INT                  NOT NULL,
    DueDate          DATE                  NOT NULL,
    PrincipalDue     DECIMAL(18,2)         NOT NULL,
    InterestDue      DECIMAL(18,2)         NOT NULL,
    TotalDue         DECIMAL(18,2)         NOT NULL,
    Status           VARCHAR(20)           NOT NULL CONSTRAINT DF_LoanSchedules_Status DEFAULT ('Scheduled') -- Scheduled|Paid|Late|Skipped
);
GO
CREATE UNIQUE INDEX UQ_LoanSchedules_LoanId_Installment ON dbo.LoanSchedules (LoanId, InstallmentNumber);
GO

CREATE TABLE dbo.LoanPayments
(
    LoanPaymentId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoanPayments PRIMARY KEY,
    LoanId            INT                   NOT NULL CONSTRAINT FK_LoanPayments_Loans REFERENCES dbo.Loans (LoanId),
    LoanScheduleId    BIGINT                NULL CONSTRAINT FK_LoanPayments_LoanSchedules REFERENCES dbo.LoanSchedules (LoanScheduleId),
    PaidUtc           DATETIME2(0)          NOT NULL CONSTRAINT DF_LoanPayments_PaidUtc DEFAULT (SYSUTCDATETIME()),
    AmountPaid        DECIMAL(18,2)         NOT NULL,
    PrincipalApplied  DECIMAL(18,2)         NOT NULL,
    InterestApplied   DECIMAL(18,2)         NOT NULL,
    LateFeeApplied    DECIMAL(18,2)         NOT NULL CONSTRAINT DF_LoanPayments_LateFeeApplied DEFAULT (0)
);
GO

CREATE TABLE dbo.NotificationTemplates
(
    NotificationTemplateId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationTemplates PRIMARY KEY,
    Code                   VARCHAR(50)        NOT NULL,
    Subject                NVARCHAR(200)      NOT NULL,
    BodyTemplate           NVARCHAR(MAX)      NOT NULL,
    CONSTRAINT UQ_NotificationTemplates_Code UNIQUE (Code)
);
GO

CREATE TABLE dbo.Notifications
(
    NotificationId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    CustomerId      INT                   NOT NULL CONSTRAINT FK_Notifications_Customers REFERENCES dbo.Customers (CustomerId),
    TemplateCode    VARCHAR(50)           NOT NULL,
    Payload         NVARCHAR(MAX)         NOT NULL, -- JSON used to render the template
    Channel         VARCHAR(20)           NOT NULL CONSTRAINT DF_Notifications_Channel DEFAULT ('Email'),
    Status          VARCHAR(20)           NOT NULL CONSTRAINT DF_Notifications_Status DEFAULT ('Pending'), -- Pending|Processing|Sent|Failed
    CreatedUtc      DATETIME2(0)          NOT NULL CONSTRAINT DF_Notifications_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    SentUtc         DATETIME2(0)          NULL,
    FailureReason   NVARCHAR(400)         NULL
);
GO
CREATE INDEX IX_Notifications_Status_CreatedUtc ON dbo.Notifications (Status, CreatedUtc);
GO
