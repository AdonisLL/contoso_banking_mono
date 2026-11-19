# Troubleshooting

## `sqlcmd` fails to connect to `(localdb)\MSSQLLocalDB` with "no named pipe instance matching..."

Some machines ship the newer cross-platform `go-sqlcmd` (`sqlcmd -?` prints `Version: v1.x`),
which does not reliably resolve the `(localdb)\InstanceName` shorthand. `scripts\Deploy-Database.ps1`
already works around this by resolving the instance's named pipe directly via
`SqlLocalDB.exe info` and connecting to that pipe instead. If you call `sqlcmd` yourself outside
the scripts, do the same:

```powershell
sqllocaldb info MSSQLLocalDB   # look for "Instance pipe name: np:\\.\pipe\LOCALDB#....\tsql\query"
sqlcmd -S "np:\\.\pipe\LOCALDB#....\tsql\query" -d ContosoLegacyBank -Q "SELECT 1"
```

## Accounts/Loans ServiceHost throws `AddressAccessDeniedException` / "HTTP could not register URL"

This happens if you switch `App.config` back to `basicHttpBinding`. WCF's self-hosted HTTP
transport always registers its HTTP.SYS listener with a `+` wildcard prefix, regardless of the
literal hostname in `baseAddress`, which requires an elevated reservation:

```powershell
netsh http add urlacl url=http://+:8090/AccountService/ user=Everyone
```

Run that once, as Administrator, before starting the service - or just use the default
`netTcpBinding` configuration, which needs no reservation at all. See
[architecture.md](architecture.md) for why net.tcp is the default.

## Statements API / Documents worker can't find job files

Both read/write `%LOCALAPPDATA%\ContosoLegacyBank\Statements\...`. If you run the Portal,
Statements API, and Documents worker as different Windows user accounts (e.g. one elevated,
one not), each will resolve a different `%LOCALAPPDATA%` and never see each other's files. Run
everything as the same user, or override the root by changing `AppPaths.Root` in
`src\Shared\Contoso.LegacyBank.Shared\AppPaths.cs`.

## `dotnet build` can't find the .NET Framework 4.8 targeting pack

Install the ".NET Framework 4.8 targeting pack" component via the Visual Studio Installer
(Individual Components tab), or confirm
`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8` exists.
`scripts\Test-Prerequisites.ps1` checks this for you.

## A statement never leaves "Processing"

The Statements API only reconciles a statement's status the next time someone calls
`GET /api/statements/{id}` - there is no background watcher. Make sure the Documents worker is
actually running (`scripts\Start-Demo.ps1` starts it), then poll the status endpoint again.
