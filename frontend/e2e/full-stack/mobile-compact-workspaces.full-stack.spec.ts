import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/mobile-002-change-002-screenshots');
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';

test('TASK-MOBILE-002 Change 002: major workspaces use compact task-first mobile composition', async ({ page, request }) => {
  test.setTimeout(150_000);
  const unique = Date.now();
  const projectTitle = `모바일 전면 개편 검수 ${unique}`;
  const projectCode = `M2C2-${unique}`;
  const projectId = await createProject(request, projectCode, projectTitle);
  const procurement = await updateProcurement(request, projectId);
  const item = procurement.items[0];

  const arrival = await postJson<{ receiptId: string }>(request, 'dev-materials', `/api/materials/items/${item.itemId}/receipts`, {
    quantity: 3,
    unit: 'EA',
    arrivalDate: '2026-07-17',
    note: 'synthetic mobile evidence'
  });
  await postJson(request, 'dev-materials', `/api/materials/receipts/${arrival.receiptId}/iqc-requests`, { expectedVersion: 1 });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/projects');
  await selectMobileDevelopmentUser(page, 'dev-sales');

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '오늘의 현장 업무' })).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '01-home-mobile-390.png');

  await page.goto('/projects');
  await expect(page.getByRole('heading', { name: '현장 프로젝트' })).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '02-project-list-mobile-390.png');

  await selectMobileDevelopmentUser(page, 'dev-production');
  await page.goto('/production-planning');
  await page.getByPlaceholder('프로젝트명, 고객사, Code, Item 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색', exact: true }).click();
  await expect(page.locator('.production-planning-mobile .procurement-project-card').filter({ hasText: projectTitle })).toBeVisible();
  await expect(page.locator('.production-planning-mobile .mobile-priority-grid')).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '03-production-planning-mobile-390.png');

  await selectMobileDevelopmentUser(page, 'dev-procurement');
  await page.goto('/procurement');
  await page.getByPlaceholder('프로젝트명, 고객사, Code, 발주품목 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색', exact: true }).click();
  const procurementCard = page.locator('.procurement-project-card').filter({ hasText: projectTitle });
  await expect(procurementCard).toBeVisible();
  await expect(procurementCard.locator('.mobile-priority-grid')).toBeVisible();
  await expect(procurementCard.getByText('프로젝트 정보', { exact: true })).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '04-procurement-dashboard-mobile-390.png');

  await page.goto(`/projects/${projectId}`);
  await page.getByRole('tab', { name: '구매' }).click();
  await expect(page.locator('[data-testid="procurement-mobile"] .mobile-priority-grid')).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '05-project-procurement-mobile-390.png');

  await page.goto(`/projects/${projectId}/procurement/edit`);
  await expect(page.getByRole('heading', { name: '구매정보 수정' })).toBeVisible();
  await expect(page.locator('[data-testid="procurement-mobile"]')).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '06-procurement-edit-mobile-390.png');

  await selectMobileDevelopmentUser(page, 'dev-materials');
  await page.goto('/materials/receipts');
  const materialCard = page.locator('.material-item-card').filter({ hasText: projectTitle });
  await expect(materialCard).toBeVisible();
  await expect(materialCard.locator('.material-item-meta--priority')).toBeVisible();
  await expect(materialCard.getByText('입고 상세', { exact: true })).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '07-material-receiving-mobile-390.png');

  await selectMobileDevelopmentUser(page, 'dev-quality');
  await page.goto('/quality/iqc');
  await expect(page.locator('.iqc-request-card').filter({ hasText: projectTitle })).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '08-iqc-mobile-390.png');

  await page.goto('/teams/activity');
  await expect(page.getByRole('heading', { name: '업무 피드' })).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '09-teams-activity-mobile-390.png');

  await selectMobileDevelopmentUser(page, 'dev-admin');
  await page.goto('/admin');
  await expect(page.getByRole('heading', { name: '관리자' })).toBeVisible();
  await expect(page.locator('.admin-dashboard-card[data-tone="danger"]')).toBeVisible();
  await assertCompactMobilePage(page);
  await capture(page, '10-admin-dashboard-mobile-390.png');

  await page.goto('/admin/users');
  await expect(page.getByRole('heading', { name: '사용자 관리' })).toBeVisible();
  const fieldToggle = page.locator('.mobile-admin-field-toggle');
  await expect(fieldToggle).toBeVisible();
  await expect(page.locator('.admin-mobile-page')).not.toHaveClass(/admin-mobile-page--all-fields/);
  await assertCompactMobilePage(page);
  await capture(page, '11-admin-users-priority-mobile-390.png');

  await fieldToggle.click();
  await expect(page.locator('.admin-mobile-page')).toHaveClass(/admin-mobile-page--all-fields/);
  await capture(page, '12-admin-users-all-fields-mobile-390.png');

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/procurement');
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'desktop');
  await expect(page.getByRole('navigation', { name: '공통 메뉴' })).toBeVisible();
  await expect(page.locator('.procurement-project-table')).toBeVisible();
  await capture(page, '13-procurement-desktop-reference-1440.png');
});

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Mobile Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount: 3,
      deliveryDate: '2026-08-31',
      salesOwnerUserId,
      packagingMethod: 'WoodenCrate',
      salesAmount: 1000,
      currencyCode: 'KRW',
      deliveryLocation: 'Synthetic Site',
      fatRequired: false
    }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}

