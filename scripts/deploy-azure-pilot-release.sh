#!/usr/bin/env bash
set -euo pipefail

required_environment=(
  SOURCE_SHA
  AZURE_SUBSCRIPTION_ID
  ACR_LOGIN_SERVER
  PUBLIC_HOSTNAME
  AZURE_RESOURCE_GROUP
  BACKEND_APP_NAME
  FRONTEND_APP_NAME
  MIGRATION_JOB_NAME
  DATABASE_BOOTSTRAP_JOB_NAME
  MEMBERSHIP_BACKFILL_JOB_NAME
  DEPLOY_BACKEND
  DEPLOY_FRONTEND
  RUN_MIGRATION
  RUN_DATABASE_BOOTSTRAP
  RUN_MEMBERSHIP_BACKFILL
  INSPECT_MEMBERSHIP_BACKFILL
)

for variable_name in "${required_environment[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    printf 'azurePilotRelease=MISSING_CONFIGURATION\n' >&2
    exit 63
  fi
done

for release_flag in \
  "${DEPLOY_BACKEND}" \
  "${DEPLOY_FRONTEND}" \
  "${RUN_MIGRATION}" \
  "${RUN_DATABASE_BOOTSTRAP}" \
  "${RUN_MEMBERSHIP_BACKFILL}" \
  "${INSPECT_MEMBERSHIP_BACKFILL}"; do
  if [[ "${release_flag}" != 'true' && "${release_flag}" != 'false' ]]; then
    printf 'azurePilotRelease=INVALID_RELEASE_SCOPE\n' >&2
    exit 65
  fi
done

if [[ ( "${RUN_DATABASE_BOOTSTRAP}" == 'true' || "${RUN_MEMBERSHIP_BACKFILL}" == 'true' ) \
  && "${RUN_MIGRATION}" != 'true' ]]; then
  printf 'azurePilotRelease=INVALID_RELEASE_SCOPE\n' >&2
  exit 65
fi

