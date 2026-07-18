import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/export-001-screenshots');

test('TASK-EXPORT-001: three screens export formula-safe workbooks on desktop and mobile', async ({ page, request }) => {
  const auditCountBefore = Number(await queryDatabaseValue("select count(*)::text from data_export_events where export_kind in ('ProjectsSelected','ProcurementDashboardSelected','MyWorkSelected');"));
  const unique = Date.now();
  const projectTitle = `Excel 내보내기 검수 ${unique}`;
  const projectId = await createProject(request, `EXPORT-${unique}`, projectTitle);
  await createPending(request, projectId, `내 업무 선택 내보내기 ${unique}`);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색', exact: true }).click();
  await expect(page.getByText(projectTitle, { exact: true })).toBeVisible();
  await exportAndVerify(page);
  await capture(page, '01-projects-desktop-1440.png');

  await page.goto('/procurement');
  await expect(page.getByRole('heading', { name: '구매' })).toBeVisible();
  await page.getByPlaceholder('프로젝트명, 고객사, Code, 발주품목 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색', exact: true }).click();
  await exportAndVerify(page);
  await capture(page, '02-procurement-desktop-1440.png');

  await page.goto('/my-work');
  await selectDevelopmentUser(page, 'dev-production');
  await expect(page.getByRole('heading', { name: '내 업무' })).toBeVisible();
  await exportAndVerify(page);
  await capture(page, '03-my-work-desktop-1440.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await selectDevelopmentUser(page, 'dev-sales');
  await page.goto('/projects');
  const filterTrigger = page.getByRole('button', { name: /검색·필터/ });
  await filterTrigger.click();
  const filterSheet = page.getByRole('dialog', { name: '프로젝트 검색·필터' });
  await filterSheet.getByPlaceholder('고객사, Item, Code, Title').fill(projectTitle);
  await filterSheet.getByRole('button', { name: '조건 적용' }).click();
  await expect(page.getByText(projectTitle, { exact: true })).toBeVisible();
  await exportAndVerify(page);
  await assertNoHorizontalOverflow(page);
  await capture(page, '04-projects-mobile-390.png');

  await page.goto('/procurement');
  await expect(page.getByRole('heading', { name: '구매' })).toBeVisible();
  await exportAndVerify(page);
  await assertNoHorizontalOverflow(page);
  await capture(page, '05-procurement-mobile-390.png');

  await page.goto('/my-work');
  await selectDevelopmentUser(page, 'dev-production');
  await expect(page.getByRole('heading', { name: '오늘 처리할 업무' })).toBeVisible();
  await exportAndVerify(page);
  await assertNoHorizontalOverflow(page);
  await capture(page, '06-my-work-mobile-390.png');

  expect(Number(await queryDatabaseValue("select count(*)::text from data_export_events where export_kind in ('ProjectsSelected','ProcurementDashboardSelected','MyWorkSelected');"))).toBe(auditCountBefore + 6);
  expect(await queryDatabaseValue("select count(*)::text from data_export_events where row_count < 0 or row_count > 10000;")).toBe('0');
});

async function exportAndVerify(page: Page) {
  const tray = page.locator('.selected-export-tray').first();
  await expect(tray).toBeVisible();
  await expect(page.getByRole('button', { name: 'Excel 내보내기', exact: true })).toHaveCount(0);
  const selectAll = tray.getByRole('checkbox', { name: '현재 목록 전체 선택', exact: true });
  await expect(selectAll).toBeEnabled();
  await selectAll.check();
  const action = tray.getByRole('button', { name: '선택 Excel 내보내기', exact: true });
  await expect(action).toBeVisible();
  const [download] = await Promise.all([
    page.waitForEvent('download'),
    action.click()
  ]);
  await expect(page.getByText(/Excel 파일 생성을 완료했습니다|0건 파일을 생성했습니다/)).toBeVisible();
  const downloadPath = await download.path();
  expect(downloadPath).not.toBeNull();
  const sheetXml = execFileSync('unzip', ['-p', downloadPath!, 'xl/worksheets/sheet1.xml'], { encoding: 'utf8' });
  expect(sheetXml).not.toMatch(/<f(?:\s|>)/);
}

async function selectDevelopmentUser(page: Page, userKey: string) {
  await page.evaluate((nextUserKey) => {
    window.localStorage.setItem('emi-qms-development-user-key', nextUserKey);
  }, userKey);
  await page.reload();
}

async function capture(page: Page, fileName: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, fileName), animations: 'disabled', fullPage: true });
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      customerName: 'Synthetic Export Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount: 3,
      deliveryDate: '2026-12-31',
      salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
      packagingMethod: 'WoodenCrate',
      salesAmount: 125000,
      currencyCode: 'KRW',
      deliveryLocation: 'Synthetic Site',
      fatRequired: false
    }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}

async function createPending(request: APIRequestContext, projectId: string, title: string) {
  const response = await request.post(`${apiBaseUrl}/api/pending`, {
    headers: { 'X-Dev-User': 'dev-quality' },
    data: {
      projectId,
      issueType: 'ManufacturingStop',
      title,
      description: '선택 내보내기 검증을 위한 격리된 합성 내 업무 항목입니다.',
      priority: 'Urgent',
      actionDepartmentCode: 'production-planning',
      assigneeUserId: '50000000-0000-0000-0000-000000000003'
    }
  });
  expect(response.ok()).toBeTruthy();
}

function queryDatabaseValue(sql: string) {
  const databaseName = requireEnv('E2E_DATABASE_NAME');
  const databaseUser = requireEnv('DATABASE_USER');
  return execFileSync(
    'docker',
    [
      'compose',
      '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'),
      '--file', requireEnv('E2E_COMPOSE_FILE'),
      'exec',
      '-T', requireEnv('E2E_POSTGRES_SERVICE'),
      'psql',
      '--username', databaseUser,
      '--dbname', databaseName,
      '--no-psqlrc',
      '--tuples-only',
      '--no-align',
      '--set', 'ON_ERROR_STOP=1',
      '--command', sql
    ],
    { encoding: 'utf8' }
  ).trim();
}

function requireEnv(name: string) {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`${name} is required.`);
  }
  return value;
}
