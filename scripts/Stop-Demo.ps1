#requires -Version 5.1
<#
.SYNOPSIS
    Stops every process started by scripts\Start-Demo.ps1.
#>
[CmdletBinding()]
param()

$runRoot = Join-Path $env:LOCALAPPDATA "ContosoLegacyBank\Run"
if (-not (Test-Path $runRoot)) {
    Write-Host "No tracked processes found ($runRoot does not exist)." -ForegroundColor Yellow
    return
}

Get-ChildItem -Path $runRoot -Filter "*.pid" | ForEach-Object {
    $name = $_.BaseName
    $pid_ = Get-Content $_.FullName -ErrorAction SilentlyContinue
    if ($pid_) {
        $process = Get-Process -Id $pid_ -ErrorAction SilentlyContinue
        if ($process) {
            Stop-Process -Id $pid_ -Force
            Write-Host "Stopped $name (PID $pid_)" -ForegroundColor Green
        }
        else {
            Write-Host "$name (PID $pid_) was already stopped." -ForegroundColor DarkGray
        }
    }
    Remove-Item $_.FullName -Force
}

Write-Host "All tracked Contoso Legacy Bank processes stopped." -ForegroundColor Cyan
