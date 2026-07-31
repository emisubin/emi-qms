#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${repo_root}"

# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"

image_ref="${1:?usage: test-production-migration-image.sh <backend-image-ref>}"
if [[ ! "${image_ref}" =~ ^[A-Za-z0-9._:/@-]+$ ]]; then
  echo "Production migration image reference contains unsupported characters." >&2
  exit 64
fi

docker image inspect "${image_ref}" >/dev/null

e2e_initialize_environment "${repo_root}"
e2e_disable_external_providers

cleanup_started=0
resource_scope_initialized=0

cleanup() {
  local original_exit_code="$?"
  local cleanup_exit_code=0

  if [[ "${cleanup_started}" == "1" ]]; then
    exit "${original_exit_code}"
  fi

  cleanup_started=1
  trap - EXIT INT TERM
  set +e

  if [[ "${resource_scope_initialized}" == "1" ]]; then
    bash "${repo_root}/scripts/e2e-db.sh" drop >&2 || cleanup_exit_code=1
    bash "${repo_root}/scripts/e2e-db.sh" assert-dropped >&2 || cleanup_exit_code=1
    e2e_stop_project >&2 || cleanup_exit_code=1
  fi

  if [[ "${original_exit_code}" -eq 0 && "${cleanup_exit_code}" -ne 0 ]]; then
    exit "${cleanup_exit_code}"
  fi

  exit "${original_exit_code}"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

resource_scope_initialized=1
e2e_start_postgres
bash "${repo_root}/scripts/e2e-db.sh" reset

network_name="${E2E_COMPOSE_PROJECT_NAME}_default"
connection_string="Host=e2e-postgres;Port=5432;Database=${E2E_DATABASE_NAME};Username=${E2E_DATABASE_USER};Password=${E2E_DATABASE_PASSWORD};SSL Mode=Disable;GSS Encryption Mode=Disable"

run_migration() {
  docker run --rm \
    --network "${network_name}" \
    --env ASPNETCORE_ENVIRONMENT=Testing \
    --env Authentication__Mode=Dev \
    --env "ConnectionStrings__QmsDatabase=${connection_string}" \
    --env Database__ApplyMigrationsOnStartup=false \
    --env DevelopmentData__SeedEnabled=false \
    --env DevAuthentication__Enabled=false \
    --env AdminUserSwitch__Enabled=false \
    "${image_ref}" \
    --migrate-only
}

run_migration
run_migration

migration_files=("${repo_root}"/database/migrations/*.sql)
expected_count="${#migration_files[@]}"
actual_count="$(
  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" \
    --dbname "${E2E_DATABASE_NAME}" \
    --no-psqlrc \
    --tuples-only \
    --no-align \
    --set ON_ERROR_STOP=1 \
    --command "select count(*) from schema_migrations;" |
    tr -d '[:space:]'
)"

if [[ "${actual_count}" != "${expected_count}" ]]; then
  echo "Production migration image ledger verification failed." >&2
  exit 1
fi

echo "productionMigrationImageFreshApply=passed"
echo "productionMigrationImageExistingApply=passed"
echo "productionMigrationLedgerExact=true"