if [[ "${RUN_MEMBERSHIP_BACKFILL}" == 'true' \
  && "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  printf 'azurePilotRelease=INVALID_RELEASE_SCOPE\n' >&2
  exit 65
fi

if [[ "${DEPLOY_BACKEND}" == 'false' \
  && "${DEPLOY_FRONTEND}" == 'false' \
  && "${RUN_MIGRATION}" == 'false' \
  && "${RUN_DATABASE_BOOTSTRAP}" == 'false' \
  && "${RUN_MEMBERSHIP_BACKFILL}" == 'false' \
  && "${INSPECT_MEMBERSHIP_BACKFILL}" == 'false' ]]; then
  printf 'azurePilotRelease=NO_CHANGES\n'
  exit 0
fi

azure_cli_bin="${AZURE_RELEASE_AZ_BIN:-az}"
http_client_bin="${AZURE_RELEASE_HTTP_BIN:-curl}"
poll_attempts="${AZURE_RELEASE_POLL_ATTEMPTS:-60}"
poll_interval_seconds="${AZURE_RELEASE_POLL_INTERVAL_SECONDS:-10}"

if [[ "${azure_cli_bin}" != 'az' || "${http_client_bin}" != 'curl' ]]; then
  if [[ "${AZURE_RELEASE_ALLOW_TEST_OVERRIDES:-false}" != 'true' \
    || "${PUBLIC_HOSTNAME}" != 'pms.synthetic.internal' ]]; then
    printf 'azurePilotRelease=COMMAND_OVERRIDE_REJECTED\n' >&2
    exit 64
  fi
fi

if [[ ! "${poll_attempts}" =~ ^[1-9][0-9]{0,2}$ \
  || ! "${poll_interval_seconds}" =~ ^[0-9]{1,2}$ ]]; then
  printf 'azurePilotRelease=INVALID_POLL_CONFIGURATION\n' >&2
  exit 65
fi

digest_pattern='sha256:[0-9a-f]{64}'
if [[ ( "${DEPLOY_BACKEND}" == 'true' \
    || "${RUN_MIGRATION}" == 'true' \
    || "${RUN_DATABASE_BOOTSTRAP}" == 'true' \
    || "${RUN_MEMBERSHIP_BACKFILL}" == 'true' \
    || "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ) \
  && ( "${BACKEND_RELEASE_IMAGE:-}" != "${ACR_LOGIN_SERVER}/pms-backend@"* \
    || ! "${BACKEND_RELEASE_IMAGE:-}" =~ @${digest_pattern}$ ) ]]; then
  printf 'azurePilotRelease=INVALID_RELEASE_IMAGE\n' >&2
  exit 66
fi
if [[ "${DEPLOY_FRONTEND}" == 'true' \
  && ( "${FRONTEND_RELEASE_IMAGE:-}" != "${ACR_LOGIN_SERVER}/pms-frontend@"* \
    || ! "${FRONTEND_RELEASE_IMAGE:-}" =~ @${digest_pattern}$ ) ]]; then
  printf 'azurePilotRelease=INVALID_RELEASE_IMAGE\n' >&2
  exit 66
fi

task_tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/pms-azure-release.XXXXXX")"
cleanup() {
  rm -f "${task_tmp_dir}/command-output" "${task_tmp_dir}/command-error"
  rmdir "${task_tmp_dir}" 2>/dev/null || true
}
trap cleanup EXIT

azure_read() {
  "${azure_cli_bin}" "$@" -o tsv \
    2>"${task_tmp_dir}/command-error"
}

azure_mutate() {
  "${azure_cli_bin}" "$@" -o none \
    >"${task_tmp_dir}/command-output" \
    2>"${task_tmp_dir}/command-error"
}

public_status() {
  local path="$1"
  "${http_client_bin}" \
    --silent \
    --show-error \
    --output /dev/null \
    --write-out '%{http_code}' \
    --max-time 20 \
    "https://${PUBLIC_HOSTNAME}${path}" \
    2>"${task_tmp_dir}/command-error"
}

wait_for_app() {
  local app_name="$1"
  local expected_image="$2"
  local attempt latest_revision ready_revision provisioning_state image health_state running_state

  for ((attempt = 1; attempt <= poll_attempts; attempt++)); do
    latest_revision="$(azure_read containerapp show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" \
      --query properties.latestRevisionName)" || latest_revision=''
    ready_revision="$(azure_read containerapp show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" \
      --query properties.latestReadyRevisionName)" || ready_revision=''
    provisioning_state="$(azure_read containerapp show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" \
      --query properties.provisioningState)" || provisioning_state=''
    image="$(azure_read containerapp show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" \
      --query 'properties.template.containers[0].image')" || image=''

    if [[ -n "${latest_revision}" && "${latest_revision}" == "${ready_revision}" \
      && "${provisioning_state}" == 'Succeeded' && "${image}" == "${expected_image}" ]]; then
      health_state="$(azure_read containerapp revision show \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --name "${app_name}" \
        --revision "${latest_revision}" \
        --query properties.healthState)" || health_state=''
      running_state="$(azure_read containerapp revision show \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --name "${app_name}" \
        --revision "${latest_revision}" \
        --query properties.runningState)" || running_state=''
      if [[ "${health_state}" == 'Healthy' \
        && ( "${running_state}" == 'Running' \
          || "${running_state}" == 'RunningAtMaxScale' ) ]]; then
        return 0
      fi
    fi

    if [[ "${poll_interval_seconds}" -gt 0 ]]; then
      sleep "${poll_interval_seconds}"
    fi
  done

  return 1
}

wait_for_job() {
  local job_name="$1"
  local execution_name="$2"
  local attempt execution_status

  for ((attempt = 1; attempt <= poll_attempts; attempt++)); do
    execution_status="$(azure_read containerapp job execution show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${job_name}" \
      --job-execution-name "${execution_name}" \
      --query properties.status)" || execution_status=''

    if [[ "${execution_status}" == 'Succeeded' ]]; then
      return 0
    fi
    if [[ "${execution_status}" == 'Failed' ]]; then
      return 1
    fi
    if [[ "${poll_interval_seconds}" -gt 0 ]]; then
      sleep "${poll_interval_seconds}"
    fi
  done

  return 1
}

previous_backend_image=''
previous_frontend_image=''
backend_changed='false'
frontend_changed='false'

