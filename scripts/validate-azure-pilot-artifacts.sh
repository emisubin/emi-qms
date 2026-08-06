#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
azure_directory="${repository_root}/infrastructure/azure-pilot"
compile_templates='false'

if [[ "${1:-}" == '--compile' ]]; then
  compile_templates='true'
elif [[ "$#" -ne 0 ]]; then
  printf 'usage: %s [--compile]\n' "$0" >&2
  exit 64
fi

required_files=(
  "${repository_root}/.github/workflows/azure-pilot-images.yml"
  "${repository_root}/scripts/validate-azure-image-publish-inputs.sh"
  "${repository_root}/scripts/test-azure-image-publish-inputs.sh"
  "${repository_root}/frontend/Dockerfile.azure"
  "${azure_directory}/nginx.conf.template"
  "${azure_directory}/foundation.bicep"
  "${azure_directory}/identity-access.bicep"
  "${azure_directory}/workloads.bicep"
  "${azure_directory}/edge.bicep"
  "${azure_directory}/foundation.json"
  "${azure_directory}/identity-access.json"
  "${azure_directory}/workloads.json"
  "${azure_directory}/edge.json"
  "${azure_directory}/foundation.parameters.example.json"
  "${azure_directory}/identity-access.parameters.example.json"
  "${azure_directory}/workloads.parameters.example.json"
  "${azure_directory}/edge.parameters.example.json"
  "${repository_root}/assets/branding/emi-logo.png"
  "${repository_root}/infrastructure/teams/manifest.template.json"
  "${repository_root}/infrastructure/teams/assets/color.png"
  "${repository_root}/infrastructure/teams/assets/outline.png"
  "${repository_root}/frontend/public/manifest.webmanifest"
  "${repository_root}/frontend/public/icons/emi-qms-192.png"
  "${repository_root}/frontend/public/icons/emi-qms-512.png"
  "${repository_root}/frontend/public/icons/emi-qms-maskable-512.png"
  "${repository_root}/frontend/public/icons/apple-touch-icon.png"
  "${repository_root}/frontend/public/icons/favicon-32.png"
)

for required_file in "${required_files[@]}"; do
  if [[ ! -f "${required_file}" ]]; then
    printf 'azurePilotArtifacts=MISSING_FILE\n' >&2
    exit 66
  fi
done

AZURE_DIRECTORY="${azure_directory}" \
REPOSITORY_ROOT="${repository_root}" \
node --input-type=module <<'NODE'
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const azure = process.env.AZURE_DIRECTORY;
const root = process.env.REPOSITORY_ROOT;
const read = path => readFileSync(path, 'utf8');
const foundation = read(join(azure, 'foundation.bicep'));
const identityAccess = read(join(azure, 'identity-access.bicep'));
const workloads = read(join(azure, 'workloads.bicep'));
const edge = read(join(azure, 'edge.bicep'));
const nginx = read(join(azure, 'nginx.conf.template'));
const dockerfile = read(join(root, 'frontend', 'Dockerfile.azure'));
const imageWorkflow = read(join(root, '.github', 'workflows', 'azure-pilot-images.yml'));

for (const name of [
  'foundation.parameters.example.json',
  'identity-access.parameters.example.json',
  'workloads.parameters.example.json',
  'edge.parameters.example.json'
]) {
  JSON.parse(read(join(azure, name)));
}
JSON.parse(read(join(root, 'infrastructure', 'teams', 'manifest.template.json')));

for (const name of [
  'foundation.json',
  'identity-access.json',
  'workloads.json',
  'edge.json'
]) {
  const template = JSON.parse(read(join(azure, name)));
  if (template.$schema !== 'https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#'
    || template.contentVersion !== '1.0.0.0'
    || template.metadata?._generator?.name !== 'bicep'
    || !Array.isArray(template.resources)
    || template.resources.length === 0) {
    process.exit(1);
  }
  for (const parameter of Object.values(template.parameters ?? {})) {
    if ((parameter.type === 'securestring' || parameter.type === 'secureObject')
      && Object.hasOwn(parameter, 'defaultValue')) {
      process.exit(1);
    }
  }
}

