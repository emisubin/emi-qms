#!/usr/bin/env bash
set -euo pipefail

source_only='false'
if [[ "${1:-}" == '--source-only' ]]; then
  source_only='true'
elif [[ "$#" -ne 0 ]]; then
  printf 'usage: %s [--source-only]\n' "$0" >&2
  exit 64
fi

source_environment=(
  SOURCE_SHA
  CONFIRM_IMAGE_PUSH
  CONFIRM_PRODUCTION_DEPLOY
)

for variable_name in "${source_environment[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    printf 'azurePilotImagePublish=MISSING_CONFIGURATION\n' >&2
    exit 63
  fi
done

required_environment=(
  AZURE_CLIENT_ID
  AZURE_TENANT_ID
  AZURE_SUBSCRIPTION_ID
  ACR_NAME
  ACR_LOGIN_SERVER
  PUBLIC_HOSTNAME
  ENTRA_API_CLIENT_ID
  ENTRA_SPA_CLIENT_ID
  ENTRA_API_SCOPE
  AZURE_RESOURCE_GROUP
  BACKEND_APP_NAME
  FRONTEND_APP_NAME
  MIGRATION_JOB_NAME
)

if [[ "${CONFIRM_IMAGE_PUSH}" != 'true' ]]; then
  printf 'azurePilotImagePublish=CONFIRMATION_REQUIRED\n' >&2
  exit 64
fi

if [[ "${CONFIRM_PRODUCTION_DEPLOY}" != 'true' ]]; then
  printf 'azurePilotImagePublish=DEPLOY_CONFIRMATION_REQUIRED\n' >&2
  exit 68
fi

if [[ ! "${SOURCE_SHA}" =~ ^[0-9a-f]{40}$ ]]; then
  printf 'azurePilotImagePublish=INVALID_SOURCE_SHA\n' >&2
  exit 65
fi

if ! git cat-file -e "${SOURCE_SHA}^{commit}" 2>/dev/null; then
  printf 'azurePilotImagePublish=SOURCE_SHA_NOT_FOUND\n' >&2
  exit 66
fi

if ! git show-ref --verify --quiet refs/remotes/origin/main \
  || [[ "$(git rev-parse refs/remotes/origin/main)" != "${SOURCE_SHA}" ]]; then
  printf 'azurePilotImagePublish=SOURCE_NOT_LATEST_MAIN\n' >&2
  exit 67
fi

if [[ "${source_only}" == 'true' ]]; then
  printf 'azurePilotImageSource=VALIDATED_MAIN_COMMIT\n'
  exit 0
fi

for variable_name in "${required_environment[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    printf 'azurePilotImagePublish=MISSING_CONFIGURATION\n' >&2
    exit 63
  fi
done

guid_pattern='^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$'
zero_guid='00000000-0000-0000-0000-000000000000'
host_pattern='^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$'
resource_group_pattern='^[A-Za-z0-9._()-]{1,90}$'
container_app_pattern='^[a-z][a-z0-9-]{0,30}[a-z0-9]$'

for identifier in \
  "${AZURE_CLIENT_ID}" \
  "${AZURE_TENANT_ID}" \
  "${AZURE_SUBSCRIPTION_ID}" \
  "${ENTRA_API_CLIENT_ID}" \
  "${ENTRA_SPA_CLIENT_ID}"; do
  if [[ ! "${identifier}" =~ ${guid_pattern} || "${identifier}" == "${zero_guid}" ]]; then
    printf 'azurePilotImagePublish=INVALID_IDENTIFIER_CONFIGURATION\n' >&2
    exit 69
  fi
done

shopt -s nocasematch
if [[ "${ENTRA_API_CLIENT_ID}" == "${ENTRA_SPA_CLIENT_ID}" ]]; then
  printf 'azurePilotImagePublish=INVALID_IDENTIFIER_CONFIGURATION\n' >&2
  exit 69
fi

if [[ ! "${ACR_NAME}" =~ ^[A-Za-z0-9]{5,50}$ \
  || "${ACR_LOGIN_SERVER}" != "${ACR_NAME}.azurecr.io" ]]; then
  printf 'azurePilotImagePublish=INVALID_REGISTRY_CONFIGURATION\n' >&2
  exit 70
fi

if [[ ! "${PUBLIC_HOSTNAME}" =~ ${host_pattern} \
  || "${PUBLIC_HOSTNAME}" =~ (^|\.)(example\.com|example|invalid|test)$ \
  || "${ENTRA_API_SCOPE}" != "api://${ENTRA_API_CLIENT_ID}/"* ]]; then
  printf 'azurePilotImagePublish=INVALID_FRONTEND_CONFIGURATION\n' >&2
  exit 71
fi
shopt -u nocasematch

if [[ ! "${AZURE_RESOURCE_GROUP}" =~ ${resource_group_pattern} \
  || ! "${BACKEND_APP_NAME}" =~ ${container_app_pattern} \
  || ! "${FRONTEND_APP_NAME}" =~ ${container_app_pattern} \
  || ! "${MIGRATION_JOB_NAME}" =~ ${container_app_pattern} \
  || "${BACKEND_APP_NAME}" == *--* \
  || "${FRONTEND_APP_NAME}" == *--* \
  || "${MIGRATION_JOB_NAME}" == *--* \
  || "${BACKEND_APP_NAME}" == "${FRONTEND_APP_NAME}" ]]; then
  printf 'azurePilotImagePublish=INVALID_RESOURCE_CONFIGURATION\n' >&2
  exit 72
fi

printf 'azurePilotImageSource=VALIDATED_MAIN_COMMIT\n'
printf 'azurePilotImageConfiguration=VALID\n'