async function updateProcurement(request: APIRequestContext, projectId: string) {
  const response = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: {
      reason: 'synthetic mobile evidence',
      items: [{
        orderItem: 'Synthetic Customer Busbar',
        supplierName: 'Synthetic Reference Vendor',
        technicalOwner: 'Synthetic Engineer',
        standardLeadTime: '14 days',
        orderDate: '2026-07-01',
        expectedReceiptDate: '2026-07-15',
        issueNote: '입고 지연 확인 필요',
        supplyType: 'CustomerSupplied',
        orderQuantity: 10,
        orderUnit: 'EA'
      }]
    }
  });
  expect(response.ok()).toBeTruthy();
  return await response.json() as { items: Array<{ itemId: string }> };
}

async function postJson<T>(request: APIRequestContext, userKey: string, route: string, data: unknown): Promise<T> {
  const response = await request.post(`${apiBaseUrl}${route}`, { headers: devHeaders(userKey), data });
  expect(response.ok()).toBeTruthy();
  return await response.json() as T;
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}

async function selectMobileDevelopmentUser(page: Page, userKey: string) {
  const trigger = page.getByRole('button', { name: '상태' });
  await trigger.click();
  const sheet = page.getByRole('dialog', { name: '앱 상태와 계정' });
  await sheet.getByLabel('개발 사용자').selectOption(userKey);
  await sheet.getByRole('button', { name: '앱 상태와 계정 닫기' }).click();
  await expect(sheet).toBeHidden();
}

async function assertCompactMobilePage(page: Page) {
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  const metrics = await page.evaluate(() => {
    const visibleButtons = Array.from(document.querySelectorAll('button')).filter((element) => {
      const rect = element.getBoundingClientRect();
      return rect.width > 0 && rect.height > 0;
    });
    return {
      overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      touchTargetsSafe: visibleButtons.every((element) => {
        const rect = element.getBoundingClientRect();
        return rect.width >= 44 && rect.height >= 44;
      }),
      appBarHeight: document.querySelector('.mobile-app-bar')?.getBoundingClientRect().height ?? 0,
      navTop: document.querySelector('.app-mobile-nav')?.getBoundingClientRect().top ?? 0
    };
  });
  expect(metrics.overflow).toBe(0);
  expect(metrics.touchTargetsSafe).toBeTruthy();
  expect(metrics.appBarHeight).toBeLessThanOrEqual(64);
  expect(metrics.navTop).toBeGreaterThan(700);
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
