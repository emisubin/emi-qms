#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
validator="${repository_root}/scripts/validate-azure-image-publish-inputs.sh"
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-image-input-test.XXXXXX")"

cleanup() {
  rm -f "${temporary_directory}/stdout" "${temporary_directory}/stderr"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

source_sha="$(git -C "${repository_root}" rev-parse refs/remotes/origin/main)"
case_number=0

run_case() {
  local expected_exit="$1"
  local expected_code="$2"
  shift 2
  case_number=$((case_number + 1))

  set +e
  env \
    SOURCE_SHA="${source_sha}" \
    CONFIRM_IMAGE_PUSH='true' \
    CONFIRM_PRODUCTION_DEPLOY='true' \
    AZURE_CLIENT_ID='11111111-1111-4111-8111-111111111111' \
    AZURE_TENANT_ID='22222222-2222-4222-8222-222222222222' \
    AZURE_SUBSCRIPTION_ID='33333333-3333-4333-8333-333333333333' \
    ACR_NAME='pilotacr123' \
    ACR_LOGIN_SERVER='pilotacr123.azurecr.io' \
    PUBLIC_HOSTNAME='pms.synthetic.internal' \
    ENTRA_API_CLIENT_ID='44444444-4444-4444-8444-444444444444' \
    ENTRA_SPA_CLIENT_ID='55555555-5555-4555-8555-555555555555' \
    ENTRA_API_SCOPE='api://44444444-4444-4444-8444-444444444444/access_as_user' \
    AZURE_RESOURCE_GROUP='pms-synthetic-rg' \
    BACKEND_APP_NAME='pms-synthetic-backend' \
    FRONTEND_APP_NAME='pms-synthetic-frontend' \
    MIGRATION_JOB_NAME='pms-synthetic-migration' \
    "$@" \
    "${validator}" \
    >"${temporary_directory}/stdout" \
    2>"${temporary_directory}/stderr"
  local actual_exit="$?"
  set -e

  if [[ "${actual_exit}" -ne "${expected_exit}" ]]; then
    printf 'azurePilotImageInputTests=UNEXPECTED_EXIT_%s_EXPECTED_%s_ACTUAL_%s\n' \
      "${case_number}" "${expected_exit}" "${actual_exit}" >&2
    exit 1
  fi
  if [[ -n "${expected_code}" ]] \
    && ! grep -Fxq "azurePilotImagePublish=${expected_code}" "${temporary_directory}/stderr"; then
    printf 'azurePilotImageInputTests=UNEXPECTED_FAILURE_CODE\n' >&2
    exit 1
  fi
}

run_case 64 CONFIRMATION_REQUIRED CONFIRM_IMAGE_PUSH='false'
run_case 68 DEPLOY_CONFIRMATION_REQUIRED CONFIRM_PRODUCTION_DEPLOY='false'
run_case 65 INVALID_SOURCE_SHA SOURCE_SHA='not-a-commit'
run_case 69 INVALID_IDENTIFIER_CONFIGURATION AZURE_CLIENT_ID='00000000-0000-0000-0000-000000000000'
run_case 70 INVALID_REGISTRY_CONFIGURATION ACR_LOGIN_SERVER='different.azurecr.io'
run_case 71 INVALID_FRONTEND_CONFIGURATION PUBLIC_HOSTNAME='pms.example.com'
run_case 72 INVALID_RESOURCE_CONFIGURATION BACKEND_APP_NAME='Invalid_Name'
run_case 72 INVALID_RESOURCE_CONFIGURATION FRONTEND_APP_NAME='pms-synthetic-backend'
run_case 0 ''

printf 'azurePilotImageInputTests=PASS\n'
