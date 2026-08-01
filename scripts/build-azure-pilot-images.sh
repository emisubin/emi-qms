#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() {
  printf 'usage: %s --host HOST --tenant-id GUID --api-client-id GUID --spa-client-id GUID --api-scope SCOPE --tag TAG\n' "$0" >&2
}

public_host=''
tenant_id=''
api_client_id=''
spa_client_id=''
api_scope=''
image_tag=''

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --host)
      public_host="${2:-}"
      shift 2
      ;;
    --tenant-id)
      tenant_id="${2:-}"
      shift 2
      ;;
    --api-client-id)
      api_client_id="${2:-}"
      shift 2
      ;;
    --spa-client-id)
      spa_client_id="${2:-}"
      shift 2
      ;;
    --api-scope)
      api_scope="${2:-}"
      shift 2
      ;;
    --tag)
      image_tag="${2:-}"
      shift 2
      ;;
    *)
      usage
      exit 64
      ;;
  esac
done

if [[ -z "${public_host}" || -z "${tenant_id}" || -z "${api_client_id}" || -z "${spa_client_id}" || -z "${api_scope}" || -z "${image_tag}" ]]; then
  usage
  exit 64
fi

guid_pattern='^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$'
if [[ ! "${tenant_id}" =~ ${guid_pattern} \
  || ! "${api_client_id}" =~ ${guid_pattern} \
  || ! "${spa_client_id}" =~ ${guid_pattern} \
  || "${api_client_id}" == "${spa_client_id}" ]]; then
  printf 'azurePilotImages=INVALID_ENTRA_CONFIGURATION\n' >&2
  exit 65
fi

if [[ "${api_scope}" != "api://${api_client_id}/"* ]]; then
  printf 'azurePilotImages=INVALID_API_SCOPE\n' >&2
  exit 65
fi

if [[ ! "${public_host}" =~ ^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$ \
  || ! "${image_tag}" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]]; then
  printf 'azurePilotImages=INVALID_HOST_OR_TAG\n' >&2
  exit 65
fi

docker build \
  --file "${repository_root}/backend/Dockerfile.production" \
  --tag "pms-backend:${image_tag}" \
  "${repository_root}"

docker build \
  --file "${repository_root}/frontend/Dockerfile.azure" \
  --tag "pms-frontend:${image_tag}" \
  --build-arg "VITE_AZURE_TENANT_ID=${tenant_id}" \
  --build-arg "VITE_AZURE_CLIENT_ID=${spa_client_id}" \
  --build-arg "VITE_AZURE_API_CLIENT_ID=${api_client_id}" \
  --build-arg "VITE_AZURE_API_SCOPE=${api_scope}" \
  --build-arg "VITE_AZURE_REDIRECT_URI=https://${public_host}" \
  "${repository_root}"

printf 'azurePilotBackendImage=BUILT\n'
printf 'azurePilotFrontendImage=BUILT\n'
printf 'azurePilotImagePush=NOT_PERFORMED\n'
