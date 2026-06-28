#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

export DATABASE_HOST="${DATABASE_HOST:-localhost}"
export DATABASE_PORT="${DATABASE_PORT:-5432}"
export DATABASE_USER="${DATABASE_USER:-emi_qms}"
export DATABASE_NAME="${UAT_DATABASE_NAME:-emi_qms_uat_004a}"
export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="http://127.0.0.1:5081"
export FRONTEND_ORIGIN="http://127.0.0.1:5174"
export DEV_AUTHENTICATION_ENABLED="${DEV_AUTHENTICATION_ENABLED:-true}"
export DEV_DATA_SEED_ENABLED="${DEV_DATA_SEED_ENABLED:-true}"
export VITE_API_BASE_URL="http://127.0.0.1:5081"
export VITE_DEV_USER_KEY="${VITE_DEV_USER_KEY:-dev-procurement}"
export VITE_HMR_HOST="127.0.0.1"
export VITE_HMR_CLIENT_PORT="5174"

if [[ -z "${DATABASE_PASSWORD:-}" ]]; then
  echo "DATABASE_PASSWORD is required. Set it in .env or the shell environment." >&2
  exit 1
fi

echo "Starting PostgreSQL container for manual UAT..."
if [[ -f .env ]]; then
  docker compose --env-file .env -f infrastructure/docker-compose.yml up -d postgres
else
  docker compose -f infrastructure/docker-compose.yml up -d postgres
fi

echo "Waiting for PostgreSQL health..."
until [[ "$(docker inspect -f '{{.State.Health.Status}}' emi-qms-postgres 2>/dev/null || true)" == "healthy" ]]; do
  sleep 2
done

if ! docker exec emi-qms-postgres psql -U "${DATABASE_USER}" -d postgres -tAc "select 1 from pg_database where datname = '${DATABASE_NAME}'" | grep -q 1; then
  echo "Creating manual UAT database ${DATABASE_NAME}..."
  docker exec emi-qms-postgres createdb -U "${DATABASE_USER}" "${DATABASE_NAME}"
fi

echo "Manual UAT fixed environment"
echo "  Backend:  http://127.0.0.1:5081"
echo "  Frontend: http://127.0.0.1:5174"
echo "  DB:       ${DATABASE_NAME}"
echo "  Note: E2E tests use their own temporary database and must not reuse this DB."

cleanup() {
  if [[ -n "${BACKEND_PID:-}" ]]; then kill "${BACKEND_PID}" 2>/dev/null || true; fi
  if [[ -n "${FRONTEND_PID:-}" ]]; then kill "${FRONTEND_PID}" 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM

dotnet run --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release &
BACKEND_PID=$!

(
  cd frontend
  corepack pnpm exec vite --host 127.0.0.1 --port 5174
) &
FRONTEND_PID=$!

wait -n "${BACKEND_PID}" "${FRONTEND_PID}"
