import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';

test('TASK-008B: customer-supplied material keeps one quantity truth across procurement, receiving, IQC, and close', async ({ page, request }) => {
  test.setTimeout(90_000);
  const unique = Date.now();
  const projectTitle = `사급 흐름 검수 ${unique}`;
  const projectId = await createProject(request, `CS-${unique}`, projectTitle);
  const procurement = await updateProcurement(request, projectId, {
    items: [{
      orderItem: 'Customer Busbar',
      supplierName: 'Reference Vendor',
      expectedReceiptDate: '2020-01-01',
      supplyType: 'CustomerSupplied',
      orderQuantity: 10,
      orderUnit: 'EA'
    }]
  });
  const item = procurement.items.find((candidate) => candidate.orderItem === 'Customer Busbar');
  expect(item).toBeTruthy();

  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await openProject(page, projectTitle);
  await page.getByRole('tab', { name: '구매' }).click();
  await page.getByRole('tab', { name: /사급 자재/ }).click();
  const purchaseTable = page.getByRole('table', { name: '구매정보' });
  await expect(purchaseTable).toContainText('사급 자재');
  await expect(purchaseTable).toContainText('10 EA');

  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await page.goto('/');
  const overdueMetric = page.getByRole('button', { name: /사급 제공 지연/ });
  await expect(overdueMetric).toContainText('1');
  await saveEvidence(page, 'home-002-change-003-materials-home.jpg');
  await overdueMetric.click();
  await expect(page).toHaveURL(/\/materials\/receipts\?risk=customer-supply-overdue$/);
  await expect(page.getByRole('button', { name: '제공 지연' })).toHaveAttribute('data-active', 'true');
  const materialCard = page.locator('.material-item-card').filter({ hasText: projectTitle });
  await expect(materialCard).toBeVisible();
  await saveEvidence(page, 'home-002-change-003-customer-supply-overdue.jpg');
  await expect(materialCard).toContainText('사급 · 제공 지연');
  await expect(materialCard).toContainText('미도착 잔량10 EA');
  await expect(materialCard).toContainText('처리 대기량0 EA');

  const arrival = await postJson<{ receiptId: string }>(request, 'dev-materials', `/api/materials/items/${item!.itemId}/receipts`, {
    quantity: 4,
    unit: 'EA',
    arrivalDate: '2026-07-15'
  });
  const iqcRequest = await postJson<{ iqcAttemptId: string }>(request, 'dev-materials', `/api/materials/receipts/${arrival.receiptId}/iqc-requests`, {
    expectedVersion: 1
  });

  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto('/quality/iqc');
  const iqcCard = page.locator('.iqc-request-card').filter({ hasText: projectTitle });
  await expect(iqcCard).toContainText('사급 · 고객 제공');
  await expect(iqcCard).toContainText('4 EA');

  await finalizeDetailedIqc(request, iqcRequest.iqcAttemptId, 2, '고객 제공품 검사 합격');
  await postJson(request, 'dev-materials', `/api/materials/receipts/${arrival.receiptId}/confirm`, {
    expectedVersion: 3
  });

  const partial = await materialItem(request, item!.itemId);
  expect(partial.remainingQuantity).toBe(6);
  expect(partial.confirmedQuantity).toBe(4);
  const earlyClose = await request.post(`${apiBaseUrl}/api/materials/items/${item!.itemId}/close-arrivals`, {
    headers: devHeaders('dev-materials'),
    data: { expectedRowVersion: partial.rowVersion, reason: '부분 제공 상태 마감 시도' }
  });
  expect(earlyClose.status()).toBe(409);

  const corrected = await updateProcurement(request, projectId, {
    reason: '실제 고객 제공 예정량 정정',
    items: [{
      itemId: item!.itemId,
      expectedRowVersion: partial.rowVersion,
      expectedReceiptDate: '2020-01-01',
      orderQuantity: 4
    }]
  });
  const correctedItem = corrected.items.find((candidate) => candidate.itemId === item!.itemId);
  expect(correctedItem?.supplyType).toBe('CustomerSupplied');
  expect(correctedItem?.orderQuantity).toBe(4);

  const close = await request.post(`${apiBaseUrl}/api/materials/items/${item!.itemId}/close-arrivals`, {
    headers: devHeaders('dev-materials'),
    data: { expectedRowVersion: correctedItem!.rowVersion, reason: '정정 수량 전량 입고 완료' }
  });
  expect(close.ok()).toBeTruthy();
  const completed = await materialItem(request, item!.itemId);
  expect(completed.remainingQuantity).toBe(0);
  expect(completed.customerSupplyOverdue).toBeFalsy();
  expect(completed.receiptCompleted).toBeTruthy();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/projects');
  await openProject(page, projectTitle);
  await page.getByRole('tab', { name: '구매' }).click();
  await page.getByRole('tab', { name: /사급 자재/ }).click();
  const mobilePurchase = page.locator('[data-testid="procurement-mobile"]');
  await expect(mobilePurchase).toContainText('사급 자재');
  await expect(mobilePurchase).toContainText('제공 예정4 EA');
  await assertNoHorizontalOverflow(page);
});

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount: 1,
      deliveryDate: '2026-12-31',
      salesOwnerUserId,
      packagingMethod: 'WoodenCrate',
      salesAmount: null,
      currencyCode: null,
      deliveryLocation: 'Synthetic Site',
      fatRequired: false
    }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}

