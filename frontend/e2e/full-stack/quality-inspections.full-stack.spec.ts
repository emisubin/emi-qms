import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';

test('TASK-012A: LQC evidence, immutable report, manufacturing confirmation and OQC handoff', async ({ page, request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const { projectId, panelId } = await createManufacturingReadyProject(request, `QLT-${unique}`, `합성 품질검사 ${unique}`);
  await completeManufacturing(request, projectId, panelId);

  expect(queryDatabase(`
    select workflow_stage_code || '|' || status || '|' || assigned_user_id::text
    from work_items where target_id = '${panelId}' and workflow_stage_code = 'LQC';
  `)).toBe('LQC|Requested|50000000-0000-0000-0000-000000000005');
  const lqcQueueResponse = await request.get(`${apiBaseUrl}/api/quality/inspections/queue?stage=LQC&projectId=${projectId}`, {
    headers: devHeaders('dev-quality')
  });
  if (!lqcQueueResponse.ok()) throw new Error(`LQC queue failed (${lqcQueueResponse.status()}): ${await lqcQueueResponse.text()}`);
  const lqcQueue = await lqcQueueResponse.json() as { projects: Array<{ panels: Array<{ panelId: string; canMutate: boolean }> }> };
  expect(lqcQueue.projects.flatMap((project) => project.panels).find((panel) => panel.panelId === panelId)?.canMutate).toBe(true);

  const qualityStarted = await postJson(request, '/api/quality/inspections/start', 'dev-quality', {
    operationId: crypto.randomUUID(), projectId, panelId, stageCode: 'LQC'
  }) as { reportId: string; version: number };
  expect(qualityStarted.reportId).toMatch(/^[0-9a-f-]{36}$/u);

  const detailResponse = await request.get(`${apiBaseUrl}/api/quality/inspections/panels/${panelId}?stage=LQC`, {
    headers: devHeaders('dev-quality')
  });
  expect(detailResponse.ok()).toBeTruthy();
  const detail = await detailResponse.json() as {
    reportId: string;
    reportVersion: number;
    items: Array<{ itemId: string; responseType: 'Check' | 'Text'; isRequired: boolean }>;
  };
  const saved = await putJson(request, `/api/quality/inspections/reports/${detail.reportId}/responses`, 'dev-quality', {
    operationId: crypto.randomUUID(),
    expectedReportVersion: detail.reportVersion,
    responses: detail.items.filter((item) => item.isRequired).map((item) => ({
      templateItemId: item.itemId,
      checkResult: item.responseType === 'Check' ? 'Pass' : null,
      textValue: item.responseType === 'Text' ? '합성 확인' : null,
      note: null
    }))
  }) as { version: number };

  const tinyPng = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
  const photoResponse = await request.post(`${apiBaseUrl}/api/quality/inspections/reports/${detail.reportId}/photos`, {
    headers: devHeaders('dev-quality'),
    multipart: {
      operationId: crypto.randomUUID(),
      templateItemId: detail.items[0].itemId,
      expectedReportVersion: String(saved.version),
      altText: '합성 LQC 사진 증빙',
      photo: { name: 'synthetic.png', mimeType: 'image/png', buffer: tinyPng }
    }
  });
  expect(photoResponse.ok(), await photoResponse.text()).toBeTruthy();
  const photoResult = await photoResponse.json() as { version: number };

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/quality/inspections?stage=LQC&project=${projectId}&panel=${panelId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto(`/quality/inspections?stage=LQC&project=${projectId}&panel=${panelId}`);
  await expect(page.getByRole('heading', { name: '품질 검사' })).toBeVisible();
  await expect(page.locator('.quality-focus-card')).toBeVisible();
  await expect(page.locator('.quality-photo-list img')).toBeVisible();
  await expect(page.getByRole('button', { name: '판정 확정' })).toBeEnabled();
  await page.screenshot({ path: '../tasks/012a-screenshots/quality-inspections-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/quality/inspections?stage=LQC&project=${projectId}&panel=${panelId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.locator('.quality-focus-card')).toBeVisible();
  await expect(page.locator('.quality-photo-list img')).toBeVisible();
  await expect(page.getByRole('button', { name: '판정 확정' })).toBeEnabled();
  await assertNoHorizontalOverflow(page);
  await page.screenshot({ path: '../tasks/012a-screenshots/quality-inspections-mobile.png', fullPage: true });

  const finalized = await postJson(request, `/api/quality/inspections/reports/${detail.reportId}/finalize`, 'dev-quality', {
    operationId: crypto.randomUUID(),
    expectedReportVersion: photoResult.version,
    result: 'Passed',
    reason: '합성 LQC 검사 기준 적합',
    actionDepartmentCode: null,
    assigneeUserId: null
  }) as { nextStageCode: string };
  expect(finalized.nextStageCode).toBe('ManufacturingCompleted');

  const finalizedDetail = await request.get(`${apiBaseUrl}/api/quality/inspections/panels/${panelId}?stage=LQC`, {
    headers: devHeaders('dev-quality')
  });
  const finalizedPayload = await finalizedDetail.json() as {
    reportStatus: string;
    pdfStatus: string;
    photos: Array<{ photoId: string }>;
  };
  expect(finalizedPayload.reportStatus).toBe('Finalized');
  expect(finalizedPayload.pdfStatus).toBe('Ready');
  expect(finalizedPayload.photos).toHaveLength(1);

  const photoContent = await request.get(
    `${apiBaseUrl}/api/quality/inspections/reports/${detail.reportId}/photos/${finalizedPayload.photos[0].photoId}/content`,
    { headers: devHeaders('dev-quality') }
  );
  expect(photoContent.ok()).toBeTruthy();
  expect(photoContent.headers()['content-type']).toContain('image/png');
  const pdf = await request.get(`${apiBaseUrl}/api/quality/inspections/reports/${detail.reportId}/pdf`, {
    headers: devHeaders('dev-quality')
  });
  expect(pdf.ok()).toBeTruthy();
  expect(pdf.headers()['content-type']).toContain('application/pdf');

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await expect(page.getByRole('button', { name: '제조 완료 확인' })).toBeVisible();
  await expect(page.locator('.manufacturing-confirmation-card')).toBeVisible();
  await page.screenshot({ path: '../tasks/012a-screenshots/manufacturing-confirmation-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await expect(page.locator('.manufacturing-confirmation-card')).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await page.screenshot({ path: '../tasks/012a-screenshots/manufacturing-confirmation-mobile.png', fullPage: true });

  const confirmed = await postJson(request, '/api/quality/inspections/manufacturing-completed/confirm', 'dev-manufacturing', {
    operationId: crypto.randomUUID(), projectId, panelId
  }) as { nextStageCode: string };
  expect(confirmed.nextStageCode).toBe('OQC');
  expect(queryDatabase(`select count(*)::text from panel_manufacturing_completion_confirmations where panel_id = '${panelId}';`)).toBe('1');
  expect(queryDatabase(`select count(*)::text from work_items where target_id = '${panelId}' and workflow_stage_code = 'OQC';`)).toBe('1');

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/quality/inspections?stage=OQC&project=${projectId}&panel=${panelId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto(`/quality/inspections?stage=OQC&project=${projectId}&panel=${panelId}`);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/quality/inspections?stage=OQC&project=${projectId}&panel=${panelId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('heading', { name: '품질 검사' })).toBeVisible();
  await expect(page.getByRole('button', { name: /OQC 자체검수/u })).toHaveClass(/active/u);
  await expect(page.getByRole('button', { name: 'OQC 자체검수 시작' })).toBeVisible();
  await assertNoHorizontalOverflow(page);

  await postJson(request, '/api/quality/inspections/start', 'dev-quality', {
    operationId: crypto.randomUUID(), projectId, panelId, stageCode: 'OQC'
  });
  await postJson(request, `/api/projects/${projectId}/cancel`, 'dev-sales', {
    reason: '합성 품질검사 취소 정합 검증'
  });
  expect(queryDatabase(`select status from panel_quality_inspection_attempts where panel_id = '${panelId}' and stage_code = 'OQC';`)).toBe('Cancelled');
  expect(queryDatabase(`select status from work_items where target_id = '${panelId}' and workflow_stage_code = 'OQC';`)).toBe('Cancelled');
  expect(queryDatabase(`
    select count(*)::text from panel_quality_reports report
    join panel_quality_inspection_attempts attempt on attempt.id = report.attempt_id
    where attempt.panel_id = '${panelId}' and attempt.stage_code = 'LQC' and report.status = 'Finalized';
  `)).toBe('1');
});

async function completeManufacturing(request: APIRequestContext, projectId: string, panelId: string) {
  const started = await postJson(request, '/api/manufacturing/executions/start', 'dev-manufacturing', {
    operationId: crypto.randomUUID(), projectId, panelId
  }) as { executionId: string; version: number };
  let version = started.version;
  const detailResponse = await request.get(`${apiBaseUrl}/api/manufacturing/panels/${panelId}`, { headers: devHeaders('dev-manufacturing') });
  const detail = await detailResponse.json() as { steps: Array<{ stepId: string }> };
  for (const step of detail.steps) {
    const checked = await postJson(request, `/api/manufacturing/executions/${started.executionId}/check-step`, 'dev-manufacturing', {
      operationId: crypto.randomUUID(), stepId: step.stepId, expectedVersion: version
    }) as { version: number };
    version = checked.version;
  }
  await postJson(request, `/api/manufacturing/executions/${started.executionId}/complete`, 'dev-manufacturing', {
    operationId: crypto.randomUUID(), expectedVersion: version
  });
}

async function createManufacturingReadyProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const created = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Quality Customer', item: 'RPP', projectCode, projectTitle,
      panelCount: 1, deliveryDate: '2026-10-10', salesOwnerUserId,
      packagingMethod: 'StretchWrap', salesAmount: null, currencyCode: null,
      deliveryLocation: 'Synthetic Site', fatRequired: false
    }
  });
  expect(created.ok(), await created.text()).toBeTruthy();
  const projectId = (await created.json() as { projectId: string }).projectId;
  const procurement = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: { items: [{ orderItem: 'Synthetic Quality Material' }] }
  });
  expect(procurement.ok(), await procurement.text()).toBeTruthy();
  queryDatabase(`
    update panel_placeholders
    set panel_name = 'MCC-Q', width_mm = 600, height_mm = 1800, depth_mm = 400, panel_info_completed = true
    where project_id = '${projectId}' and status = 'Active';
    begin;
    select set_config('emi_qms.material_receipt_write', 'allowed', true);
    update project_procurement_items
    set receipt_completed = true, receipt_completed_at_utc = now(),
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

async function postJson(request: APIRequestContext, path: string, userKey: string, data: unknown) {
  const response = await request.post(`${apiBaseUrl}${path}`, { headers: devHeaders(userKey), data });
  if (!response.ok()) throw new Error(`${path} failed (${response.status()}): ${await response.text()}`);
  return response.json();
}

async function putJson(request: APIRequestContext, path: string, userKey: string, data: unknown) {
  const response = await request.put(`${apiBaseUrl}${path}`, { headers: devHeaders(userKey), data });
  if (!response.ok()) throw new Error(`${path} failed (${response.status()}): ${await response.text()}`);
  return response.json();
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}

function queryDatabase(sql: string) {
  return execFileSync('docker', [
    'compose', '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'), '--file', requireEnv('E2E_COMPOSE_FILE'),
    'exec', '-T', requireEnv('E2E_POSTGRES_SERVICE'), 'psql', '--username', requireEnv('E2E_DATABASE_USER'),
    '--dbname', requireEnv('E2E_DATABASE_NAME'), '--no-psqlrc', '--tuples-only', '--no-align',
    '--set', 'ON_ERROR_STOP=1', '--command', sql
  ], { encoding: 'utf8' }).trim().split('\n').at(-1)?.trim() ?? '';
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for isolated full-stack validation.`);
  return value;
}

async function assertNoHorizontalOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
}
