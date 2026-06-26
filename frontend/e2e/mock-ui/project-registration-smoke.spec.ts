import { expect, type Page, type Route, test } from '@playwright/test';

const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const projectId = '71000000-0000-0000-0000-000000000010';

type ProjectRecord = {
  projectId: string;
  customerName: string;
  item: string;
  projectCode: string;
  projectTitle: string;
  activePanelCount: number;
  qrEligibleCount: number;
  manufacturingCompletedCount: number;
  inspectionCompletedCount: number;
  deliveryDate: string;
  salesOwnerUserId: string;
  salesOwnerName: string;
  packagingMethod: 'WoodenCrate' | 'StretchWrap' | 'HeavyDutyBox' | null;
  deliveryLocation: string | null;
  status: 'Active' | 'OnHold' | 'Cancelled' | 'Completed';
  projectWorkStatus: 'BeforeManufacturing' | 'OnHold' | 'Cancelled' | 'Completed';
  projectProgressPercent: number | null;
  createdAt: string;
  updatedAt: string;
  salesAmount?: number;
  currencyCode?: string;
  statusReason: string | null;
};

type PanelRecord = {
  panelId: string;
  projectId: string;
  sequenceNumber: number;
  displayCode: string;
  panelName: string | null;
  width: number | null;
  height: number | null;
  depth: number | null;
  panelStatus: 'Active' | 'Cancelled';
  workflowStage: 'BeforeManufacturing' | 'ManufacturingInProgress' | 'ManufacturingCompleted' | 'InspectionInProgress' | 'InspectionCompleted' | 'PackingCompleted' | 'ShipmentCompleted';
  panelInfoCompleted: boolean;
  qrEligible: boolean;
  createdAt: string;
  updatedAt: string;
};

test('mock UI smoke: Sales registers a project, manufacturing can read it, and Sales can increase panels and hold it', async ({ page }) => {
  const store = createStore();
  await routeApi(page, store);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');

  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, 'PJT-003A', 'TASK-003A E2E', '4');
  await page.getByRole('button', { name: '등록' }).click();

  await expect(page.getByRole('heading', { name: 'TASK-003A E2E' })).toBeVisible();
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('제조 전');
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('생성 불가');

  await page.getByRole('button', { name: '목록' }).click();
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, 'PJT-DUP', ' task-003a   e2e ', '2');
  await page.getByRole('button', { name: '등록' }).click();
  await expect(page.getByText('동일한 PJT Title이 이미 존재합니다.')).toBeVisible();

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await page.locator('.project-list-row').filter({ hasText: 'TASK-003A E2E' }).click();
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('미입력');
  await expect(page.getByText('KRW 1,250,000.5')).toHaveCount(0);
  await expect(page.getByRole('button', { name: '수정', exact: true })).toHaveCount(0);

  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await page.locator('.project-list-row').filter({ hasText: 'TASK-003A E2E' }).click();
  await page.getByRole('button', { name: '수정', exact: true }).click();
  await page.getByLabel('면수*').fill('5');
  await page.getByLabel('수정사유*').fill('추가 발주 면수 반영');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('5');

  await page.getByRole('button', { name: '보류' }).click();
  await page.getByLabel('사유').fill('고객 일정 확인');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: '보류' })).toBeVisible();
});

async function fillProjectForm(page: Page, projectCode: string, projectTitle: string, panelCount: string) {
  await page.getByLabel('고객사*').fill('EMI Test Customer');
  await page.getByLabel('Item*').fill('Control Panel');
  await page.getByLabel('PJT Code*').fill(projectCode);
  await page.getByLabel('PJT Title*').fill(projectTitle);
  await page.getByLabel('면수*').fill(panelCount);
  await page.getByLabel('납기일*').fill('2026-10-10');
  await page.getByLabel('영업담당자*').selectOption(salesOwnerId);
  await page.getByLabel('포장방식*').selectOption('WoodenCrate');
  await page.getByLabel('판매금액').fill('1250000.5');
}

function createStore() {
  return {
    project: undefined as ProjectRecord | undefined,
    panels: [] as PanelRecord[]
  };
}

