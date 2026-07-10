#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
GIT_COMMON_DIR="$(cd "${REPO_ROOT}" && git rev-parse --path-format=absolute --git-common-dir)"
PRIMARY_REPO_ROOT="$(cd "${GIT_COMMON_DIR}/.." && pwd)"

BACKEND_PORT="5092"
FRONTEND_PORT="5190"
BACKEND_SESSION="emi-qms-uat-review-backend"
FRONTEND_SESSION="emi-qms-uat-review-frontend"
BACKEND_PID_FILE="/tmp/emi-qms-uat-review-backend.pid"
FRONTEND_PID_FILE="/tmp/emi-qms-uat-review-frontend.pid"
BACKEND_LOG="/tmp/emi-qms-uat-review-backend.log"
FRONTEND_LOG="/tmp/emi-qms-uat-review-frontend.log"
REVIEW_HTTPS="${REVIEW_UAT_FRONTEND_HTTPS:-false}"

load_database_dotenv() {
  local env_file="$1"
  local line key value
  [[ -f "${env_file}" ]] || return 0

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
      DATABASE_HOST|DATABASE_PORT|DATABASE_USER|DATABASE_PASSWORD|DATABASE_NAME|UAT_DATABASE_NAME)
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
  done < "${env_file}"
}

find_port_pids() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | sort -u || true
}

pid_cwd() {
  lsof -a -p "$1" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p' | head -1
}

assert_port_free() {
  local port="$1"
  local pids
  pids="$(find_port_pids "${port}")"
  if [[ -n "${pids}" ]]; then
    echo "Review-safe UAT port ${port} is already occupied; no process was stopped." >&2
    lsof -nP -iTCP:"${port}" -sTCP:LISTEN >&2 || true
    exit 1
  fi
}

wait_url() {
  local url="$1"
  local label="$2"
  local log_file="$3"
  local curl_args="-fsS"
  for _ in $(seq 1 120); do
    if curl ${curl_args} --max-time 3 "${url}" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  echo "${label} did not become ready." >&2
  [[ -f "${log_file}" ]] && tail -80 "${log_file}" >&2 || true
  exit 1
}

assert_owned_listener() {
  local port="$1"
  local expected_root="$2"
  local pid cwd command
  pid="$(find_port_pids "${port}" | head -1)"
  [[ -n "${pid}" ]] || { echo "No listener found on ${port}." >&2; exit 1; }
  cwd="$(pid_cwd "${pid}")"
  command="$(ps -p "${pid}" -o command= 2>/dev/null || true)"
  case "${cwd}/" in
    "${expected_root}/"*) ;;
    *)
      echo "Listener ${pid} on ${port} is not owned by the Review-safe worktree." >&2
      exit 1
      ;;
  esac
  case "${command}" in
    *dotnet*|*Emi.Qms.Api*|*vite*|*node*) ;;
    *)
      echo "Listener ${pid} on ${port} has an unexpected command." >&2
      exit 1
      ;;
  esac
  printf '%s\n' "${pid}"
}

cd "${REPO_ROOT}"
load_database_dotenv "${PRIMARY_REPO_ROOT}/.env"

export DATABASE_HOST="${DATABASE_HOST:-localhost}"
export DATABASE_PORT="${DATABASE_PORT:-5432}"
export DATABASE_USER="${DATABASE_USER:-emi_qms}"
export DATABASE_NAME="${UAT_DATABASE_NAME:-emi_qms_uat_005a}"
if [[ "${DATABASE_NAME}" != "emi_qms_uat_005a" ]]; then
  echo "Review-safe UAT must use the canonical persistent UAT database." >&2
  exit 1
fi
if [[ -z "${DATABASE_PASSWORD:-}" ]]; then
  echo "DATABASE_PASSWORD is required; its value will not be printed." >&2
  exit 1
fi

if [[ "$(docker inspect -f '{{.State.Health.Status}}' emi-qms-postgres 2>/dev/null || true)" != "healthy" ]]; then
  echo "Persistent UAT PostgreSQL must already be healthy; this script will not start or restart it." >&2
  exit 1
fi
if ! docker exec emi-qms-postgres psql -U "${DATABASE_USER}" -d postgres -tAc \
  "select 1 from pg_database where datname = 'emi_qms_uat_005a'" | grep -q 1; then
  echo "Persistent UAT database is unavailable; this script will not create it." >&2
  exit 1
fi

while IFS= read -r key; do
  case "${key}" in
    Notifications__*|AzureAd__*|AUTHENTICATION_BOOTSTRAP_ADMIN_EMAILS|Authentication__BootstrapAdminEmails|AZURE_AD_*|SMTP_*|TEAMS_*|GRAPH_*)
      unset "${key}"
      ;;
  esac
done < <(compgen -e)

