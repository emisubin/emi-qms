import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/home-001-screenshots');

test('TASK-HOME-001: Home widgets stay responsive, permission-aware, and linked to source pages', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `홈 대시보드 검수 ${unique}`;
  const projectId = await createProject(request, `HOME-${unique}`, projectTitle);
  await createPending(request, projectId, `홈 긴급 병목 ${unique}`);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto('/');

  await expect(page.getByRole('heading', { name: '업무 홈' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '내 업무' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '프로젝트 병목' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Pending' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '알림' })).toBeVisible();
  await expect(page.getByRole('button', { name: projectTitle })).toBeVisible();
  await expect(page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '홈' })).toHaveClass(/active/);
  await capture(page, '01-home-desktop-1440.png');

  await page.getByRole('button', { name: projectTitle }).click();
  await expect(page).toHaveURL(new RegExp(`/projects/${projectId}$`));
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: '오늘의 현장 업무' })).toBeVisible();
  const menuTrigger = page.getByRole('button', { name: '메뉴 열기' });
  await menuTrigger.click();
  const menuDrawer = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  const mobileNavigation = menuDrawer.getByRole('navigation', { name: '모바일 공통 메뉴' });
  await expect(mobileNavigation.getByRole('button', { name: '홈' })).toHaveAttribute('aria-current', 'page');
  await expect(mobileNavigation.getByRole('button', { name: 'Pending' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await assertTouchTargets(mobileNavigation.getByRole('button'));
  await page.keyboard.press('Escape');
  await expect(menuDrawer).toBeHidden();
  await capture(page, '02-home-mobile-390.png');

  await page.goto('/projects');
  await page.route('**/api/me', async (route) => {
    const response = await route.fetch();
    const body = await response.json() as { permissions: string[] };
    await route.fulfill({
      response,
      json: {
        ...body,
        permissions: body.permissions.filter((permission) => permission !== 'Pending.Read')
      }
    });
  });
  let pendingRequestCount = 0;
  const pendingRequestListener = (requestEvent: { url(): string }) => {
    if (new URL(requestEvent.url()).pathname === '/api/pending') {
      pendingRequestCount += 1;
    }
  };
  page.on('request', pendingRequestListener);
  await page.goto('/');

  await expect(page.getByRole('heading', { name: '오늘의 현장 업무' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Pending' })).toHaveCount(0);
  await expect(page.getByLabel('프로젝트 병목 요약')).not.toContainText('Pending');
  expect(pendingRequestCount).toBe(0);
  await assertNoHorizontalOverflow(page);
  await capture(page, '03-home-without-pending-permission-390.png');
  page.off('request', pendingRequestListener);
});

async function capture(page: Page, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled', fullPage: true });
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}

async function assertTouchTargets(buttons: ReturnType<Page['getByRole']>) {
  const targets = await buttons.evaluateAll((elements) => elements.map((element) => {
    const rect = element.getBoundingClientRect();
    return { width: rect.width, height: rect.height };
  }));
  expect(targets.every((target) => target.width >= 44 && target.height >= 44)).toBeTruthy();
}

async function createProject(
  request: APIRequestContext,
  projectCode: string,
  projectTitle: string
) {
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
      description: 'Synthetic Home dashboard verification item.',
      occurrenceStage: 'Manufacturing',
      priority: 'Urgent',
      actionPlan: 'Synthetic validation only.',
      ownerUserId: '50000000-0000-0000-0000-000000000005'
    }
  });
  expect(response.ok()).toBeTruthy();
}