async function routeApi(page: Page, store: ReturnType<typeof createStore>) {
  await page.route('http://localhost:5080/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const method = request.method();
    const userKey = request.headers()['x-dev-user'] ?? 'dev-sales';

    if (method === 'OPTIONS') {
      return route.fulfill({ status: 204, headers: corsHeaders });
    }

    if (path === '/health/ready') {
      return fulfillJson(route, {
        name: 'ready',
        status: 'ok',
        database: { isReady: true, reason: 'reachable' },
        checkedAtUtc: '2026-06-25T00:00:00Z'
      });
    }

    if (path === '/api/me') {
      return fulfillJson(route, currentUser(userKey));
    }

    if (path === '/api/sales-owners') {
      return fulfillJson(route, [{ userId: salesOwnerId, displayName: 'Dev Sales User' }]);
    }

    if (path === '/api/projects' && method === 'POST') {
      const body = await request.postDataJSON() as {
        customerName: string;
        item: string;
        projectCode: string;
        projectTitle: string;
        panelCount: number;
        deliveryDate: string;
        packagingMethod: 'WoodenCrate' | 'StretchWrap' | 'HeavyDutyBox';
        salesAmount: number;
        currencyCode: string;
      };

      if (store.project && normalizeTitle(store.project.projectTitle) === normalizeTitle(body.projectTitle)) {
        return fulfillJson(route, { title: '동일한 PJT Title이 이미 존재합니다.' }, 409);
      }

      store.project = {
        projectId,
        customerName: body.customerName,
        item: body.item,
        projectCode: body.projectCode,
        projectTitle: body.projectTitle.trim(),
        activePanelCount: body.panelCount,
        qrEligibleCount: 0,
        manufacturingCompletedCount: 0,
        inspectionCompletedCount: 0,
        deliveryDate: body.deliveryDate,
        salesOwnerUserId: salesOwnerId,
        salesOwnerName: 'Dev Sales User',
        packagingMethod: body.packagingMethod,
        deliveryLocation: null,
        status: 'Active',
        projectWorkStatus: 'BeforeManufacturing',
        projectProgressPercent: 0,
        createdAt: '2026-06-25T00:00:00Z',
        updatedAt: '2026-06-25T00:00:00Z',
        salesAmount: body.salesAmount,
        currencyCode: body.currencyCode,
        statusReason: null
      };
      store.panels = createPanels(body.panelCount);

      return fulfillJson(route, filterProject(store.project, userKey), 201);
    }

    if (path === '/api/projects' && method === 'GET') {
      const items = store.project ? [filterProject(store.project, userKey)] : [];
      return fulfillJson(route, { items, page: 1, pageSize: 20, totalCount: items.length });
    }

    if (path === `/api/projects/${projectId}` && method === 'GET') {
      return fulfillJson(route, filterProject(requireProject(store), userKey));
    }

    if (path === `/api/projects/${projectId}` && method === 'PATCH') {
      const body = await request.postDataJSON() as Partial<ProjectRecord>;
      store.project = { ...requireProject(store), ...body, updatedAt: '2026-06-25T00:01:00Z' };
      return fulfillJson(route, filterProject(store.project, userKey));
    }

    if (path === `/api/projects/${projectId}/change-panel-count` && method === 'POST') {
      const body = await request.postDataJSON() as { panelCount: number; expectedActivePanelCount: number };
      const project = requireProject(store);
      if (body.expectedActivePanelCount !== project.activePanelCount) {
        return fulfillJson(route, { title: '다른 사용자가 프로젝트 면수를 변경했습니다. 화면을 새로고침한 후 다시 시도해 주세요.' }, 409);
      }

      if (body.panelCount > store.panels.filter((panel) => panel.panelStatus === 'Active').length) {
        store.panels.push(...createPanels(body.panelCount).slice(store.panels.length));
      }
      store.project = { ...project, activePanelCount: body.panelCount, updatedAt: '2026-06-25T00:02:00Z' };
      return fulfillJson(route, filterProject(store.project, userKey));
    }

    if (path === `/api/projects/${projectId}/hold` && method === 'POST') {
      store.project = { ...requireProject(store), status: 'OnHold', projectWorkStatus: 'OnHold', projectProgressPercent: null, statusReason: '고객 일정 확인', updatedAt: '2026-06-25T00:03:00Z' };
      return fulfillJson(route, filterProject(store.project, userKey));
    }

    if (path === `/api/projects/${projectId}/panels`) {
      return fulfillJson(route, store.panels);
    }

    if (path === `/api/projects/${projectId}/panel-information`) {
      const panels = store.panels.filter((panel) => panel.panelStatus === 'Active');
      return fulfillJson(route, {
        projectId,
        projectStatus: requireProject(store).status,
        packagingMethod: requireProject(store).packagingMethod,
        activePanelCount: panels.length,
        panelInfoCompletedCount: 0,
        panelInfoPendingCount: panels.length,
        qrEligibleCount: 0,
        manufacturingCompletedCount: 0,
        inspectionCompletedCount: 0,
        duplicatePanelNameGroupCount: 0,
        projectPanelInformationCompleted: false,
        panelInformationStatusMessage: null,
        panels: panels.map((panel) => ({
          panelId: panel.panelId,
          projectId,
          sequenceNumber: panel.sequenceNumber,
          panelNumber: `No.${panel.sequenceNumber}`,
          displayCode: panel.displayCode,
          panelName: panel.panelName,
          displayName: `No.${panel.sequenceNumber} · ${panel.panelName ?? '패널명 미입력'}`,
          widthMm: panel.width,
          heightMm: panel.height,
          depthMm: panel.depth,
          panelStatus: panel.panelStatus,
          workflowStage: panel.workflowStage,
          panelInfoCompleted: panel.panelInfoCompleted,
          qrEligible: panel.qrEligible,
          hasDuplicateName: false,
          duplicateNameCount: 0,
          panelInfoVersion: 0,
          createdAt: panel.createdAt,
          updatedAt: panel.updatedAt,
          panelInfoUpdatedAtUtc: null,
          panelInfoUpdatedByUserId: null,
          panelInfoUpdatedByUserName: null
        }))
      });
    }

    if (path === `/api/projects/${projectId}/audit-history`) {
      return fulfillJson(route, {
        items: [{
          auditEventId: '73000000-0000-0000-0000-000000000001',
          entityType: 'Project',
          entityId: projectId,
          projectId,
          action: 'ProjectCreated',
          changedByUserId: salesOwnerId,
          changedByUserName: 'Dev Sales User',
          changedAtUtc: '2026-06-25T00:00:00Z',
          correlationId: 'e2e'
        }]
      });
    }

    return fulfillJson(route, { title: 'not found' }, 404);
  });
}

