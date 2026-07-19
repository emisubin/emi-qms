import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const artifactDirectory = '/tmp/emi-qms-export-001-all-pages';
const screenshotDirectory = path.join(artifactDirectory, 'screenshots');
const workbookDirectory = path.join(artifactDirectory, 'workbooks');
const columnPickerScreenshotDirectory = path.resolve(process.cwd(), '../tasks/export-001-change-003-screenshots');

const pages = [
  { route: '/projects', name: '01-projects', userKey: 'dev-sales' },
  { route: '/my-work', name: '02-my-work', userKey: 'dev-design' },
  { route: '/production-planning', name: '03-production-planning', userKey: 'dev-production' },
  { route: '/procurement', name: '04-procurement', userKey: 'dev-procurement' },
  { route: '/materials/receipts', name: '05-material-receipts', userKey: 'dev-materials' },
  { route: '/materials/kitting', name: '06-material-kitting', userKey: 'dev-materials' },
  { route: '/manufacturing/work', name: '07-manufacturing', userKey: 'dev-manufacturing' },
  { route: '/quality/iqc', name: '08-material-iqc', userKey: 'dev-quality' },
  { route: '/quality/inspections', name: '09-quality-inspections', userKey: 'dev-quality' },
  { route: '/logistics', name: '10-logistics', userKey: 'dev-logistics' },
  { route: '/pending', name: '11-pending', userKey: 'dev-quality' },
  { route: '/notifications', name: '12-notifications', userKey: 'dev-design' },
  { route: '/admin/users', name: '13-admin-users', userKey: 'dev-admin' },
  { route: '/admin/departments', name: '14-admin-departments', userKey: 'dev-admin' },
  { route: '/admin/calendar/holidays', name: '15-admin-calendar-holidays', userKey: 'dev-admin' },
  { route: '/admin/permissions', name: '16-admin-permissions', userKey: 'dev-admin' },
  { route: '/admin/history/master-data', name: '17-admin-master-history', userKey: 'dev-admin' },
  { route: '/admin/history/work-items', name: '18-admin-work-history', userKey: 'dev-admin' },
  { route: '/admin/system/notification-deliveries', name: '19-admin-notification-deliveries', userKey: 'dev-admin' },
  { route: '/admin/system/work-item-escalations', name: '20-admin-work-item-escalations', userKey: 'dev-admin' }
] as const;