rollback_apps() {
  local rollback_failed='false'

  if [[ "${frontend_changed}" == 'true' && -n "${previous_frontend_image}" ]]; then
    if ! azure_mutate containerapp update \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${FRONTEND_APP_NAME}" \
      --image "${previous_frontend_image}" \
      || ! wait_for_app "${FRONTEND_APP_NAME}" "${previous_frontend_image}"; then
      rollback_failed='true'
    fi
  fi

  if [[ "${backend_changed}" == 'true' && -n "${previous_backend_image}" ]]; then
    if ! azure_mutate containerapp update \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${BACKEND_APP_NAME}" \
      --image "${previous_backend_image}" \
      || ! wait_for_app "${BACKEND_APP_NAME}" "${previous_backend_image}"; then
      rollback_failed='true'
    fi
  fi

  if [[ "${rollback_failed}" == 'true' ]]; then
    printf 'azurePilotReleaseRollback=FAILED\n' >&2
    return 1
  fi

  printf 'azurePilotReleaseRollback=PASS\n' >&2
  return 0
}

fail_after_mutation() {
  local code="$1"
  rollback_apps || true
  printf 'azurePilotRelease=%s\n' "${code}" >&2
  exit 1
}

signed_in_subscription="$(azure_read account show --query id)" || signed_in_subscription=''
if [[ "${signed_in_subscription}" != "${AZURE_SUBSCRIPTION_ID}" ]]; then
  printf 'azurePilotRelease=SUBSCRIPTION_MISMATCH\n' >&2
  exit 67
fi

backend_revision_mode="$(azure_read containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${BACKEND_APP_NAME}" \
  --query properties.configuration.activeRevisionsMode)" || backend_revision_mode=''
frontend_revision_mode="$(azure_read containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${FRONTEND_APP_NAME}" \
  --query properties.configuration.activeRevisionsMode)" || frontend_revision_mode=''
migration_trigger_type="$(azure_read containerapp job show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${MIGRATION_JOB_NAME}" \
  --query properties.configuration.triggerType)" || migration_trigger_type=''
database_bootstrap_trigger_type="$(azure_read containerapp job show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${DATABASE_BOOTSTRAP_JOB_NAME}" \
  --query properties.configuration.triggerType)" || database_bootstrap_trigger_type=''
membership_backfill_trigger_type="$(azure_read containerapp job show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
  --query properties.configuration.triggerType)" || membership_backfill_trigger_type=''

