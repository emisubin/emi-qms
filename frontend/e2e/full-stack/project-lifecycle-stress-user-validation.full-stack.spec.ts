import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type Locator, type Page } from '@playwright/test';

const screenshotDirectory = path.resolve(
  process.env.STRESS_LIFECYCLE_SCREENSHOT_DIR?.trim() || '/tmp/emi-qms-stress-lifecycle-evidence'
);
const evidenceImage = path.resolve('src/assets/emi-logo.png');
const salesUserId = '50000000-0000-0000-0000-000000000002';
const productionUserId = '50000000-0000-0000-0000-000000000003';
const manufacturingUserId = '50000000-0000-0000-0000-000000000004';
const qualityUserId = '50000000-0000-0000-0000-000000000005';
const logisticsUserId = '50000000-0000-0000-0000-000000000006';
const designUserId = '50000000-0000-0000-0000-000000000010';
const procurementUserId = '50000000-0000-0000-0000-000000000011';
const materialsUserId = '50000000-0000-0000-0000-000000000012';
const panelCount = 12;
const pendingPanelIndexes = new Set([0, 2, 4, 6, 8, 10]);
const customerReceiptDates = ['2026-07-15', '2026-07-16', '2026-07-17', '2026-07-18', '2026-07-19', '2026-07-20'];
const currentSeoulDate = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit'
}).format(new Date());

type DevelopmentUserKey =
  | 'dev-sales'
  | 'dev-production'
  | 'dev-design'
  | 'dev-procurement'
  | 'dev-materials'
  | 'dev-manufacturing'
  | 'dev-quality'
  | 'dev-logistics';

type StressEvidence = {
  panelCount: number;
  customerReceiptCount: number;
  purchasedReceiptCount: number;
  customerSupplyOverdueAlertCount: number;
  repeatedPendingCount: number;
  pendingNotificationCount: number;
  pendingExternalDeliveryChannelCount: number;
  manufacturingThreeDayPanelCount: number;
  completedWorkflowStageCount: number;
  openPendingCount: number;
  projectCompleted: boolean;
};

const developmentUserNames: Record<DevelopmentUserKey, string> = {
  'dev-sales': 'Dev Sales User',
  'dev-production': 'Dev Production Planning User',
  'dev-design': 'Dev Design User',
  'dev-procurement': 'Dev Procurement User',
  'dev-materials': 'Dev Materials User',
  'dev-manufacturing': 'Dev Manufacturing User',
  'dev-quality': 'Dev Quality User',
  'dev-logistics': 'Dev Logistics User'
};

