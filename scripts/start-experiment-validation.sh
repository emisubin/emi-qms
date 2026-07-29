#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd -P)"

backend_port="41166"
frontend_port="42983"
backend_url="http://127.0.0.1:${backend_port}"
frontend_url="http://127.0.0.1:${frontend_port}"
database_container="emi-qms-postgres"

runtime_key="$(printf '%s' "${repo_root}" | cksum | awk '{print $1}')"
tmp_root="${TMPDIR:-/tmp}"
runtime_base="${tmp_root%/}/emi-qms-experiment-validation"
runtime_dir="${runtime_base}/${runtime_key}"
backend_log="${runtime_dir}/backend.log"
frontend_log="${runtime_dir}/frontend.log"

new_backend_pid=""
new_frontend_pid=""
startup_complete="false"

say() {
  printf '%s\n' "$*"
}

fail() {
  printf '\n[실행 실패] %s\n' "$*" >&2
  exit 1
}

require_command() {
  local command_name="$1"
  command -v "${command_name}" >/dev/null 2>&1 ||
    fail "'${command_name}' 명령을 찾을 수 없습니다. 개발 도구 설치 상태를 확인해 주세요."
}

write_private_file() {
  local target="$1"
  local value="$2"
  local temporary="${target}.tmp.$$"

  umask 077
  printf '%s\n' "${value}" > "${temporary}"
  mv "${temporary}" "${target}"
}

find_port_pids() {
  local port="$1"
  lsof -nP -tiTCP:"${port}" -sTCP:LISTEN 2>/dev/null | sort -u || true
}

single_port_pid() {
  local port="$1"
  local pids
  local count

  pids="$(find_port_pids "${port}")"
  count="$(printf '%s\n' "${pids}" | awk 'NF { count += 1 } END { print count + 0 }')"
  [[ "${count}" == "1" ]] || return 1
  printf '%s\n' "${pids}"
}

pid_cwd() {
  local pid="$1"
  lsof -a -p "${pid}" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p' | head -1
}

pid_command() {
  local pid="$1"
  ps -p "${pid}" -o command= 2>/dev/null || true
}

pid_start_fingerprint() {
  local pid="$1"
  LC_ALL=C ps -p "${pid}" -o lstart= 2>/dev/null | awk '{$1=$1; print}' || true
}

path_is_within_repo() {
  local path="$1"
  [[ "${path}" == "${repo_root}" || "${path}" == "${repo_root}/"* ]]
}

is_descendant_or_same() {
  local pid="$1"
  local ancestor="$2"
  local parent
  local depth="0"

  while [[ -n "${pid}" && "${pid}" != "0" && "${depth}" -lt 40 ]]; do
    [[ "${pid}" == "${ancestor}" ]] && return 0
    parent="$(ps -p "${pid}" -o ppid= 2>/dev/null | tr -d '[:space:]' || true)"
    [[ -n "${parent}" && "${parent}" != "${pid}" ]] || return 1
    pid="${parent}"
    depth=$((depth + 1))
  done

  return 1
}

http_ok() {
  local url="$1"
  curl -fsS --max-time 3 "${url}" >/dev/null 2>&1
}

component_pid_file() {
  printf '%s/%s.pid\n' "${runtime_dir}" "$1"
}

component_session_file() {
  printf '%s/%s.session\n' "${runtime_dir}" "$1"
}

component_repo_file() {
  printf '%s/%s.repo\n' "${runtime_dir}" "$1"
}

component_log_file() {
  case "$1" in
    backend) printf '%s\n' "${backend_log}" ;;
    frontend) printf '%s\n' "${frontend_log}" ;;
    *) return 1 ;;
  esac
}

component_port() {
  case "$1" in
    backend) printf '%s\n' "${backend_port}" ;;
    frontend) printf '%s\n' "${frontend_port}" ;;
    *) return 1 ;;
  esac
}

component_script() {
  case "$1" in
    backend) printf '%s/dev-experiment-validation-backend.sh\n' "${script_dir}" ;;
    frontend) printf '%s/dev-experiment-validation-frontend.sh\n' "${script_dir}" ;;
    *) return 1 ;;
  esac
}

component_label() {
  case "$1" in
    backend) printf '백엔드' ;;
    frontend) printf '프론트엔드' ;;
    *) return 1 ;;
  esac
}

component_health_ok() {
  case "$1" in
    backend)
      http_ok "${backend_url}/health/live" &&
        http_ok "${backend_url}/health/ready"
      ;;
    frontend)
      http_ok "${frontend_url}/" &&
        http_ok "${frontend_url}/health/ready"
      ;;
    *)
      return 1
      ;;
  esac
}

expected_listener_command() {
  local component="$1"
  local pid="$2"
  local command_text

  command_text="$(pid_command "${pid}")"
  case "${component}" in
    backend)
      [[ "${command_text}" == *"dotnet"* || "${command_text}" == *"Emi.Qms.Api"* ]]
      ;;
    frontend)
      [[ "${command_text}" == *"vite"* || "${command_text}" == *"node"* ]]
      ;;
    *)
      return 1
      ;;
  esac
}

