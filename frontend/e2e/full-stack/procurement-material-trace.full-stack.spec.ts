import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = '/tmp/procurement-material-trace-001-screenshots';

test('WORKFLOW-CONTINUITY-001 Change 002: procurement quantity, split arrivals, and IQC stay on one item trace', async ({ page, request }) => {
  test.setTimeout(120_000);
  const unique = Date.now();
  const projectTitle = `구매 자재 추적 ${unique}`;
  const projectId = await createProject(request, `TRACE-${String(unique).slice(-8)}`, projectTitle);
  const procurement = await saveProcurement(request, projectId);
  const purchased = procurement.items.find((item) => item.supplyType === 'Purchased')!;

  expect(purchased.orderQuantity).toBe(10);
  expect(purchased.orderUnit).toBe('EA');
  const firstArrival = await registerArrival(request, purchased.itemId, 4, '2026-07-15');
  const secondArrival = await registerArrival(request, purchased.itemId, 6, '2026-07-18');
  expect(firstArrival.receiptId).not.toBe(secondArrival.receiptId);
  expect(firstArrival.iqcAttemptId).not.toBe(secondArrival.iqcAttemptId);

  const material = await getMaterial(request, purchased.itemId);
  expect(material.orderQuantity).toBe(10);
  expect(material.arrivedQuantity).toBe(10);
  expect(material.receipts).toHaveLength(2);
  expect(material.receipts.flatMap((receipt) => receipt.iqcAttempts)).toHaveLength(2);
  const work = await getMyWork(request, 'dev-quality');
  expect(work.items.filter((item) => item.projectId === projectId && item.workflowStageCode === 'IQC')).toHaveLength(2);
  const notifications = await getNotifications(request, 'dev-quality');
  expect(notifications.items.filter((item) => item.projectId === projectId && item.linkUrl?.startsWith('/quality/iqc?request='))).toHaveLength(2);

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await page.goto(`/projects/${projectId}?section=procurement`);
  await expect(page.getByRole('tab', { name: /도급 구매품/ })).toHaveAttribute('aria-selected', 'true');
  const purchasedTable = page.getByRole('table', { name: '구매정보' });
  await expect(purchasedTable).toContainText('도급 제어반 부품');
  await expect(purchasedTable).toContainText('10 EA');
  await capture(page, '01-procurement-purchased-desktop.png');

  await page.getByRole('tab', { name: /사급 자재/ }).click();
  await expect(page.getByRole('table', { name: '구매정보' })).toContainText('사급 동부스바');
  await expect(page.getByRole('table', { name: '구매정보' })).toContainText('12 EA');
  await capture(page, '02-procurement-customer-supplied-desktop.png');

  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await page.goto(`/projects/${projectId}?section=materials`);
  const materialRow = page.locator('.project-material-item-row').filter({ hasText: '도급 제어반 부품' });
  await expect(materialRow).toHaveCount(1);
  await materialRow.locator('summary').click();
  await expect(materialRow).toContainText('입고 1회차 · 2026-07-15 · 4 EA');
  await expect(materialRow).toContainText('입고 2회차 · 2026-07-18 · 6 EA');
  await expect(materialRow.locator('[data-iqc="true"]')).toHaveCount(2);
  await capture(page, '03-material-item-inline-history-desktop.png');

  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto(`/projects/${projectId}?section=quality`);
  await expect(page.getByRole('tab', { name: '수입검사(IQC)' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByRole('button', { name: '품질 업무 수정' })).toBeVisible();
  const qualityData = page.getByLabel('품질 입력 데이터');
  await expect(qualityData.locator('.project-department-record')).toHaveCount(2);
  await expect(qualityData).toContainText('입고 1회차');
  await expect(qualityData).toContainText('입고 2회차');
  await capture(page, '04-quality-iqc-project-tab-desktop.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}?section=materials`);
  const mobileRow = page.locator('.project-material-item-row').filter({ hasText: '도급 제어반 부품' });
  await mobileRow.locator('summary').click();
  await expect(mobileRow.locator('[data-iqc="true"]')).toHaveCount(2);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  await capture(page, '05-material-item-inline-history-mobile-390.png');

  await page.goto(`/projects/${projectId}?section=quality`);
  await expect(page.getByRole('tab', { name: '수입검사(IQC)' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  await capture(page, '06-quality-iqc-project-tab-mobile-390.png');
});

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Customer', item: 'RPP', projectCode, projectTitle, panelCount: 2,
      deliveryDate: '2026-12-31', salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
      packagingMethod: 'WoodenCrate', salesAmount: 32000000, currencyCode: 'KRW',
      deliveryLocation: 'Synthetic Site', fatRequired: false
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}

async function saveProcurement(request: APIRequestContext, projectId: string) {
  const response = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: {
      reason: '공급 구분과 발주 수량 연결 검수',
      items: [
        { orderItem: '도급 제어반 부품', supplierName: 'Synthetic Vendor', supplyType: 'Purchased', orderQuantity: 10, orderUnit: 'EA', orderDate: '2026-07-01', expectedReceiptDate: '2026-07-20' },
        { orderItem: '사급 동부스바', supplierName: 'Customer', supplyType: 'CustomerSupplied', orderQuantity: 12, orderUnit: 'EA', expectedReceiptDate: '2026-07-22' }
      ]
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { items: Array<{ itemId: string; supplyType: 'Purchased' | 'CustomerSupplied'; orderQuantity: number; orderUnit: string }> };
}

async function registerArrival(request: APIRequestContext, itemId: string, quantity: number, arrivalDate: string) {
  const response = await request.post(`${apiBaseUrl}/api/materials/items/${itemId}/receipts`, {
    headers: devHeaders('dev-materials'), data: { quantity, unit: 'EA', arrivalDate, note: `${arrivalDate} 분할 도착` }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { receiptId: string; iqcAttemptId: string };
}

async function getMaterial(request: APIRequestContext, itemId: string) {
  const response = await request.get(`${apiBaseUrl}/api/materials/receipts?includeCompleted=true`, { headers: devHeaders('dev-materials') });
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { items: Array<{ itemId: string; orderQuantity: number; arrivedQuantity: number; receipts: Array<{ iqcAttempts: unknown[] }> }> };
  return body.items.find((item) => item.itemId === itemId)!;
}

async function getMyWork(request: APIRequestContext, user: string) {
  const response = await request.get(`${apiBaseUrl}/api/my-work`, { headers: devHeaders(user) });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { items: Array<{ projectId: string; workflowStageCode: string }> };
}

async function getNotifications(request: APIRequestContext, user: string) {
  const response = await request.get(`${apiBaseUrl}/api/notifications`, { headers: devHeaders(user) });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { items: Array<{ projectId: string; linkUrl: string | null }> };
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
