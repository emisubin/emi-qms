#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_script="${repository_root}/scripts/deploy-azure-pilot-release.sh"
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-azure-release-test.XXXXXX")"

cleanup() {
  rm -f \
    "${temporary_directory}/az" \
    "${temporary_directory}/curl" \
    "${temporary_directory}/backend-image" \
    "${temporary_directory}/frontend-image" \
    "${temporary_directory}/calls" \
    "${temporary_directory}/stdout" \
    "${temporary_directory}/stderr"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

cat >"${temporary_directory}/az" <<'MOCK_AZ'
#!/usr/bin/env bash
set -euo pipefail

name=''
query=''
image=''
revision=''
for ((index = 1; index <= $#; index++)); do
  argument="${!index}"
  case "${argument}" in
    --name)
      next=$((index + 1))
      name="${!next}"
      ;;
    --query)
      next=$((index + 1))
      query="${!next}"
      ;;
    --image)
      next=$((index + 1))
      image="${!next}"
      ;;
    --revision)
      next=$((index + 1))
      revision="${!next}"
      ;;
  esac
done

command_group="${1:-} ${2:-} ${3:-}"
case "${command_group}" in
  'account show --query')
    printf '%s\n' "${AZURE_SUBSCRIPTION_ID}"
    ;;
  'containerapp show --resource-group')
    image_file="${AZURE_RELEASE_TEST_STATE}/${name}-image"
    case "${query}" in
      properties.configuration.activeRevisionsMode)
        printf 'Single\n'
        ;;
      properties.latestRevisionName)
        if grep -q '@sha256:' "${image_file}"; then
          printf '%s-new\n' "${name}"
        else
          printf '%s-old\n' "${name}"
        fi
        ;;
      properties.latestReadyRevisionName)
        if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'backend-release-failed' \
          && "${name}" == "${BACKEND_APP_NAME}" ]] \
          && grep -q '@sha256:' "${image_file}"; then
          printf '%s-old\n' "${name}"
        elif [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'frontend-release-failed' \
          && "${name}" == "${FRONTEND_APP_NAME}" ]] \
          && grep -q '@sha256:' "${image_file}"; then
          printf '%s-old\n' "${name}"
        elif grep -q '@sha256:' "${image_file}"; then
          printf '%s-new\n' "${name}"
        else
          printf '%s-old\n' "${name}"
        fi
        ;;
      properties.provisioningState)
        printf 'Succeeded\n'
        ;;
      'properties.template.containers[0].image')
        sed -n '1p' "${image_file}"
        ;;
      *)
        exit 2
        ;;
    esac
    ;;
  'containerapp revision show')
    case "${query}" in
      properties.healthState)
        printf 'Healthy\n'
        ;;
      properties.runningState)
        case "${AZURE_RELEASE_TEST_SCENARIO}" in
          success-running-at-max-scale)
            printf 'RunningAtMaxScale\n'
            ;;
          baseline-stopped)
            printf 'Stopped\n'
            ;;
          baseline-scale-to-zero)
            printf 'ScaleToZero\n'
            ;;
          baseline-degraded)
            printf 'Degraded\n'
            ;;
          baseline-unknown)
            printf 'Unknown\n'
            ;;
          *)
            printf 'Running\n'
            ;;
        esac
        ;;
      *)
        exit 2
        ;;
    esac
    ;;
  'containerapp job show')
    if [[ "${query}" == 'properties.configuration.triggerType' ]]; then
      printf 'Manual\n'
    else
      exit 2
    fi
    ;;
  'containerapp job update')
    printf 'migration-update\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
    ;;
  'containerapp job start')
    printf 'migration-start\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
    printf 'synthetic-execution\n'
    ;;
  'containerapp job execution')
    if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'migration-failed' ]]; then
      printf 'Failed\n'
    else
      printf 'Succeeded\n'
    fi
    ;;
  'containerapp update --resource-group')
    image_file="${AZURE_RELEASE_TEST_STATE}/${name}-image"
    if [[ "${name}" == "${BACKEND_APP_NAME}" ]]; then
      if [[ "${image}" == "${BACKEND_RELEASE_IMAGE}" ]]; then
        printf 'backend-update\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
      else
        printf 'backend-rollback\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
      fi
    elif [[ "${name}" == "${FRONTEND_APP_NAME}" ]]; then
      if [[ "${image}" == "${FRONTEND_RELEASE_IMAGE}" ]]; then
        printf 'frontend-update\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
      else
        printf 'frontend-rollback\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
      fi
    else
      exit 2
    fi
    printf '%s\n' "${image}" >"${image_file}"
    ;;
  *)
    exit 2
    ;;
esac
MOCK_AZ

cat >"${temporary_directory}/curl" <<'MOCK_CURL'
#!/usr/bin/env bash
set -euo pipefail

url="${!#}"
frontend_image="$(sed -n '1p' "${AZURE_RELEASE_TEST_STATE}/${FRONTEND_APP_NAME}-image")"
if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'public-security-failed' \
  && "${frontend_image}" == *'@sha256:'* \
  && "${url}" == */health/live ]]; then
  printf '500'
elif [[ "${url}" == */health/live ]]; then
  printf '200'
else
  printf '401'
fi
MOCK_CURL

chmod 700 "${temporary_directory}/az" "${temporary_directory}/curl"

