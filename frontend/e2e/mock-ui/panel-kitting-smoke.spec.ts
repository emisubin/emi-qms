import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, type Page, type Route, test } from '@playwright/test';

const screenshotDirectory = process.env.TASK010A_SCREENSHOT_DIR?.trim()
  ? path.resolve(process.env.TASK010A_SCREENSHOT_DIR)
  : path.resolve(process.cwd(), '../tasks/010a-screenshots');
const readyProjectId = '71000000-0000-0000-0000-000000000010';
const waitingProjectId = '71000000-0000-0000-0000-000000000011';

test('TASK-010A mock visual: adaptive panel kitting page and mobile drawer', async ({ page }) => {
  const store = createKittingStore();
  await routeApi(page, store);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/materials/kitting?project=${readyProjectId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await expect(page.getByRole('heading', { name: '패널 키팅' })).toBeVisible();
  await expect(page.getByText('입고 조건 충족 · 패널을 선택해 제조로 넘기세요.')).toBeVisible();
  await expect(page.locator('.kitting-panel-card')).toHaveCount(4);
  await capture(page, '01-panel-kitting-desktop-1440.jpg');

  await page.locator('.kitting-panel-card').nth(0).click();
  await page.locator('.kitting-panel-card').nth(1).click();
  await expect(page.getByRole('button', { name: '2면 키팅 완료' })).toBeEnabled();
  await page.getByRole('button', { name: '2면 키팅 완료' }).click();
  await expect(page.getByText('2면을 완료하고 제조 업무 2건을 넘겼습니다.')).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/materials/kitting?project=${readyProjectId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('button', { name: '메뉴 열기' })).toBeVisible();
  await expect(page.locator('.kitting-panel-card[data-completed="true"]')).toHaveCount(2);
  expect(await page.locator('.kitting-panel-card').evaluateAll((cards) =>
    new Set(cards.map((card) => card.getAttribute('data-shape'))).size
  )).toBeGreaterThanOrEqual(4);
  await assertNoHorizontalOverflow(page);
  await capture(page, '02-panel-kitting-mobile-390.jpg');

  await page.locator('.kitting-panel-card:not([data-completed="true"])').first().click();
  await expect(page.getByRole('button', { name: '1면 키팅 완료' })).toBeEnabled();
  await capture(page, '03-panel-kitting-selected-mobile-390.jpg');

  await page.getByRole('button', { name: '메뉴 열기' }).click();
  const menu = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  await expect(menu).toBeVisible();
  await expect(menu.getByRole('button', { name: '키팅' })).toHaveAttribute('aria-current', 'page');
  await capture(page, '04-panel-kitting-menu-mobile-390.jpg');
});

function createKittingStore() {
  return {
    completedPanelIds: new Set<string>()
  };
}

async function routeApi(page: Page, store: ReturnType<typeof createKittingStore>) {
  await page.route('http://localhost:5080/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const pathName = url.pathname;
    const userKey = request.headers()['x-dev-user'] ?? 'dev-sales';

    if (request.method() === 'OPTIONS') {
      return route.fulfill({ status: 204, headers: corsHeaders });
    }

    if (pathName === '/health/ready') {
      return fulfillJson(route, {
        name: 'ready',
        status: 'ok',
        database: { isReady: true, reason: 'reachable' },
        checkedAtUtc: '2026-07-17T00:00:00Z'
      });
    }

    if (pathName === '/api/runtime-mode') {
      return fulfillJson(route, {
        mode: 'Development',
        reviewSafe: false,
        mutationAllowed: true,
        backgroundWorkersEnabled: false,
        externalProvidersEnabled: false,
        databaseReadOnly: false,
        migrationExecutionEnabled: true,
        environment: 'Development',
        ready: true,
        reason: 'not_applicable',
        expectedMigration: '0033_panel_kitting_handoff',
        actualMigration: '0033_panel_kitting_handoff',
        migrationLedgerStatus: 'Compatible',
        expectedMigrationCount: 33,
        actualMigrationCount: 33,
        missingMigrations: [],
        unexpectedMigrations: [],
        approvedLegacyMigrations: [],
        migrationSchemaCompatible: true,
        migrationLedgerReady: true
      });
    }

    if (pathName === '/api/me') {
      return fulfillJson(route, currentUser(userKey));
    }

    if (pathName === '/api/my-work/summary') {
      return fulfillJson(route, { requestedCount: 0, inProgressCount: 0, completedCount: 0, blockingCount: 0, assignedProjectCount: 0, assignedProjectBreakdown: [] });
    }

    if (pathName === '/api/notifications/summary') {
      return fulfillJson(route, { unreadCount: 0, blockingCount: 0 });
    }

    if (pathName === '/api/materials/kitting' && request.method() === 'GET') {
      return fulfillJson(route, kittingQueue(store));
    }

    if (pathName === '/api/materials/kitting/complete' && request.method() === 'POST') {
      const body = await request.postDataJSON() as { operationId: string; panelIds: string[] };
      body.panelIds.forEach((panelId) => store.completedPanelIds.add(panelId));
      return fulfillJson(route, {
        operationId: body.operationId,
        completedPanelCount: body.panelIds.length,
        generatedWorkItemCount: body.panelIds.length,
        projectKittingCompleted: store.completedPanelIds.size === 4,
        replayed: false
      });
    }

    return fulfillJson(route, { title: 'not found' }, 404);
  });
}