test('12면 혼합 자재·분할 지연 입고·반복 제조 Pending을 최종 완료까지 실제 역할 화면으로 검수한다', async ({ page }) => {
  test.setTimeout(1_800_000);
  const unique = Date.now();
  const projectCode = `STRESS-${String(unique).slice(-7)}`;
  const projectTitle = `합성 12면 혼합자재 검수 ${unique}`;
  const billingWorkbookPath = path.join(screenshotDirectory, `billing-request-${unique}.xlsx`);
  const pendingUrls: string[] = [];

  await fs.mkdir(path.join(screenshotDirectory, 'dashboards'), { recursive: true });
  await fs.mkdir(path.join(screenshotDirectory, 'materials'), { recursive: true });
  await fs.mkdir(path.join(screenshotDirectory, 'manufacturing'), { recursive: true });
  await fs.mkdir(path.join(screenshotDirectory, 'notifications'), { recursive: true });
  await fs.mkdir(path.join(screenshotDirectory, 'stages'), { recursive: true });
  await page.setViewportSize({ width: 1440, height: 1050 });
  page.setDefaultTimeout(15_000);

  // 1. 영업 담당자가 12면 프로젝트를 등록한다.
  await page.goto('/projects');
  await switchUser(page, 'dev-sales');
  await page.goto('/projects');
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await page.getByLabel('고객사*').fill('합성 고객사');
  await page.getByLabel('Item*').selectOption('RPP');
  await page.getByLabel('PJT Code*').fill(projectCode);
  await page.getByLabel('PJT Title*').fill(projectTitle);
  await page.getByLabel('면수*').fill(String(panelCount));
  await page.getByLabel('납기일*').fill('2026-08-31');
  await page.getByLabel('영업담당자*').selectOption(salesUserId);
  await page.getByLabel('포장방식*').selectOption('WoodenCrate');
  await page.getByLabel('판매금액').fill('480000000');
  await page.getByLabel('통화').fill('KRW');
  await page.getByLabel('납품장소').fill('합성 고객 현장');
  await page.getByLabel('FAT 필요 여부').selectOption('true');
  await page.getByRole('button', { name: '등록' }).click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();

  const projectId = queryDatabase(`select id::text from projects where project_code='${projectCode}';`);
  const panelIds = queryDatabase(`select string_agg(id::text, ',' order by sequence_number) from panel_placeholders where project_id='${projectId}' and status='Active';`).split(',');
  expect(panelIds).toHaveLength(panelCount);
  await captureProjectFlow(page, projectId, 'stages/01-project-created.jpg');

  // 2. 생산관리 담당자가 계획 일정과 전 부서 정·부 담당자를 지정한다.
  await switchUser(page, 'dev-production');
  await page.goto('/production-planning');
  await page.getByPlaceholder('프로젝트명, 고객사, Code, Item 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  const productionRow = page.getByRole('table', { name: '생산계획 프로젝트 목록' })
    .locator('.production-project-row').filter({ hasText: projectTitle });
  await productionRow.click();
  await page.getByLabel('선택 프로젝트 생산계획').getByRole('button', { name: '생산계획 수정' }).click();
  await page.waitForLoadState('networkidle');
  const planDates = page.getByRole('table', { name: '생산계획 수정' }).locator('input[type="date"]');
  const plannedDates = ['2026-07-15', '2026-07-20', '2026-08-05', '2026-08-20'];
  for (let index = 0; index < await planDates.count(); index += 1) {
    await planDates.nth(index).fill(plannedDates[Math.min(index, plannedDates.length - 1)]);
  }
  const assignees: Array<[string, string]> = [
    ['영업 정', salesUserId], ['영업 부', salesUserId],
    ['설계 정', designUserId], ['설계 부', designUserId],
    ['생산관리 정', productionUserId], ['생산관리 부', productionUserId],
    ['구매 정', procurementUserId], ['구매 부', procurementUserId],
    ['자재 정', materialsUserId], ['자재 부', materialsUserId],
    ['제조 정', manufacturingUserId], ['제조 부', manufacturingUserId],
    ['물류 정', logisticsUserId], ['물류 부', logisticsUserId],
    ['IQC 정', qualityUserId], ['IQC 부', qualityUserId],
    ['LQC 정', qualityUserId], ['LQC 부', qualityUserId],
    ['OQC 정', qualityUserId], ['OQC 부', qualityUserId],
    ['전진검수/FAT 정', qualityUserId], ['전진검수/FAT 부', qualityUserId]
  ];
  for (const [label, userId] of assignees) await page.getByLabel(label).selectOption(userId);
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByText('생산계획을 저장했습니다.')).toBeVisible();

  // 3. 설계 KPI의 12면 미입력 상태를 본 뒤 담당자가 12면을 모두 입력한다.
  await captureHome(page, 'dev-design', 'dashboards/03-design-before-panel-input.jpg');
  await page.goto(`/projects/${projectId}`);
  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  for (let sequence = 1; sequence <= panelCount; sequence += 1) {
    await page.getByLabel(`No.${sequence} 패널명`).fill(`MCC-STRESS-${String(sequence).padStart(2, '0')}`);
    await page.getByLabel(`No.${sequence} W`).fill(String(760 + sequence * 5));
    await page.getByLabel(`No.${sequence} H`).fill('2000');
    await page.getByLabel(`No.${sequence} D`).fill('600');
  }
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  await expect(page.getByRole('table', { name: '설계' })).toContainText('MCC-STRESS-12');
  await captureProjectFlow(page, projectId, 'stages/03-panel-design-12.jpg');

  // 4. 구매 담당자가 일반 구매품과 사급품을 한 프로젝트에 함께 입력한다.
  await switchUser(page, 'dev-procurement');
  await page.goto(`/projects/${projectId}`);
  await page.getByRole('tab', { name: '구매' }).click();
  await page.getByRole('button', { name: '구매정보 수정' }).click();
  const procurementTable = page.getByRole('table', { name: '구매정보 수정' });
  const procurementRows = procurementTable.locator('.procurement-table-row.editable');
  await expect(procurementTable).toBeVisible();
  await page.getByRole('button', { name: '행 추가' }).click();
  await expect(procurementRows).toHaveCount(1);
  await page.getByRole('button', { name: '행 추가' }).click();
  await expect(procurementRows).toHaveCount(2);
  await fillProcurementRow(procurementRows.nth(0), {
    leadTime: '3주', item: '제어기 일반 구매품', supplier: '합성 공급사', owner: '구매 기술담당',
    orderDate: '2026-07-01', expectedDate: '2026-07-18', issue: '일반 구매 정상 입고', supplyType: 'Purchased'
  });
  await fillProcurementRow(procurementRows.nth(1), {
    leadTime: '고객 제공', item: '고객 사급 동부스바', supplier: '고객 제공', owner: '자재 추적담당',
    orderDate: '2026-07-01', expectedDate: '2026-07-14', issue: '7월 15~20일 분할 제공, 지연 추적 필요',
    supplyType: 'CustomerSupplied', quantity: '12', unit: 'EA'
  });
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByText('구매정보를 저장했습니다.')).toBeVisible();
  await expect(page.getByRole('table', { name: '구매정보', exact: true })).toContainText('사급 · 고객 제공');
  await captureHome(page, 'dev-procurement', 'dashboards/04-procurement-overdue-mixed-items.jpg');
  await captureProjectTab(page, projectId, 'dev-procurement', '구매', 'stages/04-procurement-mixed-items.jpg');

  // 5~7. 일반 구매 1회와 사급 6회 분할 도착을 모두 UI로 IQC·확정한다.
  await registerInspectAndConfirmReceipt(page, projectTitle, '제어기 일반 구매품', 12, '2026-07-18', true, '일반 구매품 정상 도착');
  for (let index = 0; index < customerReceiptDates.length; index += 1) {
    const arrivalDate = customerReceiptDates[index];
    await registerInspectAndConfirmReceipt(
      page, projectTitle, '고객 사급 동부스바', 2, arrivalDate, false,
      `사급 분할 입고 ${index + 1}/6 · 예정일 이후 도착`
    );
    if (index === 2) {
      await captureMaterialsTracking(page, projectTitle, 'materials/05-customer-supply-partial-late-0717.jpg');
      await captureHome(page, 'dev-materials', 'dashboards/05-materials-partial-late.jpg');
      await captureNotifications(page, 'dev-materials', 'notifications/05-materials-late-notifications.jpg');
    }
  }

  const customerSupplyOverdueAlertCount = Number(queryDatabase(`
    select count(*)::text from notifications n
    where n.project_id='${projectId}'
      and (n.source_kind in ('CustomerSupplyOverdue','MaterialExpectedReceiptOverdue')
        or n.idempotency_key ilike '%customer%supply%overdue%'
        or n.idempotency_key ilike '%material%expected%overdue%');
  `));
  expect(customerSupplyOverdueAlertCount).toBe(0);

  await closeMaterialItem(page, projectTitle, '제어기 일반 구매품', '일반 구매품 전량 확정');
  await closeMaterialItem(page, projectTitle, '고객 사급 동부스바', '7월 15~20일 사급 12 EA 전량 확정');
  await captureMaterialsTracking(page, projectTitle, 'materials/07-mixed-materials-completed.jpg', true);

  // 8. 자재 담당자가 12면을 한 번에 키팅 완료한다.
  await switchUser(page, 'dev-materials');
  await page.goto(`/materials/kitting?project=${projectId}`);
  const kittingCards = page.locator('.kitting-panel-card');
  await expect(kittingCards).toHaveCount(panelCount);
  for (let index = 0; index < panelCount; index += 1) await kittingCards.nth(index).click();
  await page.getByRole('button', { name: `${panelCount}면 키팅 완료` }).click();
  await expect(page.getByText(new RegExp(`제조 업무 ${panelCount}건`))).toBeVisible();
  await captureProjectFlow(page, projectId, 'stages/08-kitting-12-completed.jpg');

  // 9. 제조 담당자가 12면을 시작하고 절반에서 반복 Pending을 발생시킨다.
  await switchUser(page, 'dev-manufacturing');
  for (let index = 0; index < panelIds.length; index += 1) {
    await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelIds[index]}`);
    await page.getByRole('button', { name: '제조 시작' }).click();
    for (let sequence = 1; sequence <= 4; sequence += 1) {
      await page.getByRole('button', { name: `${sequence}단계 확인` }).click();
      await expect(page.getByRole('button', {
        name: sequence < 4 ? `${sequence + 1}단계 확인` : '제조 완료 · LQC 전달'
      })).toBeVisible();
    }
    if (pendingPanelIndexes.has(index)) {
      await page.getByRole('button', { name: '작업 중단' }).click();
      const dialog = page.getByRole('dialog', { name: '제조 작업 중단' });
      await dialog.getByPlaceholder('현장에서 확인한 문제와 필요한 조치를 10자 이상 입력하세요.')
        .fill(`${index + 1}번 패널 제조 중 배선 도면과 현장 치수가 달라 생산관리 확인이 필요합니다.`);
      await dialog.getByLabel('조치 담당 부서').selectOption('production-planning');
      await dialog.getByRole('button', { name: '작업 중단 · 긴급 Pending 생성' }).click();
      const pendingLink = page.getByRole('button', { name: /긴급 Pending/u });
      await pendingLink.click();
      pendingUrls.push(new URL(page.url()).pathname);
    }
  }
  expect(pendingUrls).toHaveLength(6);
  await captureHome(page, 'dev-manufacturing', 'dashboards/09-manufacturing-six-blocked.jpg');
  await captureHome(page, 'dev-production', 'dashboards/09-production-six-pending.jpg');
  await captureNotifications(page, 'dev-production', 'notifications/09-production-pending-notifications.jpg');
  await captureMyWork(page, 'dev-production', 'notifications/09-production-pending-my-work.jpg');

  await expect.poll(() => Number(queryDatabase(`
    select count(*)::text from notification_deliveries d
    join notifications n on n.id=d.notification_id
    where n.project_id='${projectId}' and n.source_kind='PendingAssignment'
      and d.channel in ('TeamsChannel','Mail');
  `)), { timeout: 30_000 }).toBe(12);

  // 생산관리 담당자가 6개 Pending을 모두 실제 상태 전이로 종결한다.
  for (const pendingUrl of pendingUrls) {
    await switchUser(page, 'dev-production');
    await page.goto(pendingUrl);
    await expect(page.locator('.pending-next-action')).toBeVisible();
    for (let transition = 0; transition < 5; transition += 1) {
      const nextAction = page.locator('.pending-next-action');
      if (await nextAction.count() === 0) break;
      const nextButton = nextAction.getByRole('button');
      const label = (await nextButton.textContent())?.trim() ?? '상태 변경';
      await page.getByLabel('상태 변경 사유').fill(`${label} · 도면 확인 및 작업 재개 승인`);
      await nextButton.click();
      await expect(page.getByText(`${label} 상태로 변경되었습니다.`)).toBeVisible();
    }
    await expect(page.locator('.pending-detail-header .status-badge').filter({ hasText: '종결' })).toBeVisible();
  }

  // 제조 담당자가 6개 Pending을 재개하고 12면 모두 LQC로 인계한다.
  for (let index = 0; index < panelIds.length; index += 1) {
    await switchUser(page, 'dev-manufacturing');
    await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelIds[index]}`);
    if (pendingPanelIndexes.has(index)) await page.getByRole('button', { name: 'Pending 확인 후 재개' }).click();
    await page.getByRole('button', { name: '제조 완료 · LQC 전달' }).click();
    await expect(page.getByText(/제조를 완료하고 LQC 업무를 생성했습니다/u)).toBeVisible();
  }
  queryDatabase(`
    update panel_manufacturing_executions
    set started_at_utc=completed_at_utc-interval '3 days'
    where project_id='${projectId}' and status='Completed' and completed_at_utc is not null;
  `);
  const manufacturingThreeDayPanelCount = Number(queryDatabase(`
    select count(*)::text from panel_manufacturing_executions
    where project_id='${projectId}' and status='Completed'
      and completed_at_utc-started_at_utc=interval '3 days';
  `));
  expect(manufacturingThreeDayPanelCount).toBe(panelCount);
  await captureHome(page, 'dev-quality', 'dashboards/10-quality-twelve-lqc-waiting.jpg');
  await captureProjectTab(page, projectId, 'dev-manufacturing', '제조', 'manufacturing/10-manufacturing-12-three-day.jpg');

  // 10~14. 12면 각각 LQC → 제조 확인 → OQC → 전진검수 → FAT를 입력한다.
  for (let index = 0; index < panelIds.length; index += 1) {
    await completeQualityStage(page, 'LQC', 'LQC', projectId, panelIds[index], index + 1);
    await switchUser(page, 'dev-manufacturing');
    await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelIds[index]}`);
    await page.getByRole('button', { name: '제조 완료 확인' }).click();
    await expect(page.getByText('OQC 업무가 생성되었습니다.')).toBeVisible();
    await completeQualityStage(page, 'OQC', 'OQC 자체검수', projectId, panelIds[index], index + 1);
    await completeQualityStage(page, 'CustomerInspection', '전진검수', projectId, panelIds[index], index + 1);
    await completeQualityStage(page, 'FAT', 'FAT', projectId, panelIds[index], index + 1);
  }
  await captureHome(page, 'dev-logistics', 'dashboards/15-logistics-twelve-packing-waiting.jpg');
  await captureProjectFlow(page, projectId, 'stages/14-quality-12-completed.jpg');

  // 15~17. 물류 담당자가 12면 전체를 한 묶음으로 포장·출발·납품한다.
  await completeLogisticsStage(page, 'packing', '포장 묶음 시작', '포장 확정', projectId, true);
  await captureProjectFlow(page, projectId, 'stages/15-packing-completed.jpg');
  await completeLogisticsStage(page, 'departure', '상차 확인 시작', '출발 확정', projectId, true);
  await captureProjectFlow(page, projectId, 'stages/16-departure-completed.jpg');
  await completeLogisticsStage(page, 'delivery', '인수 확인 시작', '납품 확정', projectId, false);
  await captureProjectFlow(page, projectId, 'stages/17-delivery-completed.jpg');

  // 18. 영업이 회계팀 발행요청 Excel을 만들고 회계 확인값으로 최종 완료한다.
  await switchUser(page, 'dev-sales');
  await page.goto('/sales/billing-requests');
  await page.getByLabel('시작일').fill(currentSeoulDate);
  await page.getByLabel('종료일').fill(currentSeoulDate);
  await page.getByRole('button', { name: '조회' }).click();
  const billingRow = page.locator('.billing-table tbody tr').filter({ hasText: projectTitle });
  await billingRow.getByRole('checkbox').check();
  await page.getByPlaceholder('이번 요청에 필요한 참고사항').fill('12면 혼합 자재 프로젝트 정기 발행요청');
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: '선택 1개 Excel 만들기' }).click();
  const download = await downloadPromise;
  await download.saveAs(billingWorkbookPath);
  await capture(page, 'stages/18a-billing-request-created.jpg');

  await page.goto(`/projects/${projectId}/settlement`);
  await page.getByLabel('회계팀 발행 확인일 필수').fill(currentSeoulDate);
  await page.getByLabel('회계팀 세금계산서 번호').fill(`STRESS-${String(unique).slice(-6)}`);
  await page.getByLabel('회계 확인 메모').fill('회계팀 발행 완료와 12면 납품, 미결 Pending 0건을 확인했습니다.');
  await page.getByRole('button', { name: '임시 저장' }).click();
  await page.getByRole('button', { name: '최종 완료 확인' }).click();
  await page.getByRole('button', { name: '발행 확인·프로젝트 완료' }).click();
  await expect(page.getByRole('heading', { name: '프로젝트 완료 내역' })).toBeVisible();
  await captureProjectFlow(page, projectId, 'stages/18-project-completed.jpg');
  await captureHome(page, 'dev-sales', 'dashboards/18-sales-completed-revenue.jpg');

  const completedWorkflowStageCount = Number(queryDatabase(`
    select count(distinct stage_code)::text from project_workflow_events
    where project_id='${projectId}' and event_type='StageCompleted' and event_status='Succeeded';
  `));
  const openPendingCount = Number(queryDatabase(`select count(*)::text from pending_issues where project_id='${projectId}' and status<>'Closed';`));
  const pendingNotificationCount = Number(queryDatabase(`select count(*)::text from notifications where project_id='${projectId}' and source_kind='PendingAssignment';`));
  const pendingExternalDeliveryChannelCount = Number(queryDatabase(`
    select count(distinct channel)::text from notification_deliveries d join notifications n on n.id=d.notification_id
    where n.project_id='${projectId}' and n.source_kind='PendingAssignment' and d.channel in ('TeamsChannel','Mail');
  `));
  const projectCompleted = queryDatabase(`select (status='Completed')::text from projects where id='${projectId}';`) === 'true';
  expect(completedWorkflowStageCount).toBe(18);
  expect(openPendingCount).toBe(0);
  expect(pendingNotificationCount).toBe(6);
  expect(pendingExternalDeliveryChannelCount).toBe(2);
  expect(projectCompleted).toBe(true);

  const evidence: StressEvidence = {
    panelCount,
    customerReceiptCount: 6,
    purchasedReceiptCount: 1,
    customerSupplyOverdueAlertCount,
    repeatedPendingCount: pendingUrls.length,
    pendingNotificationCount,
    pendingExternalDeliveryChannelCount,
    manufacturingThreeDayPanelCount,
    completedWorkflowStageCount,
    openPendingCount,
    projectCompleted
  };
  await fs.writeFile(path.join(screenshotDirectory, 'stress-validation-summary.json'), `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
  console.log(`STRESS_LIFECYCLE_VALIDATION_READY panels=${panelCount} customerReceipts=6 pending=6 workflow=18 openPending=0 completed=true`);
});

