import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesUserId = '50000000-0000-0000-0000-000000000002';
const logisticsUserId = '50000000-0000-0000-0000-000000000006';

test('TASK-013A: packing, departure and delivery evidence create the sales handoff exactly once', async ({ page, request }) => {
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

  const packingOperationId = crypto.randomUUID();
  const packingRequest = { operationId: packingOperationId, projectId, panelIds: [panelIds[0]], note: '패널 1 개별 포장', specification: 'RPP', weightText: '10kg' };
  const packing = await postJson(request, '/api/logistics/packing-units', 'dev-logistics', packingRequest) as Mutation;
  const replay = await postJson(request, '/api/logistics/packing-units', 'dev-logistics', packingRequest) as Mutation;
  expect(replay.replayed).toBe(true);
  expect(replay.targetId).toBe(packing.targetId);
  await page.goto(`/logistics?stage=packing&project=${projectId}&draft=${packing.targetId}`);
  await expect(page.getByText('Draft')).toBeVisible();
  await expect(page.getByText('PU-001')).toBeVisible();
  await expect(page.getByRole('button', { name: '임시 작업 취소' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  let version = await uploadEvidence(request, 'packing', packing.targetId, packing.version, tinyPng(), '합성 포장 사진');
  await finalize(request, 'packing', packing.targetId, version);

  await page.goto(`/logistics?stage=departure&project=${projectId}&unit=${packing.targetId}`);
  await expect(page.getByRole('button', { name: '02 출발' })).toHaveAttribute('aria-current', 'step');
  await expect(page.locator('.logistics-target-card')).toHaveCount(1);
  await assertNoHorizontalOverflow(page);

  const departure = await postJson(request, '/api/logistics/departure-batches', 'dev-logistics', {
    operationId: crypto.randomUUID(), projectId, unitIds: [packing.targetId], departureDate: '2026-07-18'
  }) as Mutation;
  version = await uploadEvidence(request, 'departure', departure.targetId, departure.version, tinyPng(), '합성 상차 사진');
  await finalize(request, 'departure', departure.targetId, version);

  await page.goto(`/logistics?stage=delivery&project=${projectId}&unit=${packing.targetId}`);
  await expect(page.getByRole('button', { name: '03 납품' })).toHaveAttribute('aria-current', 'step');
  await expect(page.locator('.logistics-target-card')).toHaveCount(1);
  await assertNoHorizontalOverflow(page);

  const delivery = await postJson(request, '/api/logistics/delivery-batches', 'dev-logistics', {
    operationId: crypto.randomUUID(), projectId, unitIds: [packing.targetId], departureDate: null
  }) as Mutation;
  version = await uploadEvidence(request, 'delivery', delivery.targetId, delivery.version, Buffer.from('%PDF-1.4 synthetic'), '');
  await finalize(request, 'delivery', delivery.targetId, version);

  expect(queryDatabase(`select count(*)::text from panel_placeholders where project_id='${projectId}' and workflow_stage='ShipmentCompleted';`)).toBe('1');
  expect(queryDatabase(`select count(*)::text from work_items where project_id='${projectId}' and workflow_stage_code='SalesSettlementCompleted' and target_type='Project';`)).toBe('0');
  const remainingPackingQueue = await request.get(`${apiBaseUrl}/api/logistics/queue?stage=packing&projectId=${projectId}`, {
    headers: devHeaders('dev-logistics')
  });
  expect(remainingPackingQueue.ok(), await remainingPackingQueue.text()).toBeTruthy();
  const remainingPacking = await remainingPackingQueue.json() as { projects: Array<{ items: Array<{ panelIds: string[] }> }> };
  expect(remainingPacking.projects.flatMap((project) => project.items).flatMap((item) => item.panelIds)).toEqual([panelIds[1]]);

  const secondPacking = await postJson(request, '/api/logistics/packing-units', 'dev-logistics', {
    operationId: crypto.randomUUID(), projectId, panelIds: [panelIds[1]], note: '패널 2 개별 포장', specification: 'RPP', weightText: '10kg'
  }) as Mutation;
  version = await uploadEvidence(request, 'packing', secondPacking.targetId, secondPacking.version, tinyPng(), '패널 2 포장 사진');
  await finalize(request, 'packing', secondPacking.targetId, version);
  const secondDeparture = await postJson(request, '/api/logistics/departure-batches', 'dev-logistics', {
    operationId: crypto.randomUUID(), projectId, unitIds: [secondPacking.targetId], departureDate: '2026-07-19'
  }) as Mutation;
  version = await uploadEvidence(request, 'departure', secondDeparture.targetId, secondDeparture.version, tinyPng(), '패널 2 상차 사진');
  await finalize(request, 'departure', secondDeparture.targetId, version);
  const secondDelivery = await postJson(request, '/api/logistics/delivery-batches', 'dev-logistics', {
    operationId: crypto.randomUUID(), projectId, unitIds: [secondPacking.targetId], departureDate: null
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
