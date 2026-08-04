#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
template_path="${repository_root}/infrastructure/teams/manifest.template.json"
color_icon_source="${repository_root}/infrastructure/teams/assets/color.png"
outline_icon_source="${repository_root}/infrastructure/teams/assets/outline.png"

usage() {
  printf 'usage: %s --host HOST --manifest-id GUID --activity-client-id GUID --web-resource URI --version SEMVER --output ZIP [--allow-synthetic]\n' "$0" >&2
}

public_host=''
manifest_id=''
activity_client_id=''
web_resource=''
app_version=''
output_path=''
allow_synthetic='false'

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --host)
      public_host="${2:-}"
      shift 2
      ;;
    --manifest-id)
      manifest_id="${2:-}"
      shift 2
      ;;
    --activity-client-id)
      activity_client_id="${2:-}"
      shift 2
      ;;
    --web-resource)
      web_resource="${2:-}"
      shift 2
      ;;
    --version)
      app_version="${2:-}"
      shift 2
      ;;
    --output)
      output_path="${2:-}"
      shift 2
      ;;
    --allow-synthetic)
      allow_synthetic='true'
      shift
      ;;
    *)
      usage
      exit 64
      ;;
  esac
done

if [[ -z "${public_host}" || -z "${manifest_id}" || -z "${activity_client_id}" || -z "${web_resource}" || -z "${app_version}" || -z "${output_path}" ]]; then
  usage
  exit 64
fi

if [[ ! "${manifest_id}" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ \
  || ! "${activity_client_id}" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ ]]; then
  printf 'teamsManifestPackage=INVALID_GUID\n' >&2
  exit 65
fi

if [[ ! "${public_host}" =~ ^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$ \
  || "${public_host}" == 'localhost' ]]; then
  printf 'teamsManifestPackage=INVALID_HOST\n' >&2
  exit 65
fi

if [[ "${allow_synthetic}" != 'true' && "${public_host}" =~ (^|\.)example\.(com|net|org)$ ]]; then
  printf 'teamsManifestPackage=RESERVED_HOST\n' >&2
  exit 65
fi

if [[ ! "${web_resource}" =~ ^(api://|https://)[^[:space:]]+$ ]]; then
  printf 'teamsManifestPackage=INVALID_RESOURCE\n' >&2
  exit 65
fi

if [[ ! "${app_version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  printf 'teamsManifestPackage=INVALID_VERSION\n' >&2
  exit 65
fi

if [[ ! -f "${template_path}" || ! -f "${color_icon_source}" || ! -f "${outline_icon_source}" ]]; then
  printf 'teamsManifestPackage=MISSING_SOURCE\n' >&2
  exit 66
fi

temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-teams-manifest.XXXXXX")"
cleanup() {
  rm -f "${temporary_directory}/manifest.json" \
    "${temporary_directory}/color.png" \
    "${temporary_directory}/outline.png"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

PUBLIC_HOST="${public_host}" \
TEAMS_MANIFEST_ID="${manifest_id}" \
TEAMS_ACTIVITY_CLIENT_ID="${activity_client_id}" \
TEAMS_WEB_APP_RESOURCE="${web_resource}" \
APP_VERSION="${app_version}" \
TEMPLATE_PATH="${template_path}" \
OUTPUT_PATH="${temporary_directory}/manifest.json" \
node --input-type=module <<'NODE'
import { readFileSync, writeFileSync } from 'node:fs';

const values = new Map([
  ['__PUBLIC_HOST__', process.env.PUBLIC_HOST],
  ['__TEAMS_MANIFEST_ID__', process.env.TEAMS_MANIFEST_ID],
  ['__TEAMS_ACTIVITY_CLIENT_ID__', process.env.TEAMS_ACTIVITY_CLIENT_ID],
  ['__TEAMS_WEB_APP_RESOURCE__', process.env.TEAMS_WEB_APP_RESOURCE],
  ['__APP_VERSION__', process.env.APP_VERSION]
]);

let rendered = readFileSync(process.env.TEMPLATE_PATH, 'utf8');
for (const [placeholder, value] of values) {
  rendered = rendered.replaceAll(placeholder, value);
}

const manifest = JSON.parse(rendered);
const unresolved = JSON.stringify(manifest).match(/__[A-Z0-9_]+__/u);
if (unresolved) {
  process.exit(65);
}

writeFileSync(process.env.OUTPUT_PATH, `${JSON.stringify(manifest, null, 2)}\n`, {
  encoding: 'utf8',
  mode: 0o600
});
NODE

cp "${color_icon_source}" "${temporary_directory}/color.png"
cp "${outline_icon_source}" "${temporary_directory}/outline.png"

output_directory="$(cd "$(dirname "${output_path}")" && pwd)"
output_file="${output_directory}/$(basename "${output_path}")"
if [[ -e "${output_file}" ]]; then
  printf 'teamsManifestPackage=OUTPUT_EXISTS\n' >&2
  exit 73
fi

(
  cd "${temporary_directory}"
  zip -q -X "${output_file}" manifest.json color.png outline.png
)

printf 'teamsManifestPackage=CREATED\n'
printf 'teamsManifestEntries=3\n'
