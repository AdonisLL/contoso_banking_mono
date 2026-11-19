# Architecture

## Legacy component topology

```mermaid
flowchart LR
    Teller[Teller / service rep] --> Portal[WinForms Portal<br/>.NET Framework 4.8]
    Portal -->|net.tcp (WCF)| Accounts[Accounts Service<br/>WCF self-host + ADO.NET]
    Portal -->|net.tcp (WCF)| Loans[Loans Service<br/>WCF self-host + ADO.NET]
    Portal -->|HTTP / JSON| Statements[Statements API<br/>ASP.NET Web API 2 (OWIN self-host)]
    Portal -->|direct SQL read| DB[(SQL LocalDB<br/>ContosoLegacyBank)]

    Statements -->|net.tcp (WCF)| Accounts
    Statements -->|JSON file job| Pending[(Statements\Pending)]
    Pending --> Documents[Documents Worker<br/>console + PdfSharp]
    Documents --> Output[(Documents\Output .pdf)]
    Documents --> Completed[(Statements\Completed metadata)]
    Statements -. polls .-> Completed

    Accounts --> DB
    Loans --> DB

    Batch[Batch Importer<br/>scheduled console] -->|direct ADO.NET<br/>bypasses Accounts service| DB
    Input[(Batch\Input CSV)] --> Batch
    Batch --> Reports[(Batch\Archive, Error, Reports)]

    DB -. AFTER INSERT trigger .-> Notifications[Notifications Worker<br/>polls Notifications table]
    Notifications --> Outbox[(Notifications\Outbox - simulated send)]
```

## Why this differs from a typical self-host WCF sample

Self-hosted WCF over `basicHttpBinding` registers its HTTP.SYS listener using a `+` wildcard
prefix no matter what hostname you put in `baseAddress`. That requires an elevated
`netsh http add urlacl` reservation (or running as Administrator). To keep "clone and run" true
for everyone - including on a shared or non-admin machine - **Accounts and Loans use
`netTcpBinding`** instead: a plain TCP socket that needs no reservation. The **Statements API**
self-hosts over OWIN's `Microsoft.Owin.Host.HttpListener`, which binds the literal `localhost`
hostname (not a wildcard), so it works without elevation too. If you do have admin rights and
want to demonstrate the original SOAP/HTTP binding, add a `basicHttpBinding` endpoint and run
`netsh http add urlacl url=http://+:8090/AccountService/ user=Everyone` first - see
[troubleshooting.md](troubleshooting.md).

## Local endpoints and storage

