$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$envFile = Join-Path $root ".env"
$env:PATH = "C:\Program Files\Docker\Docker\resources\bin;$env:PATH"

if (-not (Test-Path -LiteralPath $envFile)) {
    Write-Error "Root .env file was not found. Create it with: Copy-Item .env.example .env"
    exit 1
}

Push-Location $root
try {
    docker compose --env-file $envFile -f infrastructure/docker-compose.yml down
    Write-Host "Stopped PostgreSQL. Close backend/frontend terminal windows if they are still running."
}
finally {
    Pop-Location
}
