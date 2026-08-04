import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { expect, type APIRequestContext, type Page, test } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const productionUserId = '50000000-0000-0000-0000-000000000003';

test('TASK-UL891-CORRECTIONS: current design and all-set planning fit the existing desktop and mobile pages', async ({ page, request }, testInfo) => {
  await seedLinkedUl891Templates();
  const unique = Date.now();
  const projectTitle = `UL891 UI 검수 ${unique}`;
  const projectId = await createUl891Project(request, unique, projectTitle);
  markLastPanelAsHistoricalSequence(projectId);
  await assignProductionOwner(projectId);
  await saveAllSetDefault(request, projectId);

  await page.setViewportSize({ width: 1280, height: 960 });
  await openProject(page, projectTitle, 'dev-design');
  await page.getByRole('tab', { name: '설계' }).click();
  const designWorkspace = page.getByLabel('UL891 세트 설계');
  await expect(designWorkspace).toContainText('개별 패널42');
  await expect(designWorkspace).toContainText('현재 설계');
  await expect(designWorkspace).not.toContainText('V1');
  await expect(designWorkspace).not.toContainText('code');

  await designWorkspace.getByRole('button', { name: '수정' }).click();
  await expect(page.getByText('UL891 세트 설계 수정')).toBeVisible();
  await expect(page.getByText('저장한 뒤에도 같은 화면에서 계속 변경할 수 있고, 현재 설계가 제조 시작 기준으로 반영됩니다.')).toBeVisible();
  await page.getByLabel('1번 위치 패널명').fill('반복 패널');
  await page.getByLabel('2번 위치 패널명').fill('반복 패널');
  await page.getByLabel('1번 위치 widthMm').fill('600');
  await page.getByLabel('1번 위치 heightMm').fill('1800');
  await page.getByLabel('1번 위치 depthMm').fill('500');
  await page.getByLabel('2번 위치 widthMm').fill('600');
  await page.getByLabel('2번 위치 heightMm').fill('1800');
  await page.getByLabel('2번 위치 depthMm').fill('500');
  await page.getByRole('button', { name: '저장', exact: true }).click();
  await expect(page.getByText('완료 · 현재 설계를 저장했습니다. 필요할 때 같은 화면에서 다시 수정할 수 있습니다.')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('desktop-ul891-current-design.png'), fullPage: true });
  await page.getByRole('button', { name: '설계 탭으로 돌아가기' }).click();

  for (const section of ['제조', '품질', '물류']) {
    await page.getByRole('tab', { name: section, exact: true }).click();
    const panelTable = page.getByRole('table', { name: `${section} 패널 현황` });
    await expect(panelTable.locator('.project-panel-status-row')).toHaveCount(42);
    const historicalCodeRow = panelTable.locator('.project-panel-status-row').filter({ hasText: 'P52' });
    await expect(historicalCodeRow).toHaveCount(1);
    await expect(historicalCodeRow).toContainText(/^42/u);
  }

  await openProject(page, projectTitle, 'dev-production');
  await page.getByRole('tab', { name: '생산관리' }).click();
  const gantt = page.getByLabel('생산계획 계획 실적 일정표');
  await expect(gantt).toBeVisible();
  await expect(gantt.locator('[data-bar="plan"]')).not.toHaveCount(0);
  await expect(gantt.locator('.production-control-gantt-gridline')).not.toHaveCount(0);
  await expect(gantt.locator('.production-control-gantt-gridline[data-major="true"]').first()).toHaveCSS('background-color', 'rgb(143, 143, 143)');
  await expect(gantt.locator('.production-control-gantt-gridline[data-major="true"]').first()).toHaveCSS('background-image', 'none');
  await expect(gantt.locator('.production-control-gantt-gridline:not([data-major="true"])').first()).toHaveCSS('background-color', 'rgba(0, 0, 0, 0)');
  await expect(gantt.locator('.production-control-gantt-gridline:not([data-major="true"])').first()).toHaveCSS('background-image', /repeating-linear-gradient/);
  await expect(gantt.locator('.production-control-gantt-axis > div > span').first()).toHaveCSS('background-color', 'rgba(0, 0, 0, 0)');
  const ganttAxis = gantt.locator('.production-control-gantt-axis');
  const ganttBody = gantt.locator('.production-control-gantt-body');
  await expect(ganttAxis).toHaveCSS('border-top-style', 'solid');
  await expect(ganttAxis).toHaveCSS('border-left-style', 'solid');
  await expect(ganttAxis).toHaveCSS('border-right-style', 'solid');
  await expect(ganttBody).toHaveCSS('border-bottom-style', 'solid');
  await expect(ganttBody).toHaveCSS('border-left-style', 'solid');
  await expect(ganttBody).toHaveCSS('border-right-style', 'solid');
  await expect(ganttAxis.locator('> strong')).toHaveCSS('border-right-style', 'solid');
  await expect(ganttBody.locator('article > strong').first()).toHaveCSS('border-right-style', 'solid');
  const ganttEdgeGeometry = await gantt.evaluate((node) => {
    const track = node.querySelector('.production-control-gantt-body article > div');
    const lines = [...node.querySelectorAll('.production-control-gantt-body article:first-child .production-control-gantt-gridline')];
    const trackRect = track?.getBoundingClientRect();
    const firstRect = lines[0]?.getBoundingClientRect();
    const lastRect = lines.at(-1)?.getBoundingClientRect();
    return {
      trackLeft: trackRect?.left ?? 0,
      trackRight: trackRect?.right ?? 0,
      firstLineLeft: firstRect?.left ?? 0,
      lastLineRight: lastRect?.right ?? 0
    };
  });
  expect(ganttEdgeGeometry.firstLineLeft).toBeGreaterThan(ganttEdgeGeometry.trackLeft);
  expect(ganttEdgeGeometry.lastLineRight).toBeLessThan(ganttEdgeGeometry.trackRight);
  await expect(gantt.locator('.production-control-gantt-legend [data-bar="plan"]')).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  await expect(gantt.locator('.production-control-gantt-legend [data-bar="actual"]')).toHaveCSS('background-color', 'rgb(17, 17, 17)');
  const assignees = page.getByLabel('담당자 지정 현황');
  await expect(assignees).toContainText('Dev Production Planning User');
  expect(await gantt.evaluate((node, following) => Boolean(node.compareDocumentPosition(following as Node) & Node.DOCUMENT_POSITION_FOLLOWING), await assignees.elementHandle())).toBeTruthy();

  await page.getByRole('button', { name: '생산계획 수정' }).click();
  await expect(page.getByRole('tab', { name: /전체 기본계획/ })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByRole('heading', { name: '전체 세트 기본계획' })).toBeVisible();
  await expect(page.getByText('기본계획은 빈 활성 세트와 이후 추가되는 세트에 적용됩니다.', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: '담당자 지정' })).toHaveCount(0);

  await page.getByRole('tab', { name: /계획 구조/ }).click();
  const structureSection = page.locator('details.ds-input-section--collapsible').filter({ has: page.getByRole('heading', { name: '계획 구조 입력' }) });
  await structureSection.locator('summary').click();
  const structureRows = page.locator('.production-control-project-fields.is-structure-only');
  await expect(structureRows).toHaveCount(3);
  const firstStructureRow = structureRows.first();
  const requiredCheckbox = firstStructureRow.getByRole('checkbox');
  const connectionSelect = firstStructureRow.getByRole('combobox', { name: /연결할 실적/ });
  await expect(connectionSelect).not.toHaveValue('');
  const checkboxBox = await requiredCheckbox.boundingBox();
  const connectionBox = await connectionSelect.boundingBox();
  expect(checkboxBox).toMatchObject({ width: 20, height: 20 });
  expect(connectionBox!.x).toBeGreaterThan(checkboxBox!.x + checkboxBox!.width);

  await page.getByRole('tab', { name: /전체 기본계획/ }).click();
  await page.screenshot({ path: testInfo.outputPath('desktop-ul891-all-set-default.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.getByRole('heading', { name: '전체 세트 기본계획' })).toBeVisible();
  await expect(page.getByText('계획 구조에서 수정', { exact: true })).toBeVisible();
  const overflowElements = await page.evaluate(() => Array.from(document.querySelectorAll<HTMLElement>('body *'))
    .filter((element) => {
      const rect = element.getBoundingClientRect();
      return rect.right > window.innerWidth + 1 || rect.left < -1;
    })
    .slice(0, 10)
    .map((element) => ({
      className: element.className,
      right: Math.round(element.getBoundingClientRect().right),
      tagName: element.tagName,
      text: element.textContent?.trim().slice(0, 80)
    })));
  expect(overflowElements).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('mobile-ul891-all-set-default.png'), fullPage: true });
});

