import { expect, test, type Page, type Route } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const screenshotDirectory = path.resolve(process.cwd(), '../tasks/ux-001-screenshots');
const workItemId = '76000000-0000-0000-0000-000000009001';
const notificationId = '77000000-0000-0000-0000-000000009001';

test('TASK-UX-001: my work keeps context through loading and partial refresh failure', async ({ page }) => {
  const browserSignals = collectBrowserErrorSignals(page);
  let mutationCompleted = false;
  let releaseCompletion: (() => void) | undefined;

  await page.route('**/api/my-work/summary', (route) => fulfillJson(route, {
    requestedCount: 1,
    inProgressCount: 0,
    completedCount: 0,
    blockingCount: 0,
    assignedProjectCount: 1,
    assignedProjectBreakdown: []
  }));
  await page.route('**/api/my-work?*', (route) => mutationCompleted
    ? fulfillJson(route, { detail: '합성 refresh 실패' }, 500)
    : fulfillJson(route, { items: [syntheticWorkItem()] }));
  await page.route(`**/api/my-work/${workItemId}/complete`, async (route) => {
    await new Promise<void>((resolve) => { releaseCompletion = resolve; });
    mutationCompleted = true;
    await fulfillJson(route, { ...syntheticWorkItem(), status: 'Completed', statusLabel: '완료' });
  });

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/my-work');
  await expect(page.getByRole('heading', { name: '내 업무' })).toBeVisible();
  await expect(page.getByText('생산계획 확인', { exact: true })).toBeVisible();
  await capture(page, '01-my-work-desktop-normal.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '오늘 처리할 업무' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '08-my-work-mobile-normal.png');
  await page.setViewportSize({ width: 1440, height: 900 });
  await expect(page.getByRole('heading', { name: '내 업무' })).toBeVisible();

  const completionButton = page.getByRole('button', { name: '작업 완료' });
  await completionButton.click();
  await expect(page.getByRole('button', { name: '완료 처리 중' })).toBeDisabled();
  await expect(page.getByText('생산계획 확인 완료 처리 중입니다.')).toBeVisible();
  await expect.poll(() => Boolean(releaseCompletion)).toBe(true);
  await capture(page, '02-my-work-desktop-loading.png');

  releaseCompletion?.();
  const partialFeedback = page.getByText('생산계획 확인 업무는 완료했지만 최신 목록을 불러오지 못했습니다. 새로고침해 주세요.');
  await expect(partialFeedback).toBeVisible();
  await expect(partialFeedback).toBeFocused();
  await expect(page.getByText('생산계획 확인', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: '작업 완료' })).toBeEnabled();
  await assertNoHorizontalOverflow(page);
  expect(browserSignals.pageErrors).toEqual([]);
  expect(browserSignals.consoleErrorCount).toBe(1);
  await capture(page, '03-my-work-desktop-partial.png');
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '오늘 처리할 업무' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '09-my-work-mobile-partial.png');
});

test('TASK-UX-001: mobile notification error stays beside its row and receives focus', async ({ page }) => {
  const browserSignals = collectBrowserErrorSignals(page);
  let releaseRead: (() => void) | undefined;
  await installNotificationRoutes(page, {
    onRead: async (route) => {
      await new Promise<void>((resolve) => { releaseRead = resolve; });
      await fulfillJson(route, { detail: '다른 사용자가 먼저 처리했습니다.' }, 409);
    }
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: '업무 알림' })).toBeVisible();
  const notificationCard = page.locator('.workflow-card').filter({ hasText: '납기 변경 안내' });
  await expect(notificationCard).toBeVisible();
  await capture(page, '04-notifications-mobile-normal.png');

  await notificationCard.getByRole('button', { name: '읽음' }).click();
  await expect(notificationCard.getByRole('button', { name: '읽음 처리 중' })).toBeDisabled();
  await expect(notificationCard.getByText('납기 변경 안내 알림을 읽음 처리 중입니다.')).toBeVisible();
  await expect.poll(() => Boolean(releaseRead)).toBe(true);
  await capture(page, '05-notifications-mobile-loading.png');

  releaseRead?.();
  const errorFeedback = notificationCard.getByText('납기 변경 안내: 다른 사용자가 먼저 처리했습니다. 목록을 새로고침한 뒤 다시 확인해 주세요.');
  await expect(errorFeedback).toBeVisible();
  await expect(errorFeedback).toBeFocused();
  await assertNoHorizontalOverflow(page);
  expect(browserSignals.pageErrors).toEqual([]);
  expect(browserSignals.consoleErrorCount).toBe(1);
  await capture(page, '06-notifications-mobile-error.png');
});

