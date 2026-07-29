import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, type Page, type Route, test } from '@playwright/test';

const screenshotDirectory = path.resolve(process.cwd(), '../tasks/ux-001-a2-screenshots');
const projectId = '71000000-0000-0000-0000-000000000020';

test('UX-001 A2 mock visual: panel editor validation, Excel feedback, and mobile layout', async ({ page }) => {
  await routeApi(page);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/projects/${projectId}/panel-information/edit`);
  await expect(page.getByRole('heading', { name: '설계 정보 입력' })).toBeVisible();
  await expect(page.getByTestId('panel-info-edit-desktop')).toBeVisible();
  await capture(page, '01-panel-editor-desktop-1440.jpg');

  await page.getByLabel('No.1 패널명').fill('MCC-A REV');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  const validationFeedback = page.getByText('기존 설계 정보를 변경하려면 수정사유가 필요합니다.');
  await expect(validationFeedback).toBeVisible();
  await expect(validationFeedback).toHaveAttribute('data-tone', 'error');
  await capture(page, '02-panel-editor-validation-desktop-1440.jpg');

  await page.getByRole('button', { name: 'Excel 업로드' }).click();
  const dialog = page.getByRole('dialog', { name: 'Excel 업로드' });
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: 'Preview' }).click();
  const excelFeedback = dialog.getByText('Excel 파일을 선택하세요.');
  await expect(excelFeedback).toBeVisible();
  await expect(excelFeedback).toHaveAttribute('data-tone', 'error');
  await capture(page, '03-panel-excel-feedback-desktop-1440.jpg');
  await dialog.getByRole('button', { name: '닫기' }).click();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}/panel-information/edit`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByTestId('panel-info-edit-mobile')).toBeVisible();
  await expect(page.getByRole('button', { name: '메뉴 열기' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '04-panel-editor-mobile-390.jpg');
});

async function routeApi(page: Page) {
  await page.route('http://localhost:5080/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const routePath = url.pathname;

    if (request.method() === 'OPTIONS') {
      return route.fulfill({ status: 204, headers: corsHeaders });
    }
    if (routePath === '/health/ready') {
      return fulfillJson(route, {
        name: 'ready',
        status: 'ok',
        database: { isReady: true, reason: 'reachable' },
        checkedAtUtc: '2026-07-19T00:00:00Z'
      });
    }
    if (routePath === '/api/runtime-mode') {
      return fulfillJson(route, {
        mode: 'Development', reviewSafe: false, mutationAllowed: true,
        backgroundWorkersEnabled: false, externalProvidersEnabled: false,
        databaseReadOnly: false, migrationExecutionEnabled: true,
        environment: 'Development', ready: true, reason: 'not_applicable',
        expectedMigration: '0033_panel_kitting_handoff', actualMigration: '0033_panel_kitting_handoff',
        migrationLedgerStatus: 'Compatible', expectedMigrationCount: 33, actualMigrationCount: 33,
        missingMigrations: [], unexpectedMigrations: [], approvedLegacyMigrations: [],
        migrationSchemaCompatible: true, migrationLedgerReady: true
      });
    }
    if (routePath === '/api/me') return fulfillJson(route, currentUser());
    if (routePath === '/api/my-work/summary') {
      return fulfillJson(route, { requestedCount: 0, inProgressCount: 0, completedCount: 0, blockingCount: 0, assignedProjectCount: 0, assignedProjectBreakdown: [] });
    }
    if (routePath === '/api/notifications/summary') return fulfillJson(route, { unreadCount: 0, blockingCount: 0 });
    if (routePath === '/api/production-planning/product-types') return fulfillJson(route, []);
    if (routePath === `/api/projects/${projectId}`) return fulfillJson(route, project());
    if (routePath === `/api/projects/${projectId}/set-structure`) {
      return fulfillJson(route, {
        projectId,
        structureMode: 'FlatPanel',
        isLegacyFlat: false,
        canEditOrder: false,
        canEditDesign: true,
        specs: [],
        orderedProcurementItems: [],
        recoveryCases: []
      });
    }
    if (routePath === `/api/projects/${projectId}/panel-information`) return fulfillJson(route, panelInformation());
    return fulfillJson(route, { title: 'not found' }, 404);
  });
}