if [[ "${backend_revision_mode}" != 'Single' \
  || "${frontend_revision_mode}" != 'Single' \
  || "${migration_trigger_type}" != 'Manual' \
  || ( "${RUN_DATABASE_BOOTSTRAP}" == 'true' && "${database_bootstrap_trigger_type}" != 'Manual' ) \
  || ( ( "${RUN_MEMBERSHIP_BACKFILL}" == 'true' || "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ) \
    && "${membership_backfill_trigger_type}" != 'Manual' ) ]]; then
  printf 'azurePilotRelease=UNSAFE_RUNTIME_MODE\n' >&2
  exit 68
fi

previous_backend_image="$(azure_read containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${BACKEND_APP_NAME}" \
  --query 'properties.template.containers[0].image')" || previous_backend_image=''
previous_frontend_image="$(azure_read containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${FRONTEND_APP_NAME}" \
  --query 'properties.template.containers[0].image')" || previous_frontend_image=''

backend_rollback_image_safe='false'
frontend_rollback_image_safe='false'
if [[ "${previous_backend_image}" == "${ACR_LOGIN_SERVER}/pms-backend:"* ]]; then
  previous_backend_tag="${previous_backend_image#"${ACR_LOGIN_SERVER}/pms-backend:"}"
  [[ "${previous_backend_tag}" =~ ^[0-9a-f]{40}$ ]] && backend_rollback_image_safe='true'
elif [[ "${previous_backend_image}" == "${ACR_LOGIN_SERVER}/pms-backend@"* ]]; then
  previous_backend_digest="${previous_backend_image#"${ACR_LOGIN_SERVER}/pms-backend@"}"
  [[ "${previous_backend_digest}" =~ ^${digest_pattern}$ ]] && backend_rollback_image_safe='true'
fi
if [[ "${previous_frontend_image}" == "${ACR_LOGIN_SERVER}/pms-frontend:"* ]]; then
  previous_frontend_tag="${previous_frontend_image#"${ACR_LOGIN_SERVER}/pms-frontend:"}"
  [[ "${previous_frontend_tag}" =~ ^[0-9a-f]{40}$ ]] && frontend_rollback_image_safe='true'
elif [[ "${previous_frontend_image}" == "${ACR_LOGIN_SERVER}/pms-frontend@"* ]]; then
  previous_frontend_digest="${previous_frontend_image#"${ACR_LOGIN_SERVER}/pms-frontend@"}"
  [[ "${previous_frontend_digest}" =~ ^${digest_pattern}$ ]] && frontend_rollback_image_safe='true'
fi

if [[ -z "${previous_backend_image}" || -z "${previous_frontend_image}" \
  || "${previous_backend_image}" =~ [[:space:]] \
  || "${previous_frontend_image}" =~ [[:space:]] \
  || "${backend_rollback_image_safe}" != 'true' \
  || "${frontend_rollback_image_safe}" != 'true' ]]; then
  printf 'azurePilotRelease=UNSAFE_ROLLBACK_BASELINE\n' >&2
  exit 69
fi

if ! wait_for_app "${BACKEND_APP_NAME}" "${previous_backend_image}" \
  || ! wait_for_app "${FRONTEND_APP_NAME}" "${previous_frontend_image}"; then
  printf 'azurePilotRelease=BASELINE_NOT_READY\n' >&2
  exit 70
fi

baseline_live_status="$(public_status '/health/live')" || baseline_live_status=''
baseline_root_status="$(public_status '/')" || baseline_root_status=''
baseline_api_status="$(public_status '/api/me')" || baseline_api_status=''
if [[ "${baseline_live_status}" != '200' \
  || "${baseline_root_status}" != '401' \
  || "${baseline_api_status}" != '401' ]]; then
  printf 'azurePilotRelease=BASELINE_PUBLIC_SECURITY_FAILED\n' >&2
  exit 71
fi

if [[ "${RUN_DATABASE_BOOTSTRAP}" == 'true' ]]; then
  if ! azure_mutate containerapp job update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${DATABASE_BOOTSTRAP_JOB_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}"; then
    printf 'azurePilotRelease=DATABASE_BOOTSTRAP_JOB_UPDATE_FAILED\n' >&2
    exit 72
  fi

  database_bootstrap_execution="$(azure_read containerapp job start \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${DATABASE_BOOTSTRAP_JOB_NAME}" \
    --query name)" || database_bootstrap_execution=''
  if [[ -z "${database_bootstrap_execution}" || "${database_bootstrap_execution}" =~ [[:space:]] ]] \
    || ! wait_for_job "${DATABASE_BOOTSTRAP_JOB_NAME}" "${database_bootstrap_execution}"; then
    printf 'azurePilotRelease=DATABASE_BOOTSTRAP_FAILED\n' >&2
    exit 73
  fi
fi

if [[ "${RUN_MIGRATION}" == 'true' ]]; then
  if ! azure_mutate containerapp job update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MIGRATION_JOB_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}"; then
    printf 'azurePilotRelease=MIGRATION_JOB_UPDATE_FAILED\n' >&2
    exit 72
  fi

  migration_execution="$(azure_read containerapp job start \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MIGRATION_JOB_NAME}" \
    --query name)" || migration_execution=''
  if [[ -z "${migration_execution}" || "${migration_execution}" =~ [[:space:]] ]] \
    || ! wait_for_job "${MIGRATION_JOB_NAME}" "${migration_execution}"; then
    printf 'azurePilotRelease=MIGRATION_FAILED\n' >&2
    exit 74
  fi
fi

