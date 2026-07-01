#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

export E2E_DATABASE_NAME="${E2E_DATABASE_NAME:-emi_qms_e2e}"
export E2E_BACKEND_PORT="${E2E_BACKEND_PORT:-5082}"
export E2E_FRONTEND_PORT="${E2E_FRONTEND_PORT:-5175}"
export DATABASE_HOST="${DATABASE_HOST:-localhost}"
export DATABASE_PORT="${DATABASE_PORT:-5432}"
export DATABASE_USER="${DATABASE_USER:-emi_qms}"
export DATABASE_PASSWORD="${DATABASE_PASSWORD:?DATABASE_PASSWORD is required}"

cleanup_started=0
cleanup() {
  local original_exit_code="$?"
  local cleanup_exit_code=0

  if [[ "$cleanup_started" == "1" ]]; then
    exit "$original_exit_code"
  fi

  cleanup_started=1
  trap - EXIT INT TERM
  set +e

  wait_for_port_to_close "$E2E_BACKEND_PORT" || cleanup_exit_code=1
  wait_for_port_to_close "$E2E_FRONTEND_PORT" || cleanup_exit_code=1

  if ! bash "$repo_root/scripts/e2e-db.sh" drop >&2; then
    echo "Full-stack E2E cleanup failed while dropping database '$E2E_DATABASE_NAME'." >&2
    cleanup_exit_code=1
  fi

  if ! bash "$repo_root/scripts/e2e-db.sh" assert-dropped >&2; then
    echo "Full-stack E2E cleanup verification failed for database '$E2E_DATABASE_NAME'." >&2
    cleanup_exit_code=1
  fi

  if [[ "$original_exit_code" -eq 0 && "$cleanup_exit_code" -ne 0 ]]; then
    exit "$cleanup_exit_code"
  fi

  exit "$original_exit_code"
}

wait_for_port_to_close() {
  local port="$1"
  for _ in {1..50}; do
    if ! (echo >"/dev/tcp/127.0.0.1/$port") >/dev/null 2>&1; then
      return 0
    fi
    sleep 0.1
  done

  echo "Port $port is still listening after Playwright shutdown." >&2
  return 1
}

trap cleanup EXIT INT TERM

bash scripts/e2e-db.sh reset
cd frontend
corepack pnpm exec playwright test --config playwright.full-stack.config.ts "$@"
