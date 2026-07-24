import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesOwnerUserId = '50000000-0000-0000-0000-000000000002';

test('TASK-010A/011A: production releases a non-kitted panel and manufacturing completes it', async ({ page, request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const projectTitle = `합성 제조 실행 ${unique}`;
  const { projectId, panelId } = await createManufacturingProject(
    request,
    `MFG-${unique}`,
    projectTitle
  );

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/production-planning');
  await page.getByLabel('개발 사용자').selectOption('dev-production');
  await page.goto('/production-planning');
  await expect(page.getByRole('heading', { name: '생산관리' })).toBeVisible();
  await page.getByRole('tab', { name: /제조 투입/u }).click();
  const projectRow = page.locator('.production-project-row').filter({ hasText: projectTitle });
  await expect(projectRow).toBeVisible();
  await projectRow.click();
  const releasePanel = page.getByLabel('패널 제조 투입 요청');
  await expect(releasePanel.getByText('키팅 미보고')).toBeVisible();
  await releasePanel.locator('.manufacturing-release-row input[type="checkbox"]').check();
  await releasePanel.getByRole('button', { name: '선택 1면 제조 투입 요청' }).click();
  await expect(releasePanel.getByText('1면을 제조팀에 투입 요청했습니다. 제조 업무 1건이 생성되었습니다.')).toBeVisible();
  expect(queryDatabase(`
    select count(*)::text
    from notification_recipients recipient
    join notifications notification on notification.id = recipient.notification_id
    where notification.project_id = '${projectId}'
      and notification.idempotency_key like 'kitting:panel:%:manufacturing:notification';
  `)).toBe('2');

  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('heading', { name: '제조 작업' })).toBeVisible();
  await expect(page.getByText('제조 투입 요청됨 · 키팅 미보고')).toBeVisible();
  await assertNoHorizontalOverflow(page);

  await page.getByRole('button', { name: '제조 시작' }).click();
  await expect(page.getByText(/제조와 단계별 LQC를 함께 시작했습니다/u)).toBeVisible();
  expect(queryDatabase(`
    select count(*)::text
    from work_items
    where project_id = '${projectId}' and target_id = '${panelId}'
      and workflow_stage_code = 'LQC' and responsibility_type = 'QualityLQC';
  `)).toBe('1');
  let releaseFirstStep: () => void = () => undefined;
  const firstStepGate = new Promise<void>((resolve) => {
    releaseFirstStep = resolve;
  });
  let firstStepRequests = 0;
  const checkStepPattern = '**/api/manufacturing/executions/*/check-step';
  await page.route(checkStepPattern, async (route) => {
    firstStepRequests += 1;
    if (firstStepRequests === 1) await firstStepGate;
    await route.continue();
  });
  const firstStepButton = page.getByRole('button', { name: '1단계 확인' });
  await firstStepButton.evaluate((element) => {
    const button = element as HTMLButtonElement;
    button.click();
    button.click();
    button.click();
  });
  await expect(page.locator('.manufacturing-actions .primary-button')).toBeDisabled();
  await expect(page.getByText('제조 단계를 저장하는 중입니다. 완료될 때까지 잠시 기다려 주세요.')).toBeVisible();
  await fs.mkdir('/tmp/emi-qms-p2-remediation-evidence', { recursive: true });
  await page.screenshot({
    path: '/tmp/emi-qms-p2-remediation-evidence/011a-change-002-manufacturing-step-saving.jpg',
    fullPage: true,
    type: 'jpeg',
    quality: 88
  });
  expect(firstStepRequests).toBe(1);
  releaseFirstStep();
  await expect(page.getByRole('button', { name: '2단계 확인' })).toBeEnabled();
  await page.unroute(checkStepPattern);
  for (let sequence = 2; sequence <= 4; sequence += 1) {
    await page.getByRole('button', { name: `${sequence}단계 확인` }).click();
  }
  await expect(page.getByRole('button', { name: '제조 완료' })).toBeEnabled();
  await page.screenshot({
    path: '/tmp/emi-qms-p2-remediation-evidence/011a-change-002-manufacturing-four-steps-complete.jpg',
    fullPage: true,
    type: 'jpeg',
    quality: 88
  });

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
  await expect(page.getByRole('button', { name: '제조 완료' })).toBeEnabled();
  await page.getByRole('button', { name: '제조 완료' }).click();
  await expect(page.getByText(/제조를 완료했습니다/u)).toBeVisible();
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

  queryDatabase(`
    update panel_placeholders
    set panel_name = 'MCC-A', width_mm = 600, height_mm = 1800, depth_mm = 400,
        panel_info_completed = true
    where project_id = '${projectId}' and status = 'Active';
    insert into project_assignees (
      project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc
    ) values
      ('${projectId}', 'ManufacturingPrimary', '50000000-0000-0000-0000-000000000004', '${salesOwnerUserId}', now()),
      ('${projectId}', 'ManufacturingSecondary', '50000000-0000-0000-0000-000000000001', '${salesOwnerUserId}', now());
  `);
  const panelId = queryDatabase(`select id::text from panel_placeholders where project_id = '${projectId}' and status = 'Active';`);

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