backend_digest="$(printf 'a%.0s' {1..64})"
frontend_digest="$(printf 'b%.0s' {1..64})"
case_number=0

run_case() {
  local scenario="$1"
  local expected_exit="$2"
  local expected_code="$3"
  local expected_calls="$4"
  local deploy_backend="${5:-true}"
  local deploy_frontend="${6:-true}"
  local run_migration="${7:-true}"
  case_number=$((case_number + 1))

  printf '%s\n' 'pilotacr123.azurecr.io/pms-backend:cccccccccccccccccccccccccccccccccccccccc' \
    >"${temporary_directory}/pms-synthetic-backend-image"
  printf '%s\n' 'pilotacr123.azurecr.io/pms-frontend:dddddddddddddddddddddddddddddddddddddddd' \
    >"${temporary_directory}/pms-synthetic-frontend-image"
  if [[ "${scenario}" == 'unsafe-rollback' ]]; then
    printf '%s\n' 'pilotacr123.azurecr.io/pms-backend:latest' \
      >"${temporary_directory}/pms-synthetic-backend-image"
  fi
  : >"${temporary_directory}/calls"

  set +e
  env \
    SOURCE_SHA='1111111111111111111111111111111111111111' \
    AZURE_SUBSCRIPTION_ID='33333333-3333-4333-8333-333333333333' \
    ACR_LOGIN_SERVER='pilotacr123.azurecr.io' \
    PUBLIC_HOSTNAME='pms.synthetic.internal' \
    AZURE_RESOURCE_GROUP='pms-synthetic-rg' \
    BACKEND_APP_NAME='pms-synthetic-backend' \
    FRONTEND_APP_NAME='pms-synthetic-frontend' \
    MIGRATION_JOB_NAME='pms-synthetic-migration' \
    BACKEND_RELEASE_IMAGE="pilotacr123.azurecr.io/pms-backend@sha256:${backend_digest}" \
    FRONTEND_RELEASE_IMAGE="pilotacr123.azurecr.io/pms-frontend@sha256:${frontend_digest}" \
    DEPLOY_BACKEND="${deploy_backend}" \
    DEPLOY_FRONTEND="${deploy_frontend}" \
    RUN_MIGRATION="${run_migration}" \
    AZURE_RELEASE_AZ_BIN="${temporary_directory}/az" \
    AZURE_RELEASE_HTTP_BIN="${temporary_directory}/curl" \
    AZURE_RELEASE_ALLOW_TEST_OVERRIDES='true' \
    AZURE_RELEASE_POLL_ATTEMPTS='2' \
    AZURE_RELEASE_POLL_INTERVAL_SECONDS='0' \
    AZURE_RELEASE_TEST_STATE="${temporary_directory}" \
    AZURE_RELEASE_TEST_SCENARIO="${scenario}" \
    "${release_script}" \
    >"${temporary_directory}/stdout" \
    2>"${temporary_directory}/stderr"
  actual_exit="$?"
  set -e

  if [[ "${actual_exit}" -ne "${expected_exit}" ]]; then
    printf 'azurePilotReleaseTests=UNEXPECTED_EXIT_%s_EXPECTED_%s_ACTUAL_%s\n' \
      "${case_number}" "${expected_exit}" "${actual_exit}" >&2
    exit 1
  fi
  if [[ -n "${expected_code}" ]] \
    && ! grep -Fxq "azurePilotRelease=${expected_code}" "${temporary_directory}/stderr"; then
    printf 'azurePilotReleaseTests=UNEXPECTED_FAILURE_CODE_%s\n' "${case_number}" >&2
    exit 1
  fi
  if [[ "$(paste -sd, "${temporary_directory}/calls")" != "${expected_calls}" ]]; then
    printf 'azurePilotReleaseTests=UNEXPECTED_CALL_ORDER_%s\n' "${case_number}" >&2
    exit 1
  fi
}

run_case 'success' 0 '' \
  'migration-update,migration-start,backend-update,frontend-update'
run_case 'success-running-at-max-scale' 0 '' \
  'migration-update,migration-start,backend-update,frontend-update'
run_case 'baseline-stopped' 70 BASELINE_NOT_READY ''
run_case 'baseline-scale-to-zero' 70 BASELINE_NOT_READY ''
run_case 'baseline-degraded' 70 BASELINE_NOT_READY ''
run_case 'baseline-unknown' 70 BASELINE_NOT_READY ''
run_case 'unsafe-rollback' 69 UNSAFE_ROLLBACK_BASELINE ''
run_case 'migration-failed' 73 MIGRATION_FAILED \
  'migration-update,migration-start'
run_case 'backend-release-failed' 1 BACKEND_RELEASE_FAILED \
  'migration-update,migration-start,backend-update,backend-rollback'
run_case 'frontend-release-failed' 1 FRONTEND_RELEASE_FAILED \
  'migration-update,migration-start,backend-update,frontend-update,frontend-rollback,backend-rollback'
run_case 'public-security-failed' 1 PUBLIC_SECURITY_SMOKE_FAILED \
  'migration-update,migration-start,backend-update,frontend-update,frontend-rollback,backend-rollback'
run_case 'success' 0 '' 'backend-update' true false false
run_case 'success' 0 '' 'frontend-update' false true false
run_case 'success' 0 '' 'migration-update,migration-start,backend-update' true false true
run_case 'success' 0 '' '' false false false

printf 'azurePilotReleaseTests=PASS\n'
