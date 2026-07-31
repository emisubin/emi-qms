import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { markProjectAsLegacyIqc } from './legacy-iqc-fixture';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = '/tmp/workflow-continuity-change-003-screenshots';
const salesUserId = '50000000-0000-0000-0000-000000000002';
const qualityUserId = '50000000-0000-0000-0000-000000000005';
const qualitySecondaryUserId = '50000000-0000-0000-0000-000000000010';
const procurementUserId = '50000000-0000-0000-0000-000000000011';
const materialsUserId = '50000000-0000-0000-0000-000000000012';

test('WORKFLOW-CONTINUITY-001 Change 003: exact purchase error, assignee handoff, project hubs, and IQC repair stay connected', async ({ page, request }) => {
  test.setTimeout(240_000);
  const unique = Date.now();
  const projectCode = `HANDOFF-${String(unique).slice(-8)}`;
  const projectTitle = `구매 자재 IQC 연결 ${unique}`;
  const orderItem = `도급 합성 품목 ${String(unique).slice(-5)}`;
  const consoleErrors: string[] = [];
  page.on('console', (message) => { if (message.type() === 'error') consoleErrors.push(message.text()); });

  const projectId = await createProject(request, projectCode, projectTitle);
  markProjectAsLegacyIqc(projectId);
  assignProjectOwners(projectId);

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await page.goto(`/projects/${projectId}/procurement/edit`);
  await expect(page.getByRole('heading', { name: '구매정보 입력' })).toBeVisible();
  await page.getByRole('button', { name: '도급 구매품 행 추가' }).click();
  const purchaseRow = page.getByRole('table', { name: '구매정보 수정' }).locator('.procurement-table-row.editable').last();
  const rowInputs = purchaseRow.locator('input');
  await rowInputs.nth(0).fill('3W');
  await rowInputs.nth(1).fill(orderItem);
  await rowInputs.nth(2).fill('합성 공급사');
  await rowInputs.nth(3).fill('합성 기술담당');
  await rowInputs.nth(4).fill('2026-07-21');
  await rowInputs.nth(5).fill('2026-08-05');
  await purchaseRow.getByLabel('발주 수량').fill('10');
  await page.getByRole('button', { name: '저장' }).click();

  const issuePanel = page.locator('.procurement-error-panel');
  await expect(issuePanel).toContainText('저장하지 못한 위치');
  await expect(issuePanel).toContainText('도급 구매품 · 1번째 행');
  await expect(issuePanel).toContainText(orderItem);
  await expect(issuePanel).toContainText('문제 필드');
  await expect(issuePanel).toContainText('발주 단위');
  await expect(purchaseRow.getByLabel('발주 단위')).toBeFocused();
  await capture(page, '01-purchased-input-exact-error-desktop.png');

  await purchaseRow.getByLabel('발주 단위').fill('EA');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  const procurement = await getProcurement(request, projectId);
  const item = procurement.items.find((candidate) => candidate.orderItem === orderItem);
  expect(item).toBeDefined();
  await expectHandoffCount(request, 'dev-materials', projectId, 'MaterialArrived', 1);
  await expectHandoffCount(request, 'dev-procurement', projectId, 'MaterialArrived', 1);
  await expectNotificationCount(request, 'dev-materials', projectId, '구매품 신규 확인', 1);
  await expectNotificationCount(request, 'dev-procurement', projectId, '구매품 신규 확인', 1);

  await page.goto(`/projects/${projectId}/procurement/edit`);
  const changedRow = page.getByRole('table', { name: '구매정보 수정' }).locator('.procurement-table-row.editable').last();
  await changedRow.locator('input').nth(0).fill('4W');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  await expectHandoffCount(request, 'dev-materials', projectId, 'MaterialArrived', 2);
  await expectHandoffCount(request, 'dev-procurement', projectId, 'MaterialArrived', 2);
  await expectNotificationCount(request, 'dev-materials', projectId, '구매품 변경 확인', 1);
  await expectNotificationCount(request, 'dev-procurement', projectId, '구매품 변경 확인', 1);

  const hubRoutes = [
    { route: '/materials', testId: 'department-work-hub-materials', file: '02-materials-project-hub-desktop.png' },
    { route: '/quality', testId: 'department-work-hub-quality', file: '04-quality-project-hub-desktop.png' },
    { route: '/logistics', testId: 'department-work-hub-logistics', file: '05-logistics-project-hub-desktop.png' }
  ];
  for (const hub of hubRoutes) {
    await page.goto(hub.route);
    await expect(page.getByTestId(hub.testId)).toBeVisible();
    await capture(page, hub.file);
  }
  await page.goto('/manufacturing');
  await expect(page.getByTestId('manufacturing-dashboard')).toBeVisible();
  await capture(page, '03-manufacturing-project-hub-desktop.png');
  await page.goto('/pending');
  await expect(page.getByTestId('pending-dashboard').getByRole('button', { name: new RegExp(projectTitle, 'u') })).toBeVisible();
  await capture(page, '06-pending-project-hub-desktop.png');

  await page.getByLabel('개발 사용자').selectOption('dev-materials', { force: true });
  await page.goto('/materials/receipts');
  await page.getByPlaceholder('PJT 코드, 발주품목, 업체').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  await page.getByRole('button', { name: new RegExp(projectTitle, 'u') }).click();
  await expect(page.getByRole('heading', { name: '자재 입고 관리' })).toBeVisible();
  const materialCard = page.locator('.material-purchase-row').filter({ hasText: orderItem });
  await materialCard.getByRole('button', { name: '도착입력' }).click();
  const arrivalForm = page.locator('.material-action-form').filter({ hasText: orderItem });
  await arrivalForm.getByLabel('도착 수량').fill('4');
  await arrivalForm.getByLabel('도착일').fill('2026-07-21');
  await arrivalForm.getByLabel('비고').fill('합성 도착분 자동 IQC 연결');
  await arrivalForm.getByRole('button', { name: '저장' }).click();
  await expect(page.getByText('도착분 저장과 IQC 검사 업무 생성을 확인했습니다.')).toBeVisible();

  const orphanReceiptId = randomUUID();
  createOrphanArrival(orphanReceiptId, item!.itemId);
  await page.getByLabel('개발 사용자').selectOption('dev-quality', { force: true });
  await page.goto('/quality/iqc');
  const qualityHub = page.getByTestId('quality-iqc-dashboard');
  await qualityHub.getByRole('button', { name: new RegExp(projectTitle, 'u') }).click();
  await expect(page.getByRole('heading', { name: 'IQC 검사함' })).toBeVisible();
  const iqcCards = page.locator('.iqc-request-card').filter({ hasText: orderItem });
  await expect(iqcCards).toHaveCount(2);
  await expectHandoffCount(request, 'dev-quality', projectId, 'IQC', 2);
  await expectHandoffCount(request, 'dev-design', projectId, 'IQC', 2);
  await expectNotificationCount(request, 'dev-quality', projectId, 'IQC 판정', 2);
  await expectNotificationCount(request, 'dev-design', projectId, 'IQC 판정', 2);
  await capture(page, '07-quality-iqc-recovered-desktop.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByLabel('개발 사용자').selectOption('dev-materials', { force: true });
  await page.goto('/materials');
  await expect(page.getByTestId('department-work-hub-materials')).toBeVisible();
  expect(await page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - window.innerWidth))).toBe(0);
  await capture(page, '08-materials-project-hub-mobile-390.png');

  await page.getByLabel('개발 사용자').selectOption('dev-quality', { force: true });
  await page.goto('/quality/iqc');
  await page.getByTestId('quality-iqc-dashboard').getByRole('button', { name: new RegExp(projectTitle, 'u') }).click();
  await expect(page.locator('.iqc-request-card').filter({ hasText: orderItem })).toHaveCount(2);
  expect(await page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - window.innerWidth))).toBe(0);
  await capture(page, '09-quality-iqc-recovered-mobile-390.png');

  expect(consoleErrors).toEqual([]);
});

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Customer', item: 'RPP', projectCode, projectTitle, panelCount: 1,
      deliveryDate: '2026-12-31', salesOwnerUserId: salesUserId, packagingMethod: 'WoodenCrate',
      salesAmount: 10000000, currencyCode: 'KRW', deliveryLocation: 'Synthetic Site', fatRequired: false
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}

