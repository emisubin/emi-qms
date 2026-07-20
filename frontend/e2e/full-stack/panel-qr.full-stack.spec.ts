import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/qr-001-screenshots');
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';

test('TASK-QR-001: issue, preview, print, scan-route, and rotation use the real backend', async ({ page, request }) => {
  test.setTimeout(150_000);
  const unique = Date.now();
  const project = await createQrReadyProject(request, `QR-${unique}`, `QR 현장 추적 ${unique}`);

  await page.setViewportSize({ width: 1440, height: 940 });
  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await page.goto(`/projects/${project.projectId}`);
  await expect(page.getByRole('heading', { name: project.projectTitle })).toBeVisible();
  await expect(page.getByRole('heading', { name: '패널 QR' })).toBeVisible();

  for (let index = 0; index < 3; index += 1) {
    const [refreshedList] = await Promise.all([
      page.waitForResponse((response) => response.request().method() === 'GET' && new URL(response.url()).pathname === `/api/projects/${project.projectId}/qr`),
      page.getByRole('button', { name: 'QR 발급' }).first().click()
    ]);
    expect(refreshedList.status(), await refreshedList.text()).toBe(200);
    await expect(page.locator('.panel-qr-status[data-tone="issued"]')).toHaveCount(index + 1);
  }
  await expect(page.locator('.panel-qr-status[data-tone="issued"]')).toHaveCount(3);
  await page.locator('.panel-qr-manager').getByRole('button', { name: '보기', exact: true }).first().click();
  await expect(page.getByRole('heading', { name: '현장 라벨 미리보기' })).toBeVisible();
  await expect(page.locator('.panel-qr-preview img')).toHaveAttribute('src', /^blob:/);
  await capture(page, '01-panel-qr-issued-desktop-1440.png');

  await page.getByLabel('발급 QR 전체 선택').check();
  await page.getByRole('button', { name: '선택 3개 인쇄판' }).click();
  await expect(page.locator('.panel-qr-print-grid figure')).toHaveCount(3);
  await capture(page, '02-panel-qr-print-sheet-desktop-1440.png');

  const listResponse = await request.get(`${apiBaseUrl}/api/projects/${project.projectId}/qr`, { headers: devHeaders('dev-design') });
  expect(listResponse.ok()).toBeTruthy();
  const list = await listResponse.json() as { panels: Array<{ panelId: string; qr: { scanUrl: string } }> };
  const firstPanel = list.panels[0];
  const token = new URL(firstPanel.qr.scanUrl).pathname.split('/').at(-1)!;

  await page.setViewportSize({ width: 390, height: 844 });
  await page.evaluate(() => window.localStorage.setItem('emi-qms-development-user-key', 'dev-production'));
  await page.goto(`/q/${token}`);
  await expect(page.getByRole('heading', { name: 'QR-PANEL-01' })).toBeVisible();
  await expect(page.getByText(project.projectTitle)).toBeVisible();
  await expect(page.getByRole('button', { name: '현재 업무 열기' })).toBeVisible();
  await expect(page.locator('.qr-scan-card')).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '03-panel-qr-scan-mobile-390.png');

  const rotation = await request.post(
    `${apiBaseUrl}/api/projects/${project.projectId}/panels/${firstPanel.panelId}/qr/rotate`,
    { headers: devHeaders('dev-admin'), data: { reason: 'E2E 현장 라벨 훼손' } }
  );
  expect(rotation.ok()).toBeTruthy();
  await page.evaluate(() => window.localStorage.setItem('emi-qms-development-user-key', 'dev-admin'));
  await page.goto(`/q/${token}`);
  await expect(page.getByRole('heading', { name: '폐기된 QR입니다' })).toBeVisible();
  await capture(page, '04-panel-qr-revoked-mobile-390.png');
});

async function createQrReadyProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const created = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'QR Synthetic Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount: 3,
      deliveryDate: '2026-12-31',
      salesOwnerUserId,
      packagingMethod: 'WoodenCrate',
      salesAmount: 48000000,
      currencyCode: 'KRW',
      deliveryLocation: 'QR Synthetic Site',
      fatRequired: false
    }
  });
  expect(created.ok()).toBeTruthy();
  const projectId = (await created.json() as { projectId: string }).projectId;

  const panelInformation = await request.get(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, { headers: devHeaders('dev-design') });
  expect(panelInformation.ok()).toBeTruthy();
  const panelPayload = await panelInformation.json() as { panels: Array<{ panelId: string; panelInfoVersion: number }> };
  const named = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, {
    headers: devHeaders('dev-design'),
    data: {
      panels: panelPayload.panels.map((panel, index) => ({
        panelId: panel.panelId,
        expectedPanelInfoVersion: panel.panelInfoVersion,
        panelNameUpdate: { isChanged: true, value: `QR-PANEL-${String(index + 1).padStart(2, '0')}` },
        sizeUpdate: {
          isChanged: true,
          clear: false,
          inputUnit: 'Mm',
          width: 800 + index * 50,
          height: 2000,
          depth: 500
        }
      }))
    }
  });
  expect(named.ok()).toBeTruthy();
  return { projectId, projectTitle };
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
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
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled', fullPage: true });
}
