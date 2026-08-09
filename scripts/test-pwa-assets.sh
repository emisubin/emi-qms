#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

REPOSITORY_ROOT="${repository_root}" node --input-type=module <<'NODE'
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const root = process.env.REPOSITORY_ROOT;
const frontend = join(root, 'frontend');
const publicDirectory = join(frontend, 'public');
const manifestPath = join(publicDirectory, 'manifest.webmanifest');
const indexPath = join(frontend, 'index.html');
const teamsLauncherPath = join(publicDirectory, 'teams-launcher.html');
const teamsLauncherScriptPath = join(publicDirectory, 'teams-launcher.js');
const expectedIcons = new Map([
  ['/icons/emi-qms-192.png', [192, 192]],
  ['/icons/emi-qms-512.png', [512, 512]],
  ['/icons/emi-qms-maskable-512.png', [512, 512]],
  ['/icons/apple-touch-icon.png', [180, 180]],
  ['/icons/favicon-32.png', [32, 32]]
]);

function fail(code) {
  process.stderr.write(`pwaAssets=${code}\n`);
  process.exit(1);
}

function pngDimensions(path) {
  const bytes = readFileSync(path);
  if (bytes.length < 29 || bytes.toString('hex', 0, 8) !== '89504e470d0a1a0a') {
    fail('INVALID_PNG');
  }
  return [bytes.readUInt32BE(16), bytes.readUInt32BE(20)];
}

const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
if (manifest.name !== 'EMI PMS'
  || manifest.short_name !== 'EMI PMS'
  || manifest.description !== '프로젝트 생성부터 생산관리, 구매, 제조, 품질, 물류와 정산까지 연결하는 EMI 프로젝트 통합관리시스템(EMI PMS)입니다.'
  || manifest.start_url !== '/'
  || manifest.scope !== '/'
  || manifest.display !== 'standalone'
  || manifest.background_color !== '#FFFFFF'
  || manifest.theme_color !== '#DC2128') {
  fail('INVALID_MANIFEST');
}

const declaredIcons = new Map((manifest.icons ?? []).map((icon) => [icon.src, icon]));
for (const [src, dimensions] of expectedIcons) {
  const path = join(publicDirectory, src.slice(1));
  if (!existsSync(path) || JSON.stringify(pngDimensions(path)) !== JSON.stringify(dimensions)) {
    fail('INVALID_ICON');
  }
  if (src.includes('apple-touch') || src.includes('favicon')) {
    continue;
  }
  const declared = declaredIcons.get(src);
  if (!declared || declared.type !== 'image/png' || declared.sizes !== dimensions.join('x')) {
    fail('MISSING_ICON_DECLARATION');
  }
}

if (declaredIcons.get('/icons/emi-qms-maskable-512.png')?.purpose !== 'maskable'
  || declaredIcons.get('/icons/emi-qms-192.png')?.purpose !== 'any'
  || declaredIcons.get('/icons/emi-qms-512.png')?.purpose !== 'any') {
  fail('INVALID_ICON_PURPOSE');
}

const index = readFileSync(indexPath, 'utf8');
for (const expected of [
  'href="/manifest.webmanifest"',
  '<title>EMI PMS</title>',
  'name="application-name" content="EMI PMS"',
  'name="apple-mobile-web-app-title" content="EMI PMS"',
  'content="#DC2128"',
  'name="mobile-web-app-capable" content="yes"',
  'name="apple-mobile-web-app-capable" content="yes"',
  'href="/icons/favicon-32.png"',
  'href="/icons/apple-touch-icon.png"'
]) {
  if (!index.includes(expected)) {
    fail('MISSING_HTML_LINK');
  }
}

if (!existsSync(teamsLauncherPath) || !existsSync(teamsLauncherScriptPath)) {
  fail('MISSING_TEAMS_LAUNCHER');
}

const teamsLauncher = readFileSync(teamsLauncherPath, 'utf8');
const teamsLauncherScript = readFileSync(teamsLauncherScriptPath, 'utf8');
for (const expected of [
  '<title>EMI PMS</title>',
  'id="open-emi-pms"',
  'target="_blank"',
  'rel="noopener noreferrer"',
  'src="/teams-launcher.js"'
]) {
  if (!teamsLauncher.includes(expected)) {
    fail('INVALID_TEAMS_LAUNCHER');
  }
}

for (const forbidden of ['/src/main.tsx', '/assets/', 'manifest.webmanifest', 'serviceWorker', 'EMI QMS']) {
  if (teamsLauncher.includes(forbidden)) {
    fail('TEAMS_LAUNCHER_EXPOSES_APP');
  }
}

for (const expected of ['notificationPattern', '/teams/activity/notifications/', 'window.location.origin']) {
  if (!teamsLauncherScript.includes(expected)) {
    fail('INVALID_TEAMS_LAUNCHER_SCRIPT');
  }
}

for (const forbidden of ['eval(', 'new Function(', 'localStorage', 'sessionStorage']) {
  if (teamsLauncherScript.includes(forbidden)) {
    fail('UNSAFE_TEAMS_LAUNCHER_SCRIPT');
  }
}

for (const forbidden of ['service-worker.js', 'sw.js']) {
  if (existsSync(join(publicDirectory, forbidden))) {
    fail('SERVICE_WORKER_OUT_OF_SCOPE');
  }
}
NODE

printf 'pwaAssetTests=1\n'
printf 'pwaAssetTestsPassed=1\n'