function kittingQueue(store: ReturnType<typeof createKittingStore>) {
  const readyPanels = Array.from({ length: 4 }, (_, index) => {
    const panelId = `72000000-0000-0000-0000-00000000000${index + 1}`;
    const completed = store.completedPanelIds.has(panelId);
    return {
      panelId,
      displayCode: `PANEL-${String(index + 1).padStart(2, '0')}`,
      panelName: `MCC-${String.fromCharCode(65 + index)}`,
      panelInfoCompleted: true,
      kittingCompleted: completed,
      completedAtUtc: completed ? '2026-07-17T08:00:00Z' : null,
      completedByDisplayName: completed ? '합성 자재 담당자' : null,
      selectable: !completed
    };
  });

  return {
    projects: [
      {
        projectId: readyProjectId,
        projectCode: 'KIT-010A',
        projectTitle: '합성 모바일 패널 키팅',
        activeItemCount: 3,
        completedItemCount: 3,
        ready: true,
        pendingPanelCount: 4 - store.completedPanelIds.size,
        completedPanelCount: store.completedPanelIds.size,
        panels: readyPanels
      },
      {
        projectId: waitingProjectId,
        projectCode: 'KIT-WAIT',
        projectTitle: '입고 대기 프로젝트',
        activeItemCount: 3,
        completedItemCount: 1,
        ready: false,
        pendingPanelCount: 2,
        completedPanelCount: 0,
        panels: Array.from({ length: 2 }, (_, index) => ({
          panelId: `73000000-0000-0000-0000-00000000000${index + 1}`,
          displayCode: `WAIT-${index + 1}`,
          panelName: `대기 패널 ${index + 1}`,
          panelInfoCompleted: true,
          kittingCompleted: false,
          completedAtUtc: null,
          completedByDisplayName: null,
          selectable: false
        }))
      }
    ]
  };
}

function currentUser(userKey: string) {
  const principal = {
    userId: userKey === 'dev-materials'
      ? '50000000-0000-0000-0000-000000000012'
      : '50000000-0000-0000-0000-000000000002',
    developmentUserKey: userKey,
    displayName: userKey === 'dev-materials' ? '합성 자재 담당자' : '합성 영업 담당자',
    email: null,
    authProvider: 'Dev',
    isActive: true,
    approvalPending: false,
    department: userKey === 'dev-materials' ? 'Materials' : 'Sales',
    roles: [userKey.replace('dev-', '')]
  };
  return {
    ...principal,
    permissions: userKey === 'dev-materials'
      ? ['projects.read', 'Project.Read.All', 'MaterialReceipt.Update']
      : ['projects.read', 'Project.Read.All', 'Project.Create'],
    projectAccess: [],
    isTestUserSwitch: false,
    testUserKey: null,
    canUseAdminTestUserSwitch: false,
    actualUser: principal,
    effectiveUser: principal
  };
}

const corsHeaders = {
  'Access-Control-Allow-Headers': 'content-type,x-dev-user',
  'Access-Control-Allow-Methods': 'GET,POST,OPTIONS',
  'Access-Control-Allow-Origin': '*'
};

function fulfillJson(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
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
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled' });
}