async function fillProcurementRow(row: Locator, values: {
  leadTime: string; item: string; supplier: string; owner: string; orderDate: string; expectedDate: string;
  issue: string; supplyType: 'Purchased' | 'CustomerSupplied'; quantity?: string; unit?: string;
}) {
  const inputs = row.locator('input');
  await inputs.nth(0).fill(values.leadTime);
  await inputs.nth(1).fill(values.item);
  await inputs.nth(2).fill(values.supplier);
  await inputs.nth(3).fill(values.owner);
  await inputs.nth(4).fill(values.orderDate);
  await inputs.nth(5).fill(values.expectedDate);
  await inputs.nth(6).fill(values.issue);
  await row.getByLabel('공급 방식').selectOption(values.supplyType);
  if (values.supplyType === 'CustomerSupplied') {
    await row.getByLabel('제공 예정 수량').fill(values.quantity ?? '12');
    await row.getByLabel('제공 예정 단위').fill(values.unit ?? 'EA');
  }
}

async function registerInspectAndConfirmReceipt(
  page: Page,
  projectTitle: string,
  itemName: string,
  quantity: number,
  arrivalDate: string,
  firstPurchasedArrival: boolean,
  note: string
) {
  await switchUser(page, 'dev-materials');
  await page.goto('/materials/receipts');
  await searchMaterials(page, projectTitle);
  let item = page.locator('.material-continuous-item').filter({ hasText: itemName });
  await item.getByRole('button', { name: '도착분 추가' }).click();
  const action = item.locator('.material-action-form');
  if (firstPurchasedArrival) await action.getByLabel('발주 수량').fill(String(quantity));
  await action.getByLabel('도착 수량').fill(String(quantity));
  await action.getByLabel('도착일').fill(arrivalDate);
  await action.getByLabel('비고').fill(note);
  await action.getByRole('button', { name: '도착 등록', exact: true }).click();
  await expect(page.getByText('도착분을 등록하고 IQC 검사 대기로 넘겼습니다.')).toBeVisible();

  await switchUser(page, 'dev-quality');
  await page.goto('/quality/iqc');
  const iqcCard = page.locator('.iqc-request-card').filter({ hasText: itemName }).filter({ hasText: projectTitle });
  await iqcCard.click();
  await completeIqc(page.locator('.material-action-drawer--iqc-report'));

  await switchUser(page, 'dev-materials');
  await page.goto('/materials/receipts');
  await searchMaterials(page, projectTitle);
  item = page.locator('.material-continuous-item').filter({ hasText: itemName });
  await item.locator('.material-receipt-chip').filter({ hasText: arrivalDate }).click();
  await item.getByRole('button', { name: '입고 확정' }).click();
  await expect(page.getByText('입고를 확정했습니다.')).toBeVisible();
}

