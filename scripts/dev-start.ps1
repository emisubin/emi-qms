param(
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Docker\Docker\resources\bin;$env:PATH"
$databaseHost = $env:DATABASE_HOST
$databasePort = $env:DATABASE_PORT
$databaseName = $env:DATABASE_NAME
$databaseUser = $env:DATABASE_USER
$databasePassword = $env:DATABASE_PASSWORD

if ([string]::IsNullOrWhiteSpace($databaseHost)) { $databaseHost = "localhost" }
if ([string]::IsNullOrWhiteSpace($databasePort)) { $databasePort = "5432" }
if ([string]::IsNullOrWhiteSpace($databaseName)) { $databaseName = "emi_qms_dev" }
if ([string]::IsNullOrWhiteSpace($databaseUser)) { $databaseUser = "emi_qms" }
if ([string]::IsNullOrWhiteSpace($databasePassword)) { $databasePassword = "local_only_change_me" }

Push-Location $root
try {
    docker compose -f infrastructure/docker-compose.yml up -d

    if (-not $SkipInstall) {
        corepack pnpm install
    }

    Write-Host "Starting backend on http://localhost:5080"
    $backendCommand = @"
`$env:ASPNETCORE_ENVIRONMENT='Development'
`$env:DATABASE_HOST='$databaseHost'
`$env:DATABASE_PORT='$databasePort'
`$env:DATABASE_NAME='$databaseName'
`$env:DATABASE_USER='$databaseUser'
`$env:DATABASE_PASSWORD='$databasePassword'
cd '$root\backend'
dotnet run --project src\Emi.Qms.Api --urls http://localhost:5080
"@
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $backendCommand

    Write-Host "Starting frontend on http://localhost:5173"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root'; corepack pnpm --filter emi-qms-frontend run dev"
}
finally {
    Pop-Location
}
