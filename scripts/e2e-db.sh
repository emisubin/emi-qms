#!/usr/bin/env bash
set -euo pipefail

command="${1:?usage: e2e-db.sh reset|drop|assert-dropped}"
database_name="${E2E_DATABASE_NAME:-emi_qms_e2e}"
database_host="${DATABASE_HOST:-localhost}"
database_port="${DATABASE_PORT:-5432}"
database_user="${DATABASE_USER:-emi_qms}"
database_password="${DATABASE_PASSWORD:?DATABASE_PASSWORD is required}"

if [[ ! "$database_name" =~ ^[a-zA-Z0-9_]+$ ]]; then
  echo "E2E database name contains unsupported characters." >&2
  exit 1
fi

run_psql() {
  local sql="$1"
  if command -v psql >/dev/null 2>&1; then
    PGPASSWORD="$database_password" psql \
      --host "$database_host" \
      --port "$database_port" \
      --username "$database_user" \
      --dbname postgres \
      --no-psqlrc \
      --set ON_ERROR_STOP=1 \
      --command "$sql"
    return
  fi

  if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' | grep -qx 'emi-qms-postgres'; then
    docker exec -i emi-qms-postgres psql \
      --username "$database_user" \
      --dbname postgres \
      --no-psqlrc \
      --set ON_ERROR_STOP=1 \
      --command "$sql"
    return
  fi

  echo "psql was not found, and the local emi-qms-postgres container is not available." >&2
  exit 1
}

run_psql_scalar() {
  local sql="$1"
  if command -v psql >/dev/null 2>&1; then
    PGPASSWORD="$database_password" psql \
      --host "$database_host" \
      --port "$database_port" \
      --username "$database_user" \
      --dbname postgres \
      --no-psqlrc \
      --tuples-only \
      --no-align \
      --set ON_ERROR_STOP=1 \
      --command "$sql"
    return
  fi

  if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' | grep -qx 'emi-qms-postgres'; then
    docker exec -i emi-qms-postgres psql \
      --username "$database_user" \
      --dbname postgres \
      --no-psqlrc \
      --tuples-only \
      --no-align \
      --set ON_ERROR_STOP=1 \
      --command "$sql"
    return
  fi

  echo "psql was not found, and the local emi-qms-postgres container is not available." >&2
  exit 1
}

drop_database() {
  run_psql "drop database if exists \"$database_name\" with (force);"
}

assert_database_dropped() {
  local count
  count="$(run_psql_scalar "select count(*) from pg_database where datname = '$database_name';" | tr -d '[:space:]')"
  if [[ "$count" != "0" ]]; then
    echo "E2E database '$database_name' still exists after cleanup." >&2
    exit 1
  fi
}

case "$command" in
  reset)
    drop_database
    run_psql "create database \"$database_name\";"
    ;;
  drop)
    drop_database
    ;;
  assert-dropped)
    assert_database_dropped
    ;;
  *)
    echo "Unsupported E2E database command: $command" >&2
    exit 1
    ;;
esac
