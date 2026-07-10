#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${repo_root}"

# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"
e2e_initialize_environment "${repo_root}"
e2e_disable_external_providers

cleanup_started=0
resource_scope_initialized=0

wait_for_port_to_close() {
  local port="$1"
  for _ in $(seq 1 50); do
    if e2e_port_is_available "${port}"; then
      return 0
    fi
    sleep 0.1
  done

  echo "E2E port ${port} is still listening after Playwright shutdown." >&2
  return 1
}

cleanup() {
  local original_exit_code="$?"
  local cleanup_exit_code=0

  if [[ "${cleanup_started}" == "1" ]]; then
    exit "${original_exit_code}"
  fi

  cleanup_started=1
  trap - EXIT INT TERM
  set +e

  wait_for_port_to_close "${E2E_BACKEND_PORT}" || cleanup_exit_code=1
  wait_for_port_to_close "${E2E_FRONTEND_PORT}" || cleanup_exit_code=1

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

echo "Full-Stack E2E uses an isolated PostgreSQL Compose project and tmpfs storage."
cd frontend
corepack pnpm exec playwright test --config playwright.full-stack.config.ts "$@"