if [[ "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  membership_backfill_inspection_execution="$(azure_read containerapp job start \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
    --container-name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}" \
    --args=--inspect-business-unit-membership-backfill \
    --query name)" || membership_backfill_inspection_execution=''
  if [[ -z "${membership_backfill_inspection_execution}" \
    || "${membership_backfill_inspection_execution}" =~ [[:space:]] ]] \
    || ! wait_for_job "${MEMBERSHIP_BACKFILL_JOB_NAME}" "${membership_backfill_inspection_execution}"; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_INSPECTION_FAILED\n' >&2
    exit 77
  fi

  membership_backfill_inspection_summary=''
  for ((attempt = 1; attempt <= 6; attempt++)); do
    if "${azure_cli_bin}" containerapp job logs show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
      --execution "${membership_backfill_inspection_execution}" \
      --container "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
      --format text \
      --tail 100 \
      >"${task_tmp_dir}/command-output" \
      2>"${task_tmp_dir}/command-error"; then
      membership_backfill_inspection_summary="$(sed -n \
        's/^.*\(businessUnitMembershipBackfillDryRun=PASS identityCount=[0-9][0-9]* overallAdministratorCount=[0-9][0-9]* activateMembershipCount=[0-9][0-9]* deactivateMembershipCount=[0-9][0-9]* normalizeDepartmentDefaultRoleCount=[0-9][0-9]* removeManagedRoleCount=[0-9][0-9]* resetDepartmentHeadCount=[0-9][0-9]* repairOverallProfileCount=[0-9][0-9]* designateOverallAdministratorCount=[0-9][0-9]* cheongjuSystemAdminPermissionGapCount=[0-9][0-9]* osanSystemAdminPermissionGapCount=[0-9][0-9]*\).*$/\1/p' \
        "${task_tmp_dir}/command-output" | tail -n 1)"
    fi
    if [[ -n "${membership_backfill_inspection_summary}" ]]; then
      break
    fi
    if [[ "${poll_interval_seconds}" -gt 0 ]]; then
      sleep "${poll_interval_seconds}"
    fi
  done
  if [[ -z "${membership_backfill_inspection_summary}" ]]; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_INSPECTION_EVIDENCE_MISSING\n' >&2
    exit 78
  fi
  printf '%s\n' "${membership_backfill_inspection_summary}"
fi

if [[ "${RUN_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  if ! azure_mutate containerapp job update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}"; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_JOB_UPDATE_FAILED\n' >&2
    exit 75
  fi

  membership_backfill_execution="$(azure_read containerapp job start \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
    --query name)" || membership_backfill_execution=''
  if [[ -z "${membership_backfill_execution}" || "${membership_backfill_execution}" =~ [[:space:]] ]] \
    || ! wait_for_job "${MEMBERSHIP_BACKFILL_JOB_NAME}" "${membership_backfill_execution}"; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_FAILED\n' >&2
    exit 76
  fi
fi

if [[ "${DEPLOY_BACKEND}" == 'true' ]]; then
  backend_changed='true'
  if ! azure_mutate containerapp update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${BACKEND_APP_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}" \
    || ! wait_for_app "${BACKEND_APP_NAME}" "${BACKEND_RELEASE_IMAGE}"; then
    fail_after_mutation 'BACKEND_RELEASE_FAILED'
  fi
fi

if [[ "${DEPLOY_FRONTEND}" == 'true' ]]; then
  frontend_changed='true'
  if ! azure_mutate containerapp update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${FRONTEND_APP_NAME}" \
    --image "${FRONTEND_RELEASE_IMAGE}" \
    || ! wait_for_app "${FRONTEND_APP_NAME}" "${FRONTEND_RELEASE_IMAGE}"; then
    fail_after_mutation 'FRONTEND_RELEASE_FAILED'
  fi
fi

final_live_status="$(public_status '/health/live')" || final_live_status=''
final_root_status="$(public_status '/')" || final_root_status=''
final_api_status="$(public_status '/api/me')" || final_api_status=''
if [[ "${final_live_status}" != '200' \
  || "${final_root_status}" != '401' \
  || "${final_api_status}" != '401' ]]; then
  fail_after_mutation 'PUBLIC_SECURITY_SMOKE_FAILED'
fi

if [[ "${RUN_MIGRATION}" == 'true' ]]; then
  printf 'azurePilotReleaseMigration=PASS\n'
else
  printf 'azurePilotReleaseMigration=SKIPPED\n'
fi
if [[ "${RUN_DATABASE_BOOTSTRAP}" == 'true' ]]; then
  printf 'azurePilotReleaseDatabaseBootstrap=PASS\n'
else
  printf 'azurePilotReleaseDatabaseBootstrap=SKIPPED\n'
fi
if [[ "${RUN_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  printf 'azurePilotReleaseMembershipBackfill=PASS\n'
else
  printf 'azurePilotReleaseMembershipBackfill=SKIPPED\n'
fi
if [[ "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  printf 'azurePilotReleaseMembershipBackfillInspection=PASS\n'
else
  printf 'azurePilotReleaseMembershipBackfillInspection=SKIPPED\n'
fi
if [[ "${DEPLOY_BACKEND}" == 'true' ]]; then
  printf 'azurePilotReleaseBackend=PASS\n'
else
  printf 'azurePilotReleaseBackend=SKIPPED\n'
fi
if [[ "${DEPLOY_FRONTEND}" == 'true' ]]; then
  printf 'azurePilotReleaseFrontend=PASS\n'
else
  printf 'azurePilotReleaseFrontend=SKIPPED\n'
fi
printf 'azurePilotReleasePublicSecurity=PASS\n'
