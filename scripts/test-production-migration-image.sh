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

test_business_schemas="${PRODUCTION_MIGRATION_TEST_BUSINESS_SCHEMAS:-false}"
case "${test_business_schemas}" in
  true|false) ;;
  *) echo "Production migration business schema test flag is invalid." >&2; exit 64 ;;
esac

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
fixture_failure_log=''

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

  if [[ -n "${fixture_failure_log}" ]]; then
    rm -f -- "${fixture_failure_log}" || cleanup_exit_code=1
  fi

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

run_image_command() {
  local phase="$1"
  shift
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
    --env Database__ApplyMigrationsOnStartup=false \
    --env DevelopmentData__SeedEnabled=false \
    --env DevAuthentication__Enabled=false \
    --env AdminUserSwitch__Enabled=false \
    "$@"
}

run_migration() {
  run_image_command "$1" --env "ConnectionStrings__QmsDatabase=${connection_string}" \
    "${image_ref}" --migrate-only
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

if [[ "${test_business_schemas}" != true ]]; then
  exit 0
fi

# Every additional database and role lives in this run's dedicated tmpfs server.
# Nothing below accepts an external connection string or changes a server default.
e2e_assert_dedicated_postgres >/dev/null
fixture_failure_log="$(mktemp "${TMPDIR:-/tmp}/pms-image-consent.XXXXXX")"
business_environment=()
business_database_names=()

fixture_sql() {
  local database_name="$1"
  shift
  e2e_require_safe_database_name "${database_name}"
  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" --dbname "${database_name}" \
    --no-psqlrc --tuples-only --no-align --set ON_ERROR_STOP=1 "$@"
}

configure_business_fixture() {
  local fixture_phase="$1" approved="$2"
  local target section suffix database_name migrator runtime schema_contract
  business_environment=(--env BusinessUnits__Enabled=true)
  business_schema_approved="${approved}"
  business_database_names=()
  for target in DIRECTORY CHEONGJU OSAN; do
    case "${target}" in
      DIRECTORY) section=Directory; suffix=directory; schema_contract=0001_business_unit_directory ;;
      CHEONGJU) section=Units__Cheongju; suffix=cheongju; schema_contract=0086_business_unit_database_identity ;;
      OSAN) section=Units__Osan; suffix=osan; schema_contract=0086_business_unit_database_identity ;;
    esac
    database_name="${E2E_DATABASE_NAME}_${fixture_phase}_${suffix}"
    e2e_require_safe_database_name "${database_name}"
    if [[ "${#database_name}" -gt 63 ]]; then
      echo "Production migration fixture database name exceeds PostgreSQL's limit." >&2
      return 64
    fi
    business_database_names+=("${database_name}")
    migrator="image_${fixture_phase}_${suffix}_migrator"
    runtime="image_${fixture_phase}_${suffix}_runtime"
    business_environment+=(
      --env "BusinessUnits__${section}__Code=${target}"
      --env "BusinessUnits__${section}__RuntimeConnection=${target}Runtime"
      --env "BusinessUnits__${section}__MigrationConnection=${target}Migration"
      --env "BusinessUnits__${section}__AdministratorConnection=${target}Admin"
      --env "BusinessUnits__${section}__ExpectedDatabaseName=${database_name}"
      --env "BusinessUnits__${section}__MigrationRoleName=${migrator}"
      --env "BusinessUnits__${section}__RuntimeRoleName=${runtime}"
      --env "BusinessUnits__${section}__ExpectedSchemaVersion=${schema_contract}"
      --env "ConnectionStrings__${target}Admin=Host=e2e-postgres;Database=${database_name};Username=${E2E_DATABASE_USER};Password=${E2E_DATABASE_PASSWORD};SSL Mode=Disable;GSS Encryption Mode=Disable"
      --env "ConnectionStrings__${target}Migration=Host=e2e-postgres;Database=${database_name};Username=${migrator};Password=synthetic_migration_password_only;SSL Mode=Disable;GSS Encryption Mode=Disable"
      --env "ConnectionStrings__${target}Runtime=Host=e2e-postgres;Database=${database_name};Username=${runtime};Password=synthetic_runtime_password_only;SSL Mode=Disable;GSS Encryption Mode=Disable"
    )
  done
}

