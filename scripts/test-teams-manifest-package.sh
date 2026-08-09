#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
builder="${repository_root}/scripts/build-teams-manifest-package.sh"
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-teams-manifest-test.XXXXXX")"

cleanup() {
  rm -f "${temporary_directory}/package.zip"
  rm -rf "${temporary_directory}/unpacked"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

"${builder}" \
  --host pms.example.org \
  --manifest-id 11111111-1111-4111-8111-111111111111 \
  --activity-client-id 22222222-2222-4222-8222-222222222222 \
  --web-resource api://22222222-2222-4222-8222-222222222222 \
  --version 1.0.0 \
  --output "${temporary_directory}/package.zip" \
  --allow-synthetic \
  >/dev/null

mkdir "${temporary_directory}/unpacked"
unzip -q "${temporary_directory}/package.zip" -d "${temporary_directory}/unpacked"

PACKAGE_DIRECTORY="${temporary_directory}/unpacked" \
ASSET_DIRECTORY="${repository_root}/infrastructure/teams/assets" \
node --input-type=module <<'NODE'
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';

const directory = process.env.PACKAGE_DIRECTORY;
const entries = readdirSync(directory).sort();
if (JSON.stringify(entries) !== JSON.stringify(['color.png', 'manifest.json', 'outline.png'])) {
  process.exit(1);
}

const manifest = JSON.parse(readFileSync(join(directory, 'manifest.json'), 'utf8'));
const activityTypes = manifest.activities?.activityTypes?.map((activity) => activity.type);
const expectedActivityTypes = [
  'projectCreated',
  'projectDeliveryDateChanged',
  'projectStatusChanged',
  'workItemAssigned',
  'urgentPending',
  'reinspectionRequested',
  'deadlineApproaching',
  'deadlineOverdue',
  'projectCompleted',
  'generalNotification'
];
if (manifest.manifestVersion !== '1.19'
  || Object.hasOwn(manifest, 'packageName')
  || manifest.developer?.name !== 'EMI'
  || manifest.name?.short !== 'EMI PMS'
  || manifest.name?.full !== 'EMI PMS'
  || manifest.description?.short !== 'EMI 프로젝트 업무와 알림을 한 곳에서 확인합니다.'
  || manifest.staticTabs?.[0]?.entityId !== 'home'
  || manifest.staticTabs?.[0]?.name !== 'EMI PMS'
  || manifest.staticTabs?.[0]?.contentUrl !== 'https://pms.example.org/teams-launcher.html'
  || manifest.staticTabs?.[0]?.websiteUrl !== 'https://pms.example.org/'
  || manifest.validDomains?.[0] !== 'pms.example.org'
  || manifest.accentColor !== '#DC2128'
  || JSON.stringify(manifest.permissions) !== JSON.stringify(['identity'])
  || manifest.authorization?.permissions?.resourceSpecific?.[0]?.name !== 'TeamsActivity.Send.User'
  || manifest.webApplicationInfo?.id !== '22222222-2222-4222-8222-222222222222'
  || JSON.stringify(activityTypes) !== JSON.stringify(expectedActivityTypes)
  || activityTypes.includes('dailyDigest')
  || JSON.stringify(manifest).includes('__')) {
  process.exit(1);
}

function pngDimensions(path) {
  const bytes = readFileSync(path);
  if (bytes.toString('hex', 0, 8) !== '89504e470d0a1a0a') {
    process.exit(1);
  }
  return [bytes.readUInt32BE(16), bytes.readUInt32BE(20)];
}

if (JSON.stringify(pngDimensions(join(directory, 'color.png'))) !== JSON.stringify([192, 192])
  || JSON.stringify(pngDimensions(join(directory, 'outline.png'))) !== JSON.stringify([32, 32])) {
  process.exit(1);
}

for (const name of ['color.png', 'outline.png']) {
  const packageBytes = readFileSync(join(directory, name));
  const assetBytes = readFileSync(join(process.env.ASSET_DIRECTORY, name));
  if (!packageBytes.equals(assetBytes)) {
    process.exit(1);
  }
}

const expectedBrandAssetDigests = new Map([
  ['color.png', 'a46d5e1e009594482967cc96a4754134bcbb03127cb8d17463ab05eddd25be6e'],
  ['outline.png', 'd099c3eaf47c1761fc699708cb177f97598b101936b80a12b866396c0493e80a']
]);
for (const [name, expectedDigest] of expectedBrandAssetDigests) {
  const digest = createHash('sha256')
    .update(readFileSync(join(process.env.ASSET_DIRECTORY, name)))
    .digest('hex');
  if (digest !== expectedDigest) {
    process.exit(1);
  }
}
NODE

if "${builder}" \
  --host pms.example.org \
  --manifest-id invalid \
  --activity-client-id 22222222-2222-4222-8222-222222222222 \
  --web-resource api://22222222-2222-4222-8222-222222222222 \
  --version 1.0.0 \
  --output "${temporary_directory}/invalid.zip" \
  --allow-synthetic \
  >/dev/null 2>&1; then
  printf 'expected invalid GUID rejection\n' >&2
  exit 1
fi

printf 'teamsManifestPackageTests=2\n'
printf 'teamsManifestPackagePassed=2\n'
