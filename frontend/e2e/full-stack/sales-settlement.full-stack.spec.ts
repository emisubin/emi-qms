import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const salesUserId = '50000000-0000-0000-0000-000000000002';
const adminUserId = '50000000-0000-0000-0000-000000000001';
const currentSeoulDate = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Seoul',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit'
}).format(new Date());

test('TASK-014A: project settlement atomically completes the project and remains read-only', async ({ page, request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const project = await createSyntheticProject(request, `SET-${unique}`, `합성 정산 프로젝트 ${unique}`);
  const projectId = project.projectId;
  const panelIds = queryDatabase(`select string_agg(id::text, ',' order by sequence_number) from panel_placeholders where project_id='${projectId}' and status='Active';`).split(',');
  expect(panelIds).toHaveLength(2);
  seedDeliveredProject(projectId, panelIds);

  const detailBefore = await getJson(request, `/api/projects/${projectId}/settlement`, 'dev-sales') as Settlement;
  expect(detailBefore.activePanelCount).toBe(2);
  expect(detailBefore.deliveredPanelCount).toBe(2);
  expect(detailBefore.canMutate).toBe(true);

  const genericWorkId = queryDatabase(`select id::text from work_items where project_id='${projectId}' and workflow_stage_code='SalesSettlementCompleted';`);
  const bypass = await request.post(`${apiBaseUrl}/api/my-work/${genericWorkId}/complete`, { headers: devHeaders('dev-sales') });
  expect(bypass.status()).toBe(409);

  const adminMutation = await request.put(`${apiBaseUrl}/api/projects/${projectId}/settlement/draft`, {
    headers: devHeaders('dev-admin'),
    data: { expectedVersion: 0, invoiceIssuedDate: currentSeoulDate, invoiceNumber: null, note: null }
  });
  expect(adminMutation.status()).toBe(403);

  const futureDate = await request.put(`${apiBaseUrl}/api/projects/${projectId}/settlement/draft`, {
    headers: devHeaders('dev-sales'),
    data: { expectedVersion: 0, invoiceIssuedDate: '2099-01-01', invoiceNumber: null, note: null }
  });
  expect(futureDate.status()).toBe(400);

  queryDatabase(`
    insert into pending_issues(id,project_id,target_type,target_id,issue_type,title,description,status,priority,created_by_user_id,updated_by_user_id)
    values (uuid_generate_v4(),'${projectId}','Project','${projectId}','Other','합성 정산 차단','합성 데이터로 만든 정산 차단 항목입니다.','Registered','Normal','${salesUserId}','${salesUserId}');
  `);
  const pendingBlocked = await request.post(`${apiBaseUrl}/api/projects/${projectId}/settlement/complete`, {
    headers: devHeaders('dev-sales'),
    data: { operationId: crypto.randomUUID(), expectedVersion: 0, invoiceIssuedDate: currentSeoulDate, invoiceNumber: 'SYNTH-014A', note: '합성 정산' }
  });
  expect(pendingBlocked.status()).toBe(409);
  queryDatabase(`update pending_issues set status='Closed',assignee_user_id='${salesUserId}',closed_by_user_id='${salesUserId}',closed_at_utc=now(),version=version+1 where project_id='${projectId}';`);

  const draft = await putJson(request, `/api/projects/${projectId}/settlement/draft`, 'dev-sales', {
    expectedVersion: 0, invoiceIssuedDate: currentSeoulDate, invoiceNumber: 'SYNTH-014A', note: '합성 정산'
  }) as Mutation;
  expect(draft.version).toBe(1);

  await page.setViewportSize({ width: 1440, height: 980 });
  await page.goto(`/projects/${projectId}/settlement`);
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await page.goto(`/projects/${projectId}/settlement`);
  await expect(page.getByRole('heading', { name: '발행 확인 후 완료하기' })).toBeVisible();
  await expect(page.getByText('2/2')).toBeVisible();
  await expect(page.getByText('임시 저장 v1')).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await page.screenshot({ path: '/tmp/task-014a-sales-settlement-desktop.png', fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.getByRole('heading', { name: '발행 확인 후 완료하기' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await page.screenshot({ path: '/tmp/task-014a-sales-settlement-mobile.png', fullPage: true });

  const operationId = crypto.randomUUID();
  const completionRequest = { operationId, expectedVersion: 1, invoiceIssuedDate: currentSeoulDate, invoiceNumber: 'SYNTH-014A', note: '합성 정산' };
  const completed = await postJson(request, `/api/projects/${projectId}/settlement/complete`, 'dev-sales', completionRequest) as Mutation;
  const replay = await postJson(request, `/api/projects/${projectId}/settlement/complete`, 'dev-sales', completionRequest) as Mutation;
  expect(completed.status).toBe('Completed');
  expect(replay.replayed).toBe(true);

  await page.reload();
  await expect(page.getByRole('heading', { name: '프로젝트 완료 내역' })).toBeVisible();
  await expect(page.getByText('프로젝트가 최종 완료되었습니다.')).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await page.screenshot({ path: '/tmp/task-014a-sales-settlement-mobile-completed.png', fullPage: true });

  expect(queryDatabase(`select status from projects where id='${projectId}';`)).toBe('Completed');
  expect(queryDatabase(`select status from work_items where id='${genericWorkId}';`)).toBe('Completed');
  expect(queryDatabase(`select count(*)::text from sales_settlements where project_id='${projectId}' and status='Completed';`)).toBe('1');
  expect(queryDatabase(`select count(*)::text from sales_settlement_operations where project_id='${projectId}';`)).toBe('1');
  expect(queryDatabase(`select count(*)::text from project_workflow_events where project_id='${projectId}' and stage_code='SalesSettlementCompleted' and event_status='Succeeded';`)).toBe('1');
  expect(queryDatabase(`select count(*)::text from notifications where project_id='${projectId}' and idempotency_key='sales-settlement:project:${projectId}:completed' and message not like '%SYNTH-014A%';`)).toBe('1');
  expect(queryDatabase(`
    select count(*)::text
    from notification_recipients recipient
    join notifications notification on notification.id = recipient.notification_id
    where notification.idempotency_key='sales-settlement:project:${projectId}:completed';
  `)).toBe('0');
  expect(queryDatabase(`
    select count(*)::text
    from notification_deliveries delivery
    join notifications notification on notification.id = delivery.notification_id
    where notification.idempotency_key='sales-settlement:project:${projectId}:completed'
      and delivery.channel <> 'Mail';
  `)).toBe('0');
  await expect.poll(() => queryDatabase(`
    select (
      select count(*)
      from notification_deliveries delivery
      join notifications notification on notification.id = delivery.notification_id
      where notification.idempotency_key='sales-settlement:project:${projectId}:completed'
        and delivery.channel = 'Mail'
    ) = (
      select count(*)
      from qms_users user_account
      join departments department on department.id = user_account.department_id
      where user_account.is_active = true and department.code = 'sales'
    );
  `), { timeout: 15_000 }).toBe('t');

  const afterCompletionPending = await request.post(`${apiBaseUrl}/api/pending`, {
    headers: devHeaders('dev-sales'),
    data: { projectId, issueType: 'Other', title: '완료 뒤 생성 차단', description: '합성 검증을 위한 충분한 길이의 설명입니다.', priority: 'Normal', assigneeUserId: null, dueDate: null }
  });
  expect(afterCompletionPending.status()).toBe(409);

  const changePanelCount = await request.post(`${apiBaseUrl}/api/projects/${projectId}/change-panel-count`, {
    headers: devHeaders('dev-sales'), data: { panelCount: 3, expectedActivePanelCount: 2, cancelPanelIds: [], reason: '합성 완료 후 변경 시도' }
  });
  expect(changePanelCount.status()).toBe(409);

  const hold = await request.post(`${apiBaseUrl}/api/projects/${projectId}/hold`, {
    headers: devHeaders('dev-sales'), data: { reason: '합성 완료 후 상태 변경 시도' }
  });
  expect(hold.status()).toBe(409);

  const deleteResponse = await request.post(`${apiBaseUrl}/api/projects/${projectId}/delete`, {
    headers: devHeaders('dev-sales'), data: { reason: '합성 완료 후 삭제 시도', confirmProjectTitle: project.projectTitle }
  });
  expect(deleteResponse.status()).toBe(409);
});

test('TASK-014A: concurrent completion has one winner and zero-panel completion is rejected', async ({ request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const concurrentProject = await createSyntheticProject(request, `RACE-${unique}`, `합성 동시 정산 ${unique}`);
  const concurrentPanels = queryDatabase(`select string_agg(id::text, ',' order by sequence_number) from panel_placeholders where project_id='${concurrentProject.projectId}' and status='Active';`).split(',');
  seedDeliveredProject(concurrentProject.projectId, concurrentPanels);
  await putJson(request, `/api/projects/${concurrentProject.projectId}/settlement/draft`, 'dev-sales', {
    expectedVersion: 0, invoiceIssuedDate: currentSeoulDate, invoiceNumber: null, note: '합성 동시 정산'
  });

  const attempts = await Promise.all([
    request.post(`${apiBaseUrl}/api/projects/${concurrentProject.projectId}/settlement/complete`, {
      headers: devHeaders('dev-sales'),
      data: { operationId: crypto.randomUUID(), expectedVersion: 1, invoiceIssuedDate: currentSeoulDate, invoiceNumber: 'RACE-A', note: null }
    }),
    request.post(`${apiBaseUrl}/api/projects/${concurrentProject.projectId}/settlement/complete`, {
      headers: devHeaders('dev-sales'),
      data: { operationId: crypto.randomUUID(), expectedVersion: 1, invoiceIssuedDate: currentSeoulDate, invoiceNumber: 'RACE-B', note: null }
    })
  ]);
  expect(attempts.map((response) => response.status()).sort()).toEqual([200, 409]);
  expect(queryDatabase(`select count(*)::text from sales_settlement_operations where project_id='${concurrentProject.projectId}';`)).toBe('1');

  const zeroProject = await createSyntheticProject(request, `ZERO-${unique}`, `합성 빈 프로젝트 ${unique}`);
  queryDatabase(`
    update panel_placeholders set status='Cancelled',cancelled_by_user_id='${salesUserId}',cancelled_at_utc=now() where project_id='${zeroProject.projectId}';
    insert into work_items(project_id,target_type,target_id,workflow_stage_code,responsibility_type,assigned_user_id,assigned_role_code,title,description,status,priority,idempotency_key,created_by_user_id)
    values ('${zeroProject.projectId}','Project','${zeroProject.projectId}','SalesSettlementCompleted','SalesPrimary','${salesUserId}','sales','영업 정산 처리','합성 zero-panel 정산','Requested','Normal','e2e:settlement:zero:${zeroProject.projectId}','${salesUserId}');
  `);
  const zeroDetail = await getJson(request, `/api/projects/${zeroProject.projectId}/settlement`, 'dev-sales') as Settlement & { activePanelCount: number; deliveredPanelCount: number };
  expect(zeroDetail.activePanelCount).toBe(0);
  expect(zeroDetail.deliveredPanelCount).toBe(0);
  const zeroCompletion = await request.post(`${apiBaseUrl}/api/projects/${zeroProject.projectId}/settlement/complete`, {
    headers: devHeaders('dev-sales'),
    data: { operationId: crypto.randomUUID(), expectedVersion: 0, invoiceIssuedDate: currentSeoulDate, invoiceNumber: null, note: null }
  });
  expect(zeroCompletion.status()).toBe(409);
});

type Project = { projectId: string; projectTitle: string };
type Mutation = { status: string; version: number; replayed: boolean };
type Settlement = { activePanelCount: number; deliveredPanelCount: number; canMutate: boolean };

async function createSyntheticProject(request: APIRequestContext, projectCode: string, projectTitle: string): Promise<Project> {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Settlement Customer', item: 'RPP', projectCode, projectTitle,
      panelCount: 2, deliveryDate: '2026-12-10', salesOwnerUserId: salesUserId,
      packagingMethod: 'WoodenCrate', salesAmount: null, currencyCode: null,
      deliveryLocation: 'Synthetic Site', fatRequired: false
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json() as Promise<Project>;
}

function seedDeliveredProject(projectId: string, panelIds: string[]) {
  const unitId = crypto.randomUUID();
  const batchId = crypto.randomUUID();
  const billingBatchId = crypto.randomUUID();
  queryDatabase(`
    insert into project_assignees(project_id,responsibility_type,assigned_user_id,assigned_by_user_id,assigned_at_utc)
    values ('${projectId}','SalesPrimary','${salesUserId}','${salesUserId}',now())
    on conflict(project_id,responsibility_type) do update set assigned_user_id=excluded.assigned_user_id;
    insert into user_project_access(user_id,project_id) values ('${adminUserId}','${projectId}') on conflict do nothing;
    insert into logistics_packing_units(id,project_id,unit_number,status,version,created_by_user_id)
    values ('${unitId}','${projectId}',1,'Draft',1,'${salesUserId}');
    insert into logistics_packing_unit_panels(packing_unit_id,panel_id,active,added_by_user_id)
    values ('${unitId}','${panelIds[0]}',true,'${salesUserId}'),('${unitId}','${panelIds[1]}',true,'${salesUserId}');
    insert into logistics_batches(id,project_id,stage_code,batch_number,status,version,created_by_user_id)
    values ('${batchId}','${projectId}','DeliveryCompleted',1,'Draft',1,'${salesUserId}');
    insert into logistics_batch_units(batch_id,packing_unit_id,stage_code,active,added_by_user_id)
    values ('${batchId}','${unitId}','DeliveryCompleted',true,'${salesUserId}');
    insert into logistics_batch_panels(batch_id,packing_unit_id,panel_id,stage_code,active,added_by_user_id)
    values ('${batchId}','${unitId}','${panelIds[0]}','DeliveryCompleted',true,'${salesUserId}'),
           ('${batchId}','${unitId}','${panelIds[1]}','DeliveryCompleted',true,'${salesUserId}');
    insert into logistics_delivery_results(batch_id,packing_unit_id,panel_id,delivered_by_user_id)
    values ('${batchId}','${unitId}','${panelIds[0]}','${salesUserId}'),('${batchId}','${unitId}','${panelIds[1]}','${salesUserId}');
    update logistics_packing_units set status='Finalized',finalized_by_user_id='${salesUserId}',finalized_at_utc=now() where id='${unitId}';
    update logistics_batches set status='Finalized',finalized_by_user_id='${salesUserId}',finalized_at_utc=now() where id='${batchId}';
    insert into sales_billing_request_batches(
      id,period_start,period_end,project_count,workbook_file_name,workbook_size,
      workbook_sha256,workbook_content,created_by_user_id)
    values (
      '${billingBatchId}',current_date,current_date,1,'synthetic-settlement.xlsx',1,
      repeat('0',64),decode('00','hex'),'${salesUserId}');
    insert into sales_billing_request_items(
      batch_id,project_id,row_number,project_code,project_title,customer_name,item_name,
      first_departure_date,last_departure_date,active_panel_count,departed_panel_count,
      sales_amount,currency_code,sales_owner_name)
    select
      '${billingBatchId}',id,1,project_code,project_title,customer_name,item,
      current_date,current_date,2,2,coalesce(sales_amount,0),coalesce(currency_code,'KRW'),'Synthetic Sales Owner'
    from projects where id='${projectId}';
    insert into work_items(project_id,target_type,target_id,workflow_stage_code,responsibility_type,assigned_user_id,assigned_role_code,title,description,status,priority,idempotency_key,created_by_user_id)
    values ('${projectId}','Project','${projectId}','SalesSettlementCompleted','SalesPrimary','${salesUserId}','sales','영업 정산 처리','합성 정산 업무','Requested','Normal','e2e:settlement:${projectId}','${salesUserId}');
  `);
}

async function getJson(request: APIRequestContext, path: string, user: string) {
  const response = await request.get(`${apiBaseUrl}${path}`, { headers: devHeaders(user) });
  if (!response.ok()) throw new Error(`${path} failed (${response.status()}): ${await response.text()}`);
  return response.json();
}

async function putJson(request: APIRequestContext, path: string, user: string, data: unknown) {
  const response = await request.put(`${apiBaseUrl}${path}`, { headers: devHeaders(user), data });
  if (!response.ok()) throw new Error(`${path} failed (${response.status()}): ${await response.text()}`);
  return response.json();
}

async function postJson(request: APIRequestContext, path: string, user: string, data: unknown) {
  const response = await request.post(`${apiBaseUrl}${path}`, { headers: devHeaders(user), data });
  if (!response.ok()) throw new Error(`${path} failed (${response.status()}): ${await response.text()}`);
  return response.json();
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
