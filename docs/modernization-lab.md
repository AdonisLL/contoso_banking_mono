# Modernization lab

This lab uses Contoso Legacy Bank as a deliberately legacy baseline for practicing the
"deconstruct a monolith into microservices" workflow. The baseline is correct and runnable -
there is nothing to debug before you start, only architecture to discover and improve.

## 0. Baseline evidence

Before proposing any change, establish what you're starting from:

1. Run `.\scripts\Test-Prerequisites.ps1`, `.\scripts\Initialize-Demo.ps1`,
   `.\scripts\Build-All.ps1`, and `.\scripts\Start-Demo.ps1`.
2. In the Portal, search for a seeded customer (e.g. `CUST0003`), deposit/withdraw, transfer
   between two of their accounts, originate a loan, apply a loan payment, request a statement,
   and watch it complete. Run `.\scripts\Invoke-BatchSample.ps1` and read the report it writes.
3. Read [architecture.md](architecture.md) end to end, then read every file under
   `database\Scripts` and the [stored procedure catalog](stored-procedures-catalog.md). **Do not
   skip this step** - the real business rules live there, not in the C# services.

Write down, in your own words, what each of the seven verticals (Portal, Accounts, Loans,
Statements, Documents, Batch, Notifications) is responsible for and what it depends on. This is
your baseline capability map.

## 1. Find the hidden coupling

Three things in this codebase are easy to miss on a first pass. Find all three before moving on:

- A trigger that enqueues notifications with no C# caller anywhere in the repository.
- A vertical that writes to another vertical's tables directly over ADO.NET instead of calling
  its service.
- A UI tab that reads a table directly instead of calling any service at all.

(If you get stuck, the seams are all listed explicitly in
[architecture.md](architecture.md#intentional-modernization-seams) - but try to find them
yourself first; that's the actual skill being practiced.)

## 2. Propose a decomposition

Using the seven verticals as a starting point (not a final answer), propose a target set of
microservices. For each one, decide:

- What data it owns (and therefore what table(s) move with it).
- What of today's stored-procedure logic becomes application code in the new service, versus a
  database constraint/index that still enforces an invariant (e.g. the unique `ExternalId`
  index used for batch dedupe).
- What synchronous call becomes an event (e.g. does "large transaction -> notification" become
  a database trigger forever, or a domain event published by the Accounts service?).
- How the "integration database" coupling (Batch and the Portal's Notifications tab both read/
  write tables they don't own) gets resolved - a new API? An event subscription? A read model?

## 3. Modernization waves

A reasonable wave order, cheapest/lowest-risk first:

1. **Data access**: replace the hand-rolled ADO.NET repositories with EF Core, keeping the
   stored-procedure logic in place behind a repository interface, so behavior doesn't change
   yet.
2. **Workers**: Documents and Notifications are already decoupled by a queue/polling loop -
   replace the file/table polling with a managed queue (Storage Queue / Service Bus) without
   changing what they do.
3. **WCF -> REST/gRPC**: give Accounts and Loans an ASP.NET Core front door (REST or gRPC) that
   calls the same business logic, and migrate clients (Portal, Statements, Batch) one at a time.
4. **Business logic extraction**: move the rules out of T-SQL and into a tested domain layer in
   the new services, using the [stored procedure catalog](stored-procedures-catalog.md) as your
   specification. Do this *after* the service boundary exists, so you can verify behavior parity
   with characterization tests before and after.
5. **Batch and the integration-database coupling**: give Batch (and the Portal's Notifications
   read) a real API or event subscription instead of direct table access; this is usually the
   riskiest step because it's the one most likely to have silent, undocumented dependents.
6. **UI**: WinForms -> Blazor last, once every backend it depends on is stable on its new
   contracts.

## 4. Behavior-parity checklist

Before calling any wave "done," confirm:

- [ ] Overdraft limit enforcement and the overdraft fee still trigger at the same thresholds.
- [ ] A transfer still posts both legs atomically (kill the process mid-transfer in a test and
      confirm neither leg posted, or both did).
- [ ] The amortization schedule for a fixed principal/rate/term produces byte-identical
      `PrincipalDue`/`InterestDue` values to the original stored procedure.
- [ ] Batch duplicate detection (`ExternalId`) still rejects a replayed file.
- [ ] The large-transaction notification still fires at the same threshold, from wherever the
      logic now lives.
- [ ] Every existing test in `tests\` still passes against the new implementation.
