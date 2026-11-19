-- Contoso Legacy Bank - Statements status tracking
USE ContosoLegacyBank;
GO

IF OBJECT_ID(N'dbo.usp_Statement_Create', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Statement_Create;
IF OBJECT_ID(N'dbo.usp_Statement_GetById', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Statement_GetById;
IF OBJECT_ID(N'dbo.usp_Statement_MarkProcessing', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Statement_MarkProcessing;
IF OBJECT_ID(N'dbo.usp_Statement_MarkCompleted', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Statement_MarkCompleted;
IF OBJECT_ID(N'dbo.usp_Statement_MarkFailed', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Statement_MarkFailed;
GO

CREATE PROCEDURE dbo.usp_Statement_Create
    @AccountId INT,
    @PeriodStart DATE,
    @PeriodEnd DATE,
    @StatementId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Statements (AccountId, PeriodStart, PeriodEnd, Status)
    VALUES (@AccountId, @PeriodStart, @PeriodEnd, N'Requested');
    SET @StatementId = SCOPE_IDENTITY();
END
GO

CREATE PROCEDURE dbo.usp_Statement_GetById
    @StatementId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatementId, AccountId, PeriodStart, PeriodEnd, Status, RequestedUtc, CompletedUtc, FilePath, FailureReason
    FROM dbo.Statements
    WHERE StatementId = @StatementId;
END
GO

CREATE PROCEDURE dbo.usp_Statement_MarkProcessing
    @StatementId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Statements SET Status = N'Processing' WHERE StatementId = @StatementId;
END
GO

CREATE PROCEDURE dbo.usp_Statement_MarkCompleted
    @StatementId INT,
    @FilePath NVARCHAR(400)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Statements
        SET Status = N'Completed', CompletedUtc = SYSUTCDATETIME(), FilePath = @FilePath
        WHERE StatementId = @StatementId;
END
GO

CREATE PROCEDURE dbo.usp_Statement_MarkFailed
    @StatementId INT,
    @FailureReason NVARCHAR(400)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Statements
        SET Status = N'Failed', CompletedUtc = SYSUTCDATETIME(), FailureReason = @FailureReason
        WHERE StatementId = @StatementId;
END
GO
