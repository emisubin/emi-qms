import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import fs from 'node:fs/promises';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';

test('TASK-E2E-RELIABILITY-001 Change 001: procurement actions wait for the latest initial load', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `구매 초기 잠금 검수 ${unique}`;
  const projectId = await createProject(request, `PROC-READY-${unique}`, projectTitle);

  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await openProject(page, projectTitle);
  await page.getByRole('tab', { name: '구매' }).click();
  const procurementSection = page.locator('section.procurement-section').filter({
    has: page.getByRole('heading', { name: '구매정보', exact: true })
  });
  await expect(procurementSection.getByText('등록된 구매정보가 없습니다.', { exact: true })).toBeVisible();

  let releaseProcurementLoad: () => void = () => undefined;
  const procurementLoadGate = new Promise<void>((resolve) => {
    releaseProcurementLoad = resolve;
  });
  let heldInitialLoad = false;
  const procurementPattern = `**/api/projects/${projectId}/procurement`;
  await page.route(procurementPattern, async (route) => {
    if (route.request().method() === 'GET') {
      heldInitialLoad = true;
      await procurementLoadGate;
    }
    await route.continue();
  });

  const evidenceDirectory = '/tmp/emi-qms-p2-remediation-evidence';
  try {
    await page.getByRole('button', { name: '구매정보 수정' }).click();
    await expect.poll(() => heldInitialLoad).toBe(true);
    await expect(page.getByRole('status')).toContainText('프로젝트·구매정보 확인 중에는 입력할 수 없습니다.');
    for (const actionName of ['도급 구매품 행 추가', 'Excel 양식 다운로드', 'Excel 업로드', '저장']) {
      await expect(page.getByRole('button', { name: actionName })).toBeDisabled();
    }

    await fs.mkdir(evidenceDirectory, { recursive: true });
    await page.screenshot({
      path: `${evidenceDirectory}/e2e-reliability-001-change-001-procurement-locked.jpg`,
      fullPage: true,
      type: 'jpeg',
      quality: 88
    });
  } finally {
    releaseProcurementLoad();
    await page.unrouteAll({ behavior: 'wait' });
  }

  const editRows = page.getByRole('table', { name: '구매정보 수정' }).locator('.procurement-table-row.editable');
  await expect(editRows).toHaveCount(0);
  await expect(page.getByRole('button', { name: '도급 구매품 행 추가' })).toBeEnabled();
  await expect(page.getByRole('button', { name: '저장' })).toBeEnabled();

  await page.getByRole('button', { name: '도급 구매품 행 추가' }).click();
  await expect(editRows).toHaveCount(1);
  await page.screenshot({
    path: `${evidenceDirectory}/e2e-reliability-001-change-001-procurement-ready.jpg`,
    fullPage: true,
    type: 'jpeg',
    quality: 88
  });
});

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: { 'X-Dev-User': 'dev-sales' },
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

async function openProject(page: Page, projectTitle: string) {
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  const projectEntry = page.locator('.project-list-row').filter({ hasText: projectTitle });
  await expect(projectEntry).toBeVisible();
  await projectEntry.click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
}
