#requires -Version 5.1
<#
.SYNOPSIS
    Starts every Contoso Legacy Bank backend service, then the Portal UI, in the supported
    order: Accounts, Loans, Documents worker, Statements API, Notifications worker, Portal.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$runRoot = Join-Path $env:LOCALAPPDATA "ContosoLegacyBank\Run"
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

function Start-Tracked([string]$name, [string]$exeRelativePath, [switch]$Visible) {
    $exePath = Join-Path $repoRoot $exeRelativePath
    if (-not (Test-Path $exePath)) {
        throw "$name executable not found at $exePath. Run .\scripts\Build-All.ps1 -Configuration $Configuration first."
    }

    $style = if ($Visible) { "Normal" } else { "Hidden" }
    $process = Start-Process -FilePath $exePath -WindowStyle $style -PassThru
    Set-Content -Path (Join-Path $runRoot "$name.pid") -Value $process.Id
    Write-Host "Started $name (PID $($process.Id))" -ForegroundColor Green
    return $process
}

Start-Tracked "Accounts" "src\Accounts\Contoso.LegacyBank.Accounts.ServiceHost\bin\$Configuration\net48\Contoso.LegacyBank.Accounts.ServiceHost.exe"
Start-Tracked "Loans" "src\Loans\Contoso.LegacyBank.Loans.ServiceHost\bin\$Configuration\net48\Contoso.LegacyBank.Loans.ServiceHost.exe"
Start-Sleep -Seconds 2

Start-Tracked "Documents" "src\Documents\Contoso.LegacyBank.Documents\bin\$Configuration\net48\Contoso.LegacyBank.Documents.exe"
Start-Tracked "Statements" "src\Statements\Contoso.LegacyBank.Statements.Api\bin\$Configuration\net48\Contoso.LegacyBank.Statements.Api.exe"
Start-Tracked "Notifications" "src\Notifications\Contoso.LegacyBank.Notifications\bin\$Configuration\net48\Contoso.LegacyBank.Notifications.exe"
Start-Sleep -Seconds 2

Start-Tracked "Portal" "src\Portal\Contoso.LegacyBank.Portal\bin\$Configuration\net48\Contoso.LegacyBank.Portal.exe" -Visible

Write-Host ""
Write-Host "All services started. Run .\scripts\Stop-Demo.ps1 when you're done." -ForegroundColor Cyan