| Resource | Default |
|---|---|
| Accounts WCF service | `net.tcp://localhost:8090/AccountService` |
| Loans WCF service | `net.tcp://localhost:8092/LoanService` |
| Statements HTTP API | `http://localhost:8091/api/statements` |
| Database | `(localdb)\MSSQLLocalDB`, database `ContosoLegacyBank` |
| Local file-queue root | `%LOCALAPPDATA%\ContosoLegacyBank\` (Documents, Statements, Batch, Notifications, Logs) |

All values are configuration-driven (`App.config` connection strings / `appSettings` /
`system.serviceModel` client endpoints). The defaults make a fresh clone work without any
external infrastructure beyond SQL Server LocalDB.

## Key scenarios

### Browse accounts, deposit, withdraw, transfer

1. The Portal (or Statements API, or Batch) opens a WCF channel to the Accounts service.
2. The service forwards the call, essentially unchanged, to a stored procedure
   (`usp_Transaction_PostDeposit`, `usp_Transaction_PostWithdrawal`, `usp_Account_TransferFunds`).
3. The stored procedure owns every business rule: overdraft limit enforcement, the overdraft
   fee, transaction pairing for transfers, and running-balance maintenance.
4. A RAISERROR from the procedure is mapped onto a typed `FaultException<AccountFault>` so
   SOAP clients get a stable error code, not a raw SQL error.

### Originate and pay down a loan

1. The Portal resolves a `CustomerId` via Accounts (`SearchAccounts`), then calls
   `ILoanService.OriginateLoan`.
2. `usp_Loan_Originate` validates the product's amount/term range, then calls
   `usp_Loan_BuildAmortizationSchedule`, which computes a standard equal-installment schedule
   entirely in T-SQL.
3. Payments go through `usp_Loan_ApplyPayment`, which allocates a payment across the earliest
   due installments (late fee, then interest, then principal) and flips the loan to `PaidOff`
   when the outstanding principal reaches zero.

### Generate a statement

1. The Portal posts a statement request to the Statements Web API.
2. Statements validates the account and retrieves transaction history through the Accounts WCF
   service (it is itself a WCF *client*, same as the Portal).
3. It writes one complete, self-contained JSON job to `Statements\Pending` using an atomic
   write-then-rename, and records a `Statements` row (`Requested` -> `Processing`).
4. The Documents worker claims the file (atomic `File.Move` out of `Pending`), renders a PDF
   with PdfSharp, and drops completion/failure metadata into `Statements\Completed` or `Failed`.
5. Statements reconciles the DB row the next time anyone polls `GET /api/statements/{id}` -
   there is no background watcher.

### Import a nightly batch

1. The Batch console app claims every CSV in `Batch\Input` and creates a batch row.
2. Each row is parsed; a bad row (wrong column count, bad amount) is recorded as an `Error` row
   without aborting the rest of the file.
3. Valid rows post straight through the same stored procedures Accounts uses
   (`usp_Batch_ReconcileRow` -> `usp_Transaction_Post*`), with `ExternalId` used to reject
   duplicates (`usp_Transaction_PostDeposit`/`PostWithdrawal` require a unique `ExternalId`).
4. The source file is archived (clean import) or moved to `Batch\Error` (any errors), and a
   plain-text reconciliation report is written to `Batch\Reports`.

### Notify a customer

1. `usp_Notification_Enqueue` inserts a `Pending` row - called explicitly (not used yet by any
   vertical directly) **and implicitly** by `trg_Transactions_NotifyLargeTransaction`, an
   `AFTER INSERT` trigger on `Transactions` that fires for any transaction at or above the
   large-transaction threshold.
2. The Notifications worker polls `usp_Notification_ClaimPending`, which atomically claims a
   batch of rows (`UPDATE ... OUTPUT`), renders the matching template, and "sends" it by writing
   a file to `Notifications\Outbox` (there is no real email/SMS gateway in this sample).

## Intentional modernization seams

| Legacy seam | Why it exists | Expected target |
|---|---|---|
| WinForms UI, hand-built with no designer/resx | Demonstrates desktop-to-web UI replacement | Blazor |
| WCF over net.tcp (Accounts, Loans) | Demonstrates contract and client migration; net.tcp chosen over basicHttpBinding to avoid requiring admin rights (see above) | ASP.NET Core gRPC/REST |
| Business logic in T-SQL stored procedures and one trigger | Mirrors a real legacy system where the rules nobody remembers live in the database | Move rules into a tested application/domain layer (EF Core + C#) |
| ADO.NET + stored procedures (no ORM) | Demonstrates a thin, hand-rolled data layer | EF Core / Dapper against the same rules, now in code |
| Web API 2 (OWIN self-host) | Demonstrates ASP.NET Framework server migration | ASP.NET Core |
| File-system job queue (Statements/Documents) | Demonstrates coupling and local durability limits | Service Bus / Storage Queues |
| Local PDF output | Demonstrates machine-bound storage | Blob Storage |
| Scheduled CSV console (Batch) **that talks to the database directly**, bypassing Accounts | Demonstrates the classic "integration database" anti-pattern | Batch should call the Accounts API/events, not share its schema |
| Portal's Notifications tab reads the database directly | Same anti-pattern as Batch, for reporting | A read API (or read model) owned by a service |
| Notifications polling loop | Demonstrates batch-oriented, at-least-once delivery without a broker | Event Grid / Service Bus subscription |
| `(localdb)` / config-file endpoints | Demonstrates environment-specific configuration | Typed options and managed configuration (Key Vault, App Configuration) |
| Synchronous WCF calls, a new channel per request | Makes latency and resilience visible | Async I/O, pooled clients, timeouts, retries, cancellation |

The baseline must remain behaviorally correct. Legacy seams are not permission to add secrets,
unsafe deserialization, injection flaws, or silent error handling - the stored procedures use
parameters throughout and never concatenate user input into dynamic SQL.
