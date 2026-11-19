-- Contoso Legacy Bank - sample/seed data for local development and demos.
-- Entirely synthetic. Safe to re-run after a full redeploy (01_Schema.sql drops/recreates tables).
USE ContosoLegacyBank;
GO

SET NOCOUNT ON;

DECLARE @CustomerMap TABLE (CustomerNumber VARCHAR(10), CustomerId INT);

INSERT INTO dbo.Customers (CustomerNumber, FirstName, LastName, Email, Phone, DateOfBirth)
OUTPUT inserted.CustomerNumber, inserted.CustomerId INTO @CustomerMap
VALUES
    (N'CUST0001', N'Ava',     N'Thompson', N'ava.thompson@example.com',     N'555-0101', '1985-03-14'),
    (N'CUST0002', N'Marcus',  N'Lee',      N'marcus.lee@example.com',      N'555-0102', '1978-11-02'),
    (N'CUST0003', N'Priya',   N'Natarajan',N'priya.natarajan@example.com', N'555-0103', '1990-07-22'),
    (N'CUST0004', N'Diego',   N'Alvarez',  N'diego.alvarez@example.com',   N'555-0104', '1982-01-30'),
    (N'CUST0005', N'Harper',  N'Nguyen',   N'harper.nguyen@example.com',   N'555-0105', '1995-09-09'),
    (N'CUST0006', N'Sam',     N'Okafor',   N'sam.okafor@example.com',      N'555-0106', '1973-05-18');

DECLARE @CustomerId INT;

-- Ava: checking + savings
SELECT @CustomerId = CustomerId FROM @CustomerMap WHERE CustomerNumber = N'CUST0001';
INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance, OverdraftLimit, InterestRate)
VALUES (N'100000000001', @CustomerId, N'Checking', 2450.75, 500.00, 0.000000),
       (N'100000000002', @CustomerId, N'Savings',  10200.00, 0.00, 0.015000);

-- Marcus: checking
SELECT @CustomerId = CustomerId FROM @CustomerMap WHERE CustomerNumber = N'CUST0002';
INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance, OverdraftLimit, InterestRate)
VALUES (N'100000000003', @CustomerId, N'Checking', 875.20, 250.00, 0.000000);

-- Priya: checking + savings
SELECT @CustomerId = CustomerId FROM @CustomerMap WHERE CustomerNumber = N'CUST0003';
INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance, OverdraftLimit, InterestRate)
VALUES (N'100000000004', @CustomerId, N'Checking', 5230.40, 1000.00, 0.000000),
       (N'100000000005', @CustomerId, N'Savings',  32000.00, 0.00, 0.018000);

-- Diego: checking
SELECT @CustomerId = CustomerId FROM @CustomerMap WHERE CustomerNumber = N'CUST0004';
INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance, OverdraftLimit, InterestRate)
VALUES (N'100000000006', @CustomerId, N'Checking', 120.00, 200.00, 0.000000);

-- Harper: savings
SELECT @CustomerId = CustomerId FROM @CustomerMap WHERE CustomerNumber = N'CUST0005';
INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance, OverdraftLimit, InterestRate)
VALUES (N'100000000007', @CustomerId, N'Savings', 18500.50, 0.00, 0.016000);

-- Sam: checking + savings
SELECT @CustomerId = CustomerId FROM @CustomerMap WHERE CustomerNumber = N'CUST0006';
INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance, OverdraftLimit, InterestRate)
VALUES (N'100000000008', @CustomerId, N'Checking', 3100.10, 500.00, 0.000000),
       (N'100000000009', @CustomerId, N'Savings',  7600.00, 0.00, 0.014000);

-- A short transaction history for Ava's checking account.
DECLARE @AvaChecking INT = (SELECT AccountId FROM dbo.Accounts WHERE AccountNumber = N'100000000001');
DECLARE @TxId BIGINT;
EXEC dbo.usp_Transaction_PostDeposit @AccountId = @AvaChecking, @Amount = 1500.00, @Description = N'Payroll deposit', @TransactionId = @TxId OUTPUT;
EXEC dbo.usp_Transaction_PostWithdrawal @AccountId = @AvaChecking, @Amount = 220.50, @Description = N'Grocery store', @TransactionId = @TxId OUTPUT;
EXEC dbo.usp_Transaction_PostWithdrawal @AccountId = @AvaChecking, @Amount = 75.00, @Description = N'Utility bill', @TransactionId = @TxId OUTPUT;

-- Loan products
INSERT INTO dbo.LoanProducts (ProductCode, Name, AnnualInterestRate, MinTermMonths, MaxTermMonths, MinAmount, MaxAmount)
VALUES
    (N'PERSONAL',  N'Personal Loan',       0.099000, 6,  60,  1000.00,  25000.00),
    (N'AUTO',      N'Auto Loan',           0.059000, 12, 72,  5000.00,  60000.00),
    (N'HOMEEQUITY',N'Home Equity Loan',    0.065000, 24, 180, 10000.00, 250000.00);

-- A sample active loan for Priya.
SELECT @CustomerId = CustomerId FROM @CustomerMap WHERE CustomerNumber = N'CUST0003';
DECLARE @PersonalProductId INT = (SELECT LoanProductId FROM dbo.LoanProducts WHERE ProductCode = N'PERSONAL');
DECLARE @LoanId INT, @LoanNumber VARCHAR(12);
EXEC dbo.usp_Loan_Originate
    @CustomerId = @CustomerId,
    @LoanProductId = @PersonalProductId,
    @PrincipalAmount = 12000.00,
    @TermMonths = 24,
    @LoanId = @LoanId OUTPUT,
    @LoanNumber = @LoanNumber OUTPUT;

-- Notification templates
INSERT INTO dbo.NotificationTemplates (Code, Subject, BodyTemplate)
VALUES
    (N'LargeTransaction', N'Large transaction alert',
     N'A transaction of {amount} ({transactionType}) posted to account {accountNumber}.'),
    (N'StatementReady', N'Your statement is ready',
     N'Your statement for account {accountNumber} covering {periodStart} to {periodEnd} is ready to view.'),
    (N'LoanPaymentDue', N'Upcoming loan payment',
     N'Your loan {loanNumber} has a payment of {amountDue} due on {dueDate}.');
GO
