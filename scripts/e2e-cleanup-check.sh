#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${repo_root}"

# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"
export E2E_DATABASE_NAME="${E2E_DATABASE_NAME:-emi_qms_e2e_cleanup_check}"
e2e_initialize_environment "${repo_root}"

resource_scope_initialized=0
cleanup() {
  local original_exit_code="$?"
  trap - EXIT INT TERM
  set +e
  if [[ "${resource_scope_initialized}" == "1" ]]; then
    e2e_stop_project >/dev/null 2>&1 || true
  fi
  exit "${original_exit_code}"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

resource_scope_initialized=1
e2e_start_postgres
bash scripts/e2e-db.sh reset

e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
  --username "${E2E_DATABASE_USER}" \
  --dbname "${E2E_DATABASE_NAME}" \
  --no-psqlrc \
  --command "select pg_sleep(30);" >/dev/null 2>&1 &
held_connection_pid="$!"

bash scripts/e2e-db.sh drop
wait "${held_connection_pid}" >/dev/null 2>&1 || true

bash scripts/e2e-db.sh assert-dropped
bash scripts/e2e-db.sh drop
bash scripts/e2e-db.sh assert-dropped

e2e_stop_project
resource_scope_initialized=0
