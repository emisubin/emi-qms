#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"

e2e_require_safe_database_name "${E2E_DATABASE_NAME:-}"
e2e_assert_dedicated_postgres
e2e_disable_external_providers

export ASPNETCORE_ENVIRONMENT=Testing
export AUTH_MODE=Dev
export Authentication__Mode=Dev
export DEV_AUTHENTICATION_ENABLED=true
export DEV_DATA_SEED_ENABLED=true
export DATABASE_APPLY_MIGRATIONS_ON_STARTUP=true
export DATABASE_NAME="${E2E_DATABASE_NAME}"
export DATABASE_HOST="${DATABASE_HOST:?DATABASE_HOST is required}"
export DATABASE_PORT="${DATABASE_PORT:?DATABASE_PORT is required}"
export DATABASE_USER="${DATABASE_USER:?DATABASE_USER is required}"
export DATABASE_PASSWORD="${DATABASE_PASSWORD:?DATABASE_PASSWORD is required}"
export E2E_BACKEND_PORT="${E2E_BACKEND_PORT:?E2E_BACKEND_PORT is required}"
export E2E_FRONTEND_PORT="${E2E_FRONTEND_PORT:?E2E_FRONTEND_PORT is required}"
export Frontend__Origin="http://127.0.0.1:${E2E_FRONTEND_PORT}"
export FRONTEND_ORIGIN="http://127.0.0.1:${E2E_FRONTEND_PORT}"

dotnet run \
  --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj \
  --configuration Release \
  --no-build \
  --urls "http://127.0.0.1:${E2E_BACKEND_PORT}"
