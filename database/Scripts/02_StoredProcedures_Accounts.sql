-- Contoso Legacy Bank - Accounts/Transactions business logic
-- These procedures are the system of record for money movement. Application code never
-- mutates Balance directly; it always calls through here so the rules stay centralized
-- (exactly the kind of "logic trapped in the database" a modernization effort has to find).
USE ContosoLegacyBank;
GO

IF OBJECT_ID(N'dbo.usp_Account_GetByNumber', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Account_GetByNumber;
IF OBJECT_ID(N'dbo.usp_Account_GetById', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Account_GetById;
IF OBJECT_ID(N'dbo.usp_Account_Search', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Account_Search;
IF OBJECT_ID(N'dbo.usp_Account_GetBalance', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Account_GetBalance;
IF OBJECT_ID(N'dbo.usp_Account_GetTransactionHistory', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Account_GetTransactionHistory;
IF OBJECT_ID(N'dbo.usp_Statement_GetTransactionData', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Statement_GetTransactionData;
IF OBJECT_ID(N'dbo.usp_Account_ApplyOverdraftFee', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Account_ApplyOverdraftFee;
IF OBJECT_ID(N'dbo.usp_Transaction_PostDeposit', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Transaction_PostDeposit;
IF OBJECT_ID(N'dbo.usp_Transaction_PostWithdrawal', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Transaction_PostWithdrawal;
IF OBJECT_ID(N'dbo.usp_Account_TransferFunds', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Account_TransferFunds;
IF OBJECT_ID(N'dbo.usp_Account_ApplyMonthlyInterest', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Account_ApplyMonthlyInterest;
GO

-- Business constants: overdraft fee amount and the large-transaction notification threshold
-- (also used by trg_Transactions_NotifyLargeTransaction in 06_Triggers.sql) are centralized here.
IF OBJECT_ID(N'dbo.usp_Config_GetDecimal', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Config_GetDecimal;
GO
CREATE PROCEDURE dbo.usp_Config_GetDecimal
    @Key NVARCHAR(50),
    @Value DECIMAL(18,2) OUTPUT
AS
BEGIN
    SET @Value = CASE @Key
        WHEN N'OverdraftFeeAmount' THEN 35.00
        WHEN N'LargeTransactionThreshold' THEN 5000.00
        ELSE 0.00
    END;
END
GO

CREATE PROCEDURE dbo.usp_Account_GetByNumber
    @AccountNumber VARCHAR(12)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AccountId, a.AccountNumber, a.CustomerId, a.AccountType, a.Status, a.Balance,
           a.OverdraftLimit, a.InterestRate, a.OpenedUtc, a.ClosedUtc,
           c.CustomerNumber, c.FirstName, c.LastName, c.Email, c.Phone
    FROM dbo.Accounts a
    JOIN dbo.Customers c ON c.CustomerId = a.CustomerId
    WHERE a.AccountNumber = @AccountNumber;
END
GO

CREATE PROCEDURE dbo.usp_Account_GetById
    @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AccountId, a.AccountNumber, a.CustomerId, a.AccountType, a.Status, a.Balance,
           a.OverdraftLimit, a.InterestRate, a.OpenedUtc, a.ClosedUtc,
           c.CustomerNumber, c.FirstName, c.LastName, c.Email, c.Phone
    FROM dbo.Accounts a
    JOIN dbo.Customers c ON c.CustomerId = a.CustomerId
    WHERE a.AccountId = @AccountId;
END
GO

CREATE PROCEDURE dbo.usp_Account_Search
    @SearchTerm NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AccountId, a.AccountNumber, a.CustomerId, a.AccountType, a.Status, a.Balance,
           a.OverdraftLimit, a.InterestRate, a.OpenedUtc, a.ClosedUtc,
           c.CustomerNumber, c.FirstName, c.LastName, c.Email, c.Phone
    FROM dbo.Accounts a
    JOIN dbo.Customers c ON c.CustomerId = a.CustomerId
    WHERE c.CustomerNumber = @SearchTerm
       OR a.AccountNumber = @SearchTerm
       OR c.LastName LIKE @SearchTerm + '%'
       OR c.FirstName LIKE @SearchTerm + '%'
    ORDER BY c.LastName, c.FirstName, a.AccountNumber;
END
GO

CREATE PROCEDURE dbo.usp_Account_GetBalance
    @AccountId INT,
    @Balance DECIMAL(18,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @Balance = Balance FROM dbo.Accounts WHERE AccountId = @AccountId;
END
GO

CREATE PROCEDURE dbo.usp_Account_GetTransactionHistory
    @AccountId INT,
    @FromUtc DATETIME2(0) = NULL,
    @ToUtc DATETIME2(0) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TransactionId, AccountId, TransactionType, Amount, BalanceAfter, Description,
           ExternalId, RelatedTransactionId, CreatedUtc
    FROM dbo.Transactions
    WHERE AccountId = @AccountId
      AND (@FromUtc IS NULL OR CreatedUtc >= @FromUtc)
      AND (@ToUtc IS NULL OR CreatedUtc < @ToUtc)
    ORDER BY CreatedUtc DESC, TransactionId DESC;
END
GO

CREATE PROCEDURE dbo.usp_Statement_GetTransactionData
    @AccountId INT,
    @PeriodStart DATE,
    @PeriodEnd DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AccountNumber, a.AccountType, a.Balance AS CurrentBalance,
           c.CustomerNumber, c.FirstName, c.LastName
    FROM dbo.Accounts a
    JOIN dbo.Customers c ON c.CustomerId = a.CustomerId
    WHERE a.AccountId = @AccountId;

    SELECT TransactionId, TransactionType, Amount, BalanceAfter, Description, CreatedUtc
    FROM dbo.Transactions
    WHERE AccountId = @AccountId
      AND CreatedUtc >= CAST(@PeriodStart AS DATETIME2(0))
      AND CreatedUtc < DATEADD(DAY, 1, CAST(@PeriodEnd AS DATETIME2(0)))
    ORDER BY CreatedUtc ASC, TransactionId ASC;
END
GO

-- Internal helper: posts a flat overdraft fee as its own Fee transaction. Not meant to be
-- called directly by application code; usp_Transaction_PostWithdrawal invokes it.
CREATE PROCEDURE dbo.usp_Account_ApplyOverdraftFee
    @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @FeeAmount DECIMAL(18,2);
    EXEC dbo.usp_Config_GetDecimal @Key = N'OverdraftFeeAmount', @Value = @FeeAmount OUTPUT;

    DECLARE @NewBalance DECIMAL(18,2);

    UPDATE dbo.Accounts
        SET Balance = Balance - @FeeAmount,
            @NewBalance = Balance - @FeeAmount
        WHERE AccountId = @AccountId;

    INSERT INTO dbo.Transactions (AccountId, TransactionType, Amount, BalanceAfter, Description)
    VALUES (@AccountId, N'Fee', -@FeeAmount, @NewBalance, N'Overdraft fee');
END
GO

CREATE PROCEDURE dbo.usp_Transaction_PostDeposit
    @AccountId INT,
    @Amount DECIMAL(18,2),
    @Description NVARCHAR(200) = NULL,
    @ExternalId VARCHAR(50) = NULL,
    @TransactionType VARCHAR(20) = N'Deposit', -- also used internally for Interest/TransferIn postings
    @TransactionId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Amount <= 0
    BEGIN
        RAISERROR(N'Deposit amount must be greater than zero.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    DECLARE @Status VARCHAR(20);
    SELECT @Status = Status FROM dbo.Accounts WITH (UPDLOCK, ROWLOCK) WHERE AccountId = @AccountId;

    IF @Status IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR(N'Account %d was not found.', 16, 1, @AccountId);
        RETURN;
    END
    IF @Status <> 'Active'
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR(N'Account %d is not active (status: %s).', 16, 1, @AccountId, @Status);
        RETURN;
    END

    DECLARE @NewBalance DECIMAL(18,2);

    UPDATE dbo.Accounts
        SET Balance = Balance + @Amount,
            @NewBalance = Balance + @Amount
        WHERE AccountId = @AccountId;

    INSERT INTO dbo.Transactions (AccountId, TransactionType, Amount, BalanceAfter, Description, ExternalId)
    VALUES (@AccountId, @TransactionType, @Amount, @NewBalance, @Description, @ExternalId);

    SET @TransactionId = SCOPE_IDENTITY();

    COMMIT TRANSACTION;
END
GO

CREATE PROCEDURE dbo.usp_Transaction_PostWithdrawal
    @AccountId INT,
    @Amount DECIMAL(18,2),
    @Description NVARCHAR(200) = NULL,
    @ExternalId VARCHAR(50) = NULL,
    @TransactionType VARCHAR(20) = N'Withdrawal', -- also used internally for TransferOut postings
    @TransactionId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Amount <= 0
    BEGIN
        RAISERROR(N'Withdrawal amount must be greater than zero.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    DECLARE @CurrentBalance DECIMAL(18,2), @OverdraftLimit DECIMAL(18,2), @Status VARCHAR(20);
    SELECT @CurrentBalance = Balance, @OverdraftLimit = OverdraftLimit, @Status = Status
        FROM dbo.Accounts WITH (UPDLOCK, ROWLOCK)
        WHERE AccountId = @AccountId;

    IF @CurrentBalance IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR(N'Account %d was not found.', 16, 1, @AccountId);
        RETURN;
    END
    IF @Status <> 'Active'
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR(N'Account %d is not active (status: %s).', 16, 1, @AccountId, @Status);
        RETURN;
    END

    DECLARE @ProjectedBalance DECIMAL(18,2) = @CurrentBalance - @Amount;
    IF @ProjectedBalance < -@OverdraftLimit
    BEGIN
        ROLLBACK TRANSACTION;
        DECLARE @AmountText VARCHAR(30) = CONVERT(VARCHAR(30), @Amount);
        RAISERROR(N'Insufficient funds: withdrawal of %s would exceed the overdraft limit for account %d.', 16, 1, @AmountText, @AccountId);
        RETURN;
    END

    UPDATE dbo.Accounts SET Balance = @ProjectedBalance WHERE AccountId = @AccountId;

    INSERT INTO dbo.Transactions (AccountId, TransactionType, Amount, BalanceAfter, Description, ExternalId)
    VALUES (@AccountId, @TransactionType, -@Amount, @ProjectedBalance, @Description, @ExternalId);

    SET @TransactionId = SCOPE_IDENTITY();

    -- Crossing from a non-negative balance into overdraft triggers a fee; already-overdrawn
    -- accounts are not charged again on every subsequent withdrawal.
    IF @CurrentBalance >= 0 AND @ProjectedBalance < 0
    BEGIN
        EXEC dbo.usp_Account_ApplyOverdraftFee @AccountId = @AccountId;
    END

    COMMIT TRANSACTION;
END
GO

CREATE PROCEDURE dbo.usp_Account_TransferFunds
    @FromAccountId INT,
    @ToAccountId INT,
    @Amount DECIMAL(18,2),
    @Description NVARCHAR(200) = NULL,
    @FromTransactionId BIGINT OUTPUT,
    @ToTransactionId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @FromAccountId = @ToAccountId
    BEGIN
        RAISERROR(N'Cannot transfer funds to the same account.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    EXEC dbo.usp_Transaction_PostWithdrawal
        @AccountId = @FromAccountId,
        @Amount = @Amount,
        @Description = @Description,
        @TransactionType = N'TransferOut',
        @TransactionId = @FromTransactionId OUTPUT;

    EXEC dbo.usp_Transaction_PostDeposit
        @AccountId = @ToAccountId,
        @Amount = @Amount,
        @Description = @Description,
        @TransactionType = N'TransferIn',
        @TransactionId = @ToTransactionId OUTPUT;

    UPDATE dbo.Transactions SET RelatedTransactionId = @ToTransactionId WHERE TransactionId = @FromTransactionId;
    UPDATE dbo.Transactions SET RelatedTransactionId = @FromTransactionId WHERE TransactionId = @ToTransactionId;

    COMMIT TRANSACTION;
END
GO

-- Applies monthly interest accrual to every active account with a positive balance.
-- Call with @AccountId to limit to a single account (used by tests), or omit for all accounts.
CREATE PROCEDURE dbo.usp_Account_ApplyMonthlyInterest
    @AccountId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AccountId_cur INT, @InterestAmount DECIMAL(18,2), @DiscardedTransactionId BIGINT;

    DECLARE interest_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT AccountId, ROUND(Balance * InterestRate / 12.0, 2)
        FROM dbo.Accounts
        WHERE Status = 'Active'
          AND Balance > 0
          AND InterestRate > 0
          AND (@AccountId IS NULL OR AccountId = @AccountId);

    OPEN interest_cursor;
    FETCH NEXT FROM interest_cursor INTO @AccountId_cur, @InterestAmount;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @InterestAmount > 0
        BEGIN
            EXEC dbo.usp_Transaction_PostDeposit
                @AccountId = @AccountId_cur,
                @Amount = @InterestAmount,
                @Description = N'Monthly interest',
                @TransactionType = N'Interest',
                @TransactionId = @DiscardedTransactionId OUTPUT;
        END
        FETCH NEXT FROM interest_cursor INTO @AccountId_cur, @InterestAmount;
    END

    CLOSE interest_cursor;
    DEALLOCATE interest_cursor;
END
GO
