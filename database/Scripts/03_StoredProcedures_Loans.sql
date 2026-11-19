-- Contoso Legacy Bank - Loans business logic (origination, amortization, payments)
USE ContosoLegacyBank;
GO

IF OBJECT_ID(N'dbo.usp_Loan_GetById', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Loan_GetById;
IF OBJECT_ID(N'dbo.usp_Loan_GetByNumber', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Loan_GetByNumber;
IF OBJECT_ID(N'dbo.usp_Loan_Search', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Loan_Search;
IF OBJECT_ID(N'dbo.usp_Loan_GetSchedule', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Loan_GetSchedule;
IF OBJECT_ID(N'dbo.usp_LoanProduct_List', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_LoanProduct_List;
IF OBJECT_ID(N'dbo.usp_Loan_BuildAmortizationSchedule', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Loan_BuildAmortizationSchedule;
IF OBJECT_ID(N'dbo.usp_Loan_Originate', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Loan_Originate;
IF OBJECT_ID(N'dbo.usp_Loan_ApplyPayment', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Loan_ApplyPayment;
GO

CREATE PROCEDURE dbo.usp_LoanProduct_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LoanProductId, ProductCode, Name, AnnualInterestRate, MinTermMonths, MaxTermMonths, MinAmount, MaxAmount
    FROM dbo.LoanProducts
    ORDER BY ProductCode;
END
GO

CREATE PROCEDURE dbo.usp_Loan_GetById
    @LoanId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.LoanId, l.LoanNumber, l.CustomerId, l.LoanProductId, l.PrincipalAmount,
           l.AnnualInterestRate, l.TermMonths, l.Status, l.OriginatedUtc, l.MaturityDate,
           l.OutstandingPrincipal, p.ProductCode, p.Name AS ProductName,
           c.CustomerNumber, c.FirstName, c.LastName
    FROM dbo.Loans l
    JOIN dbo.LoanProducts p ON p.LoanProductId = l.LoanProductId
    JOIN dbo.Customers c ON c.CustomerId = l.CustomerId
    WHERE l.LoanId = @LoanId;
END
GO

CREATE PROCEDURE dbo.usp_Loan_GetByNumber
    @LoanNumber VARCHAR(12)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.LoanId, l.LoanNumber, l.CustomerId, l.LoanProductId, l.PrincipalAmount,
           l.AnnualInterestRate, l.TermMonths, l.Status, l.OriginatedUtc, l.MaturityDate,
           l.OutstandingPrincipal, p.ProductCode, p.Name AS ProductName,
           c.CustomerNumber, c.FirstName, c.LastName
    FROM dbo.Loans l
    JOIN dbo.LoanProducts p ON p.LoanProductId = l.LoanProductId
    JOIN dbo.Customers c ON c.CustomerId = l.CustomerId
    WHERE l.LoanNumber = @LoanNumber;
END
GO

CREATE PROCEDURE dbo.usp_Loan_Search
    @SearchTerm NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.LoanId, l.LoanNumber, l.CustomerId, l.LoanProductId, l.PrincipalAmount,
           l.AnnualInterestRate, l.TermMonths, l.Status, l.OriginatedUtc, l.MaturityDate,
           l.OutstandingPrincipal, p.ProductCode, p.Name AS ProductName,
           c.CustomerNumber, c.FirstName, c.LastName
    FROM dbo.Loans l
    JOIN dbo.LoanProducts p ON p.LoanProductId = l.LoanProductId
    JOIN dbo.Customers c ON c.CustomerId = l.CustomerId
    WHERE c.CustomerNumber = @SearchTerm
       OR l.LoanNumber = @SearchTerm
       OR c.LastName LIKE @SearchTerm + '%'
    ORDER BY l.OriginatedUtc DESC;
END
GO

CREATE PROCEDURE dbo.usp_Loan_GetSchedule
    @LoanId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LoanScheduleId, LoanId, InstallmentNumber, DueDate, PrincipalDue, InterestDue, TotalDue, Status
    FROM dbo.LoanSchedules
    WHERE LoanId = @LoanId
    ORDER BY InstallmentNumber;
END
GO

-- Standard equal-installment (amortizing) schedule. The final installment absorbs any
-- rounding residue so the schedule always zeroes out the principal exactly.
CREATE PROCEDURE dbo.usp_Loan_BuildAmortizationSchedule
    @LoanId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Principal DECIMAL(18,2), @AnnualRate DECIMAL(9,6), @TermMonths INT, @OriginatedUtc DATETIME2(0);
    SELECT @Principal = PrincipalAmount, @AnnualRate = AnnualInterestRate, @TermMonths = TermMonths,
           @OriginatedUtc = OriginatedUtc
    FROM dbo.Loans
    WHERE LoanId = @LoanId;

    IF @Principal IS NULL
    BEGIN
        RAISERROR(N'Loan %d was not found.', 16, 1, @LoanId);
        RETURN;
    END

    DELETE FROM dbo.LoanSchedules WHERE LoanId = @LoanId;

    DECLARE @MonthlyRate DECIMAL(18,10) = @AnnualRate / 12.0;
    DECLARE @PaymentAmount DECIMAL(18,2);

    IF @MonthlyRate = 0
        SET @PaymentAmount = ROUND(@Principal / @TermMonths, 2);
    ELSE
        SET @PaymentAmount = ROUND(
            @Principal * @MonthlyRate / (1.0 - POWER(1.0 + @MonthlyRate, -@TermMonths)), 2);

    DECLARE @RemainingPrincipal DECIMAL(18,2) = @Principal;
    DECLARE @InstallmentNumber INT = 1;
    DECLARE @InterestDue DECIMAL(18,2), @PrincipalDue DECIMAL(18,2);

    WHILE @InstallmentNumber <= @TermMonths
    BEGIN
        SET @InterestDue = ROUND(@RemainingPrincipal * @MonthlyRate, 2);

        IF @InstallmentNumber = @TermMonths
            SET @PrincipalDue = @RemainingPrincipal; -- absorb rounding residue on the last installment
        ELSE
            SET @PrincipalDue = @PaymentAmount - @InterestDue;

        INSERT INTO dbo.LoanSchedules (LoanId, InstallmentNumber, DueDate, PrincipalDue, InterestDue, TotalDue, Status)
        VALUES (
            @LoanId,
            @InstallmentNumber,
            CAST(DATEADD(MONTH, @InstallmentNumber, @OriginatedUtc) AS DATE),
            @PrincipalDue,
            @InterestDue,
            @PrincipalDue + @InterestDue,
            N'Scheduled');

        SET @RemainingPrincipal = @RemainingPrincipal - @PrincipalDue;
        SET @InstallmentNumber = @InstallmentNumber + 1;
    END
END
GO

CREATE PROCEDURE dbo.usp_Loan_Originate
    @CustomerId INT,
    @LoanProductId INT,
    @PrincipalAmount DECIMAL(18,2),
    @TermMonths INT,
    @LoanId INT OUTPUT,
    @LoanNumber VARCHAR(12) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @MinAmount DECIMAL(18,2), @MaxAmount DECIMAL(18,2), @MinTerm INT, @MaxTerm INT, @AnnualRate DECIMAL(9,6);
    SELECT @MinAmount = MinAmount, @MaxAmount = MaxAmount, @MinTerm = MinTermMonths,
           @MaxTerm = MaxTermMonths, @AnnualRate = AnnualInterestRate
    FROM dbo.LoanProducts
    WHERE LoanProductId = @LoanProductId;

    IF @AnnualRate IS NULL
    BEGIN
        RAISERROR(N'Loan product %d was not found.', 16, 1, @LoanProductId);
        RETURN;
    END
    IF @PrincipalAmount < @MinAmount OR @PrincipalAmount > @MaxAmount
    BEGIN
        DECLARE @PrincipalAmountText VARCHAR(30) = CONVERT(VARCHAR(30), @PrincipalAmount);
        RAISERROR(N'Requested amount %s is outside the allowed range for this product.', 16, 1, @PrincipalAmountText);
        RETURN;
    END
    IF @TermMonths < @MinTerm OR @TermMonths > @MaxTerm
    BEGIN
        RAISERROR(N'Requested term %d months is outside the allowed range for this product.', 16, 1, @TermMonths);
        RETURN;
    END

    BEGIN TRANSACTION;

    INSERT INTO dbo.Loans (LoanNumber, CustomerId, LoanProductId, PrincipalAmount, AnnualInterestRate,
                           TermMonths, Status, MaturityDate, OutstandingPrincipal)
    VALUES (N'PENDING', @CustomerId, @LoanProductId, @PrincipalAmount, @AnnualRate, @TermMonths,
            N'Active', CAST(DATEADD(MONTH, @TermMonths, SYSUTCDATETIME()) AS DATE), @PrincipalAmount);

    SET @LoanId = SCOPE_IDENTITY();
    SET @LoanNumber = 'LN' + RIGHT('0000000000' + CAST(@LoanId AS VARCHAR(10)), 10);

    UPDATE dbo.Loans SET LoanNumber = @LoanNumber WHERE LoanId = @LoanId;

    EXEC dbo.usp_Loan_BuildAmortizationSchedule @LoanId = @LoanId;

    COMMIT TRANSACTION;
END
GO

-- Applies a payment against the earliest outstanding installments in order: any accrued
-- late fee first, then interest, then principal. A single payment may cover more than one
-- installment. Marks the loan PaidOff once the outstanding principal reaches zero.
CREATE PROCEDURE dbo.usp_Loan_ApplyPayment
    @LoanId INT,
    @AmountPaid DECIMAL(18,2),
    @LoanPaymentId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AmountPaid <= 0
    BEGIN
        RAISERROR(N'Payment amount must be greater than zero.', 16, 1);
        RETURN;
    END

    DECLARE @LateFeeAmount DECIMAL(18,2) = 25.00;
    DECLARE @RemainingPayment DECIMAL(18,2) = @AmountPaid;
    DECLARE @TotalPrincipalApplied DECIMAL(18,2) = 0, @TotalInterestApplied DECIMAL(18,2) = 0, @TotalLateFeeApplied DECIMAL(18,2) = 0;
    DECLARE @LastScheduleId BIGINT = NULL;

    BEGIN TRANSACTION;

    DECLARE @ScheduleId BIGINT, @PrincipalDue DECIMAL(18,2), @InterestDue DECIMAL(18,2), @DueDate DATE;

    DECLARE schedule_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT LoanScheduleId, PrincipalDue, InterestDue, DueDate
        FROM dbo.LoanSchedules
        WHERE LoanId = @LoanId AND Status IN (N'Scheduled', N'Late')
        ORDER BY InstallmentNumber;

    OPEN schedule_cursor;
    FETCH NEXT FROM schedule_cursor INTO @ScheduleId, @PrincipalDue, @InterestDue, @DueDate;

    WHILE @@FETCH_STATUS = 0 AND @RemainingPayment > 0
    BEGIN
        DECLARE @FeeForInstallment DECIMAL(18,2) = CASE WHEN @DueDate < CAST(SYSUTCDATETIME() AS DATE) THEN @LateFeeAmount ELSE 0 END;
        DECLARE @InstallmentTotal DECIMAL(18,2) = @PrincipalDue + @InterestDue + @FeeForInstallment;

        IF @RemainingPayment >= @InstallmentTotal
        BEGIN
            SET @TotalPrincipalApplied = @TotalPrincipalApplied + @PrincipalDue;
            SET @TotalInterestApplied = @TotalInterestApplied + @InterestDue;
            SET @TotalLateFeeApplied = @TotalLateFeeApplied + @FeeForInstallment;
            SET @RemainingPayment = @RemainingPayment - @InstallmentTotal;

            UPDATE dbo.LoanSchedules SET Status = N'Paid' WHERE LoanScheduleId = @ScheduleId;
            SET @LastScheduleId = @ScheduleId;
        END
        ELSE
        BEGIN
            -- Partial payment toward the current installment: apply fee, then interest, then
            -- whatever remains toward principal; leave the installment Scheduled/Late for the
            -- remainder to be collected on a future payment.
            DECLARE @ApplyToFee DECIMAL(18,2) = CASE WHEN @RemainingPayment >= @FeeForInstallment THEN @FeeForInstallment ELSE @RemainingPayment END;
            SET @RemainingPayment = @RemainingPayment - @ApplyToFee;
            SET @TotalLateFeeApplied = @TotalLateFeeApplied + @ApplyToFee;

            DECLARE @ApplyToInterest DECIMAL(18,2) = CASE WHEN @RemainingPayment >= @InterestDue THEN @InterestDue ELSE @RemainingPayment END;
            SET @RemainingPayment = @RemainingPayment - @ApplyToInterest;
            SET @TotalInterestApplied = @TotalInterestApplied + @ApplyToInterest;

            DECLARE @ApplyToPrincipal DECIMAL(18,2) = @RemainingPayment;
            SET @RemainingPayment = 0;
            SET @TotalPrincipalApplied = @TotalPrincipalApplied + @ApplyToPrincipal;

            UPDATE dbo.LoanSchedules
                SET PrincipalDue = PrincipalDue - @ApplyToPrincipal,
                    InterestDue = InterestDue - @ApplyToInterest,
                    TotalDue = TotalDue - @ApplyToPrincipal - @ApplyToInterest,
                    Status = N'Late'
                WHERE LoanScheduleId = @ScheduleId;

            SET @LastScheduleId = @ScheduleId;
        END

        FETCH NEXT FROM schedule_cursor INTO @ScheduleId, @PrincipalDue, @InterestDue, @DueDate;
    END

    CLOSE schedule_cursor;
    DEALLOCATE schedule_cursor;

    UPDATE dbo.Loans SET OutstandingPrincipal = OutstandingPrincipal - @TotalPrincipalApplied WHERE LoanId = @LoanId;
    UPDATE dbo.Loans SET Status = N'PaidOff' WHERE LoanId = @LoanId AND OutstandingPrincipal <= 0;

    INSERT INTO dbo.LoanPayments (LoanId, LoanScheduleId, AmountPaid, PrincipalApplied, InterestApplied, LateFeeApplied)
    VALUES (@LoanId, @LastScheduleId, @AmountPaid - @RemainingPayment, @TotalPrincipalApplied, @TotalInterestApplied, @TotalLateFeeApplied);

    SET @LoanPaymentId = SCOPE_IDENTITY();

    COMMIT TRANSACTION;
END
GO
