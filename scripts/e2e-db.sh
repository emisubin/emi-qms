#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -f "$repo_root/.env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "$repo_root/.env"
  set +a
fi

command="${1:?usage: e2e-db.sh reset|drop|assert-dropped}"
database_name="${E2E_DATABASE_NAME:-emi_qms_e2e}"
database_host="${DATABASE_HOST:-localhost}"
database_port="${DATABASE_PORT:-5432}"
database_user="${DATABASE_USER:-emi_qms}"
database_password="${DATABASE_PASSWORD:?DATABASE_PASSWORD is required}"
compose_env_file="$repo_root/.env"
compose_file="$repo_root/infrastructure/docker-compose.yml"
postgres_service="${POSTGRES_COMPOSE_SERVICE:-}"

if [[ ! "$database_name" =~ ^[a-zA-Z0-9_]+$ ]]; then
  echo "E2E database name contains unsupported characters." >&2
  exit 1
fi

resolve_postgres_service() {
  if [[ -n "$postgres_service" ]]; then
    printf '%s\n' "$postgres_service"
    return
  fi

  postgres_service="$(
    docker compose \
      --env-file "$compose_env_file" \
      -f "$compose_file" \
      config --services | awk '$0 == "postgres" { print; exit }'
  )"

  if [[ -z "$postgres_service" ]]; then
    echo "PostgreSQL Compose service could not be resolved." >&2
    exit 1
  fi

  printf '%s\n' "$postgres_service"
}

run_compose_psql() {
  local extra_args=("$@")
  local service
  service="$(resolve_postgres_service)"

  docker compose \
    --env-file "$compose_env_file" \
    -f "$compose_file" \
    exec -T "$service" psql \
    --username "$database_user" \
    --dbname postgres \
    --no-psqlrc \
    --set ON_ERROR_STOP=1 \
    "${extra_args[@]}"
}

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

  if command -v docker >/dev/null 2>&1; then
    run_compose_psql --command "$sql"
    return
  fi

  echo "psql was not found, and Docker Compose PostgreSQL is not available." >&2
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

  if command -v docker >/dev/null 2>&1; then
    run_compose_psql --tuples-only --no-align --command "$sql"
    return
  fi

  echo "psql was not found, and Docker Compose PostgreSQL is not available." >&2
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
