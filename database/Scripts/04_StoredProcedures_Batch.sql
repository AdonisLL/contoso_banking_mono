-- Contoso Legacy Bank - Batch import/reconciliation business logic
USE ContosoLegacyBank;
GO

IF OBJECT_ID(N'dbo.usp_Batch_CreateBatch', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Batch_CreateBatch;
IF OBJECT_ID(N'dbo.usp_Batch_ReconcileRow', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Batch_ReconcileRow;
IF OBJECT_ID(N'dbo.usp_Batch_CompleteBatch', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Batch_CompleteBatch;
GO

CREATE PROCEDURE dbo.usp_Batch_CreateBatch
    @FileName NVARCHAR(260),
    @BatchId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.BatchImportBatches (FileName) VALUES (@FileName);
    SET @BatchId = SCOPE_IDENTITY();
END
GO

-- Validates and posts a single CSV row: unknown accounts and duplicate ExternalIds are
-- recorded as rejected rows rather than throwing, so one bad row never aborts the batch.
CREATE PROCEDURE dbo.usp_Batch_ReconcileRow
    @BatchId INT,
    @RowNumber INT,
    @ExternalId VARCHAR(50),
    @AccountNumber VARCHAR(12),
    @Amount DECIMAL(18,2),
    @TransactionType VARCHAR(20),
    @Status VARCHAR(20) OUTPUT,
    @ErrorMessage NVARCHAR(400) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ErrorMessage = NULL;

    DECLARE @AccountId INT;
    SELECT @AccountId = AccountId FROM dbo.Accounts WHERE AccountNumber = @AccountNumber;

    IF @AccountId IS NULL
    BEGIN
        SET @Status = N'Error';
        SET @ErrorMessage = N'Unknown account number: ' + @AccountNumber;
    END
    ELSE IF EXISTS (SELECT 1 FROM dbo.Transactions WHERE ExternalId = @ExternalId)
    BEGIN
        SET @Status = N'Duplicate';
        SET @ErrorMessage = N'ExternalId ' + @ExternalId + N' was already imported.';
    END
    ELSE
    BEGIN
        DECLARE @TransactionId BIGINT;
        BEGIN TRY
            IF @TransactionType = N'Deposit'
                EXEC dbo.usp_Transaction_PostDeposit
                    @AccountId = @AccountId, @Amount = @Amount, @Description = N'Batch import',
                    @ExternalId = @ExternalId, @TransactionId = @TransactionId OUTPUT;
            ELSE IF @TransactionType = N'Withdrawal'
                EXEC dbo.usp_Transaction_PostWithdrawal
                    @AccountId = @AccountId, @Amount = @Amount, @Description = N'Batch import',
                    @ExternalId = @ExternalId, @TransactionId = @TransactionId OUTPUT;
            ELSE
            BEGIN
                SET @Status = N'Error';
                SET @ErrorMessage = N'Unsupported transaction type: ' + @TransactionType;
            END

            IF @ErrorMessage IS NULL
                SET @Status = N'Posted';
        END TRY
        BEGIN CATCH
            SET @Status = N'Error';
            SET @ErrorMessage = ERROR_MESSAGE();
        END CATCH
    END

    INSERT INTO dbo.BatchImportRows (BatchId, RowNumber, ExternalId, AccountNumber, Amount, TransactionType, Status, ErrorMessage)
    VALUES (@BatchId, @RowNumber, @ExternalId, @AccountNumber, @Amount, @TransactionType, @Status, @ErrorMessage);
END
GO

CREATE PROCEDURE dbo.usp_Batch_CompleteBatch
    @BatchId INT,
    @ReportPath NVARCHAR(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE b
        SET TotalRows = agg.TotalRows,
            SuccessCount = agg.SuccessCount,
            DuplicateCount = agg.DuplicateCount,
            ErrorCount = agg.ErrorCount,
            ReportPath = COALESCE(@ReportPath, b.ReportPath)
    FROM dbo.BatchImportBatches b
    CROSS APPLY (
        SELECT COUNT(*) AS TotalRows,
               SUM(CASE WHEN Status = N'Posted' THEN 1 ELSE 0 END) AS SuccessCount,
               SUM(CASE WHEN Status = N'Duplicate' THEN 1 ELSE 0 END) AS DuplicateCount,
               SUM(CASE WHEN Status = N'Error' THEN 1 ELSE 0 END) AS ErrorCount
        FROM dbo.BatchImportRows
        WHERE BatchId = b.BatchId
    ) agg
    WHERE b.BatchId = @BatchId;
END
GO
