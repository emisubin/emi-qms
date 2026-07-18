import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/export-002-screenshots');
const workbookPath = path.join(screenshotDirectory, 'selected-projects.xlsx');

test('TASK-EXPORT-002: selected projects alone are exported on desktop and mobile', async ({ page, request }) => {
  const auditCountBefore = Number(await queryDatabaseValue("select count(*)::text from data_export_events where export_kind = 'ProjectsSelected';"));
  const unique = Date.now();
  const first = { code: `SEL-A-${unique}`, title: `합성 선택 프로젝트 A ${unique}` };
  const second = { code: `SEL-B-${unique}`, title: `합성 선택 프로젝트 B ${unique}` };
  const excluded = { code: `SEL-C-${unique}`, title: `합성 제외 프로젝트 C ${unique}` };
  await createProject(request, first.code, first.title);
  await createProject(request, second.code, second.title);
  await createProject(request, excluded.code, excluded.title);
  await fs.mkdir(screenshotDirectory, { recursive: true });

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(String(unique));
  await page.getByRole('button', { name: '검색', exact: true }).click();
  await selectProject(page, first);
  await selectProject(page, second);
  await expect(page.getByText('2개 선택')).toBeVisible();
  const selectionTray = page.getByRole('region', { name: '선택 프로젝트 내보내기' });
  await expect(page.getByRole('checkbox', { name: '현재 목록 전체 선택' })).toHaveCount(1);
  expect(await selectionTray.getByRole('checkbox', { name: '현재 목록 전체 선택' })
    .evaluate((element: HTMLInputElement) => element.indeterminate)).toBe(true);
  await capture(page, '01-selected-projects-desktop-1440.png');

  const [download] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: '선택 Excel 내보내기' }).click()
  ]);
  await download.saveAs(workbookPath);
  await expect(page.getByText('Excel 파일 생성을 완료했습니다')).toBeVisible();
  await expect(page.getByText('2개 선택')).toBeVisible();
  verifyWorkbook(workbookPath, first.title, second.title, excluded.title);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/projects');
  const filterTrigger = page.getByRole('button', { name: /검색·필터/ });
  await filterTrigger.click();
  const filterSheet = page.getByRole('dialog', { name: '프로젝트 검색·필터' });
  await filterSheet.getByPlaceholder('고객사, Item, Code, Title').fill(String(unique));
  await filterSheet.getByRole('button', { name: '조건 적용' }).click();
  await selectProject(page, first);
  await selectProject(page, second);
  await expect(page.getByLabel('선택 프로젝트 내보내기')).toContainText('2개 선택');
  await assertNoHorizontalOverflow(page);
  await capture(page, '02-selected-projects-mobile-390.png');

  expect(Number(await queryDatabaseValue("select count(*)::text from data_export_events where export_kind = 'ProjectsSelected';"))).toBe(auditCountBefore + 1);
  expect(await queryDatabaseValue("select row_count::text from data_export_events where export_kind = 'ProjectsSelected' order by succeeded_at_utc desc limit 1;")).toBe('2');
});

async function selectProject(page: Page, project: { code: string; title: string }) {
  const checkbox = page.getByRole('checkbox', { name: `${project.code} ${project.title} 선택` });
  await expect(checkbox).toBeVisible();
  await checkbox.check();
}

function verifyWorkbook(filePath: string, firstTitle: string, secondTitle: string, excludedTitle: string) {
  const worksheetXml = execFileSync('unzip', ['-p', filePath, 'xl/worksheets/sheet1.xml'], { encoding: 'utf8' });
  const sharedStringsXml = execFileSync('unzip', ['-p', filePath, 'xl/sharedStrings.xml'], { encoding: 'utf8' });
  expect(worksheetXml).not.toMatch(/<f(?:\s|>)/);
  expect(sharedStringsXml).toContain(firstTitle);
  expect(sharedStringsXml).toContain(secondTitle);
  expect(sharedStringsXml).not.toContain(excludedTitle);
  expect(sharedStringsXml).toContain('선택 프로젝트 2건');
}

async function capture(page: Page, fileName: string) {
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
      customerName: 'Synthetic Selected Export Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount: 2,
      deliveryDate: '2026-12-31',
      salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
      packagingMethod: 'WoodenCrate',
      salesAmount: 325000,
      currencyCode: 'KRW',
      deliveryLocation: 'Synthetic Site',
      fatRequired: false
    }
  });
  expect(response.ok()).toBeTruthy();
}

function queryDatabaseValue(sql: string) {
  return execFileSync(
    'docker',
    [
      'compose',
      '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'),
      '--file', requireEnv('E2E_COMPOSE_FILE'),
      'exec',
      '-T', requireEnv('E2E_POSTGRES_SERVICE'),
      'psql',
      '--username', requireEnv('DATABASE_USER'),
      '--dbname', requireEnv('E2E_DATABASE_NAME'),
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
