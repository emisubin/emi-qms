import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { ensureLqcOperational } from './lqc-operating-fixture';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';
const qualityUserId = '50000000-0000-0000-0000-000000000005';

test.beforeEach(async ({ request }) => {
  await ensureLqcOperational(request);
});

test('TASK-WORKFLOW-CONTINUITY-001: manufacturing opens step-aligned LQC and joint completion opens OQC', async ({ page, request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const { projectId, panelId } = await createManufacturingReadyProject(request, `QLT-${unique}`, `합성 품질검사 ${unique}`);
  const manufacturing = await startManufacturing(request, projectId, panelId);

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
    items: Array<{ itemId: string; responseType: 'Check' | 'Text'; isRequired: boolean; isAvailable: boolean }>;
  };
  expect(detail.items.filter((item) => item.responseType === 'Check' && item.isAvailable)).toHaveLength(1);
  const prematureSave = await request.put(
    `${apiBaseUrl}/api/quality/inspections/reports/${detail.reportId}/responses`,
    {
      headers: devHeaders('dev-quality'),
      data: {
        operationId: crypto.randomUUID(),
        expectedReportVersion: detail.reportVersion,
        responses: detail.items.filter((item) => item.isRequired).map((item) => ({
          templateItemId: item.itemId,
          checkResult: item.responseType === 'Check' ? 'Pass' : null,
          textValue: item.responseType === 'Text' ? '합성 확인' : null,
          note: null
        }))
      }
    }
  );
  expect(prematureSave.status()).toBe(400);

  const manufacturingAtFinalStep = await advanceManufacturingToFinalStep(request, manufacturing);
  const availableDetail = await getQualityDetail(request, panelId, 'LQC');
  expect(availableDetail.items.filter((item) => item.responseType === 'Check' && item.isAvailable)).toHaveLength(
    availableDetail.items.filter((item) => item.responseType === 'Check').length
  );
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
  await expect(page.getByRole('heading', { name: 'LQC 검사' })).toBeVisible();
  await expect(page.locator('.quality-focus-card')).toBeVisible();
  await expect(page.locator('.quality-item-photo')).toHaveCount(0);
  await expect(page.getByRole('button', { name: '판정 확정' })).toBeEnabled();
  await page.screenshot({ path: '../tasks/012a-screenshots/quality-inspections-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/quality/inspections?stage=LQC&project=${projectId}&panel=${panelId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.locator('.quality-focus-card')).toBeVisible();
  await expect(page.locator('.quality-item-photo')).toHaveCount(0);
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
  expect(finalized.nextStageCode).toBeNull();
  expect(queryDatabase(`select count(*)::text from work_items where target_id = '${panelId}' and workflow_stage_code = 'OQC';`)).toBe('0');
  await completeManufacturingFromFinalStep(request, manufacturingAtFinalStep);

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

  expect(queryDatabase(`select count(*)::text from panel_manufacturing_completion_confirmations where panel_id = '${panelId}';`)).toBe('1');
  expect(queryDatabase(`select count(*)::text from work_items where target_id = '${panelId}' and workflow_stage_code = 'OQC';`)).toBe('1');

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/quality/inspections?stage=OQC&project=${projectId}&panel=${panelId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto(`/quality/inspections?stage=OQC&project=${projectId}&panel=${panelId}`);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/quality/inspections?stage=OQC&project=${projectId}&panel=${panelId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('heading', { name: 'OQC 자체검수 검사' })).toBeVisible();
  await expect(page.locator('.quality-panel-chip').filter({ hasText: 'P01' })).toHaveClass(/active/u);
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

test('TASK-012A: aggregate inspection failure opens one Pending and a passed reinspection closes it', async ({ page, request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const { projectId, panelId } = await createManufacturingReadyProject(
    request,
    `AGG-${unique}`,
    `합산 판정 재검사 ${unique}`
  );
  await completeManufacturing(request, projectId, panelId);
  await finalizeChecklistInspection(request, panelId, 'LQC');
  await finalizeChecklistInspection(request, panelId, 'OQC');

  const started = await postJson(request, '/api/quality/inspections/start', 'dev-quality', {
    operationId: crypto.randomUUID(), projectId, panelId, stageCode: 'CustomerInspection'
  }) as { reportId: string; version: number };
  const aggregateDetail = await getQualityDetail(request, panelId, 'CustomerInspection');
  expect(aggregateDetail.decisionMode).toBe('Aggregate');

  const rejectedItemMutation = await request.put(
    `${apiBaseUrl}/api/quality/inspections/reports/${started.reportId}/responses`,
    {
      headers: devHeaders('dev-quality'),
      data: {
        operationId: crypto.randomUUID(),
        expectedReportVersion: started.version,
        responses: [{
          templateItemId: aggregateDetail.items[0].itemId,
          checkResult: 'Fail',
          textValue: null,
          note: '합산 판정 단계에는 항목별 입력을 저장하지 않습니다.'
        }]
      }
    }
  );
  expect(rejectedItemMutation.status()).toBe(400);

  const failed = await postJson(
    request,
    `/api/quality/inspections/reports/${started.reportId}/finalize`,
    'dev-quality',
    {
      operationId: crypto.randomUUID(),
      expectedReportVersion: started.version,
      result: 'Failed',
      reason: '고객 검수 결과 외관과 표시 사항이 기준에 맞지 않아 생산관리 조치와 재검사가 필요합니다.',
      actionDepartmentCode: 'production-planning',
      assigneeUserId: null
    }
  ) as { pendingId: string };
  expect(failed.pendingId).toMatch(/^[0-9a-f-]{36}$/u);
  expect(queryDatabase(`
    select count(*)::text from pending_issues
    where project_id = '${projectId}' and target_type = 'Panel' and target_id = '${panelId}';
  `)).toBe('1');

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/pending/${failed.pendingId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-production');
  await page.goto(`/pending/${failed.pendingId}`);
  await page.getByLabel('처리 내용').fill('표시 라벨과 외관을 재작업하고 고객 검수 부적합 근거를 기준으로 조치합니다.');
  await page.getByRole('button', { name: '조치 시작' }).click();
  await expect(page.getByText('조치 시작 상태로 변경되었습니다.')).toBeVisible();
  await expect(page.getByRole('button', { name: '조치 완료' })).toBeVisible();
  await page.getByLabel('처리 내용').fill('표시 라벨 교체와 외관 보완을 완료했으며 동일 근거로 재검사를 요청합니다.');
  await expect(page.getByRole('button', { name: '조치 완료' })).toBeEnabled();
  await page.getByRole('button', { name: '조치 완료' }).click();
  await expect(page.getByText('조치를 완료하고 품질 재검사 업무를 생성했습니다.')).toBeVisible();

  await postJson(request, '/api/quality/inspections/start', 'dev-quality', {
    operationId: crypto.randomUUID(), projectId, panelId, stageCode: 'CustomerInspection'
  });
  const reinspection = await getQualityDetail(request, panelId, 'CustomerInspection');
  expect(reinspection.decisionMode).toBe('Aggregate');
  expect(reinspection.panel.pendingId).toBe(failed.pendingId);
  expect(reinspection.reportStatus).toBe('Draft');
  expect(queryDatabase(`select status from pending_issues where id = '${failed.pendingId}';`)).toBe('ReinspectionRequested');
  const passed = await postJson(
    request,
    `/api/quality/inspections/reports/${reinspection.reportId}/finalize`,
    'dev-quality',
    {
      operationId: crypto.randomUUID(),
      expectedReportVersion: reinspection.reportVersion,
      result: 'Passed',
      reason: '부적합 근거와 조치 결과를 대조하여 적합으로 판정합니다.',
      actionDepartmentCode: null,
      assigneeUserId: null
    }
  ) as { status: string; pendingId: string };
  expect(passed.status).toBe('Passed');
  expect(passed.pendingId).toBe(failed.pendingId);
  expect(queryDatabase(`select status from pending_issues where id = '${failed.pendingId}';`)).toBe('Closed');
  expect(queryDatabase(`
    select status from panel_quality_inspection_attempts
    where panel_id = '${panelId}' and stage_code = 'CustomerInspection'
    order by attempt_number desc limit 1;
  `)).toBe('Passed');
  expect(queryDatabase(`
    select count(*)::text from pending_issues
    where project_id = '${projectId}' and target_type = 'Panel' and target_id = '${panelId}';
  `)).toBe('1');
});

test('TASK-WORKFLOW-CONTINUITY-001: OQC opens customer inspection and required FAT together, then joins at packing', async ({ request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const { projectId, panelId } = await createManufacturingReadyProject(
    request,
    `PAR-${unique}`,
    `합성 병행 품질검사 ${unique}`,
    true
  );
  await completeManufacturing(request, projectId, panelId);
  await finalizeChecklistInspection(request, panelId, 'LQC');
  const oqc = await finalizeChecklistInspection(request, panelId, 'OQC') as { nextStageCode: string };
  expect(oqc.nextStageCode).toBe('CustomerInspection');
  expect(queryDatabase(`
    select string_agg(workflow_stage_code, ',' order by workflow_stage_code)
    from work_items
    where target_id='${panelId}' and workflow_stage_code in ('CustomerInspection','FAT') and status='Requested';
  `)).toBe('CustomerInspection,FAT');

  const fat = await finalizeAggregateInspection(request, projectId, panelId, 'FAT');
  expect(fat.nextStageCode).toBeNull();
  expect(queryDatabase(`
    select count(*)::text from work_items
    where target_id='${panelId}' and workflow_stage_code='PackingCompleted';
  `)).toBe('0');

  const customerInspection = await finalizeAggregateInspection(request, projectId, panelId, 'CustomerInspection');
  expect(customerInspection.nextStageCode).toBe('PackingCompleted');
  expect(queryDatabase(`
    select count(*)::text from work_items
    where target_id='${panelId}' and workflow_stage_code='PackingCompleted' and status='Requested';
  `)).toBe('1');
});

test('TASK-WORKFLOW-CONTINUITY-001 Change 011: LQC and OQC finalization roll back invalid responses and retry the same version', async ({ request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const { projectId, panelId } = await createManufacturingReadyProject(
    request,
    `ATM-${unique}`,
    `합성 원자 품질확정 ${unique}`
  );
  await completeManufacturing(request, projectId, panelId);

  await assertAtomicChecklistRetry(request, projectId, panelId, 'LQC');
  await assertAtomicChecklistRetry(request, projectId, panelId, 'OQC');
});

test('TASK-WORKFLOW-CONTINUITY-001 Change 012: OQC reinspection exposes only failed items and closes the same Pending', async ({ request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const { projectId, panelId } = await createManufacturingReadyProject(
    request,
    `RECHECK-${unique}`,
    `OQC 항목 재검사 ${unique}`
  );
  await completeManufacturing(request, projectId, panelId);
  await finalizeChecklistInspection(request, panelId, 'LQC');

  const started = await postJson(request, '/api/quality/inspections/start', 'dev-quality', {
    operationId: crypto.randomUUID(), projectId, panelId, stageCode: 'OQC'
  }) as { reportId: string; version: number };
  const detail = await getQualityDetail(request, panelId, 'OQC');
  const requiredItems = detail.items.filter((item) => item.isRequired && item.isAvailable);
  expect(requiredItems.length).toBeGreaterThan(1);
  const failedItem = requiredItems[0];
  const nonTargetItem = requiredItems[1];
  const failed = await postJson(
    request,
    `/api/quality/inspections/reports/${started.reportId}/finalize`,
    'dev-quality',
    {
      operationId: crypto.randomUUID(),
      expectedReportVersion: started.version,
      result: 'Failed',
      reason: 'OQC 검사에서 한 항목이 기준에 미달하여 생산관리 조치와 해당 항목 재검사가 필요합니다.',
      actionDepartmentCode: 'production-planning',
      assigneeUserId: null,
      responses: requiredItems.map((item) => ({
        templateItemId: item.itemId,
        checkResult: item.itemId === failedItem.itemId ? 'Fail' : 'Pass',
        textValue: null,
        note: item.itemId === failedItem.itemId ? '외관 표시가 검사 기준과 일치하지 않습니다.' : null
      }))
    }
  ) as { pendingId: string };

  const failedDetail = await getQualityDetail(request, panelId, 'OQC');
  expect(failedDetail.items.length).toBe(detail.items.length);
  expect(failedDetail.items.every((item) => !item.isReinspectionTarget)).toBe(true);

  await postJson(request, `/api/pending/${failed.pendingId}/transition`, 'dev-production', {
    toStatus: 'InProgress',
    expectedVersion: 1,
    reason: '부적합 항목 조치를 시작합니다.'
  });
  await postJson(request, `/api/pending/${failed.pendingId}/transition`, 'dev-production', {
    toStatus: 'ReinspectionRequested',
    expectedVersion: 2,
    reason: '부적합 항목 조치를 완료했습니다.'
  });

  const reinspection = await getQualityDetail(request, panelId, 'OQC');
  expect(reinspection.panel.pendingId).toBe(failed.pendingId);
  expect(reinspection.items).toHaveLength(1);
  expect(reinspection.items[0].itemId).toBe(failedItem.itemId);
  expect(reinspection.items[0].isReinspectionTarget).toBe(true);
  expect(reinspection.items[0].previousFailureEvidence).toContain('외관 표시');

  const rejected = await request.put(
    `${apiBaseUrl}/api/quality/inspections/reports/${reinspection.reportId}/responses`,
    {
      headers: devHeaders('dev-quality'),
      data: {
        operationId: crypto.randomUUID(),
        expectedReportVersion: reinspection.reportVersion,
        responses: [
          { templateItemId: failedItem.itemId, checkResult: 'Pass', textValue: null, note: null },
          { templateItemId: nonTargetItem.itemId, checkResult: 'Pass', textValue: null, note: null }
        ]
      }
    }
  );
  expect(rejected.status()).toBe(400);

  const passed = await postJson(
    request,
    `/api/quality/inspections/reports/${reinspection.reportId}/finalize`,
    'dev-quality',
    {
      operationId: crypto.randomUUID(),
      expectedReportVersion: reinspection.reportVersion,
      result: 'Passed',
      reason: '재조치 결과 해당 항목이 기준에 적합합니다.',
      actionDepartmentCode: null,
      assigneeUserId: null,
      responses: [
        { templateItemId: failedItem.itemId, checkResult: 'Pass', textValue: null, note: null }
      ]
    }
  ) as { status: string; pendingId: string };
  expect(passed.status).toBe('Passed');
  expect(passed.pendingId).toBe(failed.pendingId);
  expect(queryDatabase(`select status from pending_issues where id = '${failed.pendingId}';`)).toBe('Closed');

  const completed = await getQualityDetail(request, panelId, 'OQC');
  expect(completed.items).toHaveLength(detail.items.length);
  expect(completed.items.find((item) => item.itemId === failedItem.itemId)?.isReinspectionTarget).toBe(true);
  expect(completed.responses.find((item) => item.templateItemId === failedItem.itemId)?.checkResult).toBe('Pass');
  expect(completed.responses.find((item) => item.templateItemId === nonTargetItem.itemId)?.checkResult).toBe('Pass');
});

async function completeManufacturing(request: APIRequestContext, projectId: string, panelId: string) {
  const manufacturing = await startManufacturing(request, projectId, panelId);
  await completeStartedManufacturing(request, manufacturing);
}

async function startManufacturing(request: APIRequestContext, projectId: string, panelId: string) {
  const started = await postJson(request, '/api/manufacturing/executions/start', 'dev-manufacturing', {
    operationId: crypto.randomUUID(), projectId, panelId
  }) as { executionId: string; version: number };
  const detailResponse = await request.get(`${apiBaseUrl}/api/manufacturing/panels/${panelId}`, { headers: devHeaders('dev-manufacturing') });
  const detail = await detailResponse.json() as { steps: Array<{ stepId: string }> };
  return { executionId: started.executionId, version: started.version, steps: detail.steps };
}

async function completeStartedManufacturing(
  request: APIRequestContext,
  manufacturing: { executionId: string; version: number; steps: Array<{ stepId: string }> }
) {
  let version = manufacturing.version;
  for (const step of manufacturing.steps) {
    const checked = await postJson(request, `/api/manufacturing/executions/${manufacturing.executionId}/check-step`, 'dev-manufacturing', {
      operationId: crypto.randomUUID(), stepId: step.stepId, expectedVersion: version
    }) as { version: number };
    version = checked.version;
  }
  await postJson(request, `/api/manufacturing/executions/${manufacturing.executionId}/complete`, 'dev-manufacturing', {
    operationId: crypto.randomUUID(), expectedVersion: version
  });
}

async function advanceManufacturingToFinalStep(
  request: APIRequestContext,
  manufacturing: { executionId: string; version: number; steps: Array<{ stepId: string }> }
) {
  let version = manufacturing.version;
  for (const step of manufacturing.steps.slice(0, -1)) {
    const checked = await postJson(request, `/api/manufacturing/executions/${manufacturing.executionId}/check-step`, 'dev-manufacturing', {
      operationId: crypto.randomUUID(), stepId: step.stepId, expectedVersion: version
    }) as { version: number };
    version = checked.version;
  }
  return { ...manufacturing, version };
}

async function completeManufacturingFromFinalStep(
  request: APIRequestContext,
  manufacturing: { executionId: string; version: number; steps: Array<{ stepId: string }> }
) {
  const finalStep = manufacturing.steps.at(-1);
  if (!finalStep) throw new Error('final manufacturing step is required');
  const checked = await postJson(request, `/api/manufacturing/executions/${manufacturing.executionId}/check-step`, 'dev-manufacturing', {
    operationId: crypto.randomUUID(), stepId: finalStep.stepId, expectedVersion: manufacturing.version
  }) as { version: number };
  await postJson(request, `/api/manufacturing/executions/${manufacturing.executionId}/complete`, 'dev-manufacturing', {
    operationId: crypto.randomUUID(), expectedVersion: checked.version
  });
}

type QualityDetail = {
  decisionMode: 'Checklist' | 'Aggregate';
  reportId: string;
  reportStatus: string;
  reportVersion: number;
  panel: { pendingId: string | null };
  items: Array<{
    itemId: string;
    responseType: 'Check' | 'Text';
    isRequired: boolean;
    isAvailable: boolean;
    isReinspectionTarget: boolean;
    previousFailureEvidence: string | null;
  }>;
};

async function getQualityDetail(request: APIRequestContext, panelId: string, stageCode: string) {
  const response = await request.get(
    `${apiBaseUrl}/api/quality/inspections/panels/${panelId}?stage=${stageCode}`,
    { headers: devHeaders('dev-quality') }
  );
  if (!response.ok()) throw new Error(`quality detail failed (${response.status()}): ${await response.text()}`);
  return response.json() as Promise<QualityDetail>;
}

async function finalizeChecklistInspection(request: APIRequestContext, panelId: string, stageCode: 'LQC' | 'OQC') {
  const projectId = queryDatabase(`select project_id::text from panel_placeholders where id = '${panelId}';`);
  const started = await postJson(request, '/api/quality/inspections/start', 'dev-quality', {
    operationId: crypto.randomUUID(), projectId, panelId, stageCode
  }) as { reportId: string; version: number };
  const detail = await getQualityDetail(request, panelId, stageCode);
  expect(detail.decisionMode).toBe('Checklist');
  const saved = await putJson(request, `/api/quality/inspections/reports/${started.reportId}/responses`, 'dev-quality', {
    operationId: crypto.randomUUID(),
    expectedReportVersion: started.version,
    responses: detail.items.filter((item) => item.isRequired).map((item) => ({
      templateItemId: item.itemId,
      checkResult: item.responseType === 'Check' ? 'Pass' : null,
      textValue: item.responseType === 'Text' ? '합성 확인' : null,
      note: null
    }))
  }) as { version: number };
  return postJson(request, `/api/quality/inspections/reports/${started.reportId}/finalize`, 'dev-quality', {
    operationId: crypto.randomUUID(),
    expectedReportVersion: saved.version,
    result: 'Passed',
    reason: `${stageCode} 합성 검사 기준 적합`,
    actionDepartmentCode: null,
    assigneeUserId: null
  });
}

async function assertAtomicChecklistRetry(
  request: APIRequestContext,
  projectId: string,
  panelId: string,
  stageCode: 'LQC' | 'OQC'
) {
  const started = await postJson(request, '/api/quality/inspections/start', 'dev-quality', {
    operationId: crypto.randomUUID(), projectId, panelId, stageCode
  }) as { reportId: string; version: number };
  const detail = await getQualityDetail(request, panelId, stageCode);
  const required = detail.items.filter((item) => item.isRequired);
  const firstCheckId = required.find((item) => item.responseType === 'Check')?.itemId;
  expect(firstCheckId).toBeTruthy();
  const invalidResponses = required.map((item) => ({
    templateItemId: item.itemId,
    checkResult: item.responseType === 'Check' ? (item.itemId === firstCheckId ? 'Fail' : 'Pass') : null,
    textValue: item.responseType === 'Text' ? '합성 원자 확정 측정값' : null,
    note: null
  }));
  const rejected = await request.post(
    `${apiBaseUrl}/api/quality/inspections/reports/${started.reportId}/finalize`,
    {
      headers: devHeaders('dev-quality'),
      data: {
        operationId: crypto.randomUUID(),
        expectedReportVersion: started.version,
        result: 'Passed',
        reason: `${stageCode} 합성 원자 확정 거절 검증`,
        actionDepartmentCode: null,
        assigneeUserId: null,
        responses: invalidResponses
      }
    }
  );
  expect(rejected.status()).toBe(400);
  expect(queryDatabase(`
    select report.version::text || '|' ||
           (select count(*)::text from panel_quality_report_responses response where response.report_id=report.id)
    from panel_quality_reports report where report.id='${started.reportId}';
  `)).toBe(`${started.version}|0`);

  const validResponses = required.map((item) => ({
    templateItemId: item.itemId,
    checkResult: item.responseType === 'Check' ? 'Pass' : null,
    textValue: item.responseType === 'Text' ? '합성 원자 확정 측정값' : null,
    note: null
  }));
  const finalized = await postJson(
    request,
    `/api/quality/inspections/reports/${started.reportId}/finalize`,
    'dev-quality',
    {
      operationId: crypto.randomUUID(),
      expectedReportVersion: started.version,
      result: 'Passed',
      reason: `${stageCode} 합성 원자 확정 재시도 적합`,
      actionDepartmentCode: null,
      assigneeUserId: null,
      responses: validResponses
    }
  ) as { status: string; version: number };
  expect(finalized.status).toBe('Passed');
  expect(finalized.version).toBe(started.version + 1);
}

async function finalizeAggregateInspection(
  request: APIRequestContext,
  projectId: string,
  panelId: string,
  stageCode: 'CustomerInspection' | 'FAT'
) {
  const started = await postJson(request, '/api/quality/inspections/start', 'dev-quality', {
    operationId: crypto.randomUUID(), projectId, panelId, stageCode
  }) as { reportId: string; version: number };
  return postJson(request, `/api/quality/inspections/reports/${started.reportId}/finalize`, 'dev-quality', {
    operationId: crypto.randomUUID(),
    expectedReportVersion: started.version,
    result: 'Passed',
    reason: `${stageCode} 합성 패널 판정 적합`,
    actionDepartmentCode: null,
    assigneeUserId: null
  }) as Promise<{ nextStageCode: string | null }>;
}

async function createManufacturingReadyProject(
  request: APIRequestContext,
  projectCode: string,
  projectTitle: string,
  fatRequired = false
) {
  const created = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Quality Customer', item: 'RPP', projectCode, projectTitle,
      panelCount: 1, deliveryDate: '2026-10-10', salesOwnerUserId,
      packagingMethod: 'StretchWrap', salesAmount: null, currencyCode: null,
      deliveryLocation: 'Synthetic Site', fatRequired
    }
  });
  expect(created.ok(), await created.text()).toBeTruthy();
  const projectId = (await created.json() as { projectId: string }).projectId;
  const procurement = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: {
      items: [{
        orderItem: 'Synthetic Quality Material',
        materialCategoryId: '67000000-0000-0000-0000-000000000005'
      }]
    }
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
    insert into project_assignees (
      project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc, note
    ) values
      ('${projectId}', 'ManufacturingPrimary', '50000000-0000-0000-0000-000000000004', '50000000-0000-0000-0000-000000000003', now(), '합성 제조 담당자'),
      ('${projectId}', 'QualityLQC', '${qualityUserId}', '50000000-0000-0000-0000-000000000003', now(), '합성 LQC 담당자'),
      ('${projectId}', 'QualityOQC', '${qualityUserId}', '50000000-0000-0000-0000-000000000003', now(), '합성 OQC 담당자'),
      ('${projectId}', 'QualityCustomerInspection', '${qualityUserId}', '50000000-0000-0000-0000-000000000003', now(), '합성 고객검수 담당자'),
      ('${projectId}', 'LogisticsPrimary', '50000000-0000-0000-0000-000000000006', '50000000-0000-0000-0000-000000000003', now(), '합성 물류 담당자')
    on conflict (project_id, responsibility_type) do update
      set assigned_user_id = excluded.assigned_user_id,
          assigned_by_user_id = excluded.assigned_by_user_id,
          assigned_at_utc = excluded.assigned_at_utc,
          note = excluded.note;
    commit;
  `);
  const panelId = queryDatabase(`select id::text from panel_placeholders where project_id = '${projectId}' and status = 'Active';`);
  const kitting = await request.post(`${apiBaseUrl}/api/materials/kitting/complete`, {
    headers: devHeaders('dev-materials'),
    data: { operationId: crypto.randomUUID(), projectId, panelIds: [panelId] }
  });
  expect(kitting.ok(), await kitting.text()).toBeTruthy();
  const released = await request.post(`${apiBaseUrl}/api/manufacturing/releases`, {
    headers: devHeaders('dev-production'),
    data: { operationId: crypto.randomUUID(), projectId, panelIds: [panelId] }
  });
  expect(released.ok(), await released.text()).toBeTruthy();
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