owned_component_is_ready() {
  local component="$1"
  local port
  local pid_file
  local session_file
  local repo_file
  local owner_pid
  local expected_fingerprint
  local actual_fingerprint
  local listener_pid
  local listener_cwd

  port="$(component_port "${component}")"
  pid_file="$(component_pid_file "${component}")"
  session_file="$(component_session_file "${component}")"
  repo_file="$(component_repo_file "${component}")"

  [[ -f "${pid_file}" && -f "${session_file}" && -f "${repo_file}" ]] || return 1
  [[ "$(cat "${repo_file}")" == "${repo_root}" ]] || return 1

  owner_pid="$(cat "${pid_file}")"
  [[ "${owner_pid}" =~ ^[0-9]+$ ]] || return 1
  kill -0 "${owner_pid}" 2>/dev/null || return 1

  expected_fingerprint="$(cat "${session_file}")"
  actual_fingerprint="$(pid_start_fingerprint "${owner_pid}")"
  [[ -n "${actual_fingerprint}" ]] || return 1

  listener_pid="$(single_port_pid "${port}")" || return 1
  is_descendant_or_same "${listener_pid}" "${owner_pid}" || return 1

  listener_cwd="$(pid_cwd "${listener_pid}")"
  [[ -n "${listener_cwd}" ]] || return 1
  path_is_within_repo "${listener_cwd}" || return 1
  expected_listener_command "${component}" "${listener_pid}" || return 1
  component_health_ok "${component}" || return 1

  if [[ "${actual_fingerprint}" != "${expected_fingerprint}" ]]; then
    write_private_file "${session_file}" "${actual_fingerprint}"
  fi

  return 0
}

remove_stale_component_state() {
  local component="$1"
  local pid_file
  local session_file
  local repo_file
  local owner_pid=""

  pid_file="$(component_pid_file "${component}")"
  session_file="$(component_session_file "${component}")"
  repo_file="$(component_repo_file "${component}")"

  if [[ -f "${pid_file}" ]]; then
    owner_pid="$(cat "${pid_file}" 2>/dev/null || true)"
  fi

  if [[ -n "${owner_pid}" && "${owner_pid}" =~ ^[0-9]+$ ]] && kill -0 "${owner_pid}" 2>/dev/null; then
    return 0
  fi

  rm -f "${pid_file}" "${session_file}" "${repo_file}"
}

terminate_component_started_here() {
  local component="$1"
  local owner_pid="$2"
  local port
  local listener_pid
  local listener_cwd

  [[ -n "${owner_pid}" ]] || return 0
  [[ "${owner_pid}" =~ ^[0-9]+$ ]] || return 0

  port="$(component_port "${component}")"
  while IFS= read -r listener_pid; do
    [[ -n "${listener_pid}" ]] || continue
    listener_cwd="$(pid_cwd "${listener_pid}")"
    if is_descendant_or_same "${listener_pid}" "${owner_pid}" &&
      [[ -n "${listener_cwd}" ]] &&
      path_is_within_repo "${listener_cwd}"; then
      kill -TERM "${listener_pid}" 2>/dev/null || true
    fi
  done <<< "$(find_port_pids "${port}")"

  kill -TERM "${owner_pid}" 2>/dev/null || true
}

cleanup_failed_startup() {
  local exit_code=$?

  if [[ "${startup_complete}" != "true" && "${exit_code}" -ne 0 ]]; then
    terminate_component_started_here "frontend" "${new_frontend_pid}"
    terminate_component_started_here "backend" "${new_backend_pid}"
  fi
}

trap cleanup_failed_startup EXIT

ensure_docker_ready() {
  local container_running
  local container_health

  if ! docker info >/dev/null 2>&1; then
    if [[ "$(uname -s)" == "Darwin" && -d "/Applications/Docker.app" ]]; then
      say "Docker Desktop을 시작하고 있습니다..."
      open -gja Docker
      for _ in $(seq 1 120); do
        docker info >/dev/null 2>&1 && break
        sleep 1
      done
    fi
  fi

  docker info >/dev/null 2>&1 ||
    fail "Docker가 실행되지 않았습니다. Docker Desktop을 먼저 실행한 뒤 다시 더블클릭해 주세요."

  docker inspect "${database_container}" >/dev/null 2>&1 ||
    fail "검수용 데이터베이스 컨테이너가 없습니다. '${database_container}' 초기 구성이 필요합니다."

  container_running="$(docker inspect -f '{{.State.Running}}' "${database_container}")"
  if [[ "${container_running}" != "true" ]]; then
    say "검수용 데이터베이스를 시작하고 있습니다..."
    docker start "${database_container}" >/dev/null
  fi

  for _ in $(seq 1 120); do
    container_health="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "${database_container}")"
    [[ "${container_health}" == "healthy" ]] && return 0
    [[ "${container_health}" == "unhealthy" ]] &&
      fail "검수용 데이터베이스 상태가 unhealthy입니다. Docker Desktop에서 상태를 확인해 주세요."
    sleep 1
  done

  fail "검수용 데이터베이스가 제한 시간 안에 준비되지 않았습니다."
}

