#requires -Version 5.1
<#
.SYNOPSIS
    Checks that this machine has everything needed to build and run Contoso Legacy Bank.
#>
[CmdletBinding()]
param()

$checks = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$name, [bool]$passed, [string]$detail) {
    $checks.Add([pscustomobject]@{ Check = $name; Passed = $passed; Detail = $detail })
}

# Windows version
$os = Get-CimInstance Win32_OperatingSystem
$isWin10OrLater = ([version]$os.Version) -ge ([version]"10.0")
Add-Check "Windows 10 or later" $isWin10OrLater "$($os.Caption) ($($os.Version))"

# .NET Framework 4.8 reference assemblies (needed to build net48 targets)
$refAssembliesPath = "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
Add-Check ".NET Framework 4.8 targeting pack" (Test-Path $refAssembliesPath) $refAssembliesPath

# MSBuild (via a Visual Studio install) or dotnet CLI
$vsWhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuildFound = $false
$msbuildDetail = "not found"
if (Test-Path $vsWhere) {
    $vsPath = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if ($vsPath) {
        $candidate = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path $candidate) {
            $msbuildFound = $true
            $msbuildDetail = $candidate
        }
    }
}
Add-Check "MSBuild (Visual Studio)" $msbuildFound $msbuildDetail

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetDetail = if ($dotnetCmd) { $dotnetCmd.Source } else { "not found" }
Add-Check "dotnet CLI" ($null -ne $dotnetCmd) $dotnetDetail

# SQL Server LocalDB
$sqlLocalDbExe = "C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe"
$localDbFound = (Test-Path $sqlLocalDbExe) -or (Get-Command SqlLocalDB -ErrorAction SilentlyContinue)
Add-Check "SQL Server LocalDB" ([bool]$localDbFound) $sqlLocalDbExe

# sqlcmd
$sqlcmdCmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
$sqlcmdDetail = if ($sqlcmdCmd) { $sqlcmdCmd.Source } else { "not found" }
Add-Check "sqlcmd" ($null -ne $sqlcmdCmd) $sqlcmdDetail

$checks | Format-Table -AutoSize

if ($checks | Where-Object { -not $_.Passed }) {
    Write-Host "One or more prerequisites are missing - see docs\troubleshooting.md." -ForegroundColor Yellow
    exit 1
}

Write-Host "All prerequisites satisfied." -ForegroundColor Green