async function seedLinkedUl891Templates() {
  const manufacturingTemplateId = randomUUID();
  const manufacturingVersionId = randomUUID();
  const planTemplateId = randomUUID();
  const planVersionId = randomUUID();
  const steps = [
    { itemId: randomUUID(), definitionKey: randomUUID(), label: '제조 착수', role: 'Assembly' },
    { itemId: randomUUID(), definitionKey: randomUUID(), label: '배선 확인', role: 'General' },
    { itemId: randomUUID(), definitionKey: randomUUID(), label: '완료 확인', role: 'General' }
  ];
  const manufacturingValues = steps.map((step, index) =>
    `('${randomUUID()}','${manufacturingVersionId}','${step.definitionKey}',${index + 1},'${step.label}','${step.role}')`).join(',');
  const planValues = steps.map((step, index) =>
    `('${step.itemId}','${planVersionId}','${step.definitionKey}',${index + 1},'${step.label}',true)`).join(',');
  const connectionValues = steps.map((step) =>
    `('${randomUUID()}','${step.itemId}','MANUFACTURING_STEP_COMPLETED','${step.definitionKey}')`).join(',');
  executeSql(`
    insert into production_control_manufacturing_templates (id, product_type_id)
    select '${manufacturingTemplateId}', id from production_product_types where code='UL891';
    insert into production_control_manufacturing_versions (id, template_id, version_number, lifecycle_status, activated_at_utc)
    values ('${manufacturingVersionId}','${manufacturingTemplateId}',1,'Active',now());
    insert into production_control_manufacturing_items (id, template_version_id, definition_key, display_order, label, step_role)
    values ${manufacturingValues};
    insert into production_control_plan_templates (id, product_type_id)
    select '${planTemplateId}', id from production_product_types where code='UL891';
    insert into production_control_plan_versions (id, template_id, version_number, lifecycle_status, activated_at_utc)
    values ('${planVersionId}','${planTemplateId}',1,'Active',now());
    insert into production_control_plan_items (id, template_version_id, definition_key, display_order, label, is_required)
    values ${planValues};
    insert into production_control_plan_connections (id, plan_item_id, source_code, source_definition_key)
    values ${connectionValues};
  `);
}