async function closeMaterialItem(page: Page, projectTitle: string, itemName: string, reason: string) {
  await switchUser(page, 'dev-materials');
  await page.goto('/materials/receipts');
  await searchMaterials(page, projectTitle);
  const item = page.locator('.material-continuous-item').filter({ hasText: itemName });
  await item.getByRole('button', { name: '품목 입고 마감' }).click();
  await item.getByLabel('마감 사유').fill(reason);
  await item.getByRole('button', { name: '입고 마감', exact: true }).click();
  await expect(page.getByText('입고를 마감하고 완료값을 계산했습니다.')).toBeVisible();
}

async function completeIqc(scope: Locator) {
  await scope.getByRole('button', { name: '검사 시작' }).click();
  const cards = scope.locator('.iqc-item-card');
  await expect(cards.first()).toBeVisible();
  for (let index = 0; index < await cards.count(); index += 1) {
    const card = cards.nth(index);
    const pass = card.getByRole('button', { name: '적합', exact: true });
    if (await pass.count()) {
      await pass.click();
    } else {
      await card.locator('textarea').fill('수량·외관·식별 상태 정상');
    }
  }
  await scope.getByRole('button', { name: '저장하고 사진 등록' }).click();
  await scope.locator('input[type="file"]').setInputFiles(evidenceImage);
  await scope.getByRole('button', { name: '사진 등록' }).click();
  await scope.getByRole('button', { name: '최종확인으로' }).click();
  await scope.getByLabel('종합 판정 사유').fill('사급·구매품 입고 검사 기준 적합을 확인했습니다.');
  await scope.getByRole('button', { name: '합격 · 성적서 확정' }).click();
  await expect(scope.getByText('IQC 합격 성적서를 확정했습니다.')).toBeVisible();
}

