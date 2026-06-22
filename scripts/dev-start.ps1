param(
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$envFile = Join-Path $root ".env"
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Docker\Docker\resources\bin;$env:PATH"

function Import-DotEnv {
    param([Parameter(Mandatory = $true)][string]$Path)

    Get-Content -LiteralPath $Path | ForEach-Object {
        $line = $_.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            return
        }

        $parts = $line -split "=", 2
        if ($parts.Count -ne 2) {
            return
        }

        $name = $parts[0].Trim()
        $value = $parts[1].Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

if (-not (Test-Path -LiteralPath $envFile)) {
    Write-Error "Root .env file was not found. Create it with: Copy-Item .env.example .env"
    exit 1
}

Import-DotEnv -Path $envFile

$requiredEnv = @("DATABASE_HOST", "DATABASE_PORT", "DATABASE_NAME", "DATABASE_USER", "DATABASE_PASSWORD")
$missingEnv = $requiredEnv | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_, "Process")) }
if ($missingEnv.Count -gt 0) {
    Write-Error "Missing required value(s) in root .env: $($missingEnv -join ', ')"
    exit 1
}

Push-Location $root
try {
    docker compose --env-file $envFile -f infrastructure/docker-compose.yml up -d

    if (-not $SkipInstall) {
        corepack pnpm install
    }

    Write-Host "Starting backend on http://localhost:5080"
    $backendCommand = @"
`$env:ASPNETCORE_ENVIRONMENT='Development'
`$env:DATABASE_HOST='$([Environment]::GetEnvironmentVariable("DATABASE_HOST", "Process"))'
`$env:DATABASE_PORT='$([Environment]::GetEnvironmentVariable("DATABASE_PORT", "Process"))'
`$env:DATABASE_NAME='$([Environment]::GetEnvironmentVariable("DATABASE_NAME", "Process"))'
`$env:DATABASE_USER='$([Environment]::GetEnvironmentVariable("DATABASE_USER", "Process"))'
`$env:DATABASE_PASSWORD='$([Environment]::GetEnvironmentVariable("DATABASE_PASSWORD", "Process"))'
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