const corsHeaders = {
  'Access-Control-Allow-Headers': 'content-type,x-dev-user',
  'Access-Control-Allow-Methods': 'GET,POST,PATCH,OPTIONS',
  'Access-Control-Allow-Origin': '*'
};

function fulfillJson(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
}

function currentUser(userKey: string) {
  const permissions = ['projects.read', 'Project.Read.All'];
  if (userKey === 'dev-sales') {
    permissions.push('Project.Create', 'Project.Update', 'Project.Hold', 'Project.Cancel', 'Project.Delete', 'Project.Deleted.Read', 'Project.SalesAmount.Read', 'PanelInfo.Update');
  }
  if (userKey === 'dev-admin') {
    permissions.push('Project.Deleted.Read', 'Project.SalesAmount.Read');
  }

  return {
    developmentUserKey: userKey,
    displayName: userKey,
    department: 'test',
    roles: [userKey.replace('dev-', '')],
    permissions,
    projectAccess: []
  };
}

function filterProject(project: ProjectRecord, userKey: string) {
  if (userKey === 'dev-sales' || userKey === 'dev-admin') {
    return project;
  }

  const safeProject = { ...project };
  delete safeProject.salesAmount;
  delete safeProject.currencyCode;
  return safeProject;
}

function createPanels(panelCount: number): PanelRecord[] {
  return Array.from({ length: panelCount }, (_, index) => ({
    panelId: `72000000-0000-0000-0000-00000000000${index + 1}`,
    projectId,
    sequenceNumber: index + 1,
    displayCode: index + 1 < 100 ? `P${String(index + 1).padStart(2, '0')}` : `P${index + 1}`,
    panelName: null,
    width: null,
    height: null,
    depth: null,
    panelStatus: 'Active',
    workflowStage: 'BeforeManufacturing',
    panelInfoCompleted: false,
    qrEligible: false,
    createdAt: '2026-06-25T00:00:00Z',
    updatedAt: '2026-06-25T00:00:00Z'
  }));
}

function normalizeTitle(value: string) {
  return value.trim().replace(/\s+/g, ' ').toLocaleUpperCase('en-US');
}

function requireProject(store: ReturnType<typeof createStore>) {
  if (!store.project) {
    throw new Error('Project fixture was not created.');
  }

  return store.project;
}