function currentUser() {
  const principal = {
    userId: '50000000-0000-0000-0000-000000000002',
    developmentUserKey: 'dev-sales',
    displayName: '합성 영업 담당자',
    email: null,
    authProvider: 'Dev',
    isActive: true,
    approvalPending: false,
    department: 'Sales',
    departmentName: '영업',
    profilePhotoVersion: null,
    roles: ['sales']
  };
  return {
    ...principal,
    permissions: ['projects.read', 'Project.Read.All', 'PanelInfo.Update'],
    projectAccess: [], isTestUserSwitch: false, testUserKey: null,
    canUseAdminTestUserSwitch: false, actualUser: principal, effectiveUser: principal
  };
}

function project() {
  return {
    projectId,
    customerName: 'WITHUS PANEL CUSTOMER',
    item: 'UL67',
    projectCode: 'UX-A2-001',
    projectTitle: '액션 피드백 디자인 검수',
    activePanelCount: 3,
    qrEligibleCount: 1,
    manufacturingCompletedCount: 0,
    inspectionCompletedCount: 0,
    deliveryDate: '2026-09-30',
    salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
    salesOwnerName: '합성 영업 담당자',
    packagingMethod: 'WoodenCrate',
    deliveryLocation: '목포장',
    status: 'Active',
    projectWorkStatus: 'ProductionPlanning',
    projectProgressPercent: 18,
    createdAt: '2026-07-19T00:00:00Z',
    updatedAt: '2026-07-19T00:00:00Z',
    salesAmount: 128000000,
    currencyCode: 'KRW',
    statusReason: null
  };
}

function panelInformation() {
  const panels = Array.from({ length: 3 }, (_, index) => {
    const completed = index === 0;
    return {
      panelId: `72000000-0000-0000-0000-00000000002${index + 1}`,
      projectId,
      sequenceNumber: index + 1,
      panelNumber: `No.${index + 1}`,
      displayCode: `P0${index + 1}`,
      panelName: completed ? 'MCC-A' : null,
      displayName: completed ? 'No.1 · MCC-A' : `No.${index + 1} · 패널명 미입력`,
      widthMm: completed ? 800 : null,
      heightMm: completed ? 1800 : null,
      depthMm: completed ? 400 : null,
      panelStatus: 'Active',
      workflowStage: 'BeforeManufacturing',
      panelInfoCompleted: completed,
      qrEligible: completed,
      hasDuplicateName: false,
      duplicateNameCount: 0,
      panelInfoVersion: completed ? 2 : 0,
      createdAt: '2026-07-19T00:00:00Z',
      updatedAt: '2026-07-19T00:00:00Z',
      panelInfoUpdatedAtUtc: completed ? '2026-07-19T01:00:00Z' : null,
      panelInfoUpdatedByUserId: completed ? '50000000-0000-0000-0000-000000000002' : null,
      panelInfoUpdatedByUserName: completed ? '합성 영업 담당자' : null
    };
  });
  return {
    projectId, projectStatus: 'Active', packagingMethod: 'WoodenCrate', activePanelCount: 3,
    panelInfoCompletedCount: 1, panelInfoPendingCount: 2, qrEligibleCount: 1,
    manufacturingCompletedCount: 0, inspectionCompletedCount: 0,
    duplicatePanelNameGroupCount: 0, projectPanelInformationCompleted: false,
    panelInformationStatusMessage: null, panels
  };
}

const corsHeaders = {
  'Access-Control-Allow-Headers': 'content-type,x-dev-user',
  'Access-Control-Allow-Methods': 'GET,POST,PATCH,OPTIONS',
  'Access-Control-Allow-Origin': '*'
};

function fulfillJson(route: Route, body: unknown, status = 200) {
  return route.fulfill({ status, headers: { ...corsHeaders, 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
}

async function assertNoHorizontalOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
}

async function capture(page: Page, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled' });
}
