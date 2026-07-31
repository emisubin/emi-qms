import { execFileSync } from 'node:child_process';
import type { Locator } from '@playwright/test';

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/u;

/**
 * Keeps pre-0067 detailed-IQC regression scenarios explicit while the isolated
 * full-stack database creates new projects with the CategoryBased policy.
 */
export function markProjectAsLegacyIqc(projectId: string) {
  if (!uuidPattern.test(projectId)) {
    throw new Error(`Invalid project id for legacy IQC fixture: ${projectId}`);
  }

  execFileSync(
    'docker',
    [
      'compose',
      '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'),
      '--file', requireEnv('E2E_COMPOSE_FILE'),
      'exec',
      '-T', requireEnv('E2E_POSTGRES_SERVICE'),
      'psql',
      '--username', requireEnv('E2E_DATABASE_USER'),
      '--dbname', requireEnv('E2E_DATABASE_NAME'),
      '--no-psqlrc',
      '--set', 'ON_ERROR_STOP=1',
      '--command', `
        begin;
        alter table projects disable trigger trg_guard_project_iqc_routing_policy_immutable;
        update projects
        set iqc_routing_policy = 'AllReceipts'
        where id = '${projectId}';
        alter table projects enable trigger trg_guard_project_iqc_routing_policy_immutable;
        commit;
      `
    ],
    { encoding: 'utf8' }
  );
}

export async function uploadRequiredIqcPhotos(scope: Locator, filePath: string) {
  const editors = scope.locator('.iqc-inline-photo');
  for (let index = 0; index < await editors.count(); index += 1) {
    const editor = editors.nth(index);
    if (await editor.locator('.iqc-photo-evidence').count()) {
      continue;
    }
    await editor.locator('input[type="file"]').setInputFiles(filePath);
    await editor.getByRole('button', { name: '이 항목에 사진 등록' }).click();
    await editor.locator('.iqc-photo-evidence').waitFor();
  }
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is required for isolated full-stack validation.`);
  }
  return value;
}
