import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/mobile-002-screenshots');

test('TASK-MOBILE-002: mobile-first composition covers the seven core field routes', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `모바일 우선 검수 ${unique}`;
  const projectId = await createProject(request, `M2-${unique}`, projectTitle);
  const pending = await createPending(request, projectId, `모바일 조치 확인 ${unique}`);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/projects');
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await selectDevelopmentUserFromMobileStatus(page, 'dev-quality');

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '업무 홈' })).toBeVisible();
  await expect(page.getByLabel('긴급·차단 우선 확인')).toBeVisible();
  await assertMobileShell(page, '홈');
  await capture(page, '01-home-mobile-390.png');

  await page.goto('/my-work');
  await expect(page.getByRole('heading', { name: '오늘 처리할 업무' })).toBeVisible();
  await expect(page.getByLabel('오늘 업무 요약')).toBeVisible();
  await assertTouchTarget(page.getByRole('button', { name: '새로고침' }));
  await assertMobileShell(page, '내 업무');
  await capture(page, '02-my-work-mobile-390.png');

  await page.goto('/projects');
  await expect(page.getByRole('heading', { name: '현장 프로젝트' })).toBeVisible();
  const projectFilterTrigger = page.getByRole('button', { name: /검색·필터/ });
  await projectFilterTrigger.click();
  const projectFilterSheet = page.getByRole('dialog', { name: '프로젝트 검색·필터' });
  await expect(projectFilterSheet).toBeVisible();
  await expect(projectFilterSheet.getByPlaceholder('고객사, Item, Code, Title')).toBeFocused();
  await capture(page, '03-project-filter-sheet-mobile-390.png');
  await projectFilterSheet.getByPlaceholder('고객사, Item, Code, Title').fill(projectTitle);
  await projectFilterSheet.getByRole('button', { name: '조건 적용' }).click();
  await expect(projectFilterSheet).toBeHidden();
  await expect(projectFilterTrigger).toBeFocused();
  await expect(page.getByText(projectTitle, { exact: true })).toBeVisible();
  await assertMobileShell(page, '프로젝트');
  await capture(page, '04-project-list-mobile-390.png');

  await page.goto(`/projects/${projectId}`);
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  await expect(page.getByLabel('프로젝트 병목 현황')).toBeVisible();
  await expect(page.getByText('프로젝트 작업')).toBeVisible();
  await assertMobileShell(page, '프로젝트');
  await assertStickyActionInsideViewport(page, page.locator('.project-bottleneck-hero .primary-button'));
  await capture(page, '05-project-detail-mobile-390.png');

  await page.goto('/pending');
  await expect(page.getByRole('heading', { name: '현장 Pending' })).toBeVisible();
  await expect(page.getByRole('button', { name: pending.title })).toBeVisible();
  const pendingFilterTrigger = page.getByRole('button', { name: /Pending 필터/ });
  await pendingFilterTrigger.click();
  const pendingFilterSheet = page.getByRole('dialog', { name: 'Pending 필터' });
  await expect(pendingFilterSheet).toBeVisible();
  await expect(pendingFilterSheet.getByLabel('프로젝트')).toBeFocused();
  await capture(page, '06-pending-filter-sheet-mobile-390.png');
  const priorityFilter = pendingFilterSheet.getByLabel('긴급도');
  await priorityFilter.click();
  await priorityFilter.selectOption('Urgent');
  await expect(priorityFilter).toBeFocused();
  await pendingFilterSheet.getByRole('button', { name: '조건 적용' }).click();
  await expect(pendingFilterSheet).toBeHidden();
  await expect(pendingFilterTrigger).toBeFocused();
  await expect(page.getByRole('button', { name: pending.title })).toBeVisible();
  await assertMobileShell(page, 'Pending');
  await capture(page, '07-pending-list-mobile-390.png');

  await page.goto(`/pending/${pending.pendingId}`);
  await expect(page.getByRole('heading', { name: pending.title })).toBeVisible();
  await expect(page.getByRole('button', { name: '← Pending List' })).toBeVisible();
  await expect(page.getByRole('button', { name: '코멘트 등록' })).toBeVisible();
  await assertMobileShell(page, 'Pending');
  await capture(page, '08-pending-detail-mobile-390.png');

  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: '업무 알림' })).toBeVisible();
  await expect(page.getByLabel('알림 우선순위')).toBeVisible();
  await assertMobileShell(page, '알림');
  await capture(page, '09-notifications-mobile-390.png');

  await page.getByRole('button', { name: '내 계정 열기' }).click();
  const accountSheet = page.getByRole('dialog', { name: '내 계정' });
  await expect(accountSheet).toBeVisible();
  await accountSheet.getByText('연결 상태', { exact: true }).click();
  await expect(accountSheet.getByLabel('모바일 시스템 상태')).toBeVisible();
  await capture(page, '10-account-sheet-mobile-390.png');
  await page.keyboard.press('Escape');
  await expect(accountSheet).toBeHidden();

  await page.setViewportSize({ width: 480, height: 800 });
  await page.goto('/teams/activity');
  await expect(page.getByRole('heading', { name: '업무 피드' })).toBeVisible();
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await assertNoHorizontalOverflow(page);
  await capture(page, '11-teams-activity-mobile-480.png');

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'desktop');
  await expect(page.getByRole('heading', { name: '업무 홈' })).toBeVisible();
  await expect(page.getByRole('button', { name: projectTitle })).toBeVisible();
  await expect(page.getByRole('navigation', { name: '공통 메뉴' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: '모바일 공통 메뉴' })).toBeHidden();
  await capture(page, '12-home-desktop-reference-1440.png');

  await page.goto('/projects');
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  await expect(page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색')).toBeVisible();
  await expect(page.getByText(projectTitle, { exact: true })).toBeVisible();
  await captureElement(page, page.locator('.page-surface'), '13-project-list-desktop-reference-1440.png');
});

