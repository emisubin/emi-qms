import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, type Page, type Route, test } from '@playwright/test';

const screenshotDirectory = process.env.TASK010A_SCREENSHOT_DIR?.trim()
  ? path.resolve(process.env.TASK010A_SCREENSHOT_DIR)
  : path.resolve(process.cwd(), '../tasks/010a-screenshots');
const change004ScreenshotDirectory = path.resolve(process.cwd(), '../tasks/010a-change-004-screenshots');
const readyProjectId = '71000000-0000-0000-0000-000000000010';
const waitingProjectId = '71000000-0000-0000-0000-000000000011';

test('TASK-010A mock visual: adaptive panel kitting page and mobile drawer', async ({ page }) => {
  const store = createKittingStore();
  await routeApi(page, store);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/materials/kitting?project=${readyProjectId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await page.goto(`/materials/kitting?project=${readyProjectId}`);
  await expect(page.getByRole('heading', { name: '패널 키팅', exact: true })).toBeVisible();
  await expect(page.getByText('전체 입고 완료 · 실제 키팅을 마친 패널만 알려 주세요.')).toBeVisible();
  await expect(page.locator('.kitting-panel-card')).toHaveCount(4);
  await capture(page, '01-panel-kitting-desktop-1440.jpg');

  await page.locator('.kitting-panel-card').nth(0).click();
  await page.locator('.kitting-panel-card').nth(1).click();
  await expect(page.getByRole('button', { name: '2면 키팅 완료 알림' })).toBeEnabled();
  await page.getByRole('button', { name: '2면 키팅 완료 알림' }).click();
  await expect(page.getByText('2면의 키팅 완료 상태를 공유했습니다.')).toBeVisible();
  await capture(page, '05-panel-kitting-success-desktop-1440.jpg', screenshotDirectory.replace('010a-screenshots', 'ux-001-a2-screenshots'));

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/materials/kitting?project=${readyProjectId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('button', { name: '메뉴 열기' })).toBeVisible();
  await expect(page.locator('.kitting-panel-card[data-completed="true"]')).toHaveCount(2);
  expect(await page.locator('.kitting-panel-card').evaluateAll((cards) =>
    new Set(cards.map((card) => card.getAttribute('data-shape-role'))).size
  )).toBeGreaterThanOrEqual(2);
  await assertNoHorizontalOverflow(page);
  await capture(page, '02-panel-kitting-mobile-390.jpg');

  await page.locator('.kitting-panel-card:not([data-completed="true"])').first().click();
  await expect(page.getByRole('button', { name: '1면 키팅 완료 알림' })).toBeEnabled();
  await capture(page, '03-panel-kitting-selected-mobile-390.jpg');

  await page.getByRole('button', { name: '메뉴 열기' }).click();
  const menu = page.getByRole('dialog', { name: '전체 업무 메뉴' });
  await expect(menu).toBeVisible();
  await expect(menu.getByRole('button', { name: '자재' })).toHaveAttribute('aria-current', 'page');
  await expect(menu.getByRole('button', { name: '키팅' })).toHaveCount(0);
  await capture(page, '04-panel-kitting-menu-mobile-390.jpg');
});

test('TASK-010A Change 004 mock visual: production planning and manufacturing release tabs', async ({ page }) => {
  const store = createKittingStore();
  await routeApi(page, store);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/production-planning');
  await page.getByLabel('개발 사용자').selectOption('dev-production');
  await page.goto('/production-planning');
  await expect(page.getByRole('heading', { name: '생산관리', exact: true })).toBeVisible();
  await expect(page.getByRole('tab', { name: /생산계획/u })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByLabel('생산계획 요약')).toBeVisible();
  await capture(page, '01-production-planning-tab-desktop-1440.jpg', change004ScreenshotDirectory);

  await page.getByRole('tab', { name: /제조 투입/u }).click();
  await expect(page.getByRole('tab', { name: /제조 투입/u })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByLabel('생산계획 요약')).toHaveCount(0);
  await page.getByRole('row', { name: /합성 제조 투입 프로젝트/u }).click();
  const releasePanel = page.getByLabel('패널 제조 투입 요청');
  await expect(releasePanel.getByRole('heading', { name: '제조 투입 요청' })).toBeVisible();
  await expect(releasePanel.getByText('자재 입고 7/10')).toBeVisible();
  await capture(page, '02-manufacturing-release-tab-desktop-1440.jpg', change004ScreenshotDirectory);

  await releasePanel.getByRole('checkbox', { name: /PANEL-01/u }).check();
  await releasePanel.getByRole('checkbox', { name: /PANEL-02/u }).check();
  await releasePanel.getByRole('button', { name: '선택 2면 제조 투입 요청' }).click();
  await expect(releasePanel.getByText('2면을 제조팀에 투입 요청했습니다. 제조 업무 2건이 생성되었습니다.')).toBeVisible();
  await capture(page, '03-manufacturing-release-success-desktop-1440.jpg', change004ScreenshotDirectory);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/production-planning');
  await expect(page.getByRole('tab', { name: /생산계획/u })).toHaveAttribute('aria-selected', 'true');
  await capture(page, '04-production-planning-tab-mobile-390.jpg', change004ScreenshotDirectory, true);
  await page.getByRole('tab', { name: /제조 투입/u }).click();
  await page.getByRole('button', { name: '투입 패널 보기' }).click();
  await expect(page.getByLabel('패널 제조 투입 요청')).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '05-manufacturing-release-tab-mobile-390.jpg', change004ScreenshotDirectory, true);
});