async function completeQualityStage(
  page: Page,
  stage: 'LQC' | 'OQC' | 'CustomerInspection' | 'FAT',
  stageLabel: string,
  projectId: string,
  panelId: string,
  sequence: number
) {
  await switchUser(page, 'dev-quality');
  await page.goto(`/quality/inspections?stage=${stage}&project=${projectId}&panel=${panelId}`);
  await page.getByRole('button', { name: `${stageLabel} 시작` }).click();
  const items = page.locator('.quality-item');
  await expect(items.first()).toBeVisible();
  for (let index = 0; index < await items.count(); index += 1) {
    const item = items.nth(index);
    const pass = item.getByRole('button', { name: '적합', exact: true });
    if (await pass.count()) await pass.click();
    const text = item.getByPlaceholder('측정값·특이사항을 입력하세요.');
    if (await text.count()) await text.fill(`${sequence}번 패널 ${stageLabel} 측정값 정상`);
  }
  await page.getByRole('button', { name: '임시 저장' }).click();
  const uploader = page.locator('.quality-photo-uploader');
  await uploader.locator('input[type="file"]').setInputFiles(evidenceImage);
  await uploader.getByPlaceholder('예: 배선 체결 상태').fill(`${sequence}번 패널 ${stageLabel} 증빙`);
  await uploader.getByRole('button', { name: '사진 등록' }).click();
  await page.getByRole('button', { name: '판정 확정' }).click();
  const decision = page.getByRole('dialog');
  await decision.locator('button[data-result="passed"]').click();
  await decision.getByLabel('판정 사유').fill(`${sequence}번 패널 ${stageLabel} 기준 적합`);
  await decision.getByRole('button', { name: '합격 확정 및 인계' }).click();
  await expect(page.locator('.quality-finalized-card')).toContainText('PASS');
}

