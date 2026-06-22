param(
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Docker\Docker\resources\bin;$env:PATH"

Push-Location $root
try {
    docker compose -f infrastructure/docker-compose.yml up -d

    if (-not $SkipInstall) {
        pnpm install
    }

    Write-Host "Starting backend on http://localhost:5080"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root\backend'; dotnet run --project src\Emi.Qms.Api --urls http://localhost:5080"

    Write-Host "Starting frontend on http://localhost:5173"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root'; pnpm --filter emi-qms-frontend run dev"
}
finally {
    Pop-Location
}