const checks = [
  [foundation, "name: 'Standard_B2s'"],
  [foundation, 'storageSizeGB: 32'],
  [foundation, 'backupRetentionDays: 14'],
  [foundation, "mode: 'Disabled'"],
  [foundation, 'dailyQuotaGb: 1'],
  [foundation, 'shareQuota: 5'],
  [foundation, "name: 'Basic'"],
  [foundation, "name: 'Standard_AzureFrontDoor'"],
  [foundation, "publicNetworkAccess: 'Disabled'"],
  [foundation, 'backendIdentityId'],
  [foundation, 'frontendIdentityId'],
  [foundation, 'migrationIdentityId'],
  [foundation, 'databaseBootstrapIdentityId'],
  [identityAccess, 'scope: databaseRuntimeSecret'],
  [identityAccess, 'scope: databaseMigrationSecret'],
  [identityAccess, 'scope: databaseAdminSecret'],
  [identityAccess, 'scope: originVerificationSecret'],
  [identityAccess, 'scope: entraAccessGateSecret'],
  [workloads, "resource frontendAuth 'Microsoft.App/containerApps/authConfigs@2024-03-01'"],
  [workloads, "unauthenticatedClientAction: 'RedirectToLoginPage'"],
  [workloads, "redirectToProvider: 'azureactivedirectory'"],
  [workloads, "convention: 'Standard'"],
  [workloads, "'/health/live'"],
  [workloads, "clientSecretSettingName: 'entra-access-gate-client-secret'"],
  [workloads, "external: false"],
  [workloads, 'minReplicas: minimumReplicaCount'],
  [workloads, "value: 'false'"],
  [workloads, "keyVaultUrl:"],
  [workloads, "workloadProfileName: 'Consumption'"],
  [workloads, "name: 'database-role-bootstrap'"],
  [workloads, "'--bootstrap-database-roles'"],
  [workloads, "'--migrate-only'"],
  [edge, "linkToDefaultDomain: 'Disabled'"],
  [edge, "certificateType: 'ManagedCertificate'"],
  [nginx, 'map_hash_bucket_size 128;'],
  [nginx, '$http_x_azure_fdid'],
  [nginx, '$http_x_pms_origin_verify'],
  [nginx, 'return 403;'],
  [nginx, 'proxy_set_header Host ${BACKEND_FQDN};'],
  [nginx, 'proxy_set_header X-Forwarded-Host ${PUBLIC_HOST};'],
  [dockerfile, 'EXPOSE 8080'],
  [dockerfile, '@sha256:']
];

for (const [source, expected] of checks) {
  if (!source.includes(expected)) {
    process.exit(1);
  }
}

const backendProbeHostHeaders = workloads.match(
  /httpHeaders:\s*\[\s*\{\s*name: 'Host'\s*value: publicHost\s*\}\s*\]/gmu
) ?? [];
if (backendProbeHostHeaders.length !== 3) {
  process.exit(1);
}

if (workloads.includes('external: true') && !workloads.includes("name: 'frontend'")) {
  process.exit(1);
}

if (nginx.includes('proxy_set_header Host ${PUBLIC_HOST};')) {
  process.exit(1);
}

if (identityAccess.includes("scope: keyVault\n")
  || workloads.includes('runtimeIdentity')
  || workloads.includes('database-connection-string')) {
  process.exit(1);
}

const secretScopes = identityAccess.match(/scope: \w+Secret$/gmu) ?? [];
if (secretScopes.length !== 11) {
  process.exit(1);
}

const fromLines = dockerfile
  .split('\n')
  .map(line => line.trim())
  .filter(line => line.startsWith('FROM '));
if (fromLines.length === 0
  || fromLines.some(line => !/@sha256:[0-9a-f]{64}(?:\s|$)/u.test(line))) {
  process.exit(1);
}

const trackedDeploymentSource = [
  foundation,
  identityAccess,
  workloads,
  edge,
  read(join(azure, 'foundation.json')),
  read(join(azure, 'identity-access.json')),
  read(join(azure, 'workloads.json')),
  read(join(azure, 'edge.json')),
  imageWorkflow,
  read(join(root, 'infrastructure', 'teams', 'manifest.template.json'))
].join('\n');
if (/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/iu.test(trackedDeploymentSource)) {
  process.exit(1);
}

