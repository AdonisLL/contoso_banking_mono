#requires -Version 5.1
<#
.SYNOPSIS
    One-time setup for a fresh clone: starts LocalDB, deploys the database with seed data, and
    creates the local file-queue folders the apps expect.
#>
[CmdletBinding()]
param(
    [string]$ServerInstance = "(localdb)\MSSQLLocalDB"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$instanceName = ($ServerInstance -replace '^\(localdb\)\\', '')
$localDbExe = "C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe"

Write-Host "Ensuring LocalDB instance '$instanceName' exists and is running ..." -ForegroundColor Cyan
if (Test-Path $localDbExe) {
    & $localDbExe create $instanceName -s 2>$null | Out-Null
    & $localDbExe start $instanceName 2>$null | Out-Null
}

& (Join-Path $repoRoot "scripts\Deploy-Database.ps1") -ServerInstance $ServerInstance

$appDataRoot = Join-Path $env:LOCALAPPDATA "ContosoLegacyBank"
$folders = @(
    "Documents\Output", "Statements\Pending", "Statements\Processing", "Statements\Completed", "Statements\Failed",
    "Batch\Input", "Batch\Archive", "Batch\Error", "Batch\Reports", "Logs", "Notifications\Outbox", "Run"
)
foreach ($folder in $folders) {
    New-Item -ItemType Directory -Path (Join-Path $appDataRoot $folder) -Force | Out-Null
}
Write-Host "Local file-queue folders ready under $appDataRoot" -ForegroundColor Cyan

Write-Host ""
Write-Host "Seeded customers and accounts:" -ForegroundColor Cyan
$pipe = $ServerInstance
if (Test-Path $localDbExe) {
    $info = & $localDbExe info $instanceName
    $pipeLine = $info | Where-Object { $_ -match 'Instance pipe name:\s*(\S+)' }
    if ($pipeLine -and $Matches[1]) { $pipe = $Matches[1] }
}
sqlcmd -S $pipe -d ContosoLegacyBank -b -Q "SET NOCOUNT ON; SELECT c.CustomerNumber, c.FirstName, c.LastName, a.AccountNumber, a.AccountType, a.Balance FROM dbo.Customers c JOIN dbo.Accounts a ON a.CustomerId = c.CustomerId ORDER BY c.CustomerNumber;"

Write-Host ""
Write-Host "Initialization complete. Next: .\scripts\Build-All.ps1 then .\scripts\Start-Demo.ps1" -ForegroundColor Green
