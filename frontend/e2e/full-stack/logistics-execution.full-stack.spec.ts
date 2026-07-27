import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesUserId = '50000000-0000-0000-0000-000000000002';
const logisticsUserId = '50000000-0000-0000-0000-000000000006';

test('Change 016: packed panels can depart and deliver independently', async ({ page, request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const created = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Logistics Customer', item: 'RPP',
      projectCode: `LOG-${unique}`, projectTitle: `합성 물류 실행 ${unique}`,
      panelCount: 2, deliveryDate: '2026-12-10', salesOwnerUserId: salesUserId,
      packagingMethod: 'WoodenCrate', salesAmount: null, currencyCode: null,
      deliveryLocation: 'Synthetic Site', fatRequired: false
    }
  });
  expect(created.ok(), await created.text()).toBeTruthy();
  const projectId = (await created.json() as { projectId: string }).projectId;
  const panelIds = queryDatabase(`select string_agg(id::text, ',' order by sequence_number) from panel_placeholders where project_id='${projectId}' and status='Active';`).split(',');
  expect(panelIds).toHaveLength(2);

  queryDatabase(`
    insert into user_project_access(user_id,project_id) values ('${logisticsUserId}','${projectId}') on conflict do nothing;
    insert into project_assignees(project_id,responsibility_type,assigned_user_id,assigned_by_user_id,assigned_at_utc)
    values ('${projectId}','LogisticsPrimary','${logisticsUserId}','${salesUserId}',now()),
           ('${projectId}','SalesPrimary','${salesUserId}','${salesUserId}',now())
    on conflict(project_id,responsibility_type) do update set assigned_user_id=excluded.assigned_user_id;
    insert into work_items(project_id,target_type,target_id,workflow_stage_code,responsibility_type,assigned_user_id,assigned_role_code,title,description,status,priority,idempotency_key,created_by_user_id)
    select '${projectId}','Panel',id,'PackingCompleted','LogisticsPrimary','${logisticsUserId}','logistics','포장 완료 · '||display_code,'합성 포장','Requested','Normal','e2e:logistics:'||id||':packing','${salesUserId}'
    from panel_placeholders where project_id='${projectId}' and status='Active';
  `);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/logistics?stage=packing&project=${projectId}`);
  await page.getByLabel('개발 사용자').selectOption('dev-logistics');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/logistics?stage=packing&project=${projectId}`);
  await expect(page.getByRole('heading', { name: '물류 실행' })).toBeVisible();
  await expect(page.locator('.logistics-target-card')).toHaveCount(2);
  await assertNoHorizontalOverflow(page);

  const genericWorkId = queryDatabase(`select id::text from work_items where target_id='${panelIds[0]}' and workflow_stage_code='PackingCompleted';`);
  const bypass = await request.post(`${apiBaseUrl}/api/my-work/${genericWorkId}/complete`, { headers: devHeaders('dev-logistics') });
  expect(bypass.status()).toBe(409);

  await page.getByRole('button', { name: /P01/u }).click();
  await page.getByRole('button', { name: /P02/u }).click();
  const packingSave = page.getByRole('button', { name: '포장 저장 및 확정' });
  await expect(packingSave).toBeDisabled();
  await page.getByLabel(/포장 사진/u).setInputFiles({
    name: 'packing.png',
    mimeType: 'image/png',
    buffer: tinyPng()
  });
  await packingSave.click();
  await expect(page.getByText(/포장 확정 완료/u)).toBeVisible();
  const packingTargetId = queryDatabase(`select id::text from logistics_packing_units where project_id='${projectId}' and status='Finalized' order by unit_number limit 1;`);
  expect(packingTargetId).not.toBe('');
  await assertNoHorizontalOverflow(page);

  await page.goto(`/logistics?stage=departure&project=${projectId}&panel=${panelIds[0]}`);
  await expect(page.getByRole('button', { name: '02 출발' })).toHaveAttribute('aria-current', 'step');
  await expect(page.locator('.logistics-target-card')).toHaveCount(2);
  await expect(page.getByText('1 선택', { exact: true })).toBeVisible();
  await page.getByLabel(/상차 사진/u).setInputFiles({
    name: 'departure.png',
    mimeType: 'image/png',
    buffer: tinyPng()
  });
  await page.getByRole('button', { name: '출발 저장 및 확정' }).click();
  await expect(page.getByText(/출발 확정 완료/u)).toBeVisible();
  await assertNoHorizontalOverflow(page);

  await page.goto(`/logistics?stage=delivery&project=${projectId}&panel=${panelIds[0]}`);
  await expect(page.getByRole('button', { name: '03 납품' })).toHaveAttribute('aria-current', 'step');
  await expect(page.locator('.logistics-target-card')).toHaveCount(1);
  await expect(page.getByText('1 선택', { exact: true })).toBeVisible();
  await page.getByLabel(/서명 명세서/u).setInputFiles({
    name: 'delivery.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.4 synthetic')
  });
  await page.getByRole('button', { name: '납품 저장 및 확정' }).click();
  await expect(page.getByText(/납품 확정 완료/u)).toBeVisible();
  await assertNoHorizontalOverflow(page);

  expect(queryDatabase(`select count(*)::text from panel_placeholders where project_id='${projectId}' and workflow_stage='ShipmentCompleted';`)).toBe('1');
  expect(queryDatabase(`select count(*)::text from work_items where project_id='${projectId}' and workflow_stage_code='SalesSettlementCompleted' and target_type='Project';`)).toBe('0');
  const remainingDepartureQueue = await request.get(`${apiBaseUrl}/api/logistics/queue?stage=departure&projectId=${projectId}`, {
    headers: devHeaders('dev-logistics')
  });
  expect(remainingDepartureQueue.ok(), await remainingDepartureQueue.text()).toBeTruthy();
  const remainingDeparture = await remainingDepartureQueue.json() as { projects: Array<{ items: Array<{ panelIds: string[] }> }> };
  expect(remainingDeparture.projects.flatMap((project) => project.items).flatMap((item) => item.panelIds)).toEqual([panelIds[1]]);

  const secondDeparture = await postJson(request, '/api/logistics/departure-batches', 'dev-logistics', {
    operationId: crypto.randomUUID(), projectId, panelIds: [panelIds[1]], departureDate: '2026-07-19'
  }) as Mutation;
  let version = await uploadEvidence(request, 'departure', secondDeparture.targetId, secondDeparture.version, tinyPng(), '패널 2 상차 사진');
  await finalize(request, 'departure', secondDeparture.targetId, version);
  const secondDelivery = await postJson(request, '/api/logistics/delivery-batches', 'dev-logistics', {
    operationId: crypto.randomUUID(), projectId, panelIds: [panelIds[1]], departureDate: null
  }) as Mutation;
  version = await uploadEvidence(request, 'delivery', secondDelivery.targetId, secondDelivery.version, Buffer.from('%PDF-1.4 panel-two'), '');
  await finalize(request, 'delivery', secondDelivery.targetId, version);

  expect(queryDatabase(`select count(*)::text from panel_placeholders where project_id='${projectId}' and workflow_stage='ShipmentCompleted';`)).toBe('2');
  expect(queryDatabase(`select count(*)::text from project_workflow_events where project_id='${projectId}' and stage_code in ('PackingCompleted','DepartureProcessed','DeliveryCompleted') and event_type='StageCompleted';`)).toBe('3');
  expect(queryDatabase(`select count(*)::text from work_items where project_id='${projectId}' and workflow_stage_code='SalesSettlementCompleted' and target_type='Project';`)).toBe('1');
});

type Mutation = { targetId: string; version: number; replayed: boolean };

async function uploadEvidence(request: APIRequestContext, stage: string, targetId: string, expectedVersion: number, buffer: Buffer, altText: string) {
  const response = await request.post(`${apiBaseUrl}/api/logistics/${stage}/${targetId}/evidence`, {
    headers: devHeaders('dev-logistics'),
    multipart: { operationId: crypto.randomUUID(), expectedVersion: String(expectedVersion), altText, file: { name: 'synthetic.bin', mimeType: 'application/octet-stream', buffer } }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json() as Mutation).version;
}

async function finalize(request: APIRequestContext, stage: string, targetId: string, expectedVersion: number) {
  await postJson(request, `/api/logistics/${stage}/${targetId}/finalize`, 'dev-logistics', { operationId: crypto.randomUUID(), expectedVersion });
}

async function postJson(request: APIRequestContext, path: string, user: string, data: unknown) {
  const response = await request.post(`${apiBaseUrl}${path}`, { headers: devHeaders(user), data });
  if (!response.ok()) throw new Error(`${path} failed (${response.status()}): ${await response.text()}`);
  return response.json();
}

function tinyPng() {
  return Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
}

function devHeaders(user: string) { return { 'X-Dev-User': user }; }

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
