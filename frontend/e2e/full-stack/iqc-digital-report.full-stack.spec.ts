import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { markProjectAsLegacyIqc, uploadRequiredIqcPhotos } from './legacy-iqc-fixture';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = process.env.TASK009A_SCREENSHOT_DIR?.trim() || null;

test('TASK-009A: mobile-first IQC report captures checklist, photo, immutable result, and PDF', async ({ page, request }) => {
  test.setTimeout(120_000);
  const unique = Date.now();
  const projectTitle = `IQC 디지털 성적서 ${unique}`;
  const projectId = await createProject(request, `IQC-${unique}`, projectTitle);
  markProjectAsLegacyIqc(projectId);
  const procurement = await updateProcurement(request, projectId, [
    { orderItem: 'Desktop Enclosure', supplierName: 'Synthetic Vendor', orderQuantity: 2, orderUnit: 'EA' },
    { orderItem: 'Mobile Control Unit', supplierName: 'Synthetic Vendor', orderQuantity: 1, orderUnit: 'SET' }
  ]);
  const desktopAttempt = await createAttempt(request, procurement.items[0].itemId, 2, 'EA');
  const mobileAttempt = await createAttempt(request, procurement.items[1].itemId, 1, 'SET');
  const desktopPreview = await request.get(`${apiBaseUrl}/api/quality/iqc/${desktopAttempt.attemptId}/report`, {
    headers: devHeaders('dev-quality')
  });
  expect(desktopPreview.status()).toBe(200);

  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto(`/quality/iqc?project=${projectId}`);
  await expect(page.getByRole('heading', { name: 'IQC 검사함' })).toBeVisible();
  await expect(page.locator('.iqc-request-card')).toHaveCount(2);
  await assertDesktopContentClearOfSidebar(page);
  await capture(page, '01-iqc-queue-desktop.png');

  const desktopCard = page.locator('.iqc-request-card').filter({ hasText: 'Desktop Enclosure' });
  await desktopCard.click();
  const desktopDrawer = page.locator('.material-action-drawer--iqc-report');
  await expect(desktopDrawer.getByRole('button', { name: '검사 시작' })).toBeVisible();
  await capture(page, '02-iqc-report-start-desktop.png');
  await desktopDrawer.getByRole('button', { name: '검사 시작' }).click();
  await expect(desktopDrawer.getByRole('heading', { name: '검사항목' })).toBeVisible();
  await completeChecklist(desktopDrawer);
  await capture(page, '03-iqc-checklist-desktop.png');
  await uploadRequiredIqcPhotos(desktopDrawer, path.resolve('src/assets/emi-logo.png'));
  await expect(desktopDrawer.locator('.iqc-photo-evidence')).toHaveCount(1);
  await capture(page, '04-iqc-photo-desktop.png');
  await desktopDrawer.getByRole('button', { name: '검사항목·사진 저장 후 최종확인' }).click();
  await desktopDrawer.getByLabel('종합 판정 사유').fill('검사항목과 외함 사진을 모두 확인했습니다.');
  await desktopDrawer.getByRole('button', { name: '합격 · 성적서 확정' }).click();
  await expect(desktopDrawer.getByText('출력본 준비 완료')).toBeVisible();
  await expect(desktopDrawer.getByText('IQC 합격', { exact: true })).toBeVisible();
  await capture(page, '05-iqc-finalized-desktop.png');
  await desktopDrawer.locator('.iqc-pdf-card').scrollIntoViewIfNeeded();
  await capture(page, '05b-iqc-pdf-desktop.png');

  const pdf = await request.get(`${apiBaseUrl}/api/quality/iqc/reports/${await reportId(request, desktopAttempt.attemptId)}/pdf`, {
    headers: devHeaders('dev-quality')
  });
  expect(pdf.ok()).toBeTruthy();
  expect((await pdf.body()).subarray(0, 4).toString()).toBe('%PDF');

  await desktopDrawer.getByRole('button', { name: '검사함으로 돌아가기' }).click();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/projects');
  await ensureMobileUser(page, 'dev-quality');
  await page.getByRole('button', { name: '메뉴 열기' }).click();
  const mobileMenu = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  await mobileMenu.getByRole('button', { name: '품질' }).click();
  await page.getByRole('button', { name: /수입검사\(IQC\)/u }).click();
  await page.getByTestId('quality-iqc-dashboard')
    .getByRole('button', { name: new RegExp(projectTitle, 'u') })
    .click();
  await expect(page.getByRole('heading', { name: 'IQC 검사함' })).toBeVisible();
  await expect(page.locator('.iqc-request-card').filter({ hasText: 'Mobile Control Unit' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '06-iqc-queue-mobile-390.png');

  await page.locator('.iqc-request-card').filter({ hasText: 'Mobile Control Unit' }).click();
  const mobileSheet = page.getByRole('dialog', { name: '디지털 검사성적서' });
  await expect(mobileSheet.getByRole('button', { name: '검사 시작' })).toBeVisible();
  await mobileSheet.getByRole('button', { name: '검사 시작' }).click();
  await expect(mobileSheet.getByRole('heading', { name: '검사항목' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '07-iqc-checklist-mobile-390.png');

  await completeChecklist(mobileSheet);
  await uploadRequiredIqcPhotos(mobileSheet, path.resolve('src/assets/emi-logo.png'));
  await expect(mobileSheet.locator('.iqc-photo-evidence')).toHaveCount(1);
  await mobileSheet.getByRole('button', { name: '검사항목·사진 저장 후 최종확인' }).click();
  await assertNoHorizontalOverflow(page);
  await capture(page, '08-iqc-photo-mobile-390.png');

  expect(desktopAttempt.attemptId).not.toBe(mobileAttempt.attemptId);
});

async function completeChecklist(scope: ReturnType<Page['locator']>) {
  const cards = scope.locator('.iqc-item-card');
  const count = await cards.count();
  for (let index = 0; index < count; index += 1) {
    const card = cards.nth(index);
    const pass = card.getByRole('button', { name: '적합', exact: true });
    if (await pass.count()) await pass.click();
  }
  const notes = scope.getByLabel('측정값·특이사항');
  if (await notes.count()) await notes.fill('합성 측정값 정상');
}

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Customer', item: 'RPP', projectCode, projectTitle, panelCount: 1,
      deliveryDate: '2026-12-31', salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
      packagingMethod: 'WoodenCrate', salesAmount: null, currencyCode: null,
      deliveryLocation: 'Synthetic Site', fatRequired: false
    }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}

async function updateProcurement(request: APIRequestContext, projectId: string, items: unknown[]) {
  const response = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: { items }
  });
  expect(response.ok()).toBeTruthy();
  return await response.json() as { items: Array<{ itemId: string }> };
}

