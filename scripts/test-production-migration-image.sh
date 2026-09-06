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

managed_owner="${PRODUCTION_MIGRATION_CONTAINER_OWNER:-}"
managed_run_id="${PRODUCTION_MIGRATION_CONTAINER_RUN_ID:-}"
managed_container_mode=0
owner_label="com.emi-qms.test.owner"
run_label="com.emi-qms.test.run-id"

if [[ -n "${managed_owner}" || -n "${managed_run_id}" ]]; then
  if [[ "${managed_owner}" != "business-unit-production-image" || -z "${managed_run_id}" ]]; then
    echo "Production migration container ownership context is invalid." >&2
    exit "${E2E_SAFETY_EXIT_CODE}"
  fi
  e2e_require_safe_run_id "${managed_run_id}" || exit $?
  managed_container_mode=1
fi

if [[ "${managed_container_mode}" == "1" ]]; then
  if ! docker image inspect "${image_ref}" >/dev/null 2>&1; then
    echo "Production migration image could not be inspected." >&2
    exit 1
  fi
else
  docker image inspect "${image_ref}" >/dev/null
fi

e2e_initialize_environment "${repo_root}"
e2e_disable_external_providers

if [[ "${managed_container_mode}" == "1" && "${E2E_RUN_ID}" != "${managed_run_id}" ]]; then
  echo "Production migration container scope does not match the E2E run id." >&2
  exit "${E2E_SAFETY_EXIT_CODE}"
fi

cleanup_started=0
resource_scope_initialized=0
managed_container_names=()
managed_container_count=0

container_state() {
  local container_name="$1"
  local matching_ids

  if docker container inspect "${container_name}" >/dev/null 2>&1; then
    printf 'PRESENT\n'
    return 0
  fi

  if ! matching_ids="$(docker ps -aq --filter "name=^/${container_name}$")"; then
    printf 'UNKNOWN\n'
    return 1
  fi

  if [[ -n "${matching_ids}" ]]; then
    printf 'UNKNOWN\n'
    return 1
  fi

  printf 'ABSENT\n'
}

assert_container_ownership() {
  local container_name="$1"
  local actual_owner
  local actual_run_id

  if ! actual_owner="$(docker container inspect \
    -f '{{ index .Config.Labels "com.emi-qms.test.owner" }}' "${container_name}")"; then
    return 2
  fi
  if ! actual_run_id="$(docker container inspect \
    -f '{{ index .Config.Labels "com.emi-qms.test.run-id" }}' "${container_name}")"; then
    return 2
  fi

  [[ "${actual_owner}" == "${managed_owner}" && "${actual_run_id}" == "${managed_run_id}" ]]
}

remove_managed_container() {
  local container_name="$1"
  local state

  if ! state="$(container_state "${container_name}")"; then
    echo "Production migration container state is unknown." >&2
    return 1
  fi
  [[ "${state}" == "ABSENT" ]] && return 0

  if ! assert_container_ownership "${container_name}"; then
    echo "Production migration container ownership verification failed." >&2
    return 1
  fi
  if ! docker container rm --force "${container_name}" >/dev/null 2>&1; then
    echo "Production migration container removal failed." >&2
    return 1
  fi
  if ! state="$(container_state "${container_name}")" || [[ "${state}" != "ABSENT" ]]; then
    echo "Production migration container removal could not be verified." >&2
    return 1
  fi
}

cleanup() {
  local original_exit_code="$?"
  local cleanup_exit_code=0
  local container_name

  if [[ "${cleanup_started}" == "1" ]]; then
    exit "${original_exit_code}"
  fi

  cleanup_started=1
  trap - EXIT INT TERM
  set +e

  if [[ "${managed_container_mode}" == "1" && "${managed_container_count}" -gt 0 ]]; then
    for container_name in "${managed_container_names[@]}"; do
      remove_managed_container "${container_name}" || cleanup_exit_code=1
    done
  fi

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
  local phase="$1"
  local container_name=""
  local state
  local docker_arguments=(run --rm)

  if [[ "${managed_container_mode}" == "1" ]]; then
    container_name="emi-qms-production-migration-${managed_run_id//_/-}-${phase}"
    if ! state="$(container_state "${container_name}")"; then
      echo "Production migration container preflight state is unknown." >&2
      return 1
    fi
    if [[ "${state}" != "ABSENT" ]]; then
      echo "Production migration container scope already exists." >&2
      return "${E2E_SAFETY_EXIT_CODE}"
    fi

    # Claim the predictable name before Docker can create the container.
    managed_container_names+=("${container_name}")
    managed_container_count=$((managed_container_count + 1))
    docker_arguments+=(
      --name "${container_name}"
      --label "${owner_label}=${managed_owner}"
      --label "${run_label}=${managed_run_id}"
    )
  fi

  docker "${docker_arguments[@]}" \
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

run_migration fresh
run_migration existing

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
