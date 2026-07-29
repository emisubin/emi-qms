#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
START_SCRIPT="${SCRIPT_DIR}/dev-uat-start.sh"
OUTPUT_FILE="/tmp/emi-qms-uat-auth-config-test.$$.log"

cleanup() {
  rm -f "${OUTPUT_FILE}"
}
trap cleanup EXIT

run_success_case() {
  local label="$1"
  shift
  if ! env \
    UAT_ENV_FILE=/dev/null \
    UAT_AUTH_CONFIG_CHECK_ONLY=true \
    "$@" \
    "${START_SCRIPT}" > "${OUTPUT_FILE}" 2>&1; then
    echo "${label}: expected success" >&2
    exit 1
  fi
  if grep -Eq '00000000-0000-0000-0000-000000000001|00000000-0000-0000-0000-000000000002' "${OUTPUT_FILE}"; then
    echo "${label}: authentication identifiers were written to output" >&2
    exit 1
  fi
  echo "${label}=passed"
}

run_failure_case() {
  local label="$1"
  shift
  if env \
    UAT_ENV_FILE=/dev/null \
    UAT_AUTH_CONFIG_CHECK_ONLY=true \
    "$@" \
    "${START_SCRIPT}" > "${OUTPUT_FILE}" 2>&1; then
    echo "${label}: expected failure" >&2
    exit 1
  fi
  echo "${label}=passed"
}

run_success_case \
  dev_config \
  UAT_AUTH_MODE=Dev \
  UAT_FRONTEND_HTTPS=false

run_success_case \
  entra_https_config \
  UAT_AUTH_MODE=EntraId \
  UAT_FRONTEND_HTTPS=true \
  ENTRA_TENANT_ID=00000000-0000-0000-0000-000000000001 \
  ENTRA_CLIENT_ID=00000000-0000-0000-0000-000000000002

run_success_case \
  entra_existing_local_key_config \
  UAT_AUTH_MODE=EntraId \
  UAT_FRONTEND_HTTPS=true \
  AzureAd__TenantId=00000000-0000-0000-0000-000000000001 \
  AzureAd__ClientId=00000000-0000-0000-0000-000000000002 \
  VITE_AZURE_TENANT_ID=00000000-0000-0000-0000-000000000001 \
  VITE_AZURE_CLIENT_ID=00000000-0000-0000-0000-000000000002

run_failure_case \
  entra_http_rejection \
  UAT_AUTH_MODE=EntraId \
  UAT_FRONTEND_HTTPS=false \
  ENTRA_TENANT_ID=00000000-0000-0000-0000-000000000001 \
  ENTRA_CLIENT_ID=00000000-0000-0000-0000-000000000002

run_failure_case \
  entra_missing_config_rejection \
  UAT_AUTH_MODE=EntraId \
  UAT_FRONTEND_HTTPS=true

run_failure_case \
  entra_placeholder_config_rejection \
  UAT_AUTH_MODE=EntraId \
  UAT_FRONTEND_HTTPS=true \
  ENTRA_TENANT_ID=00000000-0000-0000-0000-000000000000 \
  ENTRA_CLIENT_ID=00000000-0000-0000-0000-000000000000

run_failure_case \
  entra_identifier_mismatch_rejection \
  UAT_AUTH_MODE=EntraId \
  UAT_FRONTEND_HTTPS=true \
  AzureAd__TenantId=00000000-0000-0000-0000-000000000001 \
  AzureAd__ClientId=00000000-0000-0000-0000-000000000002 \
  VITE_AZURE_TENANT_ID=00000000-0000-0000-0000-000000000003 \
  VITE_AZURE_CLIENT_ID=00000000-0000-0000-0000-000000000002

run_failure_case \
  invalid_mode_rejection \
  UAT_AUTH_MODE=Unknown \
  UAT_FRONTEND_HTTPS=true
