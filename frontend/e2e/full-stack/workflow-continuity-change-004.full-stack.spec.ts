import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = '/tmp/workflow-continuity-change-004-screenshots';

test('WORKFLOW-CONTINUITY-001 Change 004: purchase-owned quantity, Materials fallback, arrival, and IQC recovery work end to end', async ({ page, request }) => {
  test.setTimeout(240_000);
  const unique = Date.now();
  const projectCode = `OWNER-${String(unique).slice(-8)}`;
  const projectTitle = `발주 수량 IQC 복구 ${unique}`;
  const orderItem = `도급 차단기 ${String(unique).slice(-5)}`;
  const projectId = await createProject(request, projectCode, projectTitle);

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await page.goto(`/projects/${projectId}/procurement/edit`);
  await page.getByRole('button', { name: '도급 구매품 행 추가' }).click();
  const row = page.getByRole('table', { name: '구매정보 수정' }).locator('.procurement-table-row.editable').last();
  const inputs = row.locator('input');
  await inputs.nth(1).fill(orderItem);
  await inputs.nth(2).fill('합성 공급사');
  await inputs.nth(4).fill('2026-07-21');
  await inputs.nth(5).fill('2026-08-05');
  await row.getByLabel('발주 수량').fill('8');
  await row.getByLabel('발주 단위').fill('EA');
  await expect(page.getByRole('note')).toContainText('발주 수량과 단위는 구매팀이 이 화면에서 입력합니다.');
  await capture(page, '01-procurement-owned-quantity-desktop.png');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();

  const itemId = await findItemId(request, projectId, orderItem);
  expect(await countMyWork(request, 'dev-materials', projectId, 'MaterialArrived')).toBe(1);
  expect(await countNotifications(request, 'dev-materials', projectId, '구매품 신규 확인')).toBe(1);

  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await page.goto('/my-work');
  await expect(page.getByRole('region', { name: `${projectTitle} 내 업무` })).toContainText('구매품 신규 확인');
  await capture(page, '02-materials-my-work-after-purchase-desktop.png');
  await page.goto('/notifications');
  await expect(page.getByText('구매품 신규 확인', { exact: false }).first()).toBeVisible();
  await capture(page, '03-materials-notification-after-purchase-desktop.png');

  await page.goto('/materials');
  const materialsHub = page.getByTestId('department-project-hub-materials');
  await materialsHub.getByRole('radio', { name: /입고 관리/u }).click();
  await materialsHub.getByRole('button', { name: new RegExp(projectTitle, 'u') }).click();
  const materialCard = page.locator('.material-item-card').filter({ hasText: orderItem });
  await materialCard.getByRole('button', { name: '도착분 추가' }).click();
  const arrivalForm = page.locator('.material-action-form').filter({ hasText: orderItem });
  await expect(arrivalForm.getByLabel('발주 수량')).toHaveCount(0);
  await expect(arrivalForm.getByLabel('도착 수량')).toBeVisible();
  await capture(page, '04-material-arrival-only-desktop.png');
  await arrivalForm.getByLabel('도착 수량').fill('3');
  await arrivalForm.getByLabel('도착일').fill('2026-07-21');
  await arrivalForm.getByRole('button', { name: '도착 등록' }).click();
  await expect(page.getByText('도착분 저장과 IQC 검사 업무 생성을 확인했습니다.')).toBeVisible();

  createOrphanArrival(randomUUID(), itemId);
  await page.getByLabel('개발 사용자').selectOption('dev-quality', { force: true });
  await page.goto('/quality');
  await page.getByTestId('department-project-hub-quality').getByRole('button', { name: new RegExp(projectTitle, 'u') }).click();
  await expect(page.getByRole('heading', { name: 'IQC 검사함' })).toBeVisible();
  await expect(page.locator('.iqc-request-card').filter({ hasText: orderItem })).toHaveCount(2);
  expect(await countMyWork(request, 'dev-quality', projectId, 'IQC')).toBe(2);
  expect(await countNotifications(request, 'dev-quality', projectId, 'IQC 판정')).toBe(2);
  await capture(page, '05-quality-iqc-current-and-recovered-desktop.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/quality');
  await page.getByTestId('department-project-hub-quality').getByRole('button', { name: new RegExp(projectTitle, 'u') }).click();
  await expect(page.locator('.iqc-request-card').filter({ hasText: orderItem })).toHaveCount(2);
  expect(await page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - window.innerWidth))).toBe(0);
  await capture(page, '06-quality-iqc-current-and-recovered-mobile-390.png');
});

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Customer', item: 'RPP', projectCode, projectTitle, panelCount: 1,
      deliveryDate: '2026-12-31', salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
      packagingMethod: 'WoodenCrate', salesAmount: 1000000, currencyCode: 'KRW',
      deliveryLocation: 'Synthetic Site', fatRequired: false
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}

async function findItemId(request: APIRequestContext, projectId: string, orderItem: string) {
  const response = await request.get(`${apiBaseUrl}/api/projects/${projectId}/procurement`, { headers: devHeaders('dev-procurement') });
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { items: Array<{ itemId: string; orderItem: string | null }> };
  return body.items.find((item) => item.orderItem === orderItem)!.itemId;
}

async function countMyWork(request: APIRequestContext, user: string, projectId: string, stage: string) {
  const response = await request.get(`${apiBaseUrl}/api/my-work`, { headers: devHeaders(user) });
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { items: Array<{ projectId: string; workflowStageCode: string }> };
  return body.items.filter((item) => item.projectId === projectId && item.workflowStageCode === stage).length;
}

async function countNotifications(request: APIRequestContext, user: string, projectId: string, title: string) {
  const response = await request.get(`${apiBaseUrl}/api/notifications`, { headers: devHeaders(user) });
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { items: Array<{ projectId: string; title: string }> };
  return body.items.filter((item) => item.projectId === projectId && item.title.includes(title)).length;
}

function createOrphanArrival(receiptId: string, itemId: string) {
  queryDatabase(`
    insert into material_receipts (
      id, procurement_item_id, quantity, unit, arrival_date, note, status,
      created_by_user_id, updated_by_user_id)
    values (
      '${receiptId}', '${itemId}', 1, 'EA', '2026-07-21', '합성 이전 누락 도착분', 'Arrived',
      '50000000-0000-0000-0000-000000000012', '50000000-0000-0000-0000-000000000012');
  `);
}

function queryDatabase(sql: string) {
  return execFileSync('docker', [
    'compose', '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'), '--file', requireEnv('E2E_COMPOSE_FILE'),
    'exec', '-T', requireEnv('E2E_POSTGRES_SERVICE'), 'psql', '--username', requireEnv('E2E_DATABASE_USER'),
    '--dbname', requireEnv('E2E_DATABASE_NAME'), '--no-psqlrc', '--tuples-only', '--no-align',
    '--set', 'ON_ERROR_STOP=1', '--command', sql
  ], { encoding: 'utf8' }).trim();
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for isolated full-stack validation.`);
  return value;
}

async function capture(page: Page, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled', fullPage: true });
}

function devHeaders(user: string) {
  return { 'X-Dev-User': user };
}
