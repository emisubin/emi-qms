import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';

test('TASK-011A: manufacturing user starts, checks, stops, resumes and completes a panel', async ({ page, request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const { projectId, panelId } = await createManufacturingProject(
    request,
    `MFG-${unique}`,
    `합성 제조 실행 ${unique}`
  );

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('heading', { name: '제조 작업' })).toBeVisible();
  await expect(page.getByText('키팅 완료 · 제조 시작 준비')).toBeVisible();
  await assertNoHorizontalOverflow(page);

  await page.getByRole('button', { name: '제조 시작' }).click();
  for (let sequence = 1; sequence <= 4; sequence += 1) {
    await page.getByRole('button', { name: `${sequence}단계 확인` }).click();
  }
  await expect(page.getByRole('button', { name: '제조 완료 · LQC 전달' })).toBeEnabled();

  await page.getByRole('button', { name: '작업 중단' }).click();
  const stopDialog = page.getByRole('dialog', { name: '제조 작업 중단' });
  await stopDialog.getByPlaceholder('현장에서 확인한 문제와 필요한 조치를 10자 이상 입력하세요.').fill('합성 자재 규격 확인이 필요해 제조 작업을 잠시 중단합니다.');
  await stopDialog.getByLabel('조치 담당 부서').selectOption({ index: 1 });
  await stopDialog.getByRole('button', { name: '작업 중단 · 긴급 Pending 생성' }).click();
  await expect(page.getByRole('button', { name: /긴급 Pending/u })).toBeVisible();
  await expect(page.locator('.manufacturing-project-card')).toHaveAttribute('data-shape-role', 'warning');

  const pendingId = queryDatabase(`
    select id::text
    from pending_issues
    where project_id = '${projectId}' and target_id = '${panelId}' and issue_type = 'ManufacturingStop'
    order by created_at_utc desc
    limit 1;
  `);
  expect(pendingId).toMatch(/^[0-9a-f-]{36}$/u);
  await closePending(request, pendingId);

  await page.getByRole('button', { name: 'Pending 확인 후 재개' }).click();
  await expect(page.getByRole('button', { name: '제조 완료 · LQC 전달' })).toBeEnabled();
  await page.getByRole('button', { name: '제조 완료 · LQC 전달' }).click();
  await expect(page.getByText(/제조를 완료하고 LQC 업무를 생성했습니다/u)).toBeVisible();
  await expect(page.locator('.manufacturing-focus-card')).toHaveAttribute('data-status', 'completed');
  await expect(page.locator('.manufacturing-project-card')).toHaveAttribute('data-shape-role', 'success');
  await assertNoHorizontalOverflow(page);

  expect(queryDatabase(`
    select count(*)::text
    from work_items
    where project_id = '${projectId}' and target_id = '${panelId}'
      and workflow_stage_code = 'LQC' and responsibility_type = 'QualityLQC';
  `)).toBe('1');
});

async function createManufacturingProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const created = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Manufacturing Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount: 1,
      deliveryDate: '2026-10-10',
      salesOwnerUserId,
      packagingMethod: 'StretchWrap',
      salesAmount: null,
      currencyCode: null,
      deliveryLocation: 'Synthetic Site',
      fatRequired: false
    }
  });
  if (!created.ok()) {
    throw new Error(`Synthetic project creation failed (${created.status()}): ${await created.text()}`);
  }
  const projectId = (await created.json() as { projectId: string }).projectId;

  const procurement = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: {
      items: [{ orderItem: 'Synthetic Manufacturing Material' }]
    }
  });
  if (!procurement.ok()) {
    throw new Error(`Synthetic procurement setup failed (${procurement.status()}): ${await procurement.text()}`);
  }

  queryDatabase(`
    update panel_placeholders
    set panel_name = 'MCC-A', width_mm = 600, height_mm = 1800, depth_mm = 400,
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
    select id::text from panel_placeholders where project_id = '${projectId}' and status = 'Active';
  `);
  const panelId = queryDatabase(`select id::text from panel_placeholders where project_id = '${projectId}' and status = 'Active';`);

  const completedKitting = await request.post(`${apiBaseUrl}/api/materials/kitting/complete`, {
    headers: devHeaders('dev-materials'),
    data: { operationId: crypto.randomUUID(), projectId, panelIds: [panelId] }
  });
  expect(completedKitting.ok()).toBeTruthy();
  return { projectId, panelId };
}

async function closePending(request: APIRequestContext, pendingId: string) {
  let version = 1;
  for (const toStatus of ['ActionRequested', 'InProgress', 'ReinspectionRequested', 'Closed']) {
    const detail = await request.get(`${apiBaseUrl}/api/pending/${pendingId}`, { headers: devHeaders('dev-production') });
    expect(detail.ok()).toBeTruthy();
    const payload = await detail.json() as { issue: { status: string; version: number }; allowedTransitions: string[] };
    version = payload.issue.version;
    if (payload.issue.status === toStatus) continue;
    if (!payload.allowedTransitions.includes(toStatus)) {
      if (toStatus === 'ActionRequested') {
        const assignees = await request.get(`${apiBaseUrl}/api/pending/assignees`, { headers: devHeaders('dev-production') });
        expect(assignees.ok()).toBeTruthy();
        const firstAssignee = (await assignees.json() as Array<{ userId: string }>)[0];
        expect(firstAssignee).toBeTruthy();
        const assigned = await request.post(`${apiBaseUrl}/api/pending/${pendingId}/assign`, {
          headers: devHeaders('dev-production'),
          data: { assigneeUserId: firstAssignee.userId, expectedVersion: version, reason: '합성 제조 중단 조치 담당 지정' }
        });
        expect(assigned.ok()).toBeTruthy();
        continue;
      }
      throw new Error(`Pending ${pendingId} cannot transition to ${toStatus}.`);
    }
    const transition = await request.post(`${apiBaseUrl}/api/pending/${pendingId}/transition`, {
      headers: devHeaders('dev-production'),
      data: { toStatus, expectedVersion: version, reason: `합성 제조 중단 ${toStatus} 확인` }
    });
    expect(transition.ok()).toBeTruthy();
  }
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}

function queryDatabase(sql: string) {
  return execFileSync(
    'docker',
    [
      'compose',
      '--project-name',
      requireEnv('E2E_COMPOSE_PROJECT_NAME'),
      '--file',
      requireEnv('E2E_COMPOSE_FILE'),
      'exec',
      '-T',
      requireEnv('E2E_POSTGRES_SERVICE'),
      'psql',
      '--username',
      requireEnv('E2E_DATABASE_USER'),
      '--dbname',
      requireEnv('E2E_DATABASE_NAME'),
      '--no-psqlrc',
      '--tuples-only',
      '--no-align',
      '--set',
      'ON_ERROR_STOP=1',
      '--command',
      sql
    ],
    { encoding: 'utf8' }
  ).trim().split('\n').at(-1)?.trim() ?? '';
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for isolated full-stack validation.`);
  return value;
}

async function assertNoHorizontalOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
}