test.describe('TASK-MOBILE-002 coarse pointer desktop', () => {
  test.use({ viewport: { width: 1024, height: 768 }, hasTouch: true });

  test('keeps desktop composition while enlarging touch targets', async ({ page }) => {
    await page.goto('/projects');
    const shell = page.locator('.app-shell');
    await expect(shell).toHaveAttribute('data-layout-mode', 'desktop');
    await expect(shell).toHaveAttribute('data-touch-optimized', 'true');
    await expect(page.getByRole('navigation', { name: '공통 메뉴' })).toBeVisible();
    await expect(page.getByRole('navigation', { name: '모바일 공통 메뉴' })).toBeHidden();
    await assertTouchTarget(page.getByRole('button', { name: '신규 프로젝트' }));
    await assertTouchTargets(page.locator('button'));
    await capture(page, '14-coarse-pointer-desktop-1024.png');
  });
});

async function selectDevelopmentUserFromMobileStatus(page: Page, userKey: string) {
  const trigger = page.getByRole('button', { name: '메뉴 열기' });
  await trigger.click();
  const drawer = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  await drawer.getByLabel('개발 사용자').selectOption(userKey);
  await drawer.getByRole('button', { name: '메뉴 닫기' }).click();
  await expect(drawer).toBeHidden();
  await expect(trigger).toBeFocused();
}

async function assertMobileShell(page: Page, activeLabel: string) {
  const menuTrigger = page.getByRole('button', { name: '메뉴 열기' });
  await expect(page.locator('.mobile-app-bar')).toBeVisible();
  await expect(page.locator('.topbar')).toBeHidden();
  await expect(menuTrigger).toBeVisible();
  await expect(page.locator('.app-mobile-nav')).toHaveCount(0);
  const appBarBounds = await page.locator('.mobile-app-bar').evaluate((element) => {
    const rect = element.getBoundingClientRect();
    return { left: rect.left, right: rect.right, viewportWidth: window.innerWidth };
  });
  expect(appBarBounds.left).toBeGreaterThanOrEqual(0);
  expect(appBarBounds.right).toBeLessThanOrEqual(appBarBounds.viewportWidth);
  await assertTouchTarget(menuTrigger);

  await menuTrigger.click();
  const menuDrawer = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  const navigation = menuDrawer.getByRole('navigation', { name: '모바일 공통 메뉴' });
  await expect(menuDrawer).toBeVisible();
  await expect(navigation.locator('[aria-current="page"]')).toContainText(activeLabel);
  await assertTouchTargets(navigation.getByRole('button'));
  await page.keyboard.press('Escape');
  await expect(menuDrawer).toBeHidden();
  await expect(menuTrigger).toBeFocused();
  await assertNoHorizontalOverflow(page);
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}

async function assertTouchTargets(buttons: Locator) {
  const targets = await buttons.evaluateAll((elements) => elements.map((element) => {
    const rect = element.getBoundingClientRect();
    return { width: rect.width, height: rect.height };
  }).filter((target) => target.width > 0 && target.height > 0));
  expect(targets.every((target) => target.width >= 44 && target.height >= 44)).toBeTruthy();
}

async function assertStickyActionInsideViewport(page: Page, action: Locator) {
  await action.evaluate((element) => element.scrollIntoView({ block: 'end' }));
  const geometry = await action.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    return { bottom: rect.bottom, viewportHeight: window.innerHeight };
  });
  expect(geometry.bottom).toBeLessThanOrEqual(geometry.viewportHeight);
  await expect(page.locator('.app-mobile-nav')).toHaveCount(0);
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

async function captureElement(page: Page, target: Locator, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await target.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled' });
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
      description: '격리된 TASK-MOBILE-002 모바일 화면 검수용 synthetic Pending입니다.',
      priority: 'Urgent',
      actionDepartmentCode: 'production-planning',
      assigneeUserId: null,
      dueDate: null
    }
  });
  expect(response.ok()).toBeTruthy();
  const detail = await response.json() as { issue: { pendingId: string; title: string } };
  return detail.issue;
}
