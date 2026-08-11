#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
gate="${repository_root}/scripts/verify-ci-gate.sh"
case_number=0

run_case() {
  local expected_exit="$1"
  shift
  case_number=$((case_number + 1))

  set +e
  env \
    CHANGE_RESULT='success' \
    RUN_BACKEND='false' \
    RUN_FRONTEND='false' \
    RUN_FULL_STACK='false' \
    RUN_POLICY_VALIDATION='false' \
    RUN_AZURE_VALIDATION='false' \
    BACKEND_RESULT='skipped' \
    FRONTEND_RESULT='skipped' \
    FULL_STACK_RESULT='skipped' \
    POLICY_RESULT='skipped' \
    "$@" \
    "${gate}" >/dev/null 2>&1
  actual_exit="$?"
  set -e

  if [[ "${actual_exit}" -ne "${expected_exit}" ]]; then
    printf 'ciGateTests=UNEXPECTED_EXIT_%s\n' "${case_number}" >&2
    exit 1
  fi
}

run_case 0
run_case 0 RUN_BACKEND='true' BACKEND_RESULT='success'
run_case 0 RUN_FRONTEND='true' FRONTEND_RESULT='success'
run_case 0 \
  RUN_BACKEND='true' BACKEND_RESULT='success' \
  RUN_FRONTEND='true' FRONTEND_RESULT='success' \
  RUN_FULL_STACK='true' FULL_STACK_RESULT='success'
run_case 0 RUN_POLICY_VALIDATION='true' POLICY_RESULT='success'
run_case 1 CHANGE_RESULT='failure'
run_case 1 RUN_BACKEND='true' BACKEND_RESULT='failure'
run_case 1 RUN_FRONTEND='true' FRONTEND_RESULT='cancelled'
run_case 1 RUN_FULL_STACK='true' FULL_STACK_RESULT='skipped'
run_case 1 RUN_AZURE_VALIDATION='true' POLICY_RESULT='failure'

printf 'ciGateTests=PASS\n'
