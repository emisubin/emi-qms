import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = process.env.TASK010A_SCREENSHOT_DIR?.trim()
  ? path.resolve(process.env.TASK010A_SCREENSHOT_DIR)
  : path.resolve(process.cwd(), '../tasks/010a-screenshots');
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';

test('TASK-010A: material user shares optional kitting completion without creating manufacturing work', async ({ page, request }) => {
  test.setTimeout(150_000);
  const unique = Date.now();
  const readyProject = await createKittingProject(request, `KIT-A-${unique}`, `합성 키팅 준비 ${unique}`, 4, true);
  await createKittingProject(request, `KIT-W-${unique}`, `합성 키팅 대기 ${unique}`, 2, false);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/materials/kitting');
  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await page.goto(`/materials/kitting?project=${readyProject.projectId}`);
  await expect(page.getByRole('heading', { name: '패널 키팅' })).toBeVisible();
  await expect(page.getByText('전체 입고 완료 · 실제 키팅을 마친 패널만 알려 주세요.')).toBeVisible();
  await expect(page.locator('.kitting-panel-card')).toHaveCount(4);
  await capture(page, '01-panel-kitting-desktop-1440.jpg');

  await page.locator('.kitting-panel-card').nth(0).click();
  await page.locator('.kitting-panel-card').nth(1).click();
  await expect(page.getByRole('button', { name: '2면 키팅 완료 알림' })).toBeEnabled();
  await page.getByRole('button', { name: '2면 키팅 완료 알림' }).click();
  await expect(page.getByText('2면의 키팅 완료 상태를 공유했습니다.')).toBeVisible();
  expect(queryDatabase(`select count(*)::text from panel_kitting_completions where project_id = '${readyProject.projectId}';`)).toBe('2');
  expect(queryDatabase(`select count(*)::text from work_items where project_id = '${readyProject.projectId}' and workflow_stage_code = 'ManufacturingWork';`)).toBe('0');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/materials/kitting?project=${readyProject.projectId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('button', { name: '메뉴 열기' })).toBeVisible();
  await expect(page.locator('.kitting-project-list')).toHaveCSS('overflow-x', 'auto');
  await expect(page.locator('.kitting-panel-grid')).toBeVisible();
  expect((await page.locator('.kitting-panel-card').evaluateAll((cards) =>
    Array.from(new Set(cards.map((card) => card.getAttribute('data-shape-role')))).sort()
  ))).toEqual(['status', 'success']);
  await assertNoHorizontalOverflow(page);
  await capture(page, '02-panel-kitting-mobile-390.jpg');

  await page.locator('.kitting-panel-card:not([data-completed="true"])').first().click();
  await expect(page.getByRole('button', { name: '1면 키팅 완료 알림' })).toBeEnabled();
  await capture(page, '03-panel-kitting-selected-mobile-390.jpg');

  await page.getByRole('button', { name: '메뉴 열기' }).click();
  const menu = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  await expect(menu).toBeVisible();
  await expect(menu.getByRole('button', { name: '자재' })).toHaveAttribute('aria-current', 'page');
  await expect(menu.getByRole('button', { name: '키팅' })).toHaveCount(0);
  await capture(page, '04-panel-kitting-menu-mobile-390.jpg');
});

async function createKittingProject(
  request: APIRequestContext,
  projectCode: string,
  projectTitle: string,
  panelCount: number,
  ready: boolean
) {
  const created = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Kitting Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount,
      deliveryDate: '2026-10-10',
      salesOwnerUserId,
      packagingMethod: 'StretchWrap',
      salesAmount: null,
      currencyCode: null,
      deliveryLocation: 'Synthetic Site',
      fatRequired: false
    }
  });
  expect(created.ok()).toBeTruthy();
  const projectId = (await created.json() as { projectId: string }).projectId;

  const procurement = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: {
      reason: 'synthetic kitting evidence',
      items: [{
        orderItem: 'Synthetic Kitting Material',
        supplierName: 'Synthetic Vendor'
      }]
    }
  });
  expect(procurement.ok()).toBeTruthy();

  queryDatabase(`
    update panel_placeholders
    set panel_name = 'MCC-' || lpad(sequence_number::text, 2, '0'),
        width_mm = 600 + sequence_number,
        height_mm = 1800,
        depth_mm = 400,
        panel_info_completed = true
    where project_id = '${projectId}' and status = 'Active';
  `);

  if (ready) {
    queryDatabase(`
      begin;
      select set_config('emi_qms.material_receipt_write', 'allowed', true);
      update project_procurement_items
      set receipt_completed = true,
          receipt_completed_at_utc = now(),
          receipt_completed_by_user_id = '50000000-0000-0000-0000-000000000012'
      where project_id = '${projectId}' and status = 'Active';
      commit;
      select 'ready';
    `);
  }

  return { projectId, projectTitle };
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}

function queryDatabase(sql: string) {
  return execFileSync(
    'docker',
    [
      'compose',
      '--project-name',
      requireEnv('E2E_COMPOSE_PROJECT_NAME'),
      '--file',
      requireEnv('E2E_COMPOSE_FILE'),
      'exec',
      '-T',
      requireEnv('E2E_POSTGRES_SERVICE'),
      'psql',
      '--username',
      requireEnv('E2E_DATABASE_USER'),
      '--dbname',
      requireEnv('E2E_DATABASE_NAME'),
      '--no-psqlrc',
      '--tuples-only',
      '--no-align',
      '--set',
      'ON_ERROR_STOP=1',
      '--command',
      sql
    ],
    { encoding: 'utf8' }
  ).trim().split('\n').at(-1)?.trim() ?? '';
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is required for isolated full-stack validation.`);
  }
  return value;
}

async function assertNoHorizontalOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
}

async function capture(page: Page, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled' });
}
