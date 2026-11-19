# Stored procedure catalog

This is the actual business-rule inventory of Contoso Legacy Bank. If you are running a
modernization assessment, start here - these procedures (plus one trigger) are where the real
rules live, not in the C# service layer, which is a thin pass-through.

Source: [`database/Scripts`](../database/Scripts).

## Accounts / Transactions (`02_StoredProcedures_Accounts.sql`)

| Procedure | Rule it encapsulates |
|---|---|
| `usp_Config_GetDecimal` | Central lookup for tunable business constants: overdraft fee ($35.00) and large-transaction notification threshold ($5,000.00). Nothing else in the codebase hardcodes these values. |
| `usp_Account_GetByNumber` / `GetById` / `Search` | Read-only account + customer lookup. |
| `usp_Account_GetBalance` | Returns the authoritative balance - callers never read `Accounts.Balance` directly. |
| `usp_Account_GetTransactionHistory` | Transaction history, optionally bounded by a date range. |
| `usp_Account_ApplyOverdraftFee` | Internal helper: posts a flat overdraft fee as its own `Fee` transaction. Only called by `usp_Transaction_PostWithdrawal`. |
| `usp_Transaction_PostDeposit` | Validates the account is `Active`, increases the balance, and posts a `Deposit`/`Interest`/`TransferIn` transaction (same procedure, parameterized transaction type). |
| `usp_Transaction_PostWithdrawal` | Validates the account is `Active`, enforces the **overdraft limit** (`balance - amount >= -OverdraftLimit`, else the withdrawal is rejected), and - if the withdrawal crosses a non-negative balance into overdraft for the first time - automatically charges the overdraft fee. Already-overdrawn accounts are not charged again. |
| `usp_Account_TransferFunds` | Wraps a withdrawal + deposit in one transaction, pairing the two resulting rows via `RelatedTransactionId` so either both post or neither does. |
| `usp_Account_ApplyMonthlyInterest` | Accrues interest (`Balance * AnnualRate / 12`, rounded to cents) for every active account with a positive balance and a non-zero rate; posts it as an `Interest` transaction through the same posting path as a deposit. |
| `usp_Account_GetTransactionHistory` / `usp_Statement_GetTransactionData` | Supply the Statements vertical with account + transaction data for a period, without exposing raw table access. |

## Loans (`03_StoredProcedures_Loans.sql`)

| Procedure | Rule it encapsulates |
|---|---|
| `usp_LoanProduct_List` | Product catalog (interest rate, min/max amount, min/max term). |
| `usp_Loan_Originate` | Validates the requested principal and term fall inside the product's configured range, creates the loan, and immediately builds its amortization schedule in the same transaction. |
| `usp_Loan_BuildAmortizationSchedule` | Computes a standard equal-installment (amortizing) schedule using the standard annuity formula; the **final installment absorbs any rounding residue** so the schedule always zeroes out the principal exactly. |
| `usp_Loan_ApplyPayment` | Allocates one payment across the earliest due installments, in order: **late fee** (a flat $25 if the installment's due date has passed), then **interest**, then **principal**. A single payment can span multiple installments, or partially pay one (reducing its remaining principal/interest and marking it `Late`). Flips the loan to `PaidOff` once `OutstandingPrincipal` reaches zero. |
| `usp_Loan_GetById` / `GetByNumber` / `Search` / `GetSchedule` | Read-only loan and schedule lookup. |

## Batch (`04_StoredProcedures_Batch.sql`)

| Procedure | Rule it encapsulates |
|---|---|
| `usp_Batch_CreateBatch` | Starts a new import batch record for a source file. |
| `usp_Batch_ReconcileRow` | Per-row dedupe-and-post: rejects unknown account numbers and duplicate `ExternalId`s as `Error`/`Duplicate` rows (never throws for bad data), otherwise posts through the same `usp_Transaction_Post*` procedures Accounts uses. |
| `usp_Batch_CompleteBatch` | Aggregates row outcomes into the batch's summary counts. |

## Statements (`06_StoredProcedures_Statements.sql`)

| Procedure | Rule it encapsulates |
|---|---|
| `usp_Statement_Create` / `GetById` / `MarkProcessing` / `MarkCompleted` / `MarkFailed` | Simple status-machine CRUD for the Statements table (`Requested` -> `Processing` -> `Completed`/`Failed`). No money-movement logic lives here. |

## Notifications (`05_StoredProcedures_Notifications.sql`)

| Procedure | Rule it encapsulates |
|---|---|
| `usp_Notification_Enqueue` | Inserts a `Pending` notification row from a template code + JSON payload. |
| `usp_Notification_ClaimPending` | **Atomically** claims up to N pending rows for one worker (`UPDATE TOP (@BatchSize) ... OUTPUT`), so two worker instances never double-send. |
| `usp_Notification_MarkSent` / `MarkFailed` | Status transitions after the worker attempts delivery. |

## Triggers (`07_Triggers.sql`)

| Trigger | Rule it encapsulates |
|---|---|
| `trg_Transactions_NotifyLargeTransaction` | **This is the one deliberately hidden rule in the whole system.** Any transaction at or above the large-transaction threshold automatically enqueues a `LargeTransaction` notification - entirely inside the database, invisible to every C# service. A real assessment has to find this before concluding it has fully inventoried the business rules. |

## Why this matters for a modernization exercise

A naive "read the C# code" inventory of this codebase will **miss** every rule in this
catalog, because the service layer (`AccountService`, `LoanService`, `BatchRepository`, ...) is
intentionally a thin pass-through. Any decomposition or rewrite plan has to:

1. Treat these procedures as the actual specification, not the C# that calls them.
2. Decide, per rule, whether it becomes application code (most of them) or stays as a
   database-enforced invariant (arguably the unique-`ExternalId` constraint used for batch
   dedupe).
3. Explicitly surface `trg_Transactions_NotifyLargeTransaction` as a requirement before removing
   or replacing the trigger - deleting it silently would stop large-transaction alerts without
   any test failing, because no C# code references it.
