#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"

command="${1:?usage: e2e-db.sh reset|drop|assert-dropped}"
database_name="${E2E_DATABASE_NAME:-}"

# This guard must stay before every Docker or SQL command.
e2e_require_safe_database_name "${database_name}"
e2e_require_safe_compose_project "${E2E_COMPOSE_PROJECT_NAME:-}"
[[ "${E2E_POSTGRES_SERVICE:-}" == "e2e-postgres" ]] || {
  e2e_safety_error "E2E_POSTGRES_SERVICE must be e2e-postgres."
  exit "${E2E_SAFETY_EXIT_CODE}"
}

e2e_assert_dedicated_postgres

run_psql() {
  local sql="$1"

  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" \
    --dbname postgres \
    --no-psqlrc \
    --set ON_ERROR_STOP=1 \
    --command "${sql}"
}

run_psql_scalar() {
  local sql="$1"

  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" \
    --dbname postgres \
    --no-psqlrc \
    --tuples-only \
    --no-align \
    --set ON_ERROR_STOP=1 \
    --command "${sql}"
}

drop_database() {
  run_psql "drop database if exists \"${database_name}\" with (force);"
}

assert_database_dropped() {
  local count
  count="$(run_psql_scalar "select count(*) from pg_database where datname = '${database_name}';" | tr -d '[:space:]')"
  if [[ "${count}" != "0" ]]; then
    echo "E2E database was not removed during cleanup." >&2
    exit 1
  fi
}

case "${command}" in
  reset)
    drop_database
    run_psql "create database \"${database_name}\";"
    ;;
  drop)
    drop_database
    ;;
  assert-dropped)
    assert_database_dropped
    ;;
  *)
    echo "Unsupported E2E database command: ${command}" >&2
    exit 64
    ;;
esac
