#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
preflight="${repository_root}/scripts/production-preflight.sh"
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/emi-qms-production-preflight-test.XXXXXX")"

cleanup() {
  rm -f "${temporary_directory}/"*
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

touch "${temporary_directory}/tls.crt"
touch "${temporary_directory}/tls.key"
touch "${temporary_directory}/database"
touch "${temporary_directory}/admins"
touch "${temporary_directory}/syslog-ca.pem"

write_env() {
  local api_client="$1"
  local spa_client="$2"
  local scope="$3"
  local legacy_client="${4:-}"
  {
    printf 'PUBLIC_HOST=qms.company.internal\n'
    printf 'PUBLIC_HTTPS_PORT=443\n'
    printf 'ENTRA_TENANT_ID=11111111-1111-4111-8111-111111111111\n'
    printf 'ENTRA_API_CLIENT_ID=%s\n' "${api_client}"
    printf 'ENTRA_SPA_CLIENT_ID=%s\n' "${spa_client}"
    printf 'ENTRA_API_AUDIENCE=api://%s\n' "${api_client}"
    printf 'ENTRA_API_SCOPE=%s\n' "${scope}"
    printf 'ENTRA_CLIENT_ID=%s\n' "${legacy_client}"
    printf 'ENTRA_DOMAIN=company.internal\n'
    printf 'RESTORE_VERIFIED_AT_UTC=2026-07-31T00:00:00Z\n'
    printf 'SECURITY_ALERT_SINK=synthetic-sink\n'
    printf 'SECURITY_SYSLOG_ADDRESS=tcp+tls://syslog.company.internal:6514\n'
    printf 'SECURITY_SYSLOG_CA_CERT=%s\n' "${temporary_directory}/syslog-ca.pem"
    printf 'TLS_CERTIFICATE_FILE=%s\n' "${temporary_directory}/tls.crt"
    printf 'TLS_PRIVATE_KEY_FILE=%s\n' "${temporary_directory}/tls.key"
    printf 'DATABASE_CONNECTION_STRING_FILE=%s\n' "${temporary_directory}/database"
    printf 'BOOTSTRAP_ADMIN_EMAILS_FILE=%s\n' "${temporary_directory}/admins"
  } >"${temporary_directory}/production.env"
}

api_client='22222222-2222-4222-8222-222222222222'
spa_client='33333333-3333-4333-8333-333333333333'

write_env "${api_client}" "${spa_client}" "api://${api_client}/access_as_user"
"${preflight}" "${temporary_directory}/production.env" >/dev/null

write_env "${api_client}" "${api_client}" "api://${api_client}/access_as_user"
if "${preflight}" "${temporary_directory}/production.env" >/dev/null 2>&1; then
  printf 'expected split-client rejection\n' >&2
  exit 1
fi

write_env "${api_client}" "${spa_client}" "api://${spa_client}/access_as_user"
if "${preflight}" "${temporary_directory}/production.env" >/dev/null 2>&1; then
  printf 'expected scope rejection\n' >&2
  exit 1
fi

write_env "${api_client}" "${spa_client}" "api://${api_client}/access_as_user" "${api_client}"
if "${preflight}" "${temporary_directory}/production.env" >/dev/null 2>&1; then
  printf 'expected legacy-client rejection\n' >&2
  exit 1
fi

printf 'productionPreflightTests=4\n'
printf 'productionPreflightPassed=4\n'