async function completeLogisticsStage(
  page: Page,
  stage: 'packing' | 'departure' | 'delivery',
  startButton: string,
  finalizeButton: string,
  projectId: string,
  needsAltText: boolean
) {
  await switchUser(page, 'dev-logistics');
  await page.goto(`/logistics?stage=${stage}&project=${projectId}`);
  const targets = page.locator('.logistics-target-card');
  await expect(targets.first()).toBeVisible();
  for (let index = 0; index < await targets.count(); index += 1) await targets.nth(index).click();
  if (stage === 'packing') await page.getByLabel('포장 메모').fill('12면 목재 포장과 방수 상태 확인');
  if (stage === 'departure') await page.getByLabel('출발일').fill(currentSeoulDate);
  await page.getByRole('button', { name: startButton }).click();
  const evidence = page.locator('.logistics-evidence-form');
  await evidence.locator('input[type="file"]').setInputFiles(evidenceImage);
  if (needsAltText) await evidence.getByLabel('사진 설명').fill(`${finalizeButton} 12면 현장 증빙`);
  const uploadButton = evidence.getByRole('button', { name: '증빙 등록' });
  await expect(uploadButton).toBeEnabled();
  await uploadButton.click();
  await expect(page.getByText('증빙을 안전하게 등록했습니다. 내용을 확인하고 확정해 주세요.')).toBeVisible();
  await page.getByRole('button', { name: finalizeButton }).click();
  await expect(page.getByText(new RegExp(`${finalizeButton.replace(' 확정', '')} 확정 완료`))).toBeVisible();
}

