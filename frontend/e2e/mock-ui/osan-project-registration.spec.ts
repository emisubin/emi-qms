import { expect, type Page, type Route, test } from '@playwright/test';

const projectId = '91000000-0000-0000-0000-000000000001';
const stepNames = ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'];

test('Osan registration is responsive and creates one project with preserved values and seven-step targets', async ({ page }, testInfo) => {
  const consoleErrors: string[] = [];
  const requestFailures: string[] = [];
  const postedBodies: Array<Record<string, unknown>> = [];
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('requestfailed', (request) => {
    requestFailures.push(`${request.method()} ${new URL(request.url()).pathname}`);
  });
  await installBackend(page, postedBodies);
  await page.addInitScript(() => {
    window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
  });

  await page.goto('/projects');
  await expect(page.getByRole('heading', { name: '오산 프로젝트' })).toBeVisible();
  await expect(page.getByText('등록된 프로젝트가 없습니다.')).toBeVisible();
  await page.getByRole('button', { name: '프로젝트 등록' }).first().click();

  const expectedFields = ['프로젝트 Title', '프로젝트 코드', '거래처', 'PO No', 'W/O No', '납기일', '제품명', '수량'];
  for (const field of expectedFields) {
    await expect(page.getByLabel(field, { exact: true })).toBeVisible();
  }
  await expect(page.locator('.osan-project-field')).toHaveCount(8);

  await page.getByLabel('프로젝트 Title').fill('  저장된 Title  ');
  await page.getByLabel('프로젝트 코드').fill('  AbC  001  ');
  await page.getByLabel('거래처').fill('  거래처  ');
  await page.getByLabel('PO No').fill('  001-PO/+  ');
  await page.getByLabel('W/O No').fill('  000-W/O  ');
  await page.getByLabel('납기일').fill('2026-12-31');
  await page.getByLabel('제품명').fill('  제품  이름  ');
  await page.getByLabel('수량').fill('2');
  await page.getByRole('button', { name: '프로젝트 등록' }).click();

  await expect(page.getByRole('heading', { name: '저장된 Title' })).toBeVisible();
  expect(postedBodies).toHaveLength(1);
  expect(postedBodies[0]).toMatchObject({
    title: '저장된 Title',
    projectCode: 'AbC  001',
    customerName: '거래처',
    poNumber: '001-PO/+',
    workOrderNumber: '000-W/O',
    deliveryDate: '2026-12-31',
    productName: '제품  이름',
    quantity: 2
  });
  expect(postedBodies[0].operationId).toMatch(/^[0-9a-f-]{36}$/i);

  const breadcrumbs = page.getByRole('navigation', { name: '현재 위치' });
  await expect(breadcrumbs).toBeVisible();
  const departmentTabs = page.getByRole('tablist', { name: '프로젝트 상세 섹션' });
  await expect(departmentTabs.getByRole('tab')).toHaveCount(1);
  const progressTab = departmentTabs.getByRole('tab', { name: '진행 관리' });
  await expect(progressTab).toHaveAttribute('aria-selected', 'true');
  await expect(progressTab).toHaveAttribute('aria-controls', 'osan-progress-panel');
  const progressPanel = page.getByRole('tabpanel', { name: '진행 관리' });
  await expect(progressPanel).toBeVisible();
  await expect(progressPanel).toHaveClass(/project-detail-tab-content/);
  await expect(progressPanel.locator('.project-department-section')).toHaveAttribute('data-department', 'manufacturing');
  await expect(progressPanel.locator('.project-department-metrics')).toBeVisible();
  const desktopTargetTable = progressPanel.getByRole('table', { name: '진행 관리 대상 현황' });
  await expect(desktopTargetTable).toBeVisible();
  await expect(desktopTargetTable).toHaveClass(/project-panel-status-table/);
  const desktopTargetRows = desktopTargetTable.getByRole('row');
  await expect(desktopTargetRows).toHaveCount(3);
  await expect(desktopTargetRows.first().getByRole('columnheader')).toHaveCount(5);
  for (let index = 1; index <= 2; index += 1) {
    await expect(desktopTargetRows.nth(index).getByRole('cell')).toHaveCount(5);
    await expect(desktopTargetRows.nth(index)).toContainText('시작 전');
    await expect(desktopTargetRows.nth(index)).toContainText('0/7단계 완료');
    await expect(desktopTargetRows.nth(index)).toContainText('입고검사');
    await expect(desktopTargetRows.nth(index).locator('.project-progress-meter')).toHaveAttribute('aria-label', /진행률 0% \(0\/7\)$/);
  }
  await expect(progressPanel.locator('.project-department-records')).toHaveCount(0);
  for (const forbiddenAction of ['Pending', '보류', '중단', '취소']) {
    await expect(page.getByRole('button', { name: forbiddenAction, exact: true })).toHaveCount(0);
  }
  await page.getByText('기본정보 전체 보기', { exact: true }).click();
  await expect(page.getByText('001-PO/+', { exact: true })).toBeVisible();
  await expect(page.getByText('000-W/O', { exact: true })).toBeVisible();
  const detailCode = page.locator('.project-summary-more dd.osan-project-code-value');
  expect(await detailCode.textContent()).toBe('AbC  001');
  expect(await detailCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await detailCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(page.locator('.project-summary-primary .status-badge')).toHaveText('시작 전');
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-detail-desktop.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '저장된 Title' })).toBeVisible();
  await expect(breadcrumbs).toBeHidden();
  await expect(page.getByRole('button', { name: '← 프로젝트' })).toBeVisible();
  const mobileDetailCode = page.locator('.mobile-detail-hero .osan-project-code-value');
  await expect(mobileDetailCode).toHaveText('AbC  001');
  expect(await mobileDetailCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await mobileDetailCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(page.locator('.mobile-detail-hero .status-badge')).toHaveText('시작 전');
  await expect(desktopTargetTable).toHaveCount(0);
  const mobileTargetCards = progressPanel.locator('.project-panel-status-cards');
  await expect(mobileTargetCards).toBeVisible();
  const mobileTargetItems = mobileTargetCards.locator('.project-panel-status-card');
  await expect(mobileTargetItems).toHaveCount(2);
  for (const target of await mobileTargetItems.all()) {
    await expect(target).toContainText('시작 전');
    await expect(target).toContainText('0/7단계 완료');
    await expect(target).toContainText('입고검사');
  }
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-detail-mobile-390.png'), fullPage: true });

  await page.getByRole('button', { name: '← 프로젝트' }).click();
  const mobileList = page.getByTestId('osan-project-list-mobile');
  await expect(mobileList).toBeVisible();
  await expect(mobileList).toHaveClass(/project-list-cards/);
  await expect(mobileList).toHaveClass(/project-list-mobile/);
  await expect(page.getByTestId('osan-project-list-desktop')).toBeHidden();
  await expect(mobileList.getByRole('article')).toHaveCount(1);
  await expect(mobileList.getByRole('article')).toHaveClass(/project-list-card/);
  const listCode = mobileList.locator('.osan-project-code-value');
  await expect(listCode).toBeVisible();
  expect(await listCode.textContent()).toBe('AbC  001');
  expect(await listCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await listCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(mobileList.getByText('시작 전')).toBeVisible();
  await expect(mobileList.getByText('0%')).toBeVisible();
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-list-mobile-390.png'), fullPage: true });

  await page.setViewportSize({ width: 1280, height: 720 });
  const desktopList = page.getByTestId('osan-project-list-desktop');
  await expect(desktopList).toBeVisible();
  await expect(desktopList).toHaveClass(/project-list-table/);
  await expect(desktopList).toHaveClass(/project-list-desktop/);
  await expect(mobileList).toBeHidden();
  await expect(desktopList.getByRole('row')).toHaveCount(2);
  await expect(desktopList.getByRole('columnheader')).toHaveCount(8);
  const desktopProjectRow = desktopList.getByRole('row', { name: '저장된 Title 상세 열기' });
  await expect(desktopProjectRow).toHaveClass(/project-list-row/);
  await expect(desktopProjectRow.getByRole('cell')).toHaveCount(8);
  const desktopListCode = desktopProjectRow.locator('.osan-project-code-value');
  expect(await desktopListCode.textContent()).toBe('AbC  001');
  expect(await desktopListCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await desktopListCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(desktopProjectRow.getByText('시작 전')).toBeVisible();
  await expect(desktopProjectRow.getByText('0%')).toBeVisible();
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-list-desktop.png'), fullPage: true });
  expect(consoleErrors).toEqual([]);
  expect(requestFailures).toEqual([]);
});

async function installBackend(page: Page, postedBodies: Array<Record<string, unknown>>) {
  let projectCreated = false;
  await page.route('http://localhost:5080/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === '/health/ready') return fulfillJson(route, { status: 'ready', database: { reason: 'reachable' } });
    if (path === '/api/runtime-mode') {
      return fulfillJson(route, {
        mode: 'Development', reviewSafe: false, mutationAllowed: true, databaseReadOnly: false, ready: true
      });
    }
    if (path === '/api/me') return fulfillJson(route, currentUser());
    if (path === '/api/osan/projects' && request.method() === 'GET') {
      return fulfillJson(route, { items: projectCreated ? [projectDetail()] : [] });
    }
    if (path === '/api/osan/projects' && request.method() === 'POST') {
      const body = request.postDataJSON() as Record<string, unknown>;
      postedBodies.push(body);
      projectCreated = true;
      return fulfillJson(route, {
        operationId: body.operationId,
        replayed: false,
        project: projectDetail()
      }, 201);
    }
    if (path === `/api/osan/projects/${projectId}`) return fulfillJson(route, projectDetail());
    return fulfillJson(route, { title: 'closed in synthetic Osan scope' }, 404);
  });
}

