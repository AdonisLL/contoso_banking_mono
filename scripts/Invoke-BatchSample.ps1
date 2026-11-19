#requires -Version 5.1
<#
.SYNOPSIS
    Drops a sample nightly CSV file (valid, duplicate, and invalid rows) into the Batch input
    folder and runs the Batch importer once against it.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$appDataRoot = Join-Path $env:LOCALAPPDATA "ContosoLegacyBank"
$inputRoot = Join-Path $appDataRoot "Batch\Input"
New-Item -ItemType Directory -Path $inputRoot -Force | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$sampleFile = Join-Path $inputRoot "sample_$stamp.csv"

# 100000000001 and 100000000003 are seeded accounts (see database\Scripts\08_SeedData.sql).
@"
ExternalId,AccountNumber,Amount,TransactionType
SAMPLE-$stamp-0001,100000000001,125.50,Deposit
SAMPLE-$stamp-0002,100000000003,40.00,Withdrawal
SAMPLE-$stamp-0003,999999999999,75.00,Deposit
SAMPLE-$stamp-0001,100000000001,125.50,Deposit
this row is not valid csv
"@ | Set-Content -Path $sampleFile -Encoding ascii

Write-Host "Wrote sample batch file: $sampleFile" -ForegroundColor Cyan

$batchExe = Join-Path $repoRoot "src\Batch\Contoso.LegacyBank.Batch\bin\$Configuration\net48\Contoso.LegacyBank.Batch.exe"
if (-not (Test-Path $batchExe)) {
    throw "Batch executable not found at $batchExe. Run .\scripts\Build-All.ps1 -Configuration $Configuration first."
}

& $batchExe
