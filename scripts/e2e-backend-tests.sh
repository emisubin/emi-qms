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

echo "Backend tests use an isolated PostgreSQL Compose project and tmpfs storage."
dotnet test "${repo_root}/backend/tests/Emi.Qms.Api.Tests/Emi.Qms.Api.Tests.csproj" \
  --configuration Release \
  --nologo \
  "$@"
