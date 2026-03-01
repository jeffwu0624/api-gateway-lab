param(
    [int]$TimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$pidFile = Join-Path $PSScriptRoot '.lab-pids.json'

$services = @(
    @{ Name = 'TokenService'; ProjectDir = Join-Path $repoRoot 'src/TokenService/TokenService.API'; Port = 5001 },
    @{ Name = 'ApiGateway'; ProjectDir = Join-Path $repoRoot 'src/ApiGateway'; Port = 5000 },
    @{ Name = 'SampleApi'; ProjectDir = Join-Path $repoRoot 'src/SampleApi'; Port = 5002 }
)

function Test-PortListening {
    param([int]$Port)

    $listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    return ($null -ne $listeners)
}

function Wait-Port {
    param(
        [int]$Port,
        [string]$Name,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-PortListening -Port $Port) {
            Write-Host "[OK] $Name listening on :$Port" -ForegroundColor Green
            return
        }
        Start-Sleep -Milliseconds 500
    }

    throw "Timeout waiting for $Name on port $Port."
}

$pidRecords = @()

foreach ($svc in $services) {
    if (Test-PortListening -Port $svc.Port) {
        Write-Host "[SKIP] Port $($svc.Port) already in use. Assume $($svc.Name) is running." -ForegroundColor Yellow
        continue
    }

    Write-Host "[START] $($svc.Name)" -ForegroundColor Cyan
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList 'run' -WorkingDirectory $svc.ProjectDir -PassThru -WindowStyle Minimized
    $pidRecords += [pscustomobject]@{
        Name = $svc.Name
        Port = $svc.Port
        Pid  = $proc.Id
    }
}

foreach ($svc in $services) {
    Wait-Port -Port $svc.Port -Name $svc.Name -TimeoutSeconds $TimeoutSeconds
}

$pidRecords | ConvertTo-Json | Set-Content -Path $pidFile -Encoding UTF8

Write-Host ''
Write-Host 'Lab services are running:' -ForegroundColor Green
foreach ($svc in $services) {
    Write-Host "- $($svc.Name): http://localhost:$($svc.Port)"
}
Write-Host "PID file: $pidFile"
