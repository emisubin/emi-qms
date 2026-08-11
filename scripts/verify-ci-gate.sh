#!/usr/bin/env bash
set -euo pipefail

required_environment=(
  CHANGE_RESULT
  RUN_BACKEND
  RUN_FRONTEND
  RUN_FULL_STACK
  RUN_POLICY_VALIDATION
  RUN_AZURE_VALIDATION
  BACKEND_RESULT
  FRONTEND_RESULT
  FULL_STACK_RESULT
  POLICY_RESULT
)

for variable_name in "${required_environment[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    printf 'ciGate=MISSING_CONFIGURATION\n' >&2
    exit 63
  fi
done

failed='false'
if [[ "${CHANGE_RESULT}" != 'success' ]]; then
  printf 'ciGate=CHANGE_CLASSIFICATION_FAILED\n' >&2
  failed='true'
fi
if [[ "${RUN_BACKEND}" == 'true' && "${BACKEND_RESULT}" != 'success' ]]; then
  printf 'ciGate=BACKEND_REQUIRED\n' >&2
  failed='true'
fi
if [[ "${RUN_FRONTEND}" == 'true' && "${FRONTEND_RESULT}" != 'success' ]]; then
  printf 'ciGate=FRONTEND_REQUIRED\n' >&2
  failed='true'
fi
if [[ "${RUN_FULL_STACK}" == 'true' && "${FULL_STACK_RESULT}" != 'success' ]]; then
  printf 'ciGate=FULL_STACK_REQUIRED\n' >&2
  failed='true'
fi
if [[ ( "${RUN_POLICY_VALIDATION}" == 'true' || "${RUN_AZURE_VALIDATION}" == 'true' ) \
  && "${POLICY_RESULT}" != 'success' ]]; then
  printf 'ciGate=POLICY_VALIDATION_REQUIRED\n' >&2
  failed='true'
fi

if [[ "${failed}" == 'true' ]]; then
  exit 1
fi

printf 'ciGate=PASS\n'
