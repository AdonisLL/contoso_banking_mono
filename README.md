# Contoso Legacy Bank (monorepo)

Contoso Legacy Bank is a deliberately legacy, end-to-end **.NET Framework 4.8** application
estate for practicing assessment and modernization of a monolith into microservices. Unlike the
multi-repo reference this is modeled on, everything here lives in **one repository and one
solution** - no git submodules.

The applications are old by design, but they are expected to build, run, and handle failures
correctly. They contain synthetic data only and do not intentionally include exploitable
vulnerabilities. A significant amount of real business logic - overdraft rules, transfer
atomicity, loan amortization and payment allocation, batch dedupe, and one notification trigger
- lives in **T-SQL stored procedures**, not in the C# services, mirroring how a lot of real
legacy banking systems are actually built. See the
[stored procedure catalog](docs/stored-procedures-catalog.md).

## Application estate

| Component | Legacy technology | Responsibility | Modernization target |
|---|---|---|---|
| [Portal](src/Portal/Contoso.LegacyBank.Portal) | WinForms, WCF/HTTP clients | Teller UI: customer/account search, transactions, loans, statements, notifications | Blazor and async REST/gRPC clients |
| [Accounts](src/Accounts) | WCF (net.tcp) + ADO.NET **+ stored procedures** | Customers, accounts, balances, transfers, interest, overdraft | ASP.NET Core REST/gRPC, EF Core, Azure SQL |
| [Loans](src/Loans) | WCF (net.tcp) + stored procedures | Loan origination, amortization schedules, payments | ASP.NET Core REST/gRPC |
| [Statements](src/Statements/Contoso.LegacyBank.Statements.Api) | ASP.NET Web API 2 (OWIN self-host) | Statement orchestration and status | ASP.NET Core API |
| [Documents](src/Documents/Contoso.LegacyBank.Documents) | Console worker, file queue, PdfSharp | PDF statement generation | Managed worker + Blob Storage |
| [Batch](src/Batch/Contoso.LegacyBank.Batch) | Scheduled console app, CSV files | Nightly transaction import and reconciliation | Azure Functions / Container Apps Jobs |
| [Notifications](src/Notifications/Contoso.LegacyBank.Notifications) | Console worker, polls a DB queue table | Simulated customer notifications | Event-driven (Service Bus / Logic Apps) |

See [docs/architecture.md](docs/architecture.md) for a full diagram, local endpoints, key
scenarios, and the list of intentional modernization seams.

## Prerequisites

- Windows 10/11
- Visual Studio 2022+ (or Build Tools) with the **.NET desktop development** and **.NET Framework
  4.8 targeting pack** workloads, or the `dotnet` SDK with the .NET Framework 4.8 reference
  assemblies installed
- SQL Server Express LocalDB
- PowerShell 5.1+

Check your machine:

```powershell
.\scripts\Test-Prerequisites.ps1
```

## Quickstart

```powershell
git clone https://github.com/AdonisLL/contoso_banking_mono.git
cd contoso_banking_mono
.\scripts\Initialize-Demo.ps1    # starts LocalDB, deploys schema + seed data
.\scripts\Build-All.ps1          # dotnet build ContosoLegacyBank.sln -c Release
.\scripts\Start-Demo.ps1         # starts Accounts, Loans, Documents, Statements, Notifications, Portal
```

Use one of the seeded customer numbers printed by `Initialize-Demo.ps1` (e.g. `CUST0003`) to
search in the Portal. Request a statement, wait a few seconds for the Documents worker to pick
it up, and check its status again to open the PDF. Run a sample nightly import separately:

```powershell
.\scripts\Invoke-BatchSample.ps1
```

Stop everything the hub started:

```powershell
.\scripts\Stop-Demo.ps1
```

Reset to a clean, re-seeded state (also clears every file-queue folder):

```powershell
.\scripts\Reset-Demo.ps1
```

## Building and testing directly

```powershell
dotnet build ContosoLegacyBank.sln -c Release
dotnet test ContosoLegacyBank.sln -c Release
```

`.github/workflows/build.yml` runs the same build/test on every push and PR against `main`.

## Modernization workshop

[docs/modernization-lab.md](docs/modernization-lab.md) walks through:

- establishing baseline evidence before changing anything;
- finding the three places where logic or coupling is hidden (a trigger with no C# caller, a
  vertical that writes another vertical's tables directly, and a UI tab that reads a table
  directly);
- proposing a microservice decomposition and a dependency-ordered set of modernization waves;
- a behavior-parity checklist to validate each wave against the original stored-procedure rules.

## Documentation

- [Architecture](docs/architecture.md)
- [Stored procedure catalog](docs/stored-procedures-catalog.md)
- [Modernization lab](docs/modernization-lab.md)
- [Troubleshooting](docs/troubleshooting.md)

## Repository layout

```
database/Scripts/    Numbered T-SQL scripts: schema, stored procedures, triggers, seed data
src/<Vertical>/       One folder per vertical, Contracts/Data/Business/ServiceHost split where applicable
tests/                MSTest projects; most are integration tests that exercise real stored procedures against LocalDB
scripts/              Operational PowerShell: prerequisites, init, build, start/stop/reset, batch sample
docs/                 Architecture, stored procedure catalog, modernization lab, troubleshooting
.github/workflows/    CI: build + test on windows-latest
```

## License

Licensed under the [MIT License](LICENSE).