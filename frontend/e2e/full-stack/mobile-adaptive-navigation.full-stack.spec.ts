import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = process.env.MOBILE_SCREENSHOT_DIR?.trim() || null;

test('TASK-MOBILE-001: adaptive field routes keep a permission-aware mobile navigation', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `모바일 현장 검수 ${unique}`;
  const projectId = await createProject(request, `MOB-${unique}`, projectTitle);
  const pending = await createPending(request, projectId, `현장 치수 재확인 ${unique}`);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/projects');
  await selectMobileDevelopmentUser(page, 'dev-quality');

  await page.goto('/my-work');
  await expect(page.getByRole('heading', { name: '오늘 처리할 업무' })).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '새로고침' }));
  await assertMobileNavigation(page, '내 업무');
  await capture(page, '01-my-work-mobile-390.png');

  await page.goto('/projects');
  await applyMobileProjectSearch(page, projectTitle);
  await expect(page.getByRole('heading', { name: '현장 프로젝트' })).toBeVisible();
  await expect(page.getByText(projectTitle, { exact: true })).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '검색' }));
  await assertMobileNavigation(page, '프로젝트');
  await capture(page, '02-project-list-mobile-390.png');

  await page.goto(`/projects/${projectId}`);
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  await expect(page.getByLabel('프로젝트 병목 현황')).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '← 프로젝트' }));
  await assertMobileNavigation(page, '프로젝트');
  await capture(page, '03-project-detail-mobile-390.png');

  await page.goto('/pending');
  await expect(page.getByRole('heading', { name: '현장 Pending' })).toBeVisible();
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

  const menuTrigger = page.getByRole('button', { name: '메뉴 열기' });
  await menuTrigger.click();
  const menuDialog = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  await expect(menuDialog).toBeVisible();
  await expect(menuDialog.locator('.mobile-menu-item').first()).toBeFocused();
  await capture(page, '07-left-menu-drawer-mobile-390.png');
  await page.keyboard.press('Escape');
  await expect(menuDialog).toBeHidden();
  await expect(menuTrigger).toBeFocused();

  await page.setViewportSize({ width: 480, height: 800 });
  await page.goto('/projects');
  await applyMobileProjectSearch(page, projectTitle);
  await expect(page.getByText(projectTitle, { exact: true })).toBeVisible();
  await assertMobileNavigation(page, '프로젝트');
  await capture(page, '08-project-list-narrow-480.png');
});

async function assertMobileNavigation(page: Page, activeLabel: string) {
  const trigger = page.getByRole('button', { name: '메뉴 열기' });
  await expect(trigger).toBeVisible();
  await expect(page.locator('.app-mobile-nav')).toHaveCount(0);
  await assertTouchTarget(trigger);

  await trigger.click();
  const drawer = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  const navigation = drawer.getByRole('navigation', { name: '모바일 공통 메뉴' });
  await expect(drawer).toBeVisible();
  await expect(navigation.locator('[aria-current="page"]')).toContainText(activeLabel);
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
  await page.keyboard.press('Escape');
  await expect(drawer).toBeHidden();
  await expect(trigger).toBeFocused();
}

async function selectMobileDevelopmentUser(page: Page, userKey: string) {
  await page.getByRole('button', { name: '상태' }).click();
  const statusSheet = page.getByRole('dialog', { name: '앱 상태와 계정' });
  await statusSheet.getByLabel('개발 사용자').selectOption(userKey);
  await statusSheet.getByRole('button', { name: '앱 상태와 계정 닫기' }).click();
}

async function applyMobileProjectSearch(page: Page, projectTitle: string) {
  await page.getByRole('button', { name: /검색·필터/ }).click();
  const filterSheet = page.getByRole('dialog', { name: '프로젝트 검색·필터' });
  await filterSheet.getByPlaceholder('고객사, Item, Code, Title').fill(projectTitle);
  await filterSheet.getByRole('button', { name: '조건 적용' }).click();
}

async function assertTouchTarget(target: Locator) {
  const box = await target.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.width).toBeGreaterThanOrEqual(44);
  expect(box!.height).toBeGreaterThanOrEqual(44);
}

async function capture(page: Page, filename: string) {
  if (!screenshotDirectory) {
    return;
  }

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
