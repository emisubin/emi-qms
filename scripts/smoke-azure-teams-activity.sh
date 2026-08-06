#!/usr/bin/env bash
set -euo pipefail

usage() {
  printf '%s\n' 'usage: smoke-azure-teams-activity.sh --inspect-installation | --confirm-one-actual-teams-activity'
}

emit_installation_result() {
  local status="$1"
  local code="$2"
  local http_status="$3"
  local personal_install_count="$4"
  local current_version_count="$5"
  jq -n \
    --arg status "$status" \
    --arg code "$code" \
    --arg httpStatus "$http_status" \
    --argjson personalInstallCount "$personal_install_count" \
    --argjson currentVersionCount "$current_version_count" \
    '{
      scope: "TEAMS_ACTIVITY_PERSONAL_INSTALLATION_READ_ONLY",
      installationInspection: $status,
      stableCode: $code,
      graphHttpStatus: $httpStatus,
      targetAppPersonalInstallCount: $personalInstallCount,
      targetVersion104Count: $currentVersionCount,
      actualProviderCallCount: 0,
      runtimeNotificationSettingsChanged: false
    }'
}

emit_result() {
  local status="$1"
  local code="$2"
  local http_status="$3"
  jq -n \
    --arg status "$status" \
    --arg code "$code" \
    --arg httpStatus "$http_status" \
    '{
      scope: "TEAMS_ACTIVITY_ONE_RECIPIENT_SYNTHETIC",
      recipientCount: 1,
      actualProviderCallCount: (if $httpStatus == "NOT_CALLED" then 0 else 1 end),
      runtimeNotificationSettingsChanged: false,
      dispatcherEnabled: false,
      mailEnabled: false,
      payloadContainsBusinessData: false,
      providerStatus: $status,
      stableCode: $code,
      graphHttpStatus: $httpStatus
    }'
}

mode="${1:-}"
if [[ "$mode" != '--inspect-installation' \
  && "$mode" != '--confirm-one-actual-teams-activity' ]]; then
  usage >&2
  exit 64