export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="http://127.0.0.1:${BACKEND_PORT}"
export AUTH_MODE="Dev"
export Authentication__Mode="Dev"
export DEV_AUTHENTICATION_ENABLED="true"
export DEV_DATA_SEED_ENABLED="false"
export DevelopmentData__SeedEnabled="false"
export Database__ApplyMigrationsOnStartup="false"
export DATABASE_APPLY_MIGRATIONS_ON_STARTUP="false"
export REVIEW_SAFE_ENABLED="true"
export ReviewSafe__Enabled="true"
export Notifications__Dispatch__Enabled="false"
export Notifications__Escalation__Enabled="false"
export Notifications__DailyDigest__Enabled="false"
export Notifications__Teams__Enabled="false"
export Notifications__TeamsActivity__Enabled="false"
export Notifications__Mail__Enabled="false"
export FRONTEND_ORIGIN="https://localhost:${FRONTEND_PORT},http://localhost:${FRONTEND_PORT},http://127.0.0.1:${FRONTEND_PORT}"
export VITE_API_BASE_URL=""
export VITE_DEV_PROXY_TARGET="http://127.0.0.1:${BACKEND_PORT}"
export VITE_DEV_SERVER_PORT="${FRONTEND_PORT}"
export VITE_HMR_CLIENT_PORT="${FRONTEND_PORT}"
export VITE_AUTH_MODE="Dev"
export VITE_DEV_USER_KEY="dev-admin"

if [[ "$(printf '%s' "${REVIEW_HTTPS}" | tr '[:upper:]' '[:lower:]')" == "true" || "${REVIEW_HTTPS}" == "1" ]]; then
  CERT_PATH="${VITE_DEV_HTTPS_CERT:-${PRIMARY_REPO_ROOT}/.certs/localhost.pem}"
  KEY_PATH="${VITE_DEV_HTTPS_KEY:-${PRIMARY_REPO_ROOT}/.certs/localhost-key.pem}"
  if [[ ! -f "${CERT_PATH}" || ! -f "${KEY_PATH}" ]]; then
    echo "Ignored localhost HTTPS certificate and key files are required." >&2
    exit 1
  fi
  export VITE_DEV_HTTPS="true"
  export VITE_DEV_HTTPS_CERT="${CERT_PATH}"
  export VITE_DEV_HTTPS_KEY="${KEY_PATH}"
  export VITE_HMR_HOST="localhost"
  FRONTEND_URL="https://localhost:${FRONTEND_PORT}"
  WRONG_PROTOCOL_URL="http://localhost:${FRONTEND_PORT}"
else
  unset VITE_DEV_HTTPS VITE_DEV_HTTPS_CERT VITE_DEV_HTTPS_KEY
  export VITE_HMR_HOST="127.0.0.1"
  FRONTEND_URL="http://localhost:${FRONTEND_PORT}"
  WRONG_PROTOCOL_URL="https://localhost:${FRONTEND_PORT}"
fi

assert_port_free "${BACKEND_PORT}"
assert_port_free "${FRONTEND_PORT}"
if screen -ls 2>/dev/null | grep -Eq "\.${BACKEND_SESSION}[[:space:]]|\.${FRONTEND_SESSION}[[:space:]]"; then
  echo "A Review-safe UAT screen session already exists; no session was changed." >&2
  exit 1
fi

[[ -d frontend/node_modules ]] || { echo "Run pnpm install --frozen-lockfile before starting Review-safe UAT." >&2; exit 1; }
: > "${BACKEND_LOG}"
: > "${FRONTEND_LOG}"

screen -dmS "${BACKEND_SESSION}" bash -c \
  "cd '${REPO_ROOT}' && dotnet run --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release > '${BACKEND_LOG}' 2>&1"
wait_url "http://127.0.0.1:${BACKEND_PORT}/health/live" "Review-safe backend live health" "${BACKEND_LOG}"
wait_url "http://127.0.0.1:${BACKEND_PORT}/health/ready" "Review-safe backend readiness" "${BACKEND_LOG}"
BACKEND_PID="$(assert_owned_listener "${BACKEND_PORT}" "${REPO_ROOT}")"
printf '%s\n' "${BACKEND_PID}" > "${BACKEND_PID_FILE}"

screen -dmS "${FRONTEND_SESSION}" bash -c \
  "cd '${REPO_ROOT}/frontend' && corepack pnpm exec vite --host 127.0.0.1 --port '${FRONTEND_PORT}' --strictPort > '${FRONTEND_LOG}' 2>&1"
wait_url "${FRONTEND_URL}/" "Review-safe frontend" "${FRONTEND_LOG}"
wait_url "${FRONTEND_URL}/teams/activity" "Review-safe Teams Activity" "${FRONTEND_LOG}"
wait_url "${FRONTEND_URL}/health/ready" "Review-safe frontend health proxy" "${FRONTEND_LOG}"
FRONTEND_PID="$(assert_owned_listener "${FRONTEND_PORT}" "${REPO_ROOT}/frontend")"
printf '%s\n' "${FRONTEND_PID}" > "${FRONTEND_PID_FILE}"

if curl -fsS --max-time 3 "${WRONG_PROTOCOL_URL}/" >/dev/null 2>&1; then
  echo "Protocol mismatch endpoint unexpectedly succeeded." >&2
  exit 1
fi

echo "Review-safe UAT started without migration, seed, worker or provider activation."
echo "Backend: http://127.0.0.1:${BACKEND_PORT} (PID ${BACKEND_PID})"
echo "Frontend: ${FRONTEND_URL} (PID ${FRONTEND_PID})"
