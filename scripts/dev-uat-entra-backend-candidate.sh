#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${UAT_ENTRA_ENV_FILE:-${REPO_ROOT}/.env.entra-local}"
BACKEND_PORT="${UAT_ENTRA_CANDIDATE_BACKEND_PORT:-5084}"
COMMAND="${1:-start}"

find_listener_pids() {
  lsof -tiTCP:"${BACKEND_PORT}" -sTCP:LISTEN 2>/dev/null || true
}

is_repo_candidate_process() {
  local pid="$1"
  local process_cwd
  local process_command

  process_cwd="$(lsof -a -p "${pid}" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p' | tail -n 1)"
  process_command="$(ps -p "${pid}" -o command= 2>/dev/null || true)"

  case "${process_cwd}" in
    "${REPO_ROOT}"|"${REPO_ROOT}"/*)
      ;;
    *)
      return 1
      ;;
  esac

  [[ "${process_command}" == *"Emi.Qms.Api"* ]]
}

stop_candidate() {
  local listener_pids
  local pid
  local attempt

  listener_pids="$(find_listener_pids)"
  if [[ -z "${listener_pids}" ]]; then
    echo "No Entra candidate backend is listening on port ${BACKEND_PORT}."
    return 0
  fi

  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    if ! is_repo_candidate_process "${pid}"; then
      echo "Port ${BACKEND_PORT} is owned by a process outside this repository candidate; nothing was stopped." >&2
      return 1
    fi
  done <<< "${listener_pids}"

  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    kill -TERM "${pid}"
  done <<< "${listener_pids}"

  attempt=0
  while (( attempt < 20 )); do
    if [[ -z "$(find_listener_pids)" ]]; then
      echo "Stopped the Entra candidate backend on port ${BACKEND_PORT}."
      return 0
    fi
    sleep 0.25
    attempt=$((attempt + 1))
  done

  echo "The Entra candidate backend did not stop after SIGTERM; no force kill was used." >&2
  return 1
}

case "${COMMAND}" in
  start)
    ;;
  stop)
    stop_candidate
    exit $?
    ;;
  *)
    echo "Usage: $0 [start|stop]" >&2
    exit 1
    ;;
esac

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "The local Entra environment file is missing." >&2
  exit 1
fi

if [[ -n "$(lsof -tiTCP:"${BACKEND_PORT}" -sTCP:LISTEN 2>/dev/null || true)" ]]; then
  echo "The Entra candidate backend port is already occupied; no process was stopped." >&2
  exit 1
fi

load_allowed_env() {
  local line key value
  while IFS= read -r line || [[ -n "${line}" ]]; do
    line="${line#"${line%%[![:space:]]*}"}"
    line="${line%"${line##*[![:space:]]}"}"
    [[ -z "${line}" || "${line}" == \#* || "${line}" != *=* ]] && continue
    key="${line%%=*}"
    value="${line#*=}"
    key="${key#"${key%%[![:space:]]*}"}"
    key="${key%"${key##*[![:space:]]}"}"
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"

    case "${key}" in
      DATABASE_HOST|DATABASE_PORT|DATABASE_NAME|DATABASE_USER|DATABASE_PASSWORD|AzureAd__*|Authentication__BootstrapAdminEmails|AdminUserSwitch__Enabled)
        ;;
      *)
        continue
        ;;
    esac

    if [[ "${value}" == \"*\" && "${value}" == *\" && "${#value}" -ge 2 ]]; then
      value="${value:1:${#value}-2}"
    elif [[ "${value}" == \'*\' && "${value}" == *\' && "${#value}" -ge 2 ]]; then
      value="${value:1:${#value}-2}"
    fi
    export "${key}=${value}"
  done < "${ENV_FILE}"
}

load_allowed_env

RUNNING_DATABASE_PASSWORD="$(
  docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' emi-qms-postgres 2>/dev/null \
    | sed -n 's/^POSTGRES_PASSWORD=//p' \
    | tail -n 1
)"
if [[ -z "${RUNNING_DATABASE_PASSWORD}" ]]; then
  echo "The running PostgreSQL password could not be resolved without changing the database." >&2
  exit 1
fi
export DATABASE_PASSWORD="${RUNNING_DATABASE_PASSWORD}"
unset RUNNING_DATABASE_PASSWORD

for required_key in AzureAd__TenantId AzureAd__ClientId AzureAd__Audience; do
  if [[ -z "$(printenv "${required_key}" 2>/dev/null || true)" ]]; then
    echo "The local Entra environment is incomplete." >&2
    exit 1
  fi
done

if [[ "${DATABASE_NAME:-}" != "emi_qms_uat_005a" ]]; then
  echo "The Entra candidate must use the existing manual UAT database." >&2
  exit 1
fi

if [[ "$(docker inspect -f '{{.State.Health.Status}}' emi-qms-postgres 2>/dev/null || true)" != "healthy" ]]; then
  echo "The existing PostgreSQL container must already be healthy." >&2
  exit 1
fi

export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="http://127.0.0.1:${BACKEND_PORT}"
export AUTH_MODE="EntraId"
export Authentication__Mode="EntraId"
export DEV_AUTHENTICATION_ENABLED="false"
export DEV_DATA_SEED_ENABLED="false"
export DevelopmentData__SeedEnabled="false"
export DATABASE_APPLY_MIGRATIONS_ON_STARTUP="false"
export Database__ApplyMigrationsOnStartup="false"
export FRONTEND_ORIGIN="https://localhost:5174"
export Frontend__Origin="https://localhost:5174"
export Notifications__Dispatch__Enabled="false"
export Notifications__DailyDigest__Enabled="false"
export Notifications__Escalation__Enabled="false"
export Notifications__Teams__Enabled="false"
export Notifications__Teams__DryRun="true"
export Notifications__TeamsActivity__Enabled="false"
export Notifications__TeamsActivity__DryRun="true"
export Notifications__Mail__Enabled="false"
export Notifications__Mail__DryRun="true"
export Notifications__Mail__Provider="DryRun"
export AdminDeletionPurge__Enabled="false"

cd "${REPO_ROOT}"
exec dotnet run \
  --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj \
  --configuration Release \
  --no-build
