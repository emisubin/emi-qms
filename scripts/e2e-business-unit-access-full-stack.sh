#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${repo_root}"

# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"

self_test_mode=""
review_server_mode=""
if [[ "${1:-}" == "--self-test-backend-startup-failure" ]]; then
  self_test_mode="$1"
  shift
elif [[ "${1:-}" == "--review-server" ]]; then
  review_server_mode="$1"
  shift
fi

e2e_initialize_environment "${repo_root}"
e2e_disable_external_providers

command -v lsof >/dev/null 2>&1 || {
  e2e_safety_error "lsof is required to prove Full-Stack E2E process ownership."
  exit "${E2E_SAFETY_EXIT_CODE}"
}

for selected_port in "${E2E_BACKEND_PORT}" "${E2E_FRONTEND_PORT}"; do
  if ! e2e_port_is_available "${selected_port}"; then
    e2e_safety_error "Selected Full-Stack E2E port is already occupied; no resources were created."
    exit "${E2E_SAFETY_EXIT_CODE}"
  fi
done

resource_scope_initialized=0
backend_pid=""
frontend_pid=""
backend_log="$(mktemp -t emi-qms-business-unit-backend.XXXXXX.log)"
frontend_log="$(mktemp -t emi-qms-business-unit-frontend.XXXXXX.log)"
operation_log="$(mktemp -t emi-qms-business-unit-setup.XXXXXX.log)"
backend_pid_file="$(mktemp -t emi-qms-business-unit-backend.XXXXXX.pid)"
frontend_pid_file="$(mktemp -t emi-qms-business-unit-frontend.XXXXXX.pid)"
backend_expected_cwd="${repo_root}"
backend_dll="${repo_root}/backend/src/Emi.Qms.Api/bin/Release/net10.0/Emi.Qms.Api.dll"
backend_expected_command_marker="${backend_dll}"
backend_expected_url_marker="127.0.0.1:${E2E_BACKEND_PORT}"
backend_expected_session="$(ps -o sess= -p "$$" | tr -d '[:space:]')"
backend_readiness_attempts=180
process_ownership_exit_code=65
suffix="${E2E_RUN_ID}"
directory_database="${E2E_DATABASE_NAME}_directory"
cheongju_database="${E2E_DATABASE_NAME}_cheongju"
osan_database="${E2E_DATABASE_NAME}_osan"
directory_migrator="bu_${suffix}_dir_m"
directory_runtime="bu_${suffix}_dir_r"
cheongju_migrator="bu_${suffix}_cj_m"
cheongju_runtime="bu_${suffix}_cj_r"
osan_migrator="bu_${suffix}_osan_m"
osan_runtime="bu_${suffix}_osan_r"
bounded_password="${E2E_DATABASE_PASSWORD}_${suffix}_bounded"

for database_name in "${directory_database}" "${cheongju_database}" "${osan_database}"; do
  e2e_require_safe_database_name "${database_name}"
done
for role_name in "${directory_migrator}" "${directory_runtime}" "${cheongju_migrator}" "${cheongju_runtime}" "${osan_migrator}" "${osan_runtime}"; do
  [[ "${role_name}" =~ ^[a-z0-9_]+$ ]] || {
    e2e_safety_error "Synthetic bounded role name is invalid."
    exit "${E2E_SAFETY_EXIT_CODE}"
  }
done

run_admin_psql() {
  local database_name="$1"
  local sql="$2"
  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" \
    --dbname "${database_name}" \
    --no-psqlrc \
    --set ON_ERROR_STOP=1 \
    --command "${sql}"
}

run_admin_scalar() {
  local sql="$1"
  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" \
    --dbname postgres \
    --no-psqlrc \
    --tuples-only \
    --no-align \
    --set ON_ERROR_STOP=1 \
    --command "${sql}" | tr -d '[:space:]'
}

read_process_cwd() {
  local process_id="$1"
  lsof -a -p "${process_id}" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p' | head -n 1
}

