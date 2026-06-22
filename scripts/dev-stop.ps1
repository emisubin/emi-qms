$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$env:PATH = "C:\Program Files\Docker\Docker\resources\bin;$env:PATH"

Push-Location $root
try {
    docker compose -f infrastructure/docker-compose.yml down
    Write-Host "Stopped PostgreSQL. Close backend/frontend terminal windows if they are still running."
}
finally {
    Pop-Location
}
