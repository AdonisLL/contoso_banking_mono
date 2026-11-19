#requires -Version 5.1
<#
.SYNOPSIS
    Deploys (or redeploys) the ContosoLegacyBank database to SQL Server LocalDB by running the
    numbered scripts under database\Scripts in order.
.DESCRIPTION
    This mirrors how a lot of real legacy banking systems actually manage schema: plain,
    numbered T-SQL scripts applied with sqlcmd, not a generated dacpac. Re-running this script
    drops and recreates all tables, so it always leaves you with a known-good seeded database.
#>
[CmdletBinding()]
param(
    [string]$ServerInstance = "(localdb)\MSSQLLocalDB",
    [string]$DatabaseName = "ContosoLegacyBank"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptsDir = Join-Path $repoRoot "database\Scripts"

$scripts = Get-ChildItem -Path $scriptsDir -Filter "*.sql" | Sort-Object Name

# The cross-platform go-sqlcmd shipped on recent machines does not reliably resolve the
# "(localdb)\InstanceName" shorthand, so resolve the instance's named pipe directly via
# SqlLocalDB.exe and connect to that instead. Falls back to $ServerInstance as-is for
# non-LocalDB targets (e.g. a real SQL Server in CI).
function Resolve-SqlConnectionTarget([string]$instance) {
    if ($instance -notmatch '^\(localdb\)\\(.+)$') {
        return $instance
    }

    $localDbName = $Matches[1]
    $localDbExe = (Get-Command SqlLocalDB -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    if (-not $localDbExe) {
        $localDbExe = "C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe"
    }
    if (-not (Test-Path $localDbExe)) {
        return $instance
    }

    & $localDbExe create $localDbName -s 2>$null | Out-Null
    $info = & $localDbExe info $localDbName
    $pipeLine = $info | Where-Object { $_ -match 'Instance pipe name:\s*(\S+)' }
    if ($pipeLine -and $Matches[1]) {
        return $Matches[1]
    }
    return $instance
}

$connectionTarget = Resolve-SqlConnectionTarget $ServerInstance

Write-Host "Deploying $DatabaseName to $ServerInstance ($connectionTarget) ..." -ForegroundColor Cyan

foreach ($script in $scripts) {
    Write-Host "  Applying $($script.Name)" -ForegroundColor DarkGray
    sqlcmd -S $connectionTarget -b -I -i $script.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed applying $($script.Name) (exit code $LASTEXITCODE)"
    }
}

Write-Host "Database deployment complete." -ForegroundColor Green