read_process_session() {
  local process_id="$1"
  ps -o sess= -p "${process_id}" 2>/dev/null | tr -d '[:space:]'
}

read_process_command() {
  local process_id="$1"
  ps -o command= -p "${process_id}" 2>/dev/null
}

read_listener_pids() {
  local port="$1"
  lsof -nP -iTCP:"${port}" -sTCP:LISTEN -t 2>/dev/null | sort -u | paste -sd ' ' -
}

assert_owned_backend_process() {
  local require_listener="$1"
  local actual_command
  local actual_cwd
  local actual_session
  local listener_pids

  [[ "${backend_pid}" =~ ^[0-9]+$ && -f "${backend_pid_file}" ]] || {
    e2e_safety_error "Backend ownership proof is incomplete; no process was terminated."
    return "${process_ownership_exit_code}"
  }
  [[ "$(cat "${backend_pid_file}")" == "${backend_pid}" ]] || {
    e2e_safety_error "Backend PID file ownership does not match; no process was terminated."
    return "${process_ownership_exit_code}"
  }
  kill -0 "${backend_pid}" 2>/dev/null || {
    e2e_safety_error "Owned backend process is no longer running."
    return 1
  }

  actual_cwd="$(read_process_cwd "${backend_pid}")"
  actual_command="$(read_process_command "${backend_pid}")"
  actual_session="$(read_process_session "${backend_pid}")"
  [[ "${actual_cwd}" == "${backend_expected_cwd}" ]] || {
    e2e_safety_error "Backend cwd ownership does not match; no process was terminated."
    return "${process_ownership_exit_code}"
  }
  [[ -n "${backend_expected_session}" && "${actual_session}" == "${backend_expected_session}" ]] || {
    e2e_safety_error "Backend session ownership does not match; no process was terminated."
    return "${process_ownership_exit_code}"
  }
  [[ "${actual_command}" == *"${backend_expected_command_marker}"* ]] || {
    e2e_safety_error "Backend command ownership does not match; no process was terminated."
    return "${process_ownership_exit_code}"
  }
  if [[ -n "${backend_expected_url_marker}" && "${actual_command}" != *"${backend_expected_url_marker}"* ]]; then
    e2e_safety_error "Backend command port ownership does not match; no process was terminated."
    return "${process_ownership_exit_code}"
  fi

  if [[ "${require_listener}" == "true" ]]; then
    listener_pids="$(read_listener_pids "${E2E_BACKEND_PORT}")"
    [[ "${listener_pids}" == "${backend_pid}" ]] || {
      e2e_safety_error "Backend listener ownership does not match; no process was terminated."
      return "${process_ownership_exit_code}"
    }
  fi
}

wait_for_owned_backend_ready() {
  local url="$1"
  for _ in $(seq 1 "${backend_readiness_attempts}"); do
    if ! kill -0 "${backend_pid}" 2>/dev/null; then
      e2e_safety_error "Owned backend exited before readiness."
      return 1
    fi
    if ! e2e_port_is_available "${E2E_BACKEND_PORT}"; then
      assert_owned_backend_process true || return $?
      if curl --fail --silent --show-error "${url}" >/dev/null 2>&1; then
        assert_owned_backend_process true || return $?
        return 0
      fi
    fi
    sleep 1
  done
  e2e_safety_error "Owned backend did not become ready before the bounded deadline."
  return 1
}

wait_for_port_to_close() {
  local port="$1"
  for _ in $(seq 1 50); do
    if e2e_port_is_available "${port}"; then
      return 0
    fi
    sleep 0.1
  done
  return 1
}