test('TASK-EXPORT-001 change-003: every selected export page uses the server column picker', async ({ page, request }) => {
  test.setTimeout(480_000);
  await fs.rm(artifactDirectory, { force: true, recursive: true });
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await fs.mkdir(workbookDirectory, { recursive: true });
  await fs.mkdir(columnPickerScreenshotDirectory, { recursive: true });
  seedExternalAdminUser();
  const syntheticProject = await createQualityReadyProject(request, Date.now());
  await completeManufacturing(request, syntheticProject.projectId, syntheticProject.panelId);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');

  let downloadedWorkbookCount = 0;
  let pickerScreenshotCount = 0;
  let customColumnSelectionCount = 0;
  const emptyDesktopRoutes: string[] = [];
  for (const target of pages) {
    await selectDevelopmentUser(page, target.userKey);
    await page.goto(target.route);
    await waitForPageData(page);
    const tray = page.locator('.selected-export-tray').first();
    if (await tray.count() === 0) {
      emptyDesktopRoutes.push(target.route);
      await capture(page, `desktop-${target.name}.png`);
      continue;
    }
    await expect(tray, `${target.route} selected export tray`).toBeVisible();
    await expect(tray.getByText('전체선택', { exact: true })).toBeVisible();
    await expect(tray.getByRole('button', { name: '선택 Excel 내보내기', exact: true })).toHaveCount(1);
    await expect(page.getByRole('button', { name: 'Excel 내보내기', exact: true })).toHaveCount(0);

    const pickerTrigger = tray.getByRole('button', { name: /컬럼 선택/ });
    await expect(pickerTrigger).toBeVisible();
    await pickerTrigger.click();
    const picker = page.getByRole('dialog', { name: '내보낼 컬럼 선택' });
    await expect(picker).toBeVisible();
    const columnCheckboxes = picker.getByRole('checkbox');
    await expect(columnCheckboxes.first()).toBeVisible();
    expect(await columnCheckboxes.count()).toBeGreaterThan(1);
    expect(await picker.locator('input[type="checkbox"]:disabled').count()).toBeGreaterThan(0);
    const expectedHeaderCount = await columnCheckboxes.count();
    if (target.name === '05-material-receipts' || target.name === '13-admin-users') {
      const optionalColumn = picker.locator('input[type="checkbox"]:not(:disabled)').last();
      await optionalColumn.uncheck();
      customColumnSelectionCount += 1;
    }
    await captureAt(page, path.join(columnPickerScreenshotDirectory, `${target.name}-column-picker-desktop-1440.png`));
    pickerScreenshotCount += 1;
    await page.keyboard.press('Escape');
    await expect(picker).toBeHidden();

    const selectAll = tray.getByRole('checkbox', { name: '현재 목록 전체 선택', exact: true });
    await expect(selectAll).toHaveCount(1);
    if (await selectAll.isEnabled()) {
      await selectAll.check();
      await expect(tray.getByText(/^[1-9][0-9]*개 선택$/)).toBeVisible();
      const [download] = await Promise.all([
        page.waitForEvent('download'),
        tray.getByRole('button', { name: '선택 Excel 내보내기', exact: true }).click()
      ]);
      const workbookPath = path.join(workbookDirectory, `${target.name}.xlsx`);
      await download.saveAs(workbookPath);
      verifyFormulaSafeWorkbook(
        workbookPath,
        expectedHeaderCount - (target.name === '05-material-receipts' || target.name === '13-admin-users' ? 1 : 0)
      );
      downloadedWorkbookCount += 1;
    }

    await capture(page, `desktop-${target.name}.png`);
  }

  await fs.writeFile(
    path.join(artifactDirectory, 'empty-desktop-routes.json'),
    `${JSON.stringify(emptyDesktopRoutes, null, 2)}\n`,
    'utf8'
  );
  expect(downloadedWorkbookCount).toBeGreaterThanOrEqual(10);
  expect(pickerScreenshotCount).toBe(20);
  expect(customColumnSelectionCount).toBe(2);

  await page.setViewportSize({ width: 390, height: 844 });
  for (const target of pages) {
    await selectDevelopmentUser(page, target.userKey);
    await page.goto(target.route);
    await waitForPageData(page);
    const tray = page.locator('.selected-export-tray').first();
    if (await tray.count() > 0) {
      await expect(tray, `${target.route} mobile selected export tray`).toBeHidden();
    }
    await expect(page.getByRole('button', { name: '선택 Excel 내보내기', exact: true })).toHaveCount(0);
    await assertNoHorizontalOverflow(page);
    await capture(page, `mobile-${target.name}.png`);
    if (target.name === '01-projects') {
      await captureAt(page, path.join(columnPickerScreenshotDirectory, '21-projects-mobile-no-export-390.png'));
    }
  }
});

function verifyFormulaSafeWorkbook(filePath: string, expectedHeaderCount: number) {
  const worksheetXml = execFileSync('unzip', ['-p', filePath, 'xl/worksheets/sheet1.xml'], { encoding: 'utf8' });
  expect(worksheetXml).not.toMatch(/<f(?:\s|>)/);
  expect(worksheetXml.match(/<(?:[a-z]+:)?c\b[^>]*\br="[A-Z]+5"/gi)?.length ?? 0).toBe(expectedHeaderCount);
}

async function selectDevelopmentUser(page: Page, userKey: string) {
  await page.evaluate((nextUserKey) => {
    window.localStorage.setItem('emi-qms-development-user-key', nextUserKey);
  }, userKey);
}

async function waitForPageData(page: Page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(250);
}

async function capture(page: Page, fileName: string) {
  await captureAt(page, path.join(screenshotDirectory, fileName));
}

async function captureAt(page: Page, outputPath: string) {
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({
    path: outputPath,
    animations: 'disabled',
    fullPage: true
  });
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}