function currentUser() {
  const principal = {
    userId: '50000000-0000-0000-0000-000000000001',
    developmentUserKey: 'dev-admin',
    displayName: 'Synthetic Osan Admin',
    email: null,
    authProvider: 'Dev',
    isActive: true,
    approvalPending: false,
    department: 'management-support',
    departmentName: '경영지원',
    profilePhotoVersion: null,
    roles: ['system-administrator']
  };
  return {
    ...principal,
    permissions: ['projects.read', 'Project.Create', 'Project.Read.All'],
    projectAccess: [],
    isTestUserSwitch: false,
    testUserKey: null,
    canUseAdminTestUserSwitch: false,
    actualUser: principal,
    effectiveUser: principal,
    businessUnitAccess: {
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    }
  };
}

function projectDetail() {
  return {
    projectId,
    title: '저장된 Title',
    projectCode: 'AbC  001',
    customerName: '거래처',
    poNumber: '001-PO/+',
    workOrderNumber: '000-W/O',
    deliveryDate: '2026-12-31',
    productName: '제품  이름',
    quantity: 2,
    status: 'Active',
    createdAtUtc: '2026-09-07T00:00:00Z',
    targets: Array.from({ length: 2 }, (_, targetIndex) => ({
      targetId: `92000000-0000-0000-0000-${String(targetIndex + 1).padStart(12, '0')}`,
      sequenceNumber: targetIndex + 1,
      displayName: `제품  이름 ${targetIndex + 1}`,
      status: 'NotStarted',
      steps: stepNames.map((stepName, stepIndex) => ({
        stepId: `${93000000 + targetIndex}-${String(stepIndex + 1).padStart(4, '0')}-0000-0000-000000000001`,
        sequenceNumber: stepIndex + 1,
        stepCode: `STEP_${stepIndex + 1}`,
        stepName,
        status: 'NotStarted'
      }))
    }))
  };
}

function fulfillJson(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    contentType: 'application/json',
    headers: {
      'Access-Control-Allow-Origin': 'http://127.0.0.1:5173',
      'Access-Control-Allow-Headers': 'Content-Type, X-Dev-User, X-Qms-Business-Unit'
    },
    body: JSON.stringify(body)
  });
}

function hasHorizontalOverflow(page: Page) {
  return page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
}
