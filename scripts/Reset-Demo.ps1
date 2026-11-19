#requires -Version 5.1
<#
.SYNOPSIS
    Stops the demo, redeploys the database to its clean seeded state, and clears every
    file-queue folder (Statements, Documents output, Batch, Notifications outbox).
#>
[CmdletBinding()]
param(
    [string]$ServerInstance = "(localdb)\MSSQLLocalDB"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

& (Join-Path $repoRoot "scripts\Stop-Demo.ps1")
& (Join-Path $repoRoot "scripts\Deploy-Database.ps1") -ServerInstance $ServerInstance

$appDataRoot = Join-Path $env:LOCALAPPDATA "ContosoLegacyBank"
$foldersToClear = @(
    "Documents\Output", "Statements\Pending", "Statements\Processing", "Statements\Completed", "Statements\Failed",
    "Batch\Input", "Batch\Archive", "Batch\Error", "Batch\Reports", "Notifications\Outbox"
)
foreach ($folder in $foldersToClear) {
    $path = Join-Path $appDataRoot $folder
    if (Test-Path $path) {
        Get-ChildItem -Path $path -Force | Remove-Item -Force -Recurse
    }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

Write-Host "Demo reset to a clean, seeded state." -ForegroundColor Green
