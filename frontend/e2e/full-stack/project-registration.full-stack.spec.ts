import { execFileSync } from 'node:child_process';
import { expect, type Page, test } from '@playwright/test';

const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const apiBaseUrl = 'http://127.0.0.1:5081';

test('full-stack: project registration, permissions, status, and panel count use the real backend and PostgreSQL', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003A Full Stack ${unique}`;

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();

  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, `FS-${unique}`, projectTitle, '4');
  await page.getByRole('button', { name: '등록' }).click();

  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  await expect(page.getByRole('button', { name: /^P01 / })).toBeVisible();
  await expect(page.getByRole('button', { name: /^P04 / })).toBeVisible();
  await expect(page.getByText('KRW 1,250,000.5')).toBeVisible();

  const projectId = await findProjectId(projectTitle);
  expect(Number(await queryDatabaseValue(`select count(*) from panel_placeholders where project_id = '${projectId}' and status = 'Active';`))).toBe(4);

  await page.reload();
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: /^P04 / })).toBeVisible();

  await page.getByRole('button', { name: '목록' }).click();
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, `FS-DUP-${unique}`, ` task   003a full stack ${unique} `, '2');
  await page.getByRole('button', { name: '등록' }).click();
  await expect(page.getByText('동일한 PJT Title이 이미 존재합니다.')).toBeVisible();

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: /^P01 / })).toBeVisible();
  await expect(page.getByText('KRW 1,250,000.5')).toHaveCount(0);
  await expect(page.getByRole('button', { name: '수정' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '보류' })).toHaveCount(0);
  const manufacturingDetail = await request.get(`${apiBaseUrl}/api/projects/${projectId}`, {
    headers: { 'X-Dev-User': 'dev-manufacturing' }
  });
  expect(manufacturingDetail.ok()).toBeTruthy();
  const manufacturingJson = await manufacturingDetail.json() as Record<string, unknown>;
  expect(manufacturingJson).not.toHaveProperty('salesAmount');
  expect(manufacturingJson).not.toHaveProperty('currencyCode');

  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await openProject(page, projectTitle);
  await expect(page.getByText('KRW 1,250,000.5')).toBeVisible();
  await expect(page.getByRole('button', { name: '수정' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '보류' })).toHaveCount(0);
  const adminWrite = await request.post(`${apiBaseUrl}/api/projects/${projectId}/hold`, {
    headers: { 'X-Dev-User': 'dev-admin' },
    data: { reason: '관리자 직접 쓰기 차단' }
  });
  expect(adminWrite.status()).toBe(403);

  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await openProject(page, projectTitle);
  await page.getByRole('button', { name: '보류' }).click();
  await page.getByLabel('사유*').fill('Full-stack 보류');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: 'OnHold' })).toBeVisible();
  expect(await queryDatabaseValue(`select status from projects where id = '${projectId}';`)).toBe('OnHold');

  await page.getByRole('button', { name: '보류 해제' }).click();
  await page.getByLabel('사유*').fill('Full-stack 보류 해제');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: 'Active' })).toBeVisible();

  await page.getByRole('button', { name: '수정' }).click();
  const panelCountInput = page.getByLabel('면수*');
  await expect(panelCountInput).toHaveValue('4');
  await panelCountInput.fill('6');
  await expect(panelCountInput).toHaveValue('6');
  await page.getByLabel('수정사유*').fill('Full-stack 면수 증가');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('button', { name: /^P05 / })).toBeVisible();
  await expect(page.getByRole('button', { name: /^P06 / })).toBeVisible();
  expect(Number(await queryDatabaseValue(`select count(*) from panel_placeholders where project_id = '${projectId}' and status = 'Active';`))).toBe(6);

  const stalePanelChange = await request.post(`${apiBaseUrl}/api/projects/${projectId}/change-panel-count`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      panelCount: 7,
      expectedActivePanelCount: 4,
      cancelPanelIds: [],
      reason: '오래된 화면 기준 요청'
    }
  });
  expect(stalePanelChange.status()).toBe(409);
});

async function fillProjectForm(page: Page, projectCode: string, projectTitle: string, panelCount: string) {
  await page.getByLabel('고객사*').fill('EMI Full Stack Customer');
  await page.getByLabel('Item*').fill('Control Panel');
  await page.getByLabel('PJT Code*').fill(projectCode);
  await page.getByLabel('PJT Title*').fill(projectTitle);
  await page.getByLabel('면수*').fill(panelCount);
  await page.getByLabel('납기일*').fill('2026-10-10');
  await page.getByLabel('영업담당자*').selectOption(salesOwnerId);
  await page.getByLabel('판매금액').fill('1250000.5');
}

async function openProject(page: Page, projectTitle: string) {
  await page.getByRole('heading', { name: '프로젝트 목록' }).waitFor();
  await page.getByRole('button', { name: projectTitle }).click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
}

async function findProjectId(projectTitle: string) {
  const normalizedTitle = projectTitle.trim().replace(/\s+/g, ' ').toUpperCase();
  return queryDatabaseValue(`select id::text from projects where project_title_normalized = '${normalizedTitle.replaceAll("'", "''")}';`);
}

function queryDatabaseValue(sql: string) {
  const databaseName = process.env.E2E_DATABASE_NAME ?? 'emi_qms_e2e';
  const databaseHost = process.env.DATABASE_HOST ?? 'localhost';
  const databasePort = process.env.DATABASE_PORT ?? '5432';
  const databaseUser = process.env.DATABASE_USER ?? 'emi_qms';
  const databasePassword = requireEnv('DATABASE_PASSWORD');

  if (commandExists('psql')) {
    return execFileSync(
      'psql',
      [
        '--host',
        databaseHost,
        '--port',
        databasePort,
        '--username',
        databaseUser,
        '--dbname',
        databaseName,
        '--no-psqlrc',
        '--tuples-only',
        '--no-align',
        '--set',
        'ON_ERROR_STOP=1',
        '--command',
        sql
      ],
      {
        encoding: 'utf8',
        env: { ...process.env, PGPASSWORD: databasePassword }
      }
    ).trim();
  }

  return execFileSync(
    'docker',
    [
      'exec',
      '-i',
      'emi-qms-postgres',
      'psql',
      '--username',
      databaseUser,
      '--dbname',
      databaseName,
      '--no-psqlrc',
      '--tuples-only',
      '--no-align',
      '--set',
      'ON_ERROR_STOP=1',
      '--command',
      sql
    ],
    { encoding: 'utf8' }
  ).trim();
}

function commandExists(command: string) {
  try {
    execFileSync('bash', ['-lc', `command -v ${command}`], { stdio: 'ignore' });
    return true;
  } catch {
    return false;
  }
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is required for full-stack E2E.`);
  }

  return value;
}
