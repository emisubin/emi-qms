#!/usr/bin/env bash
set -euo pipefail

required_environment=(
  SOURCE_SHA
  CONFIRM_IMAGE_PUSH
  AZURE_CLIENT_ID
  AZURE_TENANT_ID
  AZURE_SUBSCRIPTION_ID
  ACR_NAME
  ACR_LOGIN_SERVER
  PUBLIC_HOSTNAME
  ENTRA_API_CLIENT_ID
  ENTRA_SPA_CLIENT_ID
  ENTRA_API_SCOPE
)

for variable_name in "${required_environment[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    printf 'azurePilotImagePublish=MISSING_CONFIGURATION\n' >&2
    exit 63
  fi
done

if [[ "${CONFIRM_IMAGE_PUSH}" != 'true' ]]; then
  printf 'azurePilotImagePublish=CONFIRMATION_REQUIRED\n' >&2
  exit 64
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
  || ! git merge-base --is-ancestor "${SOURCE_SHA}" refs/remotes/origin/main; then
  printf 'azurePilotImagePublish=SOURCE_NOT_IN_MAIN\n' >&2
  exit 67
fi

guid_pattern='^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$'
zero_guid='00000000-0000-0000-0000-000000000000'
host_pattern='^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$'

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

printf 'azurePilotImageSource=VALIDATED_MAIN_COMMIT\n'
printf 'azurePilotImageConfiguration=VALID\n'