async function createAttempt(request: APIRequestContext, itemId: string, quantity: number, unit: string) {
  const arrival = await request.post(`${apiBaseUrl}/api/materials/items/${itemId}/receipts`, {
    headers: devHeaders('dev-materials'),
    data: { quantity, unit, arrivalDate: '2026-07-17' }
  });
  expect(arrival.ok()).toBeTruthy();
  const receipt = await arrival.json() as { receiptId: string };
  const iqc = await request.post(`${apiBaseUrl}/api/materials/receipts/${receipt.receiptId}/iqc-requests`, {
    headers: devHeaders('dev-materials'),
    data: { expectedVersion: 1 }
  });
  expect(iqc.ok()).toBeTruthy();
  const body = await iqc.json() as { iqcAttemptId: string };
  return { attemptId: body.iqcAttemptId };
}

async function reportId(request: APIRequestContext, attemptId: string) {
  const response = await request.get(`${apiBaseUrl}/api/quality/iqc/${attemptId}/report`, { headers: devHeaders('dev-quality') });
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { reportId: string }).reportId;
}

async function ensureMobileUser(page: Page, userKey: string) {
  await page.getByRole('button', { name: '메뉴 열기' }).click();
  const drawer = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  await drawer.getByLabel('개발 사용자').selectOption(userKey);
  await drawer.getByRole('button', { name: '메뉴 닫기' }).click();
}

async function capture(page: Page, filename: string) {
  if (!screenshotDirectory) return;
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled' });
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}

async function assertDesktopContentClearOfSidebar(page: Page) {
  const geometry = await page.evaluate(() => {
    const sidebar = document.querySelector('.app-sidebar')?.getBoundingClientRect();
    const productTitle = document.querySelector('.topbar h1')?.getBoundingClientRect();
    const pageTitle = document.querySelector('.material-hero h2')?.getBoundingClientRect();
    return {
      sidebarRight: sidebar?.right ?? 0,
      productTitleLeft: productTitle?.left ?? 0,
      pageTitleLeft: pageTitle?.left ?? 0
    };
  });
  expect(geometry.productTitleLeft).toBeGreaterThan(geometry.sidebarRight);
  expect(geometry.pageTitleLeft).toBeGreaterThan(geometry.sidebarRight);
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}