assert_owned_frontend_process() {
  local listener_pids
  [[ "${frontend_pid}" =~ ^[0-9]+$ && -f "${frontend_pid_file}" ]] || return "${process_ownership_exit_code}"
  [[ "$(cat "${frontend_pid_file}")" == "${frontend_pid}" ]] || return "${process_ownership_exit_code}"
  kill -0 "${frontend_pid}" 2>/dev/null || return 1
  [[ "$(read_process_cwd "${frontend_pid}")" == "${repo_root}/frontend" ]] || return "${process_ownership_exit_code}"
  [[ "$(read_process_session "${frontend_pid}")" == "${backend_expected_session}" ]] || return "${process_ownership_exit_code}"
  [[ "$(read_process_command "${frontend_pid}")" == *"vite"* ]] || return "${process_ownership_exit_code}"
  listener_pids="$(read_listener_pids "${E2E_FRONTEND_PORT}")"
  [[ "${listener_pids}" == "${frontend_pid}" ]] || return "${process_ownership_exit_code}"
}

wait_for_frontend_ready() {
  local url="$1"
  for _ in $(seq 1 120); do
    if ! kill -0 "${frontend_pid}" 2>/dev/null; then
      e2e_safety_error "Owned frontend exited before readiness."
      return 1
    fi
    if curl --fail --silent --show-error "${url}" >/dev/null 2>&1; then
      assert_owned_frontend_process || return $?
      return 0
    fi
    sleep 1
  done
  e2e_safety_error "Owned frontend did not become ready before the bounded deadline."
  return 1
}