async function createUl891Project(request: APIRequestContext, unique: number, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      customerName: '검수 고객 A',
      item: 'UL891',
      projectCode: `UL891-UI-${unique}`,
      projectTitle,
      panelCount: null,
      deliveryDate: '2026-12-31',
      salesOwnerUserId: salesOwnerId,
      packagingMethod: 'WoodenCrate',
      salesAmount: null,
      currencyCode: null,
      deliveryLocation: null,
      fatRequired: false,
      ul891SetSpecs: [{ name: '기본 세트', quantity: 6, panelCount: 7 }]
    }
  });
  expect(response.status()).toBe(201);
  return (await response.json() as { projectId: string }).projectId;
}

async function assignProductionOwner(projectId: string) {
  executeSql(`
    insert into project_assignees (
      project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc, row_version
    ) values (
      '${projectId}', 'ProductionPlanningPrimary', '${productionUserId}', '${productionUserId}', now(), 1
    );
  `);
}

function markLastPanelAsHistoricalSequence(projectId: string) {
  executeSql(`
    update panel_placeholders
    set sequence_number = 52, display_code = 'P52'
    where id = (
        select id from panel_placeholders
      where project_id = '${projectId}' and status = 'Active'
      order by sequence_number desc
      limit 1
    );
  `);
}

async function saveAllSetDefault(request: APIRequestContext, projectId: string) {
  const response = await request.get(`${apiBaseUrl}/api/projects/${projectId}/production-planning`, {
    headers: { 'X-Dev-User': 'dev-production' }
  });
  expect(response.ok()).toBeTruthy();
  const plan = await response.json() as {
    setDefault: { rowVersion: number; items: Array<{ itemId: string; rowVersion: number }> };
  };
  const save = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/production-planning/set-defaults`, {
    headers: { 'X-Dev-User': 'dev-production' },
    data: {
      expectedRowVersion: plan.setDefault.rowVersion,
      overwriteExisting: false,
      reason: '전체 세트 기본계획 화면 검수',
      items: plan.setDefault.items.map((item, index) => ({
        itemId: item.itemId,
        expectedRowVersion: item.rowVersion,
        plannedStartDate: `2026-08-${String(index * 3 + 1).padStart(2, '0')}`,
        plannedEndDate: `2026-08-${String(index * 3 + 3).padStart(2, '0')}`,
        assignedUserId: productionUserId,
        requiredHeadcount: 2,
        note: '세트 공통 계획'
      }))
    }
  });
  expect(save.ok()).toBeTruthy();
}

async function openProject(page: Page, projectTitle: string, userKey: string) {
  await page.goto('/projects');
  const userSelect = page.getByLabel('개발 사용자');
  await userSelect.selectOption(userKey);
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색', exact: true }).click();
  const projectRow = page.locator('.project-list-row').filter({ hasText: projectTitle });
  await expect(projectRow).toBeVisible();
  await projectRow.click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
}

function executeSql(sql: string) {
  const databaseName = requireEnv('E2E_DATABASE_NAME');
  const databaseHost = requireEnv('DATABASE_HOST');
  const databasePort = requireEnv('DATABASE_PORT');
  const databaseUser = requireEnv('DATABASE_USER');
  const databasePassword = requireEnv('DATABASE_PASSWORD');
  if (commandExists('psql')) {
    execFileSync('psql', [
      '--host', databaseHost,
      '--port', databasePort,
      '--username', databaseUser,
      '--dbname', databaseName,
      '--no-psqlrc',
      '--set', 'ON_ERROR_STOP=1',
      '--command', sql
    ], { env: { ...process.env, PGPASSWORD: databasePassword }, stdio: 'pipe' });
    return;
  }
  execFileSync('docker', [
    'compose',
    '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'),
    '--file', requireEnv('E2E_COMPOSE_FILE'),
    'exec', '-T', requireEnv('E2E_POSTGRES_SERVICE'),
    'psql', '--username', databaseUser, '--dbname', databaseName,
    '--no-psqlrc', '--set', 'ON_ERROR_STOP=1', '--command', sql
  ], { stdio: 'pipe' });
}

function commandExists(command: string) {
  try {
    execFileSync(command, ['--version'], { stdio: 'ignore' });
    return true;
  } catch {
    return false;
  }
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for isolated full-stack validation.`);
  return value;
}