async function captureHome(page: Page, userKey: DevelopmentUserKey, filename: string) {
  await switchUser(page, userKey);
  await page.goto('/');
  await expect(page.getByRole('heading', { name: '업무 홈' })).toBeVisible();
  await expect(page.getByLabel('내 부서 핵심 지표').or(page.getByLabel('영업팀 연간 매출 지표'))).toBeVisible();
  await capture(page, filename);
}

async function captureNotifications(page: Page, userKey: DevelopmentUserKey, filename: string) {
  await switchUser(page, userKey);
  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: '알림', exact: true })).toBeVisible();
  await capture(page, filename);
}

async function captureMyWork(page: Page, userKey: DevelopmentUserKey, filename: string) {
  await switchUser(page, userKey);
  await page.goto('/my-work');
  await expect(page.getByRole('heading', { name: '내 업무', exact: true })).toBeVisible();
  await capture(page, filename);
}

async function captureMaterialsTracking(page: Page, projectTitle: string, filename: string, includeCompleted = false) {
  await switchUser(page, 'dev-materials');
  await page.goto('/materials/receipts');
  if (includeCompleted) await page.getByLabel('완료 포함').check();
  await searchMaterials(page, projectTitle);
  await capture(page, filename);
}

async function captureProjectFlow(page: Page, projectId: string, filename: string) {
  await switchUser(page, 'dev-sales');
  await page.goto(`/projects/${projectId}`);
  await page.getByRole('tablist', { name: '프로젝트 상세 섹션' }).getByRole('tab', { name: '전체 흐름' }).click();
  await expect(page.locator('.workflow-stage-item')).toHaveCount(18);
  await capture(page, filename);
}

async function captureProjectTab(
  page: Page,
  projectId: string,
  userKey: DevelopmentUserKey,
  tabName: string,
  filename: string
) {
  await switchUser(page, userKey);
  await page.goto(`/projects/${projectId}`);
  await page.getByRole('tablist', { name: '프로젝트 상세 섹션' }).getByRole('tab', { name: tabName, exact: true }).click();
  await capture(page, filename);
}

async function capture(page: Page, filename: string) {
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({
    path: path.join(screenshotDirectory, filename), type: 'jpeg', quality: 84, fullPage: true, animations: 'disabled'
  });
}

async function searchMaterials(page: Page, projectTitle: string) {
  await page.getByPlaceholder('PJT 코드, 발주품목, 업체').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  await expect(page.locator('.material-item-card').filter({ hasText: projectTitle }).first()).toBeVisible();
}

async function switchUser(page: Page, userKey: DevelopmentUserKey) {
  const selector = page.getByLabel('개발 사용자');
  await expect(selector).toBeVisible();
  if (await selector.inputValue() !== userKey) await selector.selectOption(userKey);
  await expect(selector).toHaveValue(userKey);
  await expect(page.locator('.account-identity-trigger')).toContainText(developmentUserNames[userKey]);
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
