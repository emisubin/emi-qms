#!/usr/bin/env bash
set -euo pipefail

usage() {
  printf '%s\n' 'usage: activate-azure-external-notifications.sh --preflight | --confirm-send-existing-pending-notifications'
}

emit_result() {
  local status="$1"
  local code="$2"
  local mutation_performed="$3"
  local revision_ready="$4"
  jq -n \
    --arg status "$status" \
    --arg code "$code" \
    --argjson mutationPerformed "$mutation_performed" \
    --argjson revisionReady "$revision_ready" \
    '{
      scope: "AZURE_EXTERNAL_NOTIFICATION_WORKER_ACTIVATION",
      status: $status,
      stableCode: $code,
      backendCandidateCount: 1,
      maxReplicaCount: 1,
      teamsCredentialReady: true,
      gmailCredentialReady: true,
      teamsChannelWebhookChanged: false,
      notificationDeliveryStateMutationAuthorized: true,
      databaseSchemaChanged: false,
      businessWorkflowDataChanged: false,
      syntheticNotificationCreated: false,
      existingPendingSendApproved: true,
      runtimeMutationPerformed: $mutationPerformed,
      latestRevisionReady: $revisionReady
    }'
}

mode="${1:-}"
if [[ "$mode" != '--preflight' \
  && "$mode" != '--confirm-send-existing-pending-notifications' ]]; then
  usage >&2
  exit 64