function createKittingStore() {
  return {
    completedPanelIds: new Set<string>(),
    releasedPanelIds: new Set<string>()
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
        generatedWorkItemCount: 0,
        projectKittingCompleted: store.completedPanelIds.size === 4,
        replayed: false
      });
    }

    if (pathName === '/api/production-planning/summary') {
      return fulfillJson(route, { notPlannedCount: 0, planningCount: 1, plannedCount: 0, missingAssigneeProjectCount: 0 });
    }

    if (pathName === '/api/production-planning/projects') {
      return fulfillJson(route, {
        projects: [{
          projectId: readyProjectId,
          projectTitle: '합성 제조 투입 프로젝트',
          customerName: '합성 고객사',
          projectCode: 'REL-010A',
          item: 'UL67',
          activePanelCount: 4,
          deliveryDate: '2026-08-15',
          projectStatus: 'Active',
          planStatus: 'Planning',
          planStatusLabel: '작성 중',
          productTypeCode: 'UL67',
          productTypeName: 'UL67',
          requiredStepCount: 3,
          plannedRequiredStepCount: 2,
          assigneeCount: 6
        }]
      });
    }

    if (pathName === `/api/projects/${readyProjectId}/production-planning`) {
      return fulfillJson(route, productionPlanningResponse());
    }

    if (pathName === '/api/manufacturing/release-candidates' && request.method() === 'GET') {
      return fulfillJson(route, manufacturingReleaseQueue(store));
    }

    if (pathName === '/api/manufacturing/releases' && request.method() === 'POST') {
      const body = await request.postDataJSON() as { operationId: string; panelIds: string[] };
      body.panelIds.forEach((panelId) => store.releasedPanelIds.add(panelId));
      return fulfillJson(route, {
        operationId: body.operationId,
        releasedPanelCount: body.panelIds.length,
        generatedWorkItemCount: body.panelIds.length,
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
          selectable: true
        }))
      }
    ]
  };
}

function manufacturingReleaseQueue(store: ReturnType<typeof createKittingStore>) {
  return {
    projects: [{
      projectId: readyProjectId,
      projectCode: 'REL-010A',
      projectTitle: '합성 제조 투입 프로젝트',
      activeItemCount: 10,
      completedItemCount: 7,
      panels: Array.from({ length: 4 }, (_, index) => {
        const panelId = `72000000-0000-0000-0000-00000000000${index + 1}`;
        const released = store.releasedPanelIds.has(panelId);
        return {
          panelId,
          displayCode: `PANEL-${String(index + 1).padStart(2, '0')}`,
          panelName: `MCC-${String.fromCharCode(65 + index)}`,
          panelInfoCompleted: true,
          kittingCompleted: index === 0 || index === 2,
          released,
          workItemStatus: released ? 'Requested' : null,
          releasedAtUtc: released ? '2026-07-21T08:00:00Z' : null,
          selectable: !released
        };
      })
    }]
  };
}