function assignProjectOwners(projectId: string) {
  queryDatabase(`
    insert into project_assignees (project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc)
    values
      ('${projectId}', 'MaterialsPrimary', '${materialsUserId}', '${salesUserId}', now()),
      ('${projectId}', 'MaterialsSecondary', '${procurementUserId}', '${salesUserId}', now()),
      ('${projectId}', 'QualityIQC', '${qualityUserId}', '${salesUserId}', now()),
      ('${projectId}', 'QualityIQCSecondary', '${qualitySecondaryUserId}', '${salesUserId}', now());
  `);
}

function createOrphanArrival(receiptId: string, itemId: string) {
  queryDatabase(`
    insert into material_receipts (
      id, procurement_item_id, quantity, unit, arrival_date, note, status,
      created_by_user_id, updated_by_user_id)
    values (
      '${receiptId}', '${itemId}', 1, 'EA', '2026-07-21', '합성 누락 복구 도착분', 'Arrived',
      '${materialsUserId}', '${materialsUserId}');
  `);
}

async function getProcurement(request: APIRequestContext, projectId: string) {
  const response = await request.get(`${apiBaseUrl}/api/projects/${projectId}/procurement`, { headers: devHeaders('dev-procurement') });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { items: Array<{ itemId: string; orderItem: string | null }> };
}

async function expectHandoffCount(request: APIRequestContext, user: string, projectId: string, stage: string, count: number) {
  const response = await request.get(`${apiBaseUrl}/api/my-work`, { headers: devHeaders(user) });
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { items: Array<{ projectId: string; workflowStageCode: string }> };
  expect(body.items.filter((item) => item.projectId === projectId && item.workflowStageCode === stage)).toHaveLength(count);
}

async function expectNotificationCount(request: APIRequestContext, user: string, projectId: string, title: string, count: number) {
  const response = await request.get(`${apiBaseUrl}/api/notifications`, { headers: devHeaders(user) });
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { items: Array<{ projectId: string; title: string }> };
  expect(body.items.filter((item) => item.projectId === projectId && item.title.includes(title))).toHaveLength(count);
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
