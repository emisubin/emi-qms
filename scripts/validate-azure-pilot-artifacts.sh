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
  "${repository_root}/frontend/Dockerfile.azure"
  "${azure_directory}/nginx.conf.template"
  "${azure_directory}/foundation.bicep"
  "${azure_directory}/identity-access.bicep"
  "${azure_directory}/workloads.bicep"
  "${azure_directory}/edge.bicep"
  "${azure_directory}/foundation.parameters.example.json"
  "${azure_directory}/identity-access.parameters.example.json"
  "${azure_directory}/workloads.parameters.example.json"
  "${azure_directory}/edge.parameters.example.json"
  "${repository_root}/infrastructure/teams/manifest.template.json"
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

for (const name of [
  'foundation.parameters.example.json',
  'identity-access.parameters.example.json',
  'workloads.parameters.example.json',
  'edge.parameters.example.json'
]) {
  JSON.parse(read(join(azure, name)));
}
JSON.parse(read(join(root, 'infrastructure', 'teams', 'manifest.template.json')));

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
  [nginx, '$http_x_azure_fdid'],
  [nginx, '$http_x_pms_origin_verify'],
  [nginx, 'return 403;'],
  [dockerfile, 'EXPOSE 8080'],
  [dockerfile, '@sha256:']
];

for (const [source, expected] of checks) {
  if (!source.includes(expected)) {
    process.exit(1);
  }
}

if (workloads.includes('external: true') && !workloads.includes("name: 'frontend'")) {
  process.exit(1);
}

if (identityAccess.includes("scope: keyVault\n")
  || workloads.includes('runtimeIdentity')
  || workloads.includes('database-connection-string')) {
  process.exit(1);
}

const secretScopes = identityAccess.match(/scope: \w+Secret$/gmu) ?? [];
if (secretScopes.length !== 10) {
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
  read(join(root, 'infrastructure', 'teams', 'manifest.template.json'))
].join('\n');
if (/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/iu.test(trackedDeploymentSource)) {
  process.exit(1);
}
NODE

"${repository_root}/scripts/test-teams-manifest-package.sh" >/dev/null

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
  printf 'azurePilotBicepCompile=PASS\n'
else
  printf 'azurePilotBicepCompile=NOT_REQUESTED\n'
fi

printf 'azurePilotStaticValidation=PASS\n'