test('TASK-UX-001: notification success remains visible after the unread row disappears', async ({ page }) => {
  const browserSignals = collectBrowserErrorSignals(page);
  let notificationRead = false;
  await installNotificationRoutes(page, {
    listIsEmpty: () => notificationRead,
    onRead: async (route) => {
      notificationRead = true;
      await fulfillJson(route, { ...syntheticNotification(), readAtUtc: '2026-07-18T10:00:00Z' });
    }
  });

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: '알림' })).toBeVisible();
  const notificationRow = page.locator('tr').filter({ hasText: '납기 변경 안내' });
  await notificationRow.getByRole('button', { name: '읽음' }).click();

  await expect(page.getByText('납기 변경 안내 알림을 읽음 처리했습니다.')).toBeVisible();
  await expect(page.getByText('표시할 알림이 없습니다.')).toBeVisible();
  await assertNoHorizontalOverflow(page);
  expect(browserSignals.pageErrors).toEqual([]);
  expect(browserSignals.consoleErrorCount).toBe(0);
  await capture(page, '07-notifications-desktop-success.png');
});

async function installNotificationRoutes(
  page: Page,
  options: {
    listIsEmpty?: () => boolean;
    onRead: (route: Route) => Promise<void>;
  }
) {
  await page.route('**/api/notifications/summary', (route) => fulfillJson(route, {
    unreadCount: options.listIsEmpty?.() ? 0 : 1,
    blockingCount: 0
  }));
  await page.route('**/api/notifications?*', (route) => fulfillJson(route, {
    items: options.listIsEmpty?.() ? [] : [syntheticNotification()]
  }));
  await page.route(`**/api/notifications/${notificationId}/read`, options.onRead);
}

function syntheticWorkItem() {
  return {
    workItemId,
    projectId: '71000000-0000-0000-0000-000000009001',
    projectTitle: 'UX 합성 프로젝트',
    projectCode: 'UX-001',
    projectItem: 'A1',
    projectDeliveryDate: '2026-12-31',
    workflowStageCode: 'ProductionPlanning',
    workflowStageName: '생산계획·담당자',
    responsibilityType: 'ProductionPlanningPrimary',
    responsibilityLabel: '생산관리 정담당자',
    title: '생산계획 확인',
    description: '처리 상태와 다음 행동을 확인하는 합성 업무입니다.',
    status: 'Requested',
    statusLabel: '시작 전',
    priority: 'Normal',
    priorityLabel: '일반',
    dueDate: null,
    createdAtUtc: '2026-07-18T09:00:00Z',
    startedAtUtc: null,
    completedAtUtc: null,
    linkUrl: '/projects/71000000-0000-0000-0000-000000009001/production-planning/edit'
  };
}

function syntheticNotification() {
  return {
    notificationId,
    projectId: '71000000-0000-0000-0000-000000009001',
    projectTitle: 'UX 합성 프로젝트',
    projectCode: 'UX-001',
    projectItem: 'A1',
    notificationType: 'Reference',
    notificationTypeLabel: '참조',
    severity: 'Info',
    severityLabel: '정보',
    title: '납기 변경 안내',
    message: '처리 상태와 다음 행동을 확인하는 합성 알림입니다.',
    linkUrl: '/projects/71000000-0000-0000-0000-000000009001',
    createdAtUtc: '2026-07-18T09:00:00Z',
    readAtUtc: null
  };
}

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body)
  });
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

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}

function collectBrowserErrorSignals(page: Page) {
  const signals = { pageErrors: [] as string[], consoleErrorCount: 0 };
  page.on('pageerror', (error) => signals.pageErrors.push(`page:${error.name}`));
  page.on('console', (message) => {
    if (message.type() === 'error') {
      signals.consoleErrorCount += 1;
    }
  });
  return signals;
}