cleanup() {
  local original_exit_code="$?"
  local cleanup_exit_code=0
  trap - EXIT INT TERM
  set +e

  if [[ -n "${frontend_pid}" ]] && kill -0 "${frontend_pid}" 2>/dev/null; then
    if assert_owned_frontend_process; then
      kill "${frontend_pid}" >/dev/null 2>&1
      wait "${frontend_pid}" >/dev/null 2>&1
    else
      e2e_safety_error "Frontend ownership changed during cleanup; the process was not terminated."
      cleanup_exit_code="${process_ownership_exit_code}"
    fi
  fi
  if [[ -n "${backend_pid}" ]] && kill -0 "${backend_pid}" 2>/dev/null; then
    if assert_owned_backend_process true; then
      kill "${backend_pid}" >/dev/null 2>&1
      wait "${backend_pid}" >/dev/null 2>&1
    else
      e2e_safety_error "Backend ownership changed during cleanup; the process was not terminated."
      cleanup_exit_code="${process_ownership_exit_code}"
    fi
  fi
  wait_for_port_to_close "${E2E_BACKEND_PORT}" || { e2e_safety_error "Backend port remains occupied; no unowned process was terminated."; cleanup_exit_code="${process_ownership_exit_code}"; }
  wait_for_port_to_close "${E2E_FRONTEND_PORT}" || { e2e_safety_error "Frontend port remains occupied; no unowned process was terminated."; cleanup_exit_code="${process_ownership_exit_code}"; }

  if [[ "${resource_scope_initialized}" == "1" ]]; then
    for database_name in "${directory_database}" "${cheongju_database}" "${osan_database}"; do
      run_admin_psql postgres "drop database if exists \"${database_name}\" with (force);" >/dev/null 2>&1 || { echo "Synthetic database cleanup failed." >&2; cleanup_exit_code=1; }
    done
    for role_name in "${directory_migrator}" "${directory_runtime}" "${cheongju_migrator}" "${cheongju_runtime}" "${osan_migrator}" "${osan_runtime}"; do
      run_admin_psql postgres "drop role if exists \"${role_name}\";" >/dev/null 2>&1 || { echo "Synthetic role cleanup failed." >&2; cleanup_exit_code=1; }
    done
    [[ "$(run_admin_scalar "select count(*) from pg_database where datname in ('${directory_database}', '${cheongju_database}', '${osan_database}');")" == "0" ]] || { echo "Synthetic database drop assertion failed." >&2; cleanup_exit_code=1; }
    [[ "$(run_admin_scalar "select count(*) from pg_roles where rolname in ('${directory_migrator}', '${directory_runtime}', '${cheongju_migrator}', '${cheongju_runtime}', '${osan_migrator}', '${osan_runtime}');")" == "0" ]] || { echo "Synthetic role drop assertion failed." >&2; cleanup_exit_code=1; }
    e2e_stop_project >/dev/null 2>&1 || { echo "Synthetic Compose cleanup failed." >&2; cleanup_exit_code=1; }
  fi
  rm -f "${backend_log}" "${frontend_log}" "${operation_log}" "${backend_pid_file}" "${frontend_pid_file}" \
    || { echo "Synthetic temporary-file cleanup failed." >&2; cleanup_exit_code=1; }
  [[ ! -e "${backend_log}" && ! -e "${frontend_log}" && ! -e "${operation_log}" \
      && ! -e "${backend_pid_file}" && ! -e "${frontend_pid_file}" ]] \
    || { echo "Synthetic temporary-file cleanup assertion failed." >&2; cleanup_exit_code=1; }

  if [[ "${cleanup_exit_code}" -eq 0 ]]; then
    echo "Synthetic three-database, bounded-role, process, and Compose cleanup verified."
  fi

  if [[ "${cleanup_exit_code}" -ne 0 ]]; then
    exit "${cleanup_exit_code}"
  fi
  exit "${original_exit_code}"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

resource_scope_initialized=1
e2e_start_postgres
for database_name in "${directory_database}" "${cheongju_database}" "${osan_database}"; do
  run_admin_psql postgres "drop database if exists \"${database_name}\" with (force);"
  run_admin_psql postgres "create database \"${database_name}\";"
done

admin_connection() {
  local database_name="$1"
  printf 'Host=%s;Port=%s;Database=%s;Username=%s;Password=%s;Pooling=false' \
    "${DATABASE_HOST}" "${DATABASE_PORT}" "${database_name}" "${E2E_DATABASE_USER}" "${E2E_DATABASE_PASSWORD}"
}

bounded_connection() {
  local database_name="$1"
  local role_name="$2"
  printf 'Host=%s;Port=%s;Database=%s;Username=%s;Password=%s;Pooling=false' \
    "${DATABASE_HOST}" "${DATABASE_PORT}" "${database_name}" "${role_name}" "${bounded_password}"
}

export ASPNETCORE_ENVIRONMENT=Testing
export AUTH_MODE=Dev
export Authentication__Mode=Dev
export DEV_AUTHENTICATION_ENABLED=true
export DevAuthentication__Enabled=true
export DEV_DATA_SEED_ENABLED=true
export DevelopmentData__SeedEnabled=true
export DATABASE_APPLY_MIGRATIONS_ON_STARTUP=false
export Database__ApplyMigrationsOnStartup=false
export BusinessUnits__Enabled=true
export BusinessUnits__Directory__Code=DIRECTORY
export BusinessUnits__Directory__RuntimeConnection=DirectoryRuntime
export BusinessUnits__Directory__MigrationConnection=DirectoryMigration
export BusinessUnits__Directory__AdministratorConnection=DirectoryAdmin
export BusinessUnits__Directory__ExpectedDatabaseName="${directory_database}"
export BusinessUnits__Directory__MigrationRoleName="${directory_migrator}"
export BusinessUnits__Directory__RuntimeRoleName="${directory_runtime}"
export BusinessUnits__Directory__ExpectedSchemaVersion=0001_business_unit_directory
export BusinessUnits__Units__Cheongju__Code=CHEONGJU
export BusinessUnits__Units__Cheongju__RuntimeConnection=CheongjuRuntime
export BusinessUnits__Units__Cheongju__MigrationConnection=CheongjuMigration
export BusinessUnits__Units__Cheongju__AdministratorConnection=CheongjuAdmin
export BusinessUnits__Units__Cheongju__ExpectedDatabaseName="${cheongju_database}"
export BusinessUnits__Units__Cheongju__MigrationRoleName="${cheongju_migrator}"
export BusinessUnits__Units__Cheongju__RuntimeRoleName="${cheongju_runtime}"
export BusinessUnits__Units__Cheongju__ExpectedSchemaVersion=0086_business_unit_database_identity
export BusinessUnits__Units__Osan__Code=OSAN
export BusinessUnits__Units__Osan__RuntimeConnection=OsanRuntime
export BusinessUnits__Units__Osan__MigrationConnection=OsanMigration
export BusinessUnits__Units__Osan__AdministratorConnection=OsanAdmin
export BusinessUnits__Units__Osan__ExpectedDatabaseName="${osan_database}"
export BusinessUnits__Units__Osan__MigrationRoleName="${osan_migrator}"
export BusinessUnits__Units__Osan__RuntimeRoleName="${osan_runtime}"
export BusinessUnits__Units__Osan__ExpectedSchemaVersion=0086_business_unit_database_identity
export BusinessUnits__DevelopmentSeedUnits__0=CHEONGJU
export BusinessUnits__DevelopmentSeedUnits__1=OSAN
export ConnectionStrings__DirectoryAdmin="$(admin_connection "${directory_database}")"
export ConnectionStrings__DirectoryMigration="$(bounded_connection "${directory_database}" "${directory_migrator}")"
export ConnectionStrings__DirectoryRuntime="$(bounded_connection "${directory_database}" "${directory_runtime}")"
export ConnectionStrings__CheongjuAdmin="$(admin_connection "${cheongju_database}")"
export ConnectionStrings__CheongjuMigration="$(bounded_connection "${cheongju_database}" "${cheongju_migrator}")"
export ConnectionStrings__CheongjuRuntime="$(bounded_connection "${cheongju_database}" "${cheongju_runtime}")"
export ConnectionStrings__OsanAdmin="$(admin_connection "${osan_database}")"
export ConnectionStrings__OsanMigration="$(bounded_connection "${osan_database}" "${osan_migrator}")"
export ConnectionStrings__OsanRuntime="$(bounded_connection "${osan_database}" "${osan_runtime}")"
export AdminDeletionPurge__Enabled=false
export RateLimiting__Enabled=false
export E2E_BACKEND_PORT
export E2E_FRONTEND_PORT
export Frontend__Origin="http://127.0.0.1:${E2E_FRONTEND_PORT}"
export FRONTEND_ORIGIN="http://127.0.0.1:${E2E_FRONTEND_PORT}"

if ! dotnet build backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release --no-restore --nologo >"${operation_log}" 2>&1 \
  || ! dotnet run --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release --no-build -- --bootstrap-database-roles >>"${operation_log}" 2>&1 \
  || ! dotnet run --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release --no-build -- --migrate-only >>"${operation_log}" 2>&1; then
  cat "${operation_log}" >&2
  exit 1
fi
echo "Release build, bounded-role bootstrap, and directory/Cheongju/Osan migrations completed."

run_admin_psql "${directory_database}" "
insert into directory_identities (user_id, auth_provider, external_subject, display_name, is_active)
values
  ('50000000-0000-0000-0000-000000000001', 'Dev', 'dev-admin', 'Synthetic Overall Admin', true),
  ('50000000-0000-0000-0000-000000000002', 'Dev', 'dev-sales', 'Synthetic Sales User', true),
  ('50000000-0000-0000-0000-000000000005', 'Dev', 'dev-quality', 'Synthetic Osan User', true),
  ('71000000-0000-0000-0000-000000000003', 'EntraId', 'synthetic-pending-user', 'Synthetic Pending User', true);
insert into directory_business_unit_memberships (user_id, business_unit_code, is_active)
values
  ('50000000-0000-0000-0000-000000000001', 'CHEONGJU', true),
  ('50000000-0000-0000-0000-000000000001', 'OSAN', true),
  ('50000000-0000-0000-0000-000000000002', 'CHEONGJU', true),
  ('50000000-0000-0000-0000-000000000005', 'OSAN', true);
insert into directory_overall_administrators (user_id, is_active)
values ('50000000-0000-0000-0000-000000000001', true);"

if [[ "${self_test_mode}" == "--self-test-backend-startup-failure" ]]; then
  backend_expected_command_marker="emi-qms-business-unit-startup-failure-injection"
  backend_expected_url_marker=""
  backend_readiness_attempts=3
  bash -c 'sleep 0.2; exit 98' emi-qms-business-unit-startup-failure-injection >"${backend_log}" 2>&1 &
  backend_pid="$!"
  printf '%s\n' "${backend_pid}" >"${backend_pid_file}"
  if wait_for_owned_backend_ready "http://127.0.0.1:${E2E_BACKEND_PORT}/health/ready"; then
    e2e_safety_error "Injected backend startup failure unexpectedly reported readiness."
    exit 1
  fi
  set +e
  wait "${backend_pid}" >/dev/null 2>&1
  backend_exit_code="$?"
  set -e
  backend_pid=""
  if [[ "${backend_exit_code}" -ne 98 ]]; then
    e2e_safety_error "Injected backend startup process returned an unexpected exit code."
    exit 1
  fi
  echo "Injected backend startup failure was detected after three-database bootstrap and migration; owned cleanup will now run."
  exit 0
fi

dotnet "${backend_dll}" \
  --urls "http://127.0.0.1:${E2E_BACKEND_PORT}" >"${backend_log}" 2>&1 &
backend_pid="$!"
printf '%s\n' "${backend_pid}" >"${backend_pid_file}"
wait_for_owned_backend_ready "http://127.0.0.1:${E2E_BACKEND_PORT}/health/ready"

run_admin_psql "${cheongju_database}" "
update qms_users set display_name = 'Cheongju Admin' where id = '50000000-0000-0000-0000-000000000001';
insert into qms_users (id, development_user_key, display_name, department_id, is_active, auth_provider)
values ('79000000-0000-0000-0000-000000000001', 'boundary-profile', 'Cheongju Boundary Profile', null, true, 'Dev');"
run_admin_psql "${osan_database}" "
update qms_users set display_name = 'Osan Admin' where id = '50000000-0000-0000-0000-000000000001';
insert into qms_users (id, development_user_key, display_name, department_id, is_active, auth_provider)
values ('79000000-0000-0000-0000-000000000001', 'boundary-profile', 'Osan Boundary Profile', null, true, 'Dev');"

if [[ "${review_server_mode}" == "--review-server" ]]; then
  (
    cd "${repo_root}/frontend"
    export VITE_AUTH_MODE=Dev
    export VITE_API_BASE_URL="http://127.0.0.1:${E2E_BACKEND_PORT}"
    export VITE_DEV_USER_KEY=dev-admin
    export VITE_DEV_SERVER_PORT="${E2E_FRONTEND_PORT}"
    exec ./node_modules/.bin/vite --host 127.0.0.1 --port "${E2E_FRONTEND_PORT}" --strictPort
  ) >"${frontend_log}" 2>&1 &
  frontend_pid="$!"
  printf '%s\n' "${frontend_pid}" >"${frontend_pid_file}"
  wait_for_frontend_ready "http://127.0.0.1:${E2E_FRONTEND_PORT}/admin/users"
  echo "Synthetic exact-source review server is ready."
  echo "Frontend URL: http://127.0.0.1:${E2E_FRONTEND_PORT}/admin/users"
  echo "Personas: dev-admin (overall), dev-sales (Cheongju only), dev-quality (Osan only)."
  echo "Pending user: Synthetic Pending User. External providers and workers are disabled."
  while kill -0 "${backend_pid}" 2>/dev/null && kill -0 "${frontend_pid}" 2>/dev/null; do
    sleep 5
  done
  e2e_safety_error "A review server component exited unexpectedly."
  exit 1
fi

echo "Business-unit access Full-Stack E2E uses owned tmpfs PostgreSQL with three databases and six bounded roles."
cd frontend
corepack pnpm exec playwright test --config playwright.business-unit-access.full-stack.config.ts "$@"
