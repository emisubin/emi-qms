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
if (manifest.name !== 'EMI 프로젝트 통합관리시스템'
  || manifest.short_name !== 'EMI QMS'
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

for (const forbidden of ['service-worker.js', 'sw.js']) {
  if (existsSync(join(publicDirectory, forbidden))) {
    fail('SERVICE_WORKER_OUT_OF_SCOPE');
  }
}
NODE

printf 'pwaAssetTests=1\n'
printf 'pwaAssetTestsPassed=1\n'
