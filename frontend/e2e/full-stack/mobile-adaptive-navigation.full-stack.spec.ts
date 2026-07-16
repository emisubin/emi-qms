import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = process.env.MOBILE_SCREENSHOT_DIR?.trim()
  || path.resolve(process.cwd(), '../tasks/mobile-001-screenshots');

test('TASK-MOBILE-001: adaptive field routes keep a permission-aware mobile navigation', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `모바일 현장 검수 ${unique}`;
  const projectId = await createProject(request, `MOB-${unique}`, projectTitle);
  const pending = await createPending(request, projectId, `현장 치수 재확인 ${unique}`);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');

  await page.goto('/my-work');
  await expect(page.getByRole('heading', { name: '내 업무' })).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '새로고침' }));
  await assertMobileNavigation(page, '내 업무');
  await capture(page, '01-my-work-mobile-390.png');

  await page.goto('/');
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  await expect(page.getByText(projectTitle, { exact: true })).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '검색' }));
  await assertMobileNavigation(page, '프로젝트');
  await capture(page, '02-project-list-mobile-390.png');

  await page.goto(`/projects/${projectId}`);
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  await expect(page.getByLabel('프로젝트 병목 현황')).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '목록' }));
  await assertMobileNavigation(page, '프로젝트');
  await capture(page, '03-project-detail-mobile-390.png');

  await page.goto('/pending');
  await expect(page.getByRole('heading', { name: 'Pending List' })).toBeVisible();
  await expect(page.getByRole('button', { name: pending.title })).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '+ Pending 등록' }));
  await assertMobileNavigation(page, 'Pending');
  await capture(page, '04-pending-list-mobile-390.png');

  await page.goto(`/pending/${pending.pendingId}`);
  await expect(page.getByRole('heading', { name: pending.title })).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '← Pending List' }));
  await assertMobileNavigation(page, 'Pending');
  await capture(page, '05-pending-detail-mobile-390.png');

  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: '알림' })).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '전체 읽음' }));
  await assertMobileNavigation(page, '알림');
  await capture(page, '06-notifications-mobile-390.png');

  const moreTrigger = page.getByRole('button', { name: '더보기' });
  await moreTrigger.click();
  const moreDialog = page.getByRole('dialog', { name: '더 많은 업무 메뉴' });
  await expect(moreDialog).toBeVisible();
  await expect(moreDialog.locator('.app-mobile-more-item').first()).toBeFocused();
  await capture(page, '07-more-sheet-mobile-390.png');
  await page.keyboard.press('Escape');
  await expect(moreDialog).toBeHidden();
  await expect(moreTrigger).toBeFocused();

  await page.setViewportSize({ width: 480, height: 800 });
  await page.goto('/');
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  await expect(page.getByText(projectTitle, { exact: true })).toBeVisible();
  await assertMobileNavigation(page, '프로젝트');
  await capture(page, '08-project-list-narrow-480.png');
});

async function assertMobileNavigation(page: Page, activeLabel: string) {
  const navigation = page.getByRole('navigation', { name: '모바일 공통 메뉴' });
  await expect(navigation).toBeVisible();
  await expect(navigation.getByRole('button', { name: activeLabel })).toHaveAttribute('aria-current', 'page');
  await expect(navigation.getByRole('button', { name: 'Pending' })).toBeVisible();

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);

  const targets = await navigation.getByRole('button').evaluateAll((buttons) =>
    buttons.map((button) => {
      const rect = button.getBoundingClientRect();
      return { width: rect.width, height: rect.height };
    })
  );
  expect(targets.every((target) => target.width >= 44 && target.height >= 44)).toBeTruthy();
}

async function assertTouchTarget(target: Locator) {
  const box = await target.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.width).toBeGreaterThanOrEqual(44);
  expect(box!.height).toBeGreaterThanOrEqual(44);
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

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      customerName: 'Synthetic Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount: 3,
      deliveryDate: '2026-12-31',
      salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
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

async function createPending(request: APIRequestContext, projectId: string, title: string) {
  const response = await request.post(`${apiBaseUrl}/api/pending`, {
    headers: { 'X-Dev-User': 'dev-quality' },
    data: {
      projectId,
      issueType: 'ManufacturingStop',
      title,
      description: '격리된 TASK-MOBILE-001 시각 검수용 synthetic Pending입니다.',
      priority: 'Urgent'
    }
  });
  expect(response.ok()).toBeTruthy();
  const detail = await response.json() as { issue: { pendingId: string; title: string } };
  return detail.issue;
}