ensure_frontend_dependencies() {
  local install_log="${runtime_dir}/frontend-install.log"

  if [[ -x "${repo_root}/frontend/node_modules/.bin/vite" ]]; then
    return 0
  fi

  say "프론트엔드 패키지를 처음 한 번 설치하고 있습니다..."
  (
    cd "${repo_root}/frontend"
    corepack pnpm install --frozen-lockfile
  ) > "${install_log}" 2>&1 ||
    fail "프론트엔드 패키지 설치에 실패했습니다. 로그: ${install_log}"
}

start_component() {
  local component="$1"
  local label
  local port
  local run_script
  local log_file
  local pid_file
  local session_file
  local repo_file
  local existing_pids
  local owner_pid
  local fingerprint
  local listener_pid
  local listener_cwd

  label="$(component_label "${component}")"
  port="$(component_port "${component}")"
  run_script="$(component_script "${component}")"
  log_file="$(component_log_file "${component}")"
  pid_file="$(component_pid_file "${component}")"
  session_file="$(component_session_file "${component}")"
  repo_file="$(component_repo_file "${component}")"

  if owned_component_is_ready "${component}"; then
    say "✓ ${label}가 이미 정상 실행 중입니다. (${port})"
    return 0
  fi

  existing_pids="$(find_port_pids "${port}")"
  if [[ -n "${existing_pids}" ]]; then
    fail "${port} 포트가 이 실행 파일이 소유하지 않은 process에 사용 중입니다. 안전을 위해 종료하거나 다른 포트로 우회하지 않았습니다."
  fi

  remove_stale_component_state "${component}"
  if [[ -f "${pid_file}" ]]; then
    fail "${label} 실행 기록의 process가 살아 있지만 준비되지 않았습니다. 로그: ${log_file}"
  fi

  [[ -f "${run_script}" ]] || fail "${label} 실행 script가 없습니다: ${run_script}"
  : > "${log_file}"
  chmod 600 "${log_file}"

  say "${label}를 시작하고 있습니다..."
  nohup bash "${run_script}" >> "${log_file}" 2>&1 < /dev/null &
  owner_pid=$!

  fingerprint="$(pid_start_fingerprint "${owner_pid}")"
  [[ -n "${fingerprint}" ]] || fail "${label} 시작 process를 확인하지 못했습니다."
  write_private_file "${pid_file}" "${owner_pid}"
  write_private_file "${session_file}" "${fingerprint}"
  write_private_file "${repo_file}" "${repo_root}"

  case "${component}" in
    backend) new_backend_pid="${owner_pid}" ;;
    frontend) new_frontend_pid="${owner_pid}" ;;
  esac

  for _ in $(seq 1 180); do
    kill -0 "${owner_pid}" 2>/dev/null ||
      fail "${label}가 준비되기 전에 종료됐습니다. 로그: ${log_file}"

    if component_health_ok "${component}"; then
      listener_pid="$(single_port_pid "${port}")" ||
        fail "${label} 포트의 단일 listener를 확인하지 못했습니다."
      is_descendant_or_same "${listener_pid}" "${owner_pid}" ||
        fail "${label} listener가 이번 실행 session 소유가 아닙니다."
      listener_cwd="$(pid_cwd "${listener_pid}")"
      if [[ -z "${listener_cwd}" ]] || ! path_is_within_repo "${listener_cwd}"; then
        fail "${label} listener의 작업 경로가 현재 실험 Repository가 아닙니다."
      fi
      expected_listener_command "${component}" "${listener_pid}" ||
        fail "${label} listener 명령이 예상한 개발 서버가 아닙니다."

      say "✓ ${label} 준비 완료 (${port})"
      return 0
    fi

    sleep 1
  done

  fail "${label}가 제한 시간 안에 준비되지 않았습니다. 로그: ${log_file}"
}

main() {
  require_command "awk"
  require_command "corepack"
  require_command "curl"
  require_command "docker"
  require_command "dotnet"
  require_command "lsof"
  require_command "open"
  require_command "ps"

  mkdir -p "${runtime_dir}"
  chmod 700 "${runtime_dir}"

  say "EMI 실험 사용자 검수 서버"
  say "Repository: ${repo_root}"
  say

  ensure_docker_ready
  ensure_frontend_dependencies
  start_component "backend"
  start_component "frontend"

  component_health_ok "backend" || fail "최종 백엔드 readiness 확인에 실패했습니다."
  component_health_ok "frontend" || fail "최종 프론트엔드 readiness 확인에 실패했습니다."

  startup_complete="true"
  say
  say "검수 서버가 준비됐습니다."
  say "프론트엔드: ${frontend_url}"
  say "백엔드:     ${backend_url}"
  say "로그 위치:  ${runtime_dir}"
  say
  say "이 Terminal 창은 닫아도 서버가 계속 실행됩니다."

  if [[ "${EXPERIMENT_VALIDATION_NO_BROWSER:-0}" != "1" ]]; then
    open "${frontend_url}"
  fi
}

main "$@"
