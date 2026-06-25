#!/usr/bin/env bash
set -euo pipefail

export ASPNETCORE_ENVIRONMENT=Testing
export DEV_AUTHENTICATION_ENABLED=true
export DEV_DATA_SEED_ENABLED=true
export DATABASE_APPLY_MIGRATIONS_ON_STARTUP=true
export DATABASE_NAME="${E2E_DATABASE_NAME:-emi_qms_e2e}"
export DATABASE_HOST="${DATABASE_HOST:-localhost}"
export DATABASE_PORT="${DATABASE_PORT:-5432}"
export DATABASE_USER="${DATABASE_USER:-emi_qms}"
export DATABASE_PASSWORD="${DATABASE_PASSWORD:?DATABASE_PASSWORD is required}"
export Frontend__Origin=http://127.0.0.1:5174
export FRONTEND_ORIGIN=http://127.0.0.1:5174

dotnet run \
  --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj \
  --configuration Release \
  --no-build \
  --urls http://127.0.0.1:5081
