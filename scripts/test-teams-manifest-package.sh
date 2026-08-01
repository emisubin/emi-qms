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

PACKAGE_DIRECTORY="${temporary_directory}/unpacked" node --input-type=module <<'NODE'
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const directory = process.env.PACKAGE_DIRECTORY;
const entries = readdirSync(directory).sort();
if (JSON.stringify(entries) !== JSON.stringify(['color.png', 'manifest.json', 'outline.png'])) {
  process.exit(1);
}

const manifest = JSON.parse(readFileSync(join(directory, 'manifest.json'), 'utf8'));
if (manifest.manifestVersion !== '1.19'
  || manifest.staticTabs?.[0]?.entityId !== 'home'
  || manifest.validDomains?.[0] !== 'pms.example.org'
  || manifest.authorization?.permissions?.resourceSpecific?.[0]?.name !== 'TeamsActivity.Send.User'
  || manifest.activities?.activityTypes?.length !== 6
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