fi
if [[ $# -ne 1 ]]; then
  usage >&2
  exit 64
fi

for command_name in az curl jq; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    emit_result 'BLOCKED' 'REQUIRED_COMMAND_MISSING' 'NOT_CALLED'
    exit 1
  fi
done

task_tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/emi-qms-teams-smoke.XXXXXX")"
# The EXIT trap invokes cleanup on every exit path.
# shellcheck disable=SC2329
cleanup() {
  rm -f -- \
    "$task_tmp_dir/azure-error" \
    "$task_tmp_dir/provider-error" \
    "$task_tmp_dir/graph-response.json" \
    "$task_tmp_dir/installed-apps.json"
  rmdir "$task_tmp_dir"
}
trap cleanup EXIT

if ! apps_json="$(az containerapp list -o json 2>"$task_tmp_dir/azure-error")"; then
  emit_result 'BLOCKED' 'AZURE_CONTAINERAPP_READ_FAILED' 'NOT_CALLED'
  exit 1
fi

backend_candidates="$(jq '[.[] | select(any(.properties.template.containers[0].env[]?; .name == "Notifications__TeamsActivity__ClientId"))]' <<<"$apps_json")"
backend_count="$(jq 'length' <<<"$backend_candidates")"
if [[ "$backend_count" != '1' ]]; then
  emit_result 'BLOCKED' 'BACKEND_CANDIDATE_COUNT_INVALID' 'NOT_CALLED'
  exit 1
fi

resource_group="$(jq -r '.[0].resourceGroup // empty' <<<"$backend_candidates")"
backend_name="$(jq -r '.[0].name // empty' <<<"$backend_candidates")"
env_json="$(jq '.[0].properties.template.containers[0].env' <<<"$backend_candidates")"

env_value() {
  local name="$1"
  jq -r --arg name "$name" '[.[] | select(.name == $name) | .value][0] // empty' <<<"$env_json"
}

dispatch_enabled="$(env_value 'Notifications__Dispatch__Enabled')"
teams_enabled="$(env_value 'Notifications__TeamsActivity__Enabled')"
teams_dry_run="$(env_value 'Notifications__TeamsActivity__DryRun')"
mail_enabled="$(env_value 'Notifications__Mail__Enabled')"
mail_dry_run="$(env_value 'Notifications__Mail__DryRun')"
tenant_id="$(env_value 'Notifications__TeamsActivity__TenantId')"
client_id="$(env_value 'Notifications__TeamsActivity__ClientId')"
manifest_id="$(env_value 'Notifications__TeamsActivity__TeamsManifestExternalId')"
topic_web_url="$(env_value 'Notifications__TeamsActivity__TopicWebUrl')"

if [[ "$dispatch_enabled" != 'false' \
  || "$teams_enabled" != 'false' \
  || "$teams_dry_run" != 'true' \
  || "$mail_enabled" != 'false' \
  || "$mail_dry_run" != 'true' ]]; then
  emit_result 'BLOCKED' 'RUNTIME_NOTIFICATION_SAFETY_STATE_INVALID' 'NOT_CALLED'
  exit 1
fi

if [[ -z "$resource_group" \
  || -z "$backend_name" \
  || -z "$tenant_id" \
  || -z "$client_id" \
  || -z "$manifest_id" \
  || "$topic_web_url" != https://* ]]; then
  emit_result 'BLOCKED' 'TEAMS_ACTIVITY_CONFIG_INCOMPLETE' 'NOT_CALLED'
  exit 1
fi

if ! secrets_json="$(az containerapp secret list --resource-group "$resource_group" --name "$backend_name" -o json 2>"$task_tmp_dir/azure-error")"; then
  emit_result 'BLOCKED' 'AZURE_SECRET_BINDING_READ_FAILED' 'NOT_CALLED'
  exit 1
fi

teams_secret_url="$(jq -r '[.[] | select(.name == "teams-activity-client-secret") | .keyVaultUrl][0] // empty' <<<"$secrets_json")"
bootstrap_secret_url="$(jq -r '[.[] | select(.name == "bootstrap-administrator-emails") | .keyVaultUrl][0] // empty' <<<"$secrets_json")"
if [[ -z "$teams_secret_url" || -z "$bootstrap_secret_url" ]]; then
  emit_result 'BLOCKED' 'KEY_VAULT_SECRET_BINDING_MISSING' 'NOT_CALLED'
  exit 1
fi

if ! client_secret="$(az keyvault secret show --id "$teams_secret_url" --query value -o tsv 2>"$task_tmp_dir/azure-error")" \
  || ! bootstrap_admins="$(az keyvault secret show --id "$bootstrap_secret_url" --query value -o tsv 2>"$task_tmp_dir/azure-error")"; then
  emit_result 'BLOCKED' 'KEY_VAULT_SECRET_READ_FAILED' 'NOT_CALLED'
  exit 1
fi

if ! account_type="$(az account show --query user.type -o tsv 2>"$task_tmp_dir/azure-error")" \
  || ! account_user="$(az account show --query user.name -o tsv 2>"$task_tmp_dir/azure-error")" \
  || [[ "$account_type" != 'user' || -z "$account_user" ]]; then
  emit_result 'BLOCKED' 'SIGNED_IN_AZURE_USER_REQUIRED' 'NOT_CALLED'
  exit 1
fi

bootstrap_match='false'
normalized_admins="${bootstrap_admins//;/,}"
account_user_lower="$(printf '%s' "$account_user" | tr '[:upper:]' '[:lower:]')"
IFS=',' read -r -a admin_entries <<<"$normalized_admins"
for admin_entry in "${admin_entries[@]}"; do
  trimmed_entry="${admin_entry#"${admin_entry%%[![:space:]]*}"}"
  trimmed_entry="${trimmed_entry%"${trimmed_entry##*[![:space:]]}"}"
  trimmed_entry_lower="$(printf '%s' "$trimmed_entry" | tr '[:upper:]' '[:lower:]')"
  if [[ "$trimmed_entry_lower" == "$account_user_lower" ]]; then
    bootstrap_match='true'
    break
  fi
done
if [[ "$bootstrap_match" != 'true' ]]; then
  emit_result 'BLOCKED' 'SIGNED_IN_USER_NOT_BOOTSTRAP_ADMIN' 'NOT_CALLED'
  exit 1
fi

if ! recipient_object_id="$(az ad signed-in-user show --query id -o tsv 2>"$task_tmp_dir/azure-error")" \
  || [[ -z "$recipient_object_id" ]]; then
  emit_result 'BLOCKED' 'RECIPIENT_OBJECT_ID_UNAVAILABLE' 'NOT_CALLED'
  exit 1
fi

token_response="$(curl --silent --show-error --fail-with-body \
  --request POST \
  --url "https://login.microsoftonline.com/${tenant_id}/oauth2/v2.0/token" \
  --header 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode "client_id=${client_id}" \
  --data-urlencode "client_secret=${client_secret}" \
  --data-urlencode 'scope=https://graph.microsoft.com/.default' \
  --data-urlencode 'grant_type=client_credentials' \
  2>"$task_tmp_dir/provider-error" || true)"
access_token="$(jq -r '.access_token // empty' <<<"$token_response" 2>/dev/null || true)"
if [[ -z "$access_token" ]]; then
  if [[ "$mode" == '--inspect-installation' ]]; then
    emit_installation_result 'BLOCKED' 'TEAMS_ACTIVITY_TOKEN_FAILED' 'NOT_CALLED' null null
  else
    emit_result 'FAILED' 'TEAMS_ACTIVITY_TOKEN_FAILED' 'NOT_CALLED'
  fi
  exit 1
fi

if [[ "$mode" == '--inspect-installation' ]]; then
  # This is the literal Microsoft Graph OData query parameter name.
  # shellcheck disable=SC2016
  installation_http_status="$(curl --silent --show-error \
    --output "$task_tmp_dir/installed-apps.json" \
    --write-out '%{http_code}' \
    --get \
    --url "https://graph.microsoft.com/v1.0/users/${recipient_object_id}/teamwork/installedApps" \
    --data-urlencode '$expand=teamsApp,teamsAppDefinition' \
    --header "Authorization: Bearer ${access_token}" \
    2>"$task_tmp_dir/provider-error" || true)"

  if [[ "$installation_http_status" == '200' ]]; then
    personal_install_count="$(jq --arg manifestId "$manifest_id" \
      '[.value[]? | select(.teamsApp.externalId == $manifestId)] | length' \
      "$task_tmp_dir/installed-apps.json")"
    current_version_count="$(jq --arg manifestId "$manifest_id" \
      '[.value[]? | select(.teamsApp.externalId == $manifestId and .teamsAppDefinition.version == "1.0.4")] | length' \
      "$task_tmp_dir/installed-apps.json")"
    emit_installation_result \
      'COMPLETED' \
      'TEAMS_ACTIVITY_INSTALLATION_READ_SUCCEEDED' \
      "$installation_http_status" \
      "$personal_install_count" \
      "$current_version_count"
    exit 0
  fi

  installation_code='TEAMS_ACTIVITY_INSTALLATION_READ_FAILED'
  if [[ "$installation_http_status" == '401' ]]; then
    installation_code='TEAMS_ACTIVITY_INSTALLATION_READ_UNAUTHORIZED'
  elif [[ "$installation_http_status" == '403' ]]; then
    installation_code='TEAMS_ACTIVITY_INSTALLATION_READ_PERMISSION_DENIED'
  elif [[ "$installation_http_status" == '404' ]]; then
    installation_code='TEAMS_ACTIVITY_INSTALLATION_TARGET_NOT_FOUND'
  elif [[ "$installation_http_status" == '429' ]]; then
    installation_code='TEAMS_ACTIVITY_INSTALLATION_READ_THROTTLED'
  elif [[ "$installation_http_status" == 5* ]]; then
    installation_code='TEAMS_ACTIVITY_INSTALLATION_GRAPH_SERVER_ERROR'
  fi
  emit_installation_result \
    'BLOCKED' \
    "$installation_code" \
    "${installation_http_status:-UNKNOWN}" \
    null \
    null
  exit 1
fi

activity_title='EMI PMS Teams Activity 연결 테스트'
activity_preview='EMI PMS 공개 운영 알림 연결을 확인하는 합성 테스트입니다. 실제 업무 알림이 아닙니다.'
deep_link="$(jq -nr \
  --arg appId "$manifest_id" \
  --arg webUrl "${topic_web_url%/}/teams/activity" \
  '"https://teams.microsoft.com/l/entity/" + ($appId | @uri) + "/home?webUrl=" + ($webUrl | @uri)')"
payload="$(jq -n \
  --arg title "$activity_title" \
  --arg preview "$activity_preview" \
  --arg webUrl "$deep_link" \
  '{
    topic: {source: "text", value: $title, webUrl: $webUrl},
    activityType: "generalNotification",
    previewText: {content: $preview},
    templateParameters: [{name: "title", value: $title}]
  }')"

graph_http_status="$(curl --silent --show-error \
  --output "$task_tmp_dir/graph-response.json" \
  --write-out '%{http_code}' \
  --request POST \
  --url "https://graph.microsoft.com/v1.0/users/${recipient_object_id}/teamwork/sendActivityNotification" \
  --header "Authorization: Bearer ${access_token}" \
  --header 'Content-Type: application/json' \
  --data "$payload" \
  2>"$task_tmp_dir/provider-error" || true)"

if [[ "$graph_http_status" == '204' ]]; then
  emit_result 'SENT' 'TEAMS_ACTIVITY_GRAPH_ACCEPTED' "$graph_http_status"
  exit 0
fi

graph_message="$(jq -r '.error.message // empty' "$task_tmp_dir/graph-response.json" 2>/dev/null || true)"
stable_code='TEAMS_ACTIVITY_GRAPH_ERROR'
if [[ "$graph_message" == *'Failed to find Teams application'* && "$graph_message" == *'installed applications'* ]]; then
  stable_code='TEAMS_ACTIVITY_APP_NOT_INSTALLED'
elif [[ "$graph_message" == *"Invalid 'webUrl'"* || "$graph_message" == *'validDomains'* ]]; then
  stable_code='TEAMS_ACTIVITY_INVALID_TOPIC'
elif [[ "$graph_http_status" == '401' || "$graph_http_status" == '403' ]]; then
  stable_code='TEAMS_ACTIVITY_PERMISSION_DENIED'
elif [[ "$graph_http_status" == '404' ]]; then
  stable_code='TEAMS_ACTIVITY_USER_OR_APP_NOT_FOUND'
elif [[ "$graph_http_status" == '400' ]]; then
  stable_code='TEAMS_ACTIVITY_INVALID_REQUEST'
elif [[ "$graph_http_status" == '429' ]]; then
  stable_code='TEAMS_ACTIVITY_THROTTLED'
elif [[ "$graph_http_status" == 5* ]]; then
  stable_code='TEAMS_ACTIVITY_GRAPH_SERVER_ERROR'
fi

emit_result 'FAILED' "$stable_code" "${graph_http_status:-UNKNOWN}"
exit 1
