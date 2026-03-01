Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pidFile = Join-Path $PSScriptRoot '.lab-pids.json'
$ports = @(5000, 5001, 5002)

function Stop-PidSafe {
    param([int]$ProcessId)

    $proc = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $proc) {
        return
    }

    try {
        Stop-Process -Id $ProcessId -Force -ErrorAction Stop
        Write-Host "[STOP] PID $ProcessId" -ForegroundColor Green
    }
    catch {
        Write-Host "[WARN] Failed to stop PID ${ProcessId}: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if (Test-Path $pidFile) {
    try {
        $records = Get-Content $pidFile -Raw | ConvertFrom-Json
        foreach ($r in $records) {
            Stop-PidSafe -ProcessId ([int]$r.Pid)
        }
    }
    catch {
        Write-Host "[WARN] PID file is invalid. Falling back to port-based stop." -ForegroundColor Yellow
    }

    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
}

$pidsByPort = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $ports -contains $_.LocalPort } |
    Select-Object -ExpandProperty OwningProcess -Unique

foreach ($procId in $pidsByPort) {
    Stop-PidSafe -ProcessId ([int]$procId)
}

Write-Host '[DONE] Stop command completed.' -ForegroundColor Cyan
