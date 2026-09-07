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

  const targets = page.locator('.osan-project-targets');
  await expect(targets.getByRole('article')).toHaveCount(2);
  for (const target of await targets.getByRole('article').all()) {
    await expect(target.getByRole('listitem')).toHaveCount(7);
    await expect(target.getByText('시작 전')).toHaveCount(8);
  }
  await expect(page.getByText('001-PO/+', { exact: true })).toBeVisible();
  await expect(page.getByText('000-W/O', { exact: true })).toBeVisible();
  const detailCode = page.locator('.osan-project-values dd.osan-project-code-value');
  expect(await detailCode.textContent()).toBe('AbC  001');
  expect(await detailCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-registration-desktop.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '저장된 Title' })).toBeVisible();
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-registration-mobile-390.png'), fullPage: true });

  await page.getByRole('button', { name: '목록으로' }).click();
  const listCode = page.locator('.osan-project-card__code.osan-project-code-value');
  await expect(listCode).toBeVisible();
  expect(await listCode.textContent()).toBe('AbC  001');
  expect(await listCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
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