function productionPlanningResponse() {
  const responsibilities = [
    ['SalesPrimary', '영업 정담당자', '합성 영업 담당자'],
    ['DesignPrimary', '설계 정담당자', '합성 설계 담당자'],
    ['ProductionPlanningPrimary', '생산관리 정담당자', '합성 생산관리 담당자'],
    ['ProcurementPrimary', '구매 정담당자', '합성 구매 담당자'],
    ['MaterialsPrimary', '자재 정담당자', '합성 자재 담당자'],
    ['ManufacturingPrimary', '제조 정담당자', '합성 제조 담당자']
  ];
  return {
    projectId: readyProjectId,
    projectTitle: '합성 제조 투입 프로젝트',
    projectCode: 'REL-010A',
    deliveryDate: '2026-08-15',
    planId: '77000000-0000-0000-0000-000000000301',
    rowVersion: 1,
    planStatus: 'Planning',
    planStatusLabel: '작성 중',
    productTypeId: '77000000-0000-0000-0000-000000000001',
    templateId: '77000000-0000-0000-0000-000000000101',
    productTypeCode: 'UL67',
    productTypeName: 'UL67',
    notes: '패널별 순차 투입',
    items: ['자재 입고', '조립 시작', '배선 시작'].map((stepName, index) => ({
      itemId: `77000000-0000-0000-0000-00000000040${index + 1}`,
      templateStepId: `77000000-0000-0000-0000-00000000020${index + 1}`,
      sequenceNumber: index + 1,
      stepName,
      isRequired: true,
      plannedDate: index === 1 ? null : `2026-08-0${index + 1}`,
      note: null,
      rowVersion: 0
    })),
    assignees: responsibilities.map(([responsibilityType, responsibilityLabel, assignedUserName], index) => ({
      assigneeId: `77000000-0000-0000-0000-0000000005${String(index + 1).padStart(2, '0')}`,
      responsibilityType,
      responsibilityLabel,
      assignedUserId: `50000000-0000-0000-0000-0000000000${index + 2}`,
      assignedUserName,
      note: responsibilityLabel,
      rowVersion: 0
    })),
    assigneeCandidates: [],
    fallbacks: []
  };
}

function currentUser(userKey: string) {
  const principal = {
    userId: userKey === 'dev-materials'
      ? '50000000-0000-0000-0000-000000000012'
      : userKey === 'dev-production'
        ? '50000000-0000-0000-0000-000000000003'
      : '50000000-0000-0000-0000-000000000002',
    developmentUserKey: userKey,
    displayName: userKey === 'dev-materials'
      ? '합성 자재 담당자'
      : userKey === 'dev-production'
        ? '합성 생산관리 담당자'
        : '합성 영업 담당자',
    email: null,
    authProvider: 'Dev',
    isActive: true,
    approvalPending: false,
    department: userKey === 'dev-materials' ? 'Materials' : userKey === 'dev-production' ? 'ProductionPlanning' : 'Sales',
    roles: [userKey.replace('dev-', '')]
  };
  return {
    ...principal,
    permissions: userKey === 'dev-materials'
      ? ['projects.read', 'Project.Read.All', 'MaterialReceipt.Update']
      : userKey === 'dev-production'
        ? ['projects.read', 'Project.Read.All', 'ProductionPlan.Update']
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

async function capture(page: Page, filename: string, targetDirectory = screenshotDirectory, fullPage = false) {
  await fs.mkdir(targetDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({ path: path.join(targetDirectory, filename), animations: 'disabled', fullPage });
}