assert_target_schema() {
  local target="$1" database_name="$2" actual expected file
  local expected_versions='' source_paths=()
  if [[ "${target}" == DIRECTORY ]]; then
    source_paths=("${repo_root}"/database/directory-migrations/*.sql)
  else
    source_paths=("${repo_root}"/database/migrations/*.sql)
    case "${target}" in
      CHEONGJU) source_paths+=("${repo_root}"/database/business-migrations/cheongju/*.sql) ;;
      OSAN) source_paths+=("${repo_root}"/database/business-migrations/osan/*.sql) ;;
    esac
  fi
  for file in "${source_paths[@]}"; do
    [[ -f "${file}" ]] || { echo "Business migration catalog is missing." >&2; return 1; }
    file="${file##*/}"
    expected_versions+="${file%.sql}"$'\n'
  done
  expected_versions="$(printf '%s' "${expected_versions}" | LC_ALL=C sort)"
  actual="$(fixture_sql "${database_name}" --command 'select version from schema_migrations order by version;')"
  [[ "${actual}" == "${expected_versions}" ]] || { echo "Business migration ledger differs from its selected catalog." >&2; return 1; }
  if [[ "${target}" == DIRECTORY ]]; then
    actual="$(fixture_sql "${database_name}" --command "select database_kind || ':' || coalesce(business_unit_code, '') from qms_database_identity;")"
    [[ "${actual}" == directory: ]] || return 1
    return 0
  fi
  actual="$(fixture_sql "${database_name}" --command "select
    (select count(*) from pg_tables where schemaname='public')::text || ':' ||
    (select count(*) from information_schema.columns where table_schema='public' and table_name='projects')::text || ':' ||
    (select count(*) from information_schema.columns where table_schema='public' and table_name='notifications')::text || ':' ||
    (select count(*) from information_schema.columns where table_schema='public' and table_name='notification_deliveries')::text || ':' ||
    (select count(*) from information_schema.columns where table_schema='public' and column_name='project_profile')::text;")"
  case "${target}" in
    CHEONGJU) expected=182:36:14:46:0 ;;
    OSAN) expected=56:19:12:45:0 ;;
  esac
  [[ "${actual}" == "${expected}" ]] || { echo "Business migration schema contract failed for ${target}." >&2; return 1; }
  actual="$(fixture_sql "${database_name}" --command 'select business_unit_code from qms_database_identity;')"
  [[ "${actual}" == "${target}" ]] || return 1
}