async function updateProcurement(request: APIRequestContext, projectId: string, data: unknown) {
  const response = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data
  });
  expect(response.ok()).toBeTruthy();
  return await response.json() as {
    items: Array<{
      itemId: string;
      orderItem: string | null;
      supplyType: 'Purchased' | 'CustomerSupplied';
      orderQuantity: number | null;
      rowVersion: number;
    }>;
  };
}

async function postJson<T>(request: APIRequestContext, userKey: string, path: string, data: unknown): Promise<T> {
  const response = await request.post(`${apiBaseUrl}${path}`, { headers: devHeaders(userKey), data });
  expect(response.ok()).toBeTruthy();
  return await response.json() as T;
}

async function finalizeDetailedIqc(request: APIRequestContext, attemptId: string, receiptVersion: number, reason: string) {
  const initialized = await postJson<IqcReport>(request, 'dev-quality', `/api/quality/iqc/${attemptId}/reports`, {});
  const responses = initialized.items.map((item) => ({
    templateItemId: item.itemId,
    checkResult: item.responseType === 'Check' ? 'Pass' : null,
    textValue: item.responseType === 'Text' ? '합성 검사값 정상' : null,
    note: null
  }));
  const saved = await request.put(`${apiBaseUrl}/api/quality/iqc/reports/${initialized.reportId}/responses`, {
    headers: devHeaders('dev-quality'),
    data: { expectedReportVersion: initialized.reportVersion, responses }
  });
  expect(saved.ok()).toBeTruthy();
  const savedReport = await saved.json() as IqcReport;
  const enclosure = savedReport.items.find((item) => item.itemCode === 'ENCLOSURE')!;
  const photo = await fs.readFile(path.resolve('src/assets/emi-logo.png'));
  const uploaded = await request.post(`${apiBaseUrl}/api/quality/iqc/reports/${savedReport.reportId}/photos`, {
    headers: devHeaders('dev-quality'),
    multipart: {
      templateItemId: enclosure.itemId,
      expectedReportVersion: String(savedReport.reportVersion),
      altText: '합성 외함 전체 상태',
      photo: { name: 'synthetic-enclosure.png', mimeType: 'image/png', buffer: photo }
    }
  });
  expect(uploaded.ok()).toBeTruthy();
  const uploadedReport = await uploaded.json() as IqcReport;
  const finalized = await request.post(`${apiBaseUrl}/api/quality/iqc/reports/${uploadedReport.reportId}/finalize`, {
    headers: devHeaders('dev-quality'),
    data: {
      expectedReportVersion: uploadedReport.reportVersion,
      expectedReceiptVersion: receiptVersion,
      result: 'Passed',
      reason
    }
  });
  expect(finalized.ok()).toBeTruthy();
}

type IqcReport = {
  reportId: string;
  reportVersion: number;
  items: Array<{ itemId: string; itemCode: string; responseType: 'Check' | 'Text' }>;
};

async function materialItem(request: APIRequestContext, itemId: string) {
  const response = await request.get(`${apiBaseUrl}/api/materials/receipts?includeCompleted=true`, {
    headers: devHeaders('dev-materials')
  });
  expect(response.ok()).toBeTruthy();
  const body = await response.json() as {
    items: Array<{
      itemId: string;
      remainingQuantity: number;
      confirmedQuantity: number;
      customerSupplyOverdue: boolean;
      receiptCompleted: boolean;
      rowVersion: number;
    }>;
  };
  return body.items.find((candidate) => candidate.itemId === itemId)!;
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}

async function saveEvidence(page: Page, fileName: string) {
  const evidenceDirectory = path.join('/tmp', 'emi-qms-p2-remediation-evidence');
  await fs.mkdir(evidenceDirectory, { recursive: true });
  await page.screenshot({
    path: path.join(evidenceDirectory, fileName),
    fullPage: true,
    type: 'jpeg',
    quality: 88
  });
}

async function openProject(page: Page, projectTitle: string) {
  const isMobile = Boolean(page.viewportSize()?.width && page.viewportSize()!.width <= 760);
  if (isMobile) {
    await page.getByRole('heading', { name: '현장 프로젝트' }).waitFor();
    await page.getByRole('button', { name: /검색·필터/ }).click();
    const sheet = page.getByRole('dialog', { name: '프로젝트 검색·필터' });
    await sheet.getByPlaceholder('고객사, Item, Code, Title').fill(projectTitle);
    await sheet.getByRole('button', { name: '조건 적용' }).click();
  } else {
    await page.getByRole('heading', { name: '프로젝트 목록' }).waitFor();
    await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(projectTitle);
    await page.getByRole('button', { name: '검색' }).click();
  }
  const projectEntry = page.locator('.project-list-row, .project-list-card').filter({ hasText: projectTitle });
  await expect(projectEntry).toBeVisible();
  const desktopRow = page.locator('.project-list-row').filter({ hasText: projectTitle });
  if (await desktopRow.count() > 0) {
    await desktopRow.click();
  } else {
    await page.locator('.project-list-card').filter({ hasText: projectTitle }).getByRole('button', { name: '상세 보기' }).click();
  }
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
}