fi
if [[ $# -ne 1 ]]; then
  usage >&2
  exit 64
fi

for command_name in az jq; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    emit_result 'BLOCKED' 'REQUIRED_COMMAND_MISSING' false false
    exit 1
  fi
done

task_tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/emi-qms-notification-activation.XXXXXX")"
# The EXIT trap removes only the fixed files owned by this invocation.
# shellcheck disable=SC2329
cleanup() {
  rm -f -- \
    "$task_tmp_dir/azure-error" \
    "$task_tmp_dir/apps.json" \
    "$task_tmp_dir/secrets.json" \
    "$task_tmp_dir/show-before.json" \
    "$task_tmp_dir/show-after.json" \
    "$task_tmp_dir/revisions.json" \
    "$task_tmp_dir/update.json"
  rmdir "$task_tmp_dir"
}
trap cleanup EXIT

if ! az containerapp list -o json >"$task_tmp_dir/apps.json" 2>"$task_tmp_dir/azure-error"; then
  emit_result 'BLOCKED' 'AZURE_CONTAINERAPP_READ_FAILED' false false
  exit 1
fi

backend_candidates="$(jq '[.[] | select(any(.properties.template.containers[0].env[]?; .name == "Notifications__TeamsActivity__ClientId"))]' "$task_tmp_dir/apps.json")"
backend_count="$(jq 'length' <<<"$backend_candidates")"
if [[ "$backend_count" != '1' ]]; then
  jq -n --argjson count "$backend_count" '{
    scope: "AZURE_EXTERNAL_NOTIFICATION_WORKER_ACTIVATION",
    status: "BLOCKED",
    stableCode: "BACKEND_CANDIDATE_COUNT_INVALID",
    backendCandidateCount: $count,
    runtimeMutationPerformed: false
  }'
  exit 1
fi

resource_group="$(jq -r '.[0].resourceGroup // empty' <<<"$backend_candidates")"
backend_name="$(jq -r '.[0].name // empty' <<<"$backend_candidates")"
if [[ -z "$resource_group" || -z "$backend_name" ]]; then
  emit_result 'BLOCKED' 'BACKEND_IDENTITY_INCOMPLETE' false false
  exit 1
fi

if ! az containerapp show \
  --resource-group "$resource_group" \
  --name "$backend_name" \
  -o json >"$task_tmp_dir/show-before.json" 2>"$task_tmp_dir/azure-error"; then
  emit_result 'BLOCKED' 'AZURE_BACKEND_READ_FAILED' false false
  exit 1
fi

env_json="$(jq '.properties.template.containers[0].env' "$task_tmp_dir/show-before.json")"
env_value() {
  local name="$1"
  jq -r --arg name "$name" '[.[] | select(.name == $name) | .value][0] // empty' <<<"$env_json"
}

dispatch_enabled="$(env_value 'Notifications__Dispatch__Enabled')"
teams_enabled="$(env_value 'Notifications__TeamsActivity__Enabled')"
teams_dry_run="$(env_value 'Notifications__TeamsActivity__DryRun')"
teams_strategy="$(env_value 'Notifications__TeamsActivity__PersonalChannelStrategy')"
mail_enabled="$(env_value 'Notifications__Mail__Enabled')"
mail_dry_run="$(env_value 'Notifications__Mail__DryRun')"
mail_provider="$(env_value 'Notifications__Mail__Provider')"
smtp_host="$(env_value 'Notifications__Mail__Smtp__Host')"
smtp_port="$(env_value 'Notifications__Mail__Smtp__Port')"
smtp_security="$(env_value 'Notifications__Mail__Smtp__Security')"
max_replicas="$(jq -r '.properties.template.scale.maxReplicas // 0' "$task_tmp_dir/show-before.json")"
running_status="$(jq -r '.properties.runningStatus // empty' "$task_tmp_dir/show-before.json")"

if [[ "$dispatch_enabled" != 'false' \
  || "$teams_enabled" != 'false' \
  || "$teams_dry_run" != 'true' \
  || "$mail_enabled" != 'false' \
  || "$mail_dry_run" != 'true' ]]; then
  emit_result 'BLOCKED' 'RUNTIME_NOTIFICATION_SAFETY_STATE_INVALID' false false
  exit 1
fi

mail_provider_lower="$(printf '%s' "$mail_provider" | tr '[:upper:]' '[:lower:]')"
smtp_security_lower="$(printf '%s' "$smtp_security" | tr '[:upper:]' '[:lower:]')"
if [[ "$teams_strategy" != 'TeamsActivity' \
  || "$mail_provider_lower" != 'smtp' \
  || "$smtp_host" != 'smtp.gmail.com' \
  || "$smtp_port" != '587' \
  || "$smtp_security_lower" != 'starttls' ]]; then
  emit_result 'BLOCKED' 'EXTERNAL_PROVIDER_CONFIG_INVALID' false false
  exit 1
fi

if [[ "$max_replicas" != '1' || "$running_status" != 'Running' ]]; then
  emit_result 'BLOCKED' 'BACKEND_RUNTIME_BASELINE_INVALID' false false
  exit 1
fi

if ! az containerapp secret list \
  --resource-group "$resource_group" \
  --name "$backend_name" \
  -o json >"$task_tmp_dir/secrets.json" 2>"$task_tmp_dir/azure-error"; then
  emit_result 'BLOCKED' 'AZURE_SECRET_BINDING_READ_FAILED' false false
  exit 1
fi

secret_url() {
  local name="$1"
  jq -r --arg name "$name" '[.[] | select(.name == $name) | .keyVaultUrl][0] // empty' "$task_tmp_dir/secrets.json"
}

teams_secret_url="$(secret_url 'teams-activity-client-secret')"
gmail_username_url="$(secret_url 'gmail-username')"
gmail_password_url="$(secret_url 'gmail-app-password')"
if [[ -z "$teams_secret_url" || -z "$gmail_username_url" || -z "$gmail_password_url" ]]; then
  emit_result 'BLOCKED' 'EXTERNAL_PROVIDER_SECRET_BINDING_MISSING' false false
  exit 1
fi

if ! teams_secret="$(az keyvault secret show --id "$teams_secret_url" --query value -o tsv 2>"$task_tmp_dir/azure-error")" \
  || ! gmail_username="$(az keyvault secret show --id "$gmail_username_url" --query value -o tsv 2>"$task_tmp_dir/azure-error")" \
  || ! gmail_password="$(az keyvault secret show --id "$gmail_password_url" --query value -o tsv 2>"$task_tmp_dir/azure-error")"; then
  emit_result 'BLOCKED' 'EXTERNAL_PROVIDER_SECRET_READ_FAILED' false false
  exit 1
fi

if [[ -z "$teams_secret" \
  || "$gmail_username" != *@* \
  || ${#gmail_password} -lt 12 ]]; then
  emit_result 'BLOCKED' 'EXTERNAL_PROVIDER_SECRET_VALUE_INVALID' false false
  exit 1
fi

if [[ "$mode" == '--preflight' ]]; then
  emit_result 'PASS' 'EXTERNAL_NOTIFICATION_ACTIVATION_PREFLIGHT_PASSED' false true
  exit 0
fi

if ! az containerapp update \
  --resource-group "$resource_group" \
  --name "$backend_name" \
  --set-env-vars \
    Notifications__Dispatch__Enabled=true \
    Notifications__TeamsActivity__Enabled=true \
    Notifications__TeamsActivity__DryRun=false \
    Notifications__Mail__Enabled=true \
    Notifications__Mail__DryRun=false \
  --only-show-errors \
  -o json >"$task_tmp_dir/update.json" 2>"$task_tmp_dir/azure-error"; then
  emit_result 'FAILED' 'AZURE_NOTIFICATION_ACTIVATION_UPDATE_FAILED' true false
  exit 1
fi

deadline=$((SECONDS + 300))
revision_ready='false'
while (( SECONDS < deadline )); do
  if az containerapp show \
    --resource-group "$resource_group" \
    --name "$backend_name" \
    -o json >"$task_tmp_dir/show-after.json" 2>"$task_tmp_dir/azure-error" \
    && az containerapp revision list \
      --resource-group "$resource_group" \
      --name "$backend_name" \
      -o json >"$task_tmp_dir/revisions.json" 2>"$task_tmp_dir/azure-error"; then
    latest_revision="$(jq -r '.properties.latestRevisionName // empty' "$task_tmp_dir/show-after.json")"
    after_env="$(jq '.properties.template.containers[0].env' "$task_tmp_dir/show-after.json")"
    actual_flag_count="$(jq '[
      [.[] | select(.name == "Notifications__Dispatch__Enabled" and .value == "true")],
      [.[] | select(.name == "Notifications__TeamsActivity__Enabled" and .value == "true")],
      [.[] | select(.name == "Notifications__TeamsActivity__DryRun" and .value == "false")],
      [.[] | select(.name == "Notifications__Mail__Enabled" and .value == "true")],
      [.[] | select(.name == "Notifications__Mail__DryRun" and .value == "false")]
    ] | map(select(length == 1)) | length' <<<"$after_env")"
    revision_state="$(jq -r --arg name "$latest_revision" '[.[] | select(.name == $name)][0].properties.provisioningState // empty' "$task_tmp_dir/revisions.json")"
    revision_health="$(jq -r --arg name "$latest_revision" '[.[] | select(.name == $name)][0].properties.healthState // empty' "$task_tmp_dir/revisions.json")"
    revision_active="$(jq -r --arg name "$latest_revision" '[.[] | select(.name == $name)][0].properties.active // false' "$task_tmp_dir/revisions.json")"
    after_running_status="$(jq -r '.properties.runningStatus // empty' "$task_tmp_dir/show-after.json")"

    if [[ -n "$latest_revision" \
      && "$actual_flag_count" == '5' \
      && "$revision_state" == 'Provisioned' \
      && "$revision_health" == 'Healthy' \
      && "$revision_active" == 'true' \
      && "$after_running_status" == 'Running' ]]; then
      revision_ready='true'
      break
    fi
  fi
  sleep 10
done

if [[ "$revision_ready" != 'true' ]]; then
  emit_result 'FAILED' 'AZURE_NOTIFICATION_ACTIVATION_REVISION_NOT_READY' true false
  exit 1
fi

emit_result 'ACTIVE' 'EXTERNAL_NOTIFICATION_WORKER_ACTIVATED' true true