for fixture_phase in fresh upgrade; do
  configure_business_fixture "${fixture_phase}" false
  if [[ "${fixture_phase}" == fresh ]]; then
    # Production validation must return a fixed error and exit 1 before attempting
    # a connection, even when the operation target/credentials are invalid.
    for invalid_configuration in missing-target invalid-target missing-credentials migration-invalid-target; do
      invalid_arguments=(--env ASPNETCORE_ENVIRONMENT=Production)
      operation=--deployment-drain-check
      case "${invalid_configuration}" in
        missing-target) invalid_arguments+=(--env Database__MigrationTarget=) ;;
        invalid-target) invalid_arguments+=(--env Database__MigrationTarget=UNKNOWN) ;;
        missing-credentials) invalid_arguments+=(--env Database__MigrationTarget=CHEONGJU --env ConnectionStrings__CHEONGJUMigration=) ;;
        migration-invalid-target) invalid_arguments+=(--env Database__MigrationTarget=UNKNOWN); operation=--migrate-only ;;
      esac
      invalid_exit=0
      run_image_command "business-configuration-${invalid_configuration}" \
        "${business_environment[@]}" "${invalid_arguments[@]}" \
        "${image_ref}" "${operation}" >"${fixture_failure_log}" 2>&1 || invalid_exit=$?
      if [[ "${invalid_exit}" -ne 1 ]] \
        || ! rg -q 'Code=database_operation_configuration_invalid' "${fixture_failure_log}"; then
        echo "Packaged CLI did not safely reject invalid operation configuration." >&2
        exit 1
      fi
    done
  fi
  for database_name in "${business_database_names[@]}"; do
    # The identifier has already passed the strict synthetic-name and length guards.
    fixture_sql "${E2E_DATABASE_NAME}" --command "create database \"${database_name}\";" >/dev/null
  done
  for target in DIRECTORY CHEONGJU OSAN; do
    run_image_command "business-${fixture_phase}-${target}-bootstrap" \
      "${business_environment[@]}" --env "Database__BootstrapTarget=${target}" \
      "${image_ref}" --bootstrap-database-roles
  done
  if [[ "${fixture_phase}" == upgrade ]]; then
    target_index=0
    for target in DIRECTORY CHEONGJU OSAN; do
      database_name="${business_database_names[${target_index}]}"
      target_index=$((target_index + 1))
      [[ "${target}" != DIRECTORY ]] || continue
      # The real packaged CLI builds the common prefix, then must refuse 0131.
      # Do not accept an unrelated startup/target error as this negative result.
      if run_image_command "business-${fixture_phase}-${target}-unapproved" \
        "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
        --env Database__BusinessSchemaSeparationApproved=false \
        "${image_ref}" --migrate-only >"${fixture_failure_log}" 2>&1; then
        echo "Business schema migration unexpectedly accepted missing consent." >&2
        exit 1
      fi
      if ! grep -Fq business_schema_explicit_consent_required "${fixture_failure_log}"; then
        echo "Unapproved business migration failed for an unexpected reason." >&2
        exit 1
      fi
      baseline="$(fixture_sql "${database_name}" --command "select
        (select count(*) from schema_migrations)::text || ':' ||
        (select max(version) from schema_migrations) || ':' ||
        (select count(*) from pg_tables where schemaname='public')::text || ':' ||
        (select count(*) from information_schema.columns where table_schema='public' and table_name='projects')::text;")"
      last_common="${migration_files[${#migration_files[@]}-1]##*/}"
      [[ "${baseline}" == "${expected_count}:${last_common%.sql}:209:43" ]] || {
        echo "Unapproved business migration did not preserve its complete common schema prefix." >&2
        exit 1
      }
    done
  fi
  configure_business_fixture "${fixture_phase}" true
  target_index=0
  for target in DIRECTORY CHEONGJU OSAN; do
    database_name="${business_database_names[${target_index}]}"
    target_index=$((target_index + 1))
    target_schema_approved=false
    [[ "${target}" == DIRECTORY ]] || target_schema_approved="${business_schema_approved}"
    run_image_command "business-${fixture_phase}-${target}-apply" \
      "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
      --env "Database__BusinessSchemaSeparationApproved=${target_schema_approved}" \
      "${image_ref}" --migrate-only
    assert_target_schema "${target}" "${database_name}"
    run_image_command "business-${fixture_phase}-${target}-existing" \
      "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
      --env Database__BusinessSchemaSeparationApproved=false \
      "${image_ref}" --migrate-only
    assert_target_schema "${target}" "${database_name}"
    run_image_command "business-${fixture_phase}-${target}-drain" \
      "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
      --env Database__BusinessSchemaSeparationApproved=false \
      --env "DeploymentDrain__RequireMaintenance=$([[ "${target}" == DIRECTORY ]] && echo true || echo false)" \
      --env DeploymentDrain__ReleaseId=60000000-0000-0000-0000-000000000001 \
      "${image_ref}" --deployment-drain-check
    if [[ "${target}" != DIRECTORY ]]; then
      if run_image_command "business-${fixture_phase}-${target}-drain-required" \
        "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
        --env DeploymentDrain__RequireMaintenance=true \
        --env DeploymentDrain__ReleaseId=60000000-0000-0000-0000-000000000001 \
        "${image_ref}" --deployment-drain-check >"${fixture_failure_log}" 2>&1; then
        echo "Packaged drain CLI accepted an inactive maintenance state." >&2
        exit 1
      fi
      if ! rg -q 'Code=deployment_drain_maintenance_state_mismatch' "${fixture_failure_log}"; then
        echo "Packaged drain CLI failed for an unexpected reason." >&2
        exit 1
      fi
    fi
  done
done

echo "productionBusinessMigrationFreshApply=passed"
echo "productionBusinessMigrationUpgradeApply=passed"
echo "productionBusinessMigrationSchemaContracts=passed"
echo "productionBusinessDeploymentDrainCli=passed"
echo "productionBusinessOperationConfigurationFailures=passed"