const workflowChecks = [
  'workflow_dispatch:',
  'source_sha:',
  'confirm_image_push:',
  'environment: azure-pilot-image-publish',
  'id-token: write',
  'scripts/validate-azure-image-publish-inputs.sh',
  'azure/login@eec3c95657c1536435858eda1f3ff5437fee8474',
  'docker/setup-buildx-action@bb05f3f5519dd87d3ba754cc423b652a5edd6d2c',
  'docker/build-push-action@53b7df96c91f9c12dcc8a07bcb9ccacbed38856a',
  'provenance: mode=min',
  'sbom: true',
  'AZURE_CORE_OUTPUT: none',
  'Mutable latest tag:',
  'Workload deployment:'
];
if (workflowChecks.some(expected => !imageWorkflow.includes(expected))
  || /^\s{2}(push|pull_request|schedule):/mu.test(imageWorkflow)
  || /azure\/login@[^\s#]{1,39}(?:\s|#|$)/u.test(imageWorkflow)
  || /docker\/(?:setup-buildx-action|build-push-action)@[^\s#]{1,39}(?:\s|#|$)/u.test(imageWorkflow)
  || /client-secret|AZURE_CLIENT_SECRET|\bcreds:/u.test(imageWorkflow)
  || /(?:^|[/:])latest(?:\s|$)/mu.test(imageWorkflow)) {
  process.exit(1);
}
NODE

"${repository_root}/scripts/test-teams-manifest-package.sh" >/dev/null
"${repository_root}/scripts/test-pwa-assets.sh" >/dev/null
"${repository_root}/scripts/test-azure-image-publish-inputs.sh" >/dev/null

if [[ "${compile_templates}" == 'true' ]]; then
  temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-bicep-build.XXXXXX")"
  cleanup() {
    rm -f "${temporary_directory}/foundation.json" \
      "${temporary_directory}/identity-access.json" \
      "${temporary_directory}/workloads.json" \
      "${temporary_directory}/edge.json"
    rmdir "${temporary_directory}" 2>/dev/null || true
  }
  trap cleanup EXIT

  if command -v bicep >/dev/null 2>&1; then
    bicep build "${azure_directory}/foundation.bicep" --outfile "${temporary_directory}/foundation.json"
    bicep build "${azure_directory}/identity-access.bicep" --outfile "${temporary_directory}/identity-access.json"
    bicep build "${azure_directory}/workloads.bicep" --outfile "${temporary_directory}/workloads.json"
    bicep build "${azure_directory}/edge.bicep" --outfile "${temporary_directory}/edge.json"
  elif command -v az >/dev/null 2>&1; then
    az bicep build --file "${azure_directory}/foundation.bicep" --outfile "${temporary_directory}/foundation.json"
    az bicep build --file "${azure_directory}/identity-access.bicep" --outfile "${temporary_directory}/identity-access.json"
    az bicep build --file "${azure_directory}/workloads.bicep" --outfile "${temporary_directory}/workloads.json"
    az bicep build --file "${azure_directory}/edge.bicep" --outfile "${temporary_directory}/edge.json"
  else
    printf 'azurePilotBicepCompile=TOOL_MISSING\n' >&2
    exit 69
  fi

  AZURE_DIRECTORY="${azure_directory}" \
  TEMPORARY_DIRECTORY="${temporary_directory}" \
  node --input-type=module <<'NODE'
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { isDeepStrictEqual } from 'node:util';

const azure = process.env.AZURE_DIRECTORY;
const temporary = process.env.TEMPORARY_DIRECTORY;
const normalize = path => {
  const template = JSON.parse(readFileSync(path, 'utf8'));
  if (template.metadata?._generator) {
    delete template.metadata._generator;
  }
  if (template.metadata && Object.keys(template.metadata).length === 0) {
    delete template.metadata;
  }
  return template;
};

for (const name of [
  'foundation.json',
  'identity-access.json',
  'workloads.json',
  'edge.json'
]) {
  if (!isDeepStrictEqual(
    normalize(join(azure, name)),
    normalize(join(temporary, name))
  )) {
    process.exit(1);
  }
}
NODE
  printf 'azurePilotBicepCompile=PASS\n'
  printf 'azurePilotPortalTemplates=PASS\n'
else
  printf 'azurePilotBicepCompile=NOT_REQUESTED\n'
fi

printf 'azurePilotStaticValidation=PASS\n'
