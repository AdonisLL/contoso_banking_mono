#requires -Version 5.1
<#
.SYNOPSIS
    Builds the entire Contoso Legacy Bank solution.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "ContosoLegacyBank.sln"

Write-Host "Building $solution ($Configuration) ..." -ForegroundColor Cyan
dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (exit code $LASTEXITCODE)."
}

Write-Host "Build succeeded." -ForegroundColor Green