async function createQualityReadyProject(request: APIRequestContext, unique: number) {
  const created = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Selected Export Customer',
      item: 'RPP',
      projectCode: `ALL-EXPORT-${unique}`,
      projectTitle: `합성 전체 선택 내보내기 ${unique}`,
      panelCount: 1,
      deliveryDate: '2026-12-31',
      salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
      packagingMethod: 'StretchWrap',
      salesAmount: null,
      currencyCode: null,
      deliveryLocation: 'Synthetic Site',
      fatRequired: false
    }
  });
  expect(created.ok(), await created.text()).toBeTruthy();
  const projectId = (await created.json() as { projectId: string }).projectId;

  await postJson(request, '/api/admin/notification-deliveries/send-manual', 'dev-admin', {
    sendMode: 'WorkAssignment',
    notificationKind: 'WorkItemAssigned',
    projectId,
    title: '합성 선택 내보내기 검수 업무',
    message: '격리 E2E에서 내 업무 선택 내보내기 화면을 검수합니다.',
    channels: [],
    workAssigneeUserIds: ['50000000-0000-0000-0000-000000000010'],
    workflowStageCode: 'ProductionPlanning',
    dueDate: '2026-12-30'
  });

  const procurement = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: { items: [{ orderItem: 'Synthetic Selected Export Material' }] }
  });
  expect(procurement.ok(), await procurement.text()).toBeTruthy();

  queryDatabase(`
    update panel_placeholders
    set panel_name = 'EXPORT-PANEL', width_mm = 600, height_mm = 1800, depth_mm = 400,
        panel_info_completed = true
    where project_id = '${projectId}' and status = 'Active';
    begin;
    select set_config('emi_qms.material_receipt_write', 'allowed', true);
    update project_procurement_items
    set receipt_completed = true,
        receipt_completed_at_utc = now(),
        receipt_completed_by_user_id = '50000000-0000-0000-0000-000000000012'
    where project_id = '${projectId}' and status = 'Active';
    commit;
  `);
  const panelId = queryDatabase(`select id::text from panel_placeholders where project_id = '${projectId}' and status = 'Active';`);

  const kitting = await request.post(`${apiBaseUrl}/api/materials/kitting/complete`, {
    headers: devHeaders('dev-materials'),
    data: { operationId: crypto.randomUUID(), projectId, panelIds: [panelId] }
  });
  expect(kitting.ok(), await kitting.text()).toBeTruthy();
  return { projectId, panelId };
}

async function completeManufacturing(request: APIRequestContext, projectId: string, panelId: string) {
  const started = await postJson(request, '/api/manufacturing/executions/start', 'dev-manufacturing', {
    operationId: crypto.randomUUID(), projectId, panelId
  }) as { executionId: string; version: number };
  const detailResponse = await request.get(`${apiBaseUrl}/api/manufacturing/panels/${panelId}`, {
    headers: devHeaders('dev-manufacturing')
  });
  expect(detailResponse.ok(), await detailResponse.text()).toBeTruthy();
  const detail = await detailResponse.json() as { steps: Array<{ stepId: string }> };
  let version = started.version;
  for (const step of detail.steps) {
    const checked = await postJson(
      request,
      `/api/manufacturing/executions/${started.executionId}/check-step`,
      'dev-manufacturing',
      { operationId: crypto.randomUUID(), stepId: step.stepId, expectedVersion: version }
    ) as { version: number };
    version = checked.version;
  }
  await postJson(
    request,
    `/api/manufacturing/executions/${started.executionId}/complete`,
    'dev-manufacturing',
    { operationId: crypto.randomUUID(), expectedVersion: version }
  );
}

function seedExternalAdminUser() {
  queryDatabase(`
    insert into qms_users (
      id, development_user_key, display_name, department_id, is_active,
      auth_provider, entra_object_id, email
    ) values (
      '5e000000-0000-0000-0000-000000000001',
      'e2e-selected-export-user',
      'Synthetic Export User',
      '10000000-0000-0000-0000-000000000001',
      true,
      'EntraId',
      'synthetic-export-object',
      'synthetic-export@example.invalid'
    );
  `);
}

async function postJson(request: APIRequestContext, route: string, userKey: string, data: unknown) {
  const response = await request.post(`${apiBaseUrl}${route}`, { headers: devHeaders(userKey), data });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}

function queryDatabase(sql: string) {
  return execFileSync(
    'docker',
    [
      'compose',
      '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'),
      '--file', requireEnv('E2E_COMPOSE_FILE'),
      'exec',
      '-T', requireEnv('E2E_POSTGRES_SERVICE'),
      'psql',
      '--username', requireEnv('E2E_DATABASE_USER'),
      '--dbname', requireEnv('E2E_DATABASE_NAME'),
      '--no-psqlrc',
      '--tuples-only',
      '--no-align',
      '--set', 'ON_ERROR_STOP=1',
      '--command', sql
    ],
    { encoding: 'utf8' }
  ).trim().split('\n').at(-1)?.trim() ?? '';
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for isolated full-stack validation.`);
  return value;
}
