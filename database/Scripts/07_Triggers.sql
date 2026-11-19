-- Contoso Legacy Bank - triggers
-- NOTE: this trigger is a deliberate legacy smell for the modernization workshop. It enqueues a
-- customer notification whenever a large transaction posts, but that rule lives only here -
-- nowhere in the C# business layer - which is exactly the kind of hidden logic an assessment
-- tool or engineer has to discover before a safe decomposition/rewrite.
USE ContosoLegacyBank;
GO

IF OBJECT_ID(N'dbo.trg_Transactions_NotifyLargeTransaction', N'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_Transactions_NotifyLargeTransaction;
GO

CREATE TRIGGER dbo.trg_Transactions_NotifyLargeTransaction
ON dbo.Transactions
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Threshold DECIMAL(18,2);
    EXEC dbo.usp_Config_GetDecimal @Key = N'LargeTransactionThreshold', @Value = @Threshold OUTPUT;

    DECLARE @CustomerId INT, @AccountNumber VARCHAR(12), @Amount DECIMAL(18,2),
            @TransactionType VARCHAR(20), @TransactionId BIGINT, @DiscardedId BIGINT;

    DECLARE notify_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT a.CustomerId, a.AccountNumber, i.Amount, i.TransactionType, i.TransactionId
        FROM inserted i
        JOIN dbo.Accounts a ON a.AccountId = i.AccountId
        WHERE ABS(i.Amount) >= @Threshold;

    OPEN notify_cursor;
    FETCH NEXT FROM notify_cursor INTO @CustomerId, @AccountNumber, @Amount, @TransactionType, @TransactionId;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @Payload NVARCHAR(MAX) = (
            SELECT @AccountNumber AS accountNumber, @Amount AS amount,
                   @TransactionType AS transactionType, @TransactionId AS transactionId
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        );

        EXEC dbo.usp_Notification_Enqueue
            @CustomerId = @CustomerId,
            @TemplateCode = N'LargeTransaction',
            @Payload = @Payload,
            @Channel = N'Email',
            @NotificationId = @DiscardedId OUTPUT;

        FETCH NEXT FROM notify_cursor INTO @CustomerId, @AccountNumber, @Amount, @TransactionType, @TransactionId;
    END

    CLOSE notify_cursor;
    DEALLOCATE notify_cursor;
END
GO
