#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose_file="${repository_root}/infrastructure/docker-compose.production.yml"
env_file="${1:-}"

fail() {
  printf 'productionPreflight=false\n'
  printf 'failureCode=%s\n' "$1"
  exit 1
}

read_env_value() {
  local key="$1"
  awk -F= -v expected_key="${key}" '
    $0 !~ /^[[:space:]]*#/ && $1 == expected_key {
      value = substr($0, index($0, "=") + 1)
    }
    END {
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", value)
      print value
    }
  ' "${env_file}"
}

if [[ -z "${env_file}" || ! -f "${env_file}" || ! -r "${env_file}" ]]; then
  fail "ENV_FILE_UNAVAILABLE"
fi

if ! command -v docker >/dev/null 2>&1; then
  fail "DOCKER_UNAVAILABLE"
fi

tenant_id="$(read_env_value "ENTRA_TENANT_ID")"
api_client_id="$(read_env_value "ENTRA_API_CLIENT_ID")"
spa_client_id="$(read_env_value "ENTRA_SPA_CLIENT_ID")"
api_scope="$(read_env_value "ENTRA_API_SCOPE")"
legacy_client_id="$(read_env_value "ENTRA_CLIENT_ID")"
public_host="$(read_env_value "PUBLIC_HOST")"
api_client_id_lower="$(printf '%s' "${api_client_id}" | tr '[:upper:]' '[:lower:]')"
spa_client_id_lower="$(printf '%s' "${spa_client_id}" | tr '[:upper:]' '[:lower:]')"
api_scope_lower="$(printf '%s' "${api_scope}" | tr '[:upper:]' '[:lower:]')"

guid_pattern='^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$'
zero_guid='00000000-0000-0000-0000-000000000000'

if [[ ! "${tenant_id}" =~ ${guid_pattern} || "${tenant_id}" == "${zero_guid}" ]]; then
  fail "ENTRA_TENANT_INVALID"
fi
if [[ ! "${api_client_id}" =~ ${guid_pattern} || "${api_client_id}" == "${zero_guid}" ]]; then
  fail "ENTRA_API_CLIENT_INVALID"
fi
if [[ ! "${spa_client_id}" =~ ${guid_pattern} || "${spa_client_id}" == "${zero_guid}" ]]; then
  fail "ENTRA_SPA_CLIENT_INVALID"
fi
if [[ "${api_client_id_lower}" == "${spa_client_id_lower}" ]]; then
  fail "ENTRA_CLIENTS_NOT_SPLIT"
fi
if [[ -n "${legacy_client_id}" ]]; then
  fail "ENTRA_LEGACY_CLIENT_AMBIGUOUS"
fi
if [[ "${api_scope_lower}" != "api://${api_client_id_lower}/access_as_user" ]]; then
  fail "ENTRA_SCOPE_MISMATCH"
fi
if [[ -z "${public_host}" \
  || "${public_host}" == "localhost" \
  || "${public_host}" == *.example.com \
  || "${public_host}" == *.invalid \
  || "${public_host}" == *.test ]]; then
  fail "PUBLIC_HOST_INVALID"
fi

temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/emi-qms-production-preflight.XXXXXX")"
cleanup() {
  rm -f "${temporary_directory}/compose-config.yml"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

if ! docker compose \
  --env-file "${env_file}" \
  -f "${compose_file}" \
  --profile operations \
  config >"${temporary_directory}/compose-config.yml" 2>/dev/null; then
  fail "COMPOSE_CONFIG_INVALID"
fi

if ! grep -q -- '--migrate-only' "${temporary_directory}/compose-config.yml"; then
  fail "MIGRATION_JOB_MISSING"
fi

if [[ "${PRODUCTION_PREFLIGHT_VALIDATE_TLS:-false}" == "true" ]]; then
  if ! docker compose \
    --env-file "${env_file}" \
    -f "${compose_file}" \
    run --rm --no-deps tls-validator >/dev/null 2>&1; then
    fail "TLS_VALIDATION_FAILED"
  fi
fi

printf 'productionPreflight=true\n'
printf 'entraSplit=true\n'
printf 'composeConfig=true\n'
printf 'migrationJob=true\n'
printf 'tlsValidation=%s\n' "${PRODUCTION_PREFLIGHT_VALIDATE_TLS:-false}"
