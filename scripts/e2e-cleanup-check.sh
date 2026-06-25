#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

export E2E_DATABASE_NAME="${E2E_DATABASE_NAME:-emi_qms_e2e_cleanup_check}"
export DATABASE_HOST="${DATABASE_HOST:-localhost}"
export DATABASE_PORT="${DATABASE_PORT:-5432}"
export DATABASE_USER="${DATABASE_USER:-emi_qms}"
export DATABASE_PASSWORD="${DATABASE_PASSWORD:?DATABASE_PASSWORD is required}"

bash scripts/e2e-db.sh reset

held_connection_pid=""
if command -v psql >/dev/null 2>&1; then
  PGPASSWORD="$DATABASE_PASSWORD" psql \
    --host "$DATABASE_HOST" \
    --port "$DATABASE_PORT" \
    --username "$DATABASE_USER" \
    --dbname "$E2E_DATABASE_NAME" \
    --no-psqlrc \
    --command "select pg_sleep(30);" >/dev/null 2>&1 &
  held_connection_pid="$!"
elif command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' | grep -qx 'emi-qms-postgres'; then
  docker exec -i emi-qms-postgres psql \
    --username "$DATABASE_USER" \
    --dbname "$E2E_DATABASE_NAME" \
    --no-psqlrc \
    --command "select pg_sleep(30);" >/dev/null 2>&1 &
  held_connection_pid="$!"
fi

bash scripts/e2e-db.sh drop

if [[ -n "$held_connection_pid" ]]; then
  wait "$held_connection_pid" >/dev/null 2>&1 || true
fi

bash scripts/e2e-db.sh assert-dropped
bash scripts/e2e-db.sh drop
bash scripts/e2e-db.sh assert-dropped
