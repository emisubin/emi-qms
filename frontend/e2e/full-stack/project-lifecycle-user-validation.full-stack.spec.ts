import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type Locator, type Page } from '@playwright/test';
import { markProjectAsLegacyIqc, uploadRequiredIqcPhotos } from './legacy-iqc-fixture';
import { ensureLqcOperational } from './lqc-operating-fixture';

const screenshotDirectory = path.resolve(
  process.env.LIFECYCLE_SCREENSHOT_DIR?.trim() || '/tmp/emi-qms-lifecycle-evidence'
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
const currentSeoulDate = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit'
}).format(new Date());

type DevelopmentUserKey =
  | 'dev-admin'
  | 'dev-sales'
  | 'dev-production'
  | 'dev-design'
  | 'dev-procurement'
  | 'dev-materials'
  | 'dev-manufacturing'
  | 'dev-quality'
  | 'dev-logistics'
  | 'dev-viewer';

type HandoffEvidence = {
  checkpoint: string;
  userKey: DevelopmentUserKey;
  notificationProjectVisible: boolean;
  myWorkProjectVisible: boolean | null;
};

const developmentUsers: Array<{ key: DevelopmentUserKey; displayName: string }> = [
  { key: 'dev-admin', displayName: 'Dev System Administrator' },
  { key: 'dev-sales', displayName: 'Dev Sales User' },
  { key: 'dev-production', displayName: 'Dev Production Planning User' },
  { key: 'dev-design', displayName: 'Dev Design User' },
  { key: 'dev-procurement', displayName: 'Dev Procurement User' },
  { key: 'dev-materials', displayName: 'Dev Materials User' },
  { key: 'dev-manufacturing', displayName: 'Dev Manufacturing User' },
  { key: 'dev-quality', displayName: 'Dev Quality User' },
  { key: 'dev-logistics', displayName: 'Dev Logistics User' },
  { key: 'dev-viewer', displayName: 'Dev Read Only User' }
];

test('영업 등록부터 세금계산서 완료까지 역할별 화면 입력으로 18단계를 연속 검수한다', async ({ page, request }) => {
  test.setTimeout(900_000);
  const unique = Date.now();
  const projectCode = `LIFE-${String(unique).slice(-8)}`;
  const projectTitle = `합성 전체업무 검수 ${unique}`;
  const handoffEvidence: HandoffEvidence[] = [];
  const billingWorkbookPath = path.join(screenshotDirectory, `billing-request-${unique}.xlsx`);

  await fs.mkdir(screenshotDirectory, { recursive: true });
  await fs.mkdir(path.join(screenshotDirectory, 'creation-notifications'), { recursive: true });
  await fs.mkdir(path.join(screenshotDirectory, 'handoffs'), { recursive: true });
  await page.setViewportSize({ width: 1440, height: 1050 });
  page.setDefaultTimeout(10_000);

  await ensureLqcOperational(request);

  // 1. 영업이 프로젝트를 화면에서 신규 등록한다.
  await page.goto('/projects');
  await switchUser(page, 'dev-sales');
  await page.goto('/projects');
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await page.getByLabel('고객사*').fill('합성 고객사');
  await page.getByLabel('Item*').selectOption('RPP');
  await page.getByLabel('PJT Code*').fill(projectCode);
  await page.getByLabel('PJT Title*').fill(projectTitle);
  await page.getByLabel('면수*').fill('1');
  await page.getByLabel('납기일*').fill('2026-12-10');
  await page.getByLabel('영업담당자*').selectOption(salesUserId);
  await page.getByLabel('포장방식*').selectOption('WoodenCrate');
  await page.getByLabel('판매금액').fill('125000000');
  await page.getByLabel('통화').fill('KRW');
  await page.getByLabel('납품장소').fill('합성 고객 현장');
  await page.getByRole('group', { name: 'FAT 필요 여부' }).getByRole('button', { name: /^필요 패널별/u }).click();
  await page.getByRole('button', { name: '등록' }).click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();

  const projectId = queryDatabase(`select id::text from projects where project_code='${projectCode}';`);
  const panelId = queryDatabase(`select id::text from panel_placeholders where project_id='${projectId}' and status='Active';`);
  expect(projectId).toMatch(/^[0-9a-f-]{36}$/u);
  markProjectAsLegacyIqc(projectId);
  expect(panelId).toMatch(/^[0-9a-f-]{36}$/u);
  await captureProjectDetail(page, projectId, '01-project-created.jpg');
  const creationEvidence: HandoffEvidence[] = [];
  for (const user of developmentUsers) {
    const evidence = await captureNotificationEvidence(
      page,
      user.key,
      projectTitle,
      `creation-notifications/01-project-created-${user.key}.jpg`,
      '01-project-created'
    );
    creationEvidence.push(evidence);
    handoffEvidence.push(evidence);
  }
  for (const evidence of creationEvidence) {
    const shouldReceive = evidence.userKey !== 'dev-admin' && evidence.userKey !== 'dev-viewer';
    expect(evidence.notificationProjectVisible, `${evidence.userKey} 프로젝트 생성 알림`).toBe(shouldReceive);
  }
  const productionBeforeAssignment = await captureAssigneeEvidence(
    page, 'dev-production', projectTitle, '01-to-production', '01-to-production'
  );
  expect(productionBeforeAssignment.notificationProjectVisible).toBe(true);
  expect(productionBeforeAssignment.myWorkProjectVisible).toBe(false);
  handoffEvidence.push(productionBeforeAssignment);

  // 2. 생산관리 담당자가 일정과 모든 정 담당자를 화면에서 지정한다.
  await switchUser(page, 'dev-production');
  await page.goto('/production-planning/plans');
  await page.getByPlaceholder('프로젝트명, 고객사, Code, Item 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  const productionRow = page.getByRole('table', { name: '생산계획 프로젝트 목록' })
    .locator('.production-project-row').filter({ hasText: projectTitle });
  await expect(productionRow).toBeVisible();
  await productionRow.click();
  await page.getByLabel('선택 프로젝트 생산계획').getByRole('button', { name: '생산계획 수정' }).click();
  await page.waitForLoadState('networkidle');
  await openInputSection(page, '생산계획표 입력');
  const planDates = page.getByRole('table', { name: '생산계획 수정' }).locator('input[type="date"]');
  const plannedDates = ['2026-07-20', '2026-08-10', '2026-09-10', '2026-10-10'];
  for (let index = 0; index < await planDates.count(); index += 1) {
    const plannedDate = plannedDates[Math.min(index, plannedDates.length - 1)];
    await planDates.nth(index).fill(plannedDate);
    await expect(planDates.nth(index)).toHaveValue(plannedDate);
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
  await openInputSection(page, '담당자 지정');
  for (const [label, userId] of assignees) {
    await page.getByLabel(label).selectOption(userId);
    await expect(page.getByLabel(label)).toHaveValue(userId);
  }
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByText('생산계획을 저장했습니다.')).toBeVisible();
  expect(queryDatabase(`select count(*)::text from project_production_plan_items item join project_production_plans plan on plan.id=item.production_plan_id where plan.project_id='${projectId}' and item.is_active and item.is_required and item.planned_date is not null;`)).toBe('4');
  expect(queryDatabase(`select count(*)::text from project_assignees assignee where assignee.project_id='${projectId}' and assignee.assigned_user_id is not null and assignee.responsibility_type in ('SalesPrimary','DesignPrimary','ProductionPlanningPrimary','ProcurementPrimary','MaterialsPrimary','ManufacturingPrimary','LogisticsPrimary','QualityIQC','QualityLQC','QualityOQC','QualityCustomerInspection');`)).toBe('11');
  expect(queryDatabase(`select exists(select 1 from project_workflow_events event where event.project_id='${projectId}' and event.stage_code='ProductionPlanning' and event.event_type='StageCompleted' and event.event_status='Succeeded')::text;`)).toBe('true');
  await captureProjectDetail(page, projectId, '02-production-plan-assignees.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-design', projectTitle, '02-to-design', '02-to-design'
  ));
  const procurementBeforeDesign = await captureAssigneeEvidence(
    page, 'dev-procurement', projectTitle, '02-procurement-assigned', '02-procurement-assigned'
  );
  expect(procurementBeforeDesign.notificationProjectVisible).toBe(true);
  expect(procurementBeforeDesign.myWorkProjectVisible).toBe(true);
  handoffEvidence.push(procurementBeforeDesign);

  // 3. 설계 담당자가 패널명과 치수를 직접 입력한다.
  await switchUser(page, 'dev-design');
  await page.goto(`/projects/${projectId}`);
  await page.getByRole('tab', { name: '설계' }).click();
  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  await page.getByLabel('No.1 패널명').fill('MCC-LIFE-01');
  await page.getByLabel('No.1 W').fill('800');
  await page.getByLabel('No.1 H').fill('2000');
  await page.getByLabel('No.1 D').fill('600');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  await expect(page.getByRole('table', { name: '설계' })).toContainText('MCC-LIFE-01');
  await captureProjectDetail(page, projectId, '03-panel-design.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-procurement', projectTitle, '03-to-procurement', '03-to-procurement'
  ));

  // 4. 구매 담당자가 발주정보를 화면에서 입력한다.
  await switchUser(page, 'dev-procurement');
  await page.goto(`/projects/${projectId}`);
  await page.getByRole('tab', { name: '구매' }).click();
  await page.getByRole('button', { name: '구매정보 수정' }).click();
  await page.getByRole('button', { name: '도급 구매품 행 추가' }).click();
  const procurementTable = page.getByRole('table', { name: '구매정보 수정' });
  await expect(procurementTable).toBeVisible();
  const procurementRows = procurementTable.locator('.procurement-table-row.editable');
  await expect(procurementRows).toHaveCount(1);
  const procurementRow = procurementRows.first();
  const procurementInputs = procurementRow.locator('input');
  await procurementInputs.nth(0).fill('4주');
  await procurementInputs.nth(1).fill('제어반 외함');
  await procurementInputs.nth(2).fill('합성 공급사');
  await procurementInputs.nth(3).fill('합성 기술담당');
  await procurementInputs.nth(4).fill('2026-07-20');
  await procurementInputs.nth(5).fill('2026-08-10');
  await procurementInputs.nth(6).fill('초도 발주');
  await procurementRow.getByLabel('발주 수량').fill('1');
  await procurementRow.getByLabel('발주 단위').fill('EA');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('table', { name: '구매정보' })).toContainText('제어반 외함');
  await captureProjectDetail(page, projectId, '04-procurement-entered.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-materials', projectTitle, '04-to-materials', '04-to-materials'
  ));

  // 5. 자재 담당자가 도착 수량을 등록한다.
  await switchUser(page, 'dev-materials');
  await page.goto('/materials/receipts');
  await searchMaterials(page, projectTitle);
  let materialCard = page.locator('.material-purchase-row').filter({ hasText: '제어반 외함' });
  await materialCard.getByRole('button', { name: '도착입력' }).click();
  await page.getByLabel('도착 수량').fill('1');
  const unitInput = page.getByLabel('단위');
  if (await unitInput.isEnabled() && !(await unitInput.inputValue())) await unitInput.fill('EA');
  await page.getByLabel('비고').fill('외관 손상 없이 도착');
  await page.getByRole('button', { name: '도착분 저장', exact: true }).click();
  await materialCard.getByText(/행을 눌러 이력 보기/u).click();
  await expect(materialCard.locator('.material-receipt-line')).toBeVisible();
  await captureProjectDetail(page, projectId, '05-material-arrived.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-quality', projectTitle, '05-to-quality-iqc', '05-to-quality-iqc'
  ));

  // 6. 도착 등록으로 자동 생성된 IQC를 품질 담당자가 체크·사진·판정한다.
  await switchUser(page, 'dev-quality');
  await page.goto('/quality/iqc');
  await openOperationalProject(page, projectTitle);
  const iqcCard = page.locator('.iqc-request-card').filter({ hasText: projectTitle });
  await expect(iqcCard).toBeVisible();
  await iqcCard.click();
  const iqcDrawer = page.locator('.material-action-drawer--iqc-report');
  await completeIqc(iqcDrawer);
  await expect(iqcDrawer.getByText('IQC 합격 성적서를 확정했습니다.')).toBeVisible();
  await expect(iqcDrawer.getByText('IQC 합격', { exact: true })).toBeVisible();
  await captureProjectDetail(page, projectId, '06-iqc-passed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-materials', projectTitle, '06-to-material-receipt', '06-to-material-receipt'
  ));

  // 7. 자재 담당자가 합격분을 입고 확정하면 전량 품목은 자동 마감된다.
  await switchUser(page, 'dev-materials');
  await page.goto('/materials/receipts');
  await searchMaterials(page, projectTitle);
  materialCard = page.locator('.material-purchase-row').filter({ hasText: '제어반 외함' });
  await materialCard.getByText(/행을 눌러 이력 보기/u).click();
  await materialCard.locator('.material-receipt-line').click();
  await page.getByRole('button', { name: '입고 확정' }).click();
  await expect(page.getByText('입고를 확정했습니다. 전량 확정이면 품목도 자동 완료됩니다.')).toBeVisible();
  await page.getByLabel('완료 포함').check();
  await searchMaterials(page, projectTitle);
  materialCard = page.locator('.material-purchase-row').filter({ hasText: '제어반 외함' });
  await expect(materialCard).toContainText('입고 완료');
  await captureProjectDetail(page, projectId, '07-material-receipt-confirmed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-materials', projectTitle, '07-to-kitting', '07-to-kitting'
  ));

  // 8. 자재 담당자가 패널을 선택해 키팅 완료한다.
  await switchUser(page, 'dev-materials');
  await page.goto(`/materials/kitting?project=${projectId}`);
  const kittingPanel = page.locator('.kitting-panel-card').filter({ hasText: 'MCC-LIFE-01' });
  await expect(kittingPanel).toBeVisible();
  await expect(kittingPanel).toContainText('선택 가능');
  await kittingPanel.click();
  await page.getByRole('button', { name: '1면 키팅 완료' }).click();
  await expect(page.getByText('마지막 패널까지 키팅 완료 상태를 공유했습니다.')).toBeVisible();
  await captureProjectDetail(page, projectId, '08-kitting-completed.jpg');
  expect(queryDatabase(`select count(*)::text from work_items where project_id='${projectId}' and workflow_stage_code='Manufacturing' and status <> 'Cancelled';`)).toBe('0');

  // 키팅은 참고 상태다. 생산관리 담당자가 실제 투입 패널을 선택해야 제조 업무가 생성된다.
  await switchUser(page, 'dev-production');
  await page.goto('/production-planning/releases');
  const releaseProject = page.locator('.production-project-row').filter({ hasText: projectTitle });
  await expect(releaseProject).toBeVisible();
  await releaseProject.click();
  const releasePanel = page.getByLabel('패널 제조 투입 요청');
  await expect(releasePanel.getByText('키팅 완료')).toBeVisible();
  await releasePanel.locator('.manufacturing-release-row input[type="checkbox"]').check();
  await releasePanel.getByRole('button', { name: '선택 1면 제조 투입 요청' }).click();
  await expect(releasePanel.getByText('1면을 제조팀에 투입 요청했습니다. 제조 업무 1건이 생성되었습니다.')).toBeVisible();
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-manufacturing', projectTitle, '08-to-manufacturing', '08-to-manufacturing'
  ));

  // 9. 제조 담당자가 작업을 입력하고 중단 Pending을 만든다.
  await switchUser(page, 'dev-manufacturing');
  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await page.getByRole('button', { name: '제조 시작' }).click();
  for (let sequence = 1; sequence <= 4; sequence += 1) {
    await page.getByRole('button', { name: `${sequence}단계 확인` }).click();
  }
  await page.getByRole('button', { name: '작업 중단' }).click();
  const stopDialog = page.getByRole('dialog', { name: '제조 작업 중단' });
  await stopDialog.getByPlaceholder('현장에서 확인한 문제와 필요한 조치를 10자 이상 입력하세요.')
    .fill('도면 치수와 현장 측정값을 다시 대조해야 해서 작업을 중단합니다.');
  await stopDialog.getByLabel('조치 담당 부서').selectOption('production-planning');
  await stopDialog.getByRole('button', { name: '작업 중단 · 긴급 Pending 생성' }).click();
  const pendingLink = page.getByRole('button', { name: /긴급 Pending/u });
  await expect(pendingLink).toBeVisible();
  await pendingLink.click();
  const pendingUrl = new URL(page.url()).pathname;
  await captureProjectDetail(page, projectId, '09a-manufacturing-pending-open.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-production', projectTitle, '09a-pending-to-production', '09a-pending-to-production'
  ));
  await expect.poll(() => Number(queryDatabase(`
    select count(distinct delivery.channel)::text
    from notification_deliveries delivery
    join notifications notification on notification.id=delivery.notification_id
    where notification.project_id='${projectId}'
      and notification.source_kind='PendingAssignment'
      and delivery.channel in ('TeamsChannel','Mail');
  `)), { timeout: 20_000 }).toBe(2);

  // 자동 지정된 생산관리 담당자가 실제 Pending 화면에서 종결까지 처리한다.
  await switchUser(page, 'dev-production');
  await page.goto(pendingUrl);
  await expect(page.getByRole('definition').filter({ hasText: 'Dev Production Planning User' })).toBeVisible();
  for (let transition = 0; transition < 5; transition += 1) {
    const nextAction = page.locator('.pending-next-action');
    if (await nextAction.count() === 0) break;
    const nextButton = nextAction.getByRole('button');
    const nextLabel = (await nextButton.textContent())?.trim() ?? '상태 변경';
    await page.getByLabel('처리 내용').fill(`${nextLabel} 담당자 화면 입력 확인`);
    await nextButton.click();
    await expect(page.getByText(nextLabel === '조치 완료'
      ? '조치를 완료하고 품질 재검사 업무를 생성했습니다.'
      : `${nextLabel} 상태로 변경되었습니다.`)).toBeVisible();
  }
  await expect(page.locator('.pending-detail-header .status-badge').filter({ hasText: '종결' })).toBeVisible();
  await captureProjectDetail(page, projectId, '09b-manufacturing-pending-closed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-manufacturing', projectTitle, '09b-pending-back-to-manufacturing', '09b-pending-back-to-manufacturing'
  ));

  // 제조 담당자가 Pending 종결을 확인하고 패널 제조를 완료한다.
  await switchUser(page, 'dev-manufacturing');
  await page.goto(`/manufacturing/work?project=${projectId}&panel=${panelId}`);
  await page.getByRole('button', { name: 'Pending 확인 후 재개' }).click();
  await page.getByRole('button', { name: '제조 완료' }).click();
  await expect(page.getByText(/제조를 완료했습니다/u)).toBeVisible();
  await captureProjectDetail(page, projectId, '09-manufacturing-completed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-quality', projectTitle, '09-to-lqc', '09-to-lqc'
  ));

  // 10. 품질 담당자가 LQC를 입력·확정한다.
  await completeQualityStage(page, 'LQC', 'LQC', projectId, panelId);
  await captureProjectDetail(page, projectId, '10-lqc-passed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-quality', projectTitle, '10-to-oqc', '10-to-oqc'
  ));

  // 11. 제조와 LQC 공동 완료가 자동 기록되고 OQC가 열린다.
  await captureProjectDetail(page, projectId, '11-manufacturing-confirmed.jpg');

  // 12~14. 품질 담당자가 OQC, 전진검수, FAT를 같은 실제 입력 흐름으로 확정한다.
  await completeQualityStage(page, 'OQC', 'OQC 자체검수', projectId, panelId);
  await captureProjectDetail(page, projectId, '12-oqc-passed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-quality', projectTitle, '12-to-customer-inspection', '12-to-customer-inspection'
  ));
  await completeQualityStage(page, 'CustomerInspection', '전진검수', projectId, panelId);
  await captureProjectDetail(page, projectId, '13-customer-inspection-passed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-quality', projectTitle, '13-to-fat', '13-to-fat'
  ));
  await completeQualityStage(page, 'FAT', 'FAT', projectId, panelId);
  await captureProjectDetail(page, projectId, '14-fat-passed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-logistics', projectTitle, '14-to-packing', '14-to-packing'
  ));

  // 15~17. 물류 담당자가 대상 선택, 증빙, 확정을 화면에서 단계별 수행한다.
  await completeLogisticsStage(page, 'packing', projectId, true);
  await captureProjectDetail(page, projectId, '15-packing-completed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-logistics', projectTitle, '15-to-departure', '15-to-departure'
  ));
  await completeLogisticsStage(page, 'departure', projectId, true);
  await captureProjectDetail(page, projectId, '16-departure-completed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-logistics', projectTitle, '16-to-delivery', '16-to-delivery'
  ));
  await completeLogisticsStage(page, 'delivery', projectId, false);
  await captureProjectDetail(page, projectId, '17-delivery-completed.jpg');
  handoffEvidence.push(await captureAssigneeEvidence(
    page, 'dev-sales', projectTitle, '17-to-sales-settlement', '17-to-sales-settlement'
  ));

  // 18. 영업담당자가 출하 프로젝트를 선택해 회계팀 발행요청 Excel을 만들고, 회계 발행 확인 후 최종 완료한다.
  const shipmentDate = queryDatabase(`
    select max(batch.departure_date)::text
    from logistics_batches batch
    where batch.project_id='${projectId}' and batch.stage_code='DepartureProcessed' and batch.status='Finalized';
  `);
  expect(shipmentDate).toMatch(/^\d{4}-\d{2}-\d{2}$/u);
  expect(queryDatabase(`select count(*)::text from panel_placeholders where project_id='${projectId}' and status='Active';`)).toBe('1');
  expect(queryDatabase(`
    select count(distinct panel.id)::text
    from panel_placeholders panel
    join logistics_packing_unit_panels membership on membership.panel_id=panel.id and membership.active
    join logistics_batch_units batch_unit on batch_unit.packing_unit_id=membership.packing_unit_id and batch_unit.stage_code='DepartureProcessed' and batch_unit.active
    join logistics_batches batch on batch.id=batch_unit.batch_id and batch.status='Finalized'
    where panel.project_id='${projectId}' and panel.status='Active';
  `)).toBe('1');
  expect(queryDatabase(`select count(*)::text from pending_issues where project_id='${projectId}' and status<>'Closed';`)).toBe('0');
  const shipmentMonthStart = `${shipmentDate.slice(0, 7)}-01`;
  await switchUser(page, 'dev-sales');
  await page.goto('/sales/billing-requests');
  await expect(page.getByRole('heading', { name: '세금계산서 발행요청' })).toBeVisible();
  await page.getByLabel('종료일').fill(shipmentDate);
  await page.getByLabel('시작일').fill(shipmentMonthStart);
  await page.getByRole('button', { name: '조회' }).click();
  const billingRow = page.locator('.billing-table tbody tr').filter({ hasText: projectTitle });
  await expect(billingRow).toBeVisible();
  await billingRow.getByRole('checkbox').check();
  await page.getByPlaceholder('이번 요청에 필요한 참고사항').fill('1·16일 정기 회계팀 발행요청');
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: '선택 1개 Excel 만들기' }).click();
  const download = await downloadPromise;
  await download.saveAs(billingWorkbookPath);
  await expect(page.getByText('1개 프로젝트의 회계팀 발행요청 자료를 만들었습니다.')).toBeVisible();
  await captureEvidenceScreenshot(page, '18a-billing-request-created.jpg');

  await page.goto(`/projects/${projectId}/settlement`);
  await expect(page.getByRole('heading', { name: '발행 확인 후 완료하기' })).toBeVisible();
  await expect(page.getByText(/요청 #/u)).toBeVisible();
  await page.getByLabel('회계팀 발행 확인일 필수').fill(currentSeoulDate);
  await page.getByLabel('회계팀 세금계산서 번호').fill(`LIFE-${String(unique).slice(-6)}`);
  await page.getByLabel('회계 확인 메모').fill('회계팀 발행 완료 회신과 미결 Pending 0건을 확인했습니다.');
  await expectInputFlowReadable(page, '.settlement-form > .ds-input-flow');
  await expectDarkSurfaceTextReadable(page);
  await captureEvidenceScreenshot(page, '18b-settlement-input-layout.jpg');
  await page.setViewportSize({ width: 390, height: 844 });
  await expectInputFlowReadable(page, '.settlement-form > .ds-input-flow');
  await expectNoHorizontalOverflow(page);
  await captureEvidenceScreenshot(page, '18b-settlement-input-layout-mobile.jpg');
  await page.setViewportSize({ width: 1440, height: 1050 });
  await page.getByRole('button', { name: '발행 확인 저장' }).click();
  await expect(page.getByText('회계팀 발행 확인 정보를 임시 저장했습니다.')).toBeVisible();
  await page.getByRole('button', { name: '최종 완료 확인' }).click();
  await page.getByRole('button', { name: '발행 확인·프로젝트 완료' }).click();
  await expect(page.getByRole('heading', { name: '프로젝트 완료 내역' })).toBeVisible();
  await captureProjectDetail(page, projectId, '18-tax-invoice-project-completed.jpg');
  await captureProjectDepartmentTabs(page, projectId);
  await captureProductionPlanningTabs(page, projectId);

  expect(queryDatabase(`select status from projects where id='${projectId}';`)).toBe('Completed');
  expect(queryDatabase(`select count(*)::text from pending_issues where project_id='${projectId}' and status <> 'Closed';`)).toBe('0');
  const succeededWorkflowEventCount = Number(queryDatabase(
    `select count(*)::text from project_workflow_events where project_id='${projectId}' and event_status='Succeeded';`
  ));
  const completedWorkflowStageCount = Number(queryDatabase(
    `select count(distinct stage_code)::text from project_workflow_events where project_id='${projectId}' and event_type='StageCompleted' and event_status='Succeeded';`
  ));
  expect(completedWorkflowStageCount).toBe(18);
  await fs.writeFile(
    path.join(screenshotDirectory, 'handoff-coverage.json'),
    `${JSON.stringify({
      projectCode,
      projectTitle,
      projectStatus: 'Completed',
      openPendingCount: 0,
      succeededWorkflowEventCount,
      completedWorkflowStageCount,
      evidence: handoffEvidence
    }, null, 2)}\n`,
    'utf8'
  );
  console.log(`LIFECYCLE_VALIDATION_READY ${projectCode} ${projectTitle} ${billingWorkbookPath}`);

  const inspectionHoldMs = Number(process.env.LIFECYCLE_INSPECTION_HOLD_MS ?? '0');
  if (Number.isFinite(inspectionHoldMs) && inspectionHoldMs > 0) await page.waitForTimeout(inspectionHoldMs);
});

async function switchUser(page: Page, userKey: DevelopmentUserKey) {
  const selector = page.getByLabel('개발 사용자');
  await expect(selector).toBeVisible();
  if (await selector.inputValue() !== userKey) await selector.selectOption(userKey);
  await expect(selector).toHaveValue(userKey);
  const user = developmentUsers.find((candidate) => candidate.key === userKey);
  if (user) await expect(page.locator('.account-identity-trigger')).toContainText(user.displayName);
}

async function captureNotificationEvidence(
  page: Page,
  userKey: DevelopmentUserKey,
  projectTitle: string,
  filename: string,
  checkpoint: string
): Promise<HandoffEvidence> {
  await switchUser(page, userKey);
  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: '알림', exact: true })).toBeVisible();
  await expect(page.getByText('알림을 불러오는 중입니다.')).toHaveCount(0);
  const notificationProjectVisible = await page.locator('.workflow-project-group')
    .filter({ hasText: projectTitle }).isVisible().catch(() => false);
  await captureEvidenceScreenshot(page, filename);
  return { checkpoint, userKey, notificationProjectVisible, myWorkProjectVisible: null };
}

async function captureAssigneeEvidence(
  page: Page,
  userKey: DevelopmentUserKey,
  projectTitle: string,
  filenamePrefix: string,
  checkpoint: string
): Promise<HandoffEvidence> {
  await switchUser(page, userKey);
  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: '알림', exact: true })).toBeVisible();
  await expect(page.getByText('알림을 불러오는 중입니다.')).toHaveCount(0);
  const notificationProjectVisible = await page.locator('.workflow-project-group')
    .filter({ hasText: projectTitle }).isVisible().catch(() => false);
  await captureEvidenceScreenshot(page, `handoffs/${filenamePrefix}-notifications.jpg`);

  await page.goto('/my-work');
  await expect(page.getByRole('heading', { name: '내 업무', exact: true })).toBeVisible();
  await expect(page.getByText('내 업무를 불러오는 중입니다.')).toHaveCount(0);
  const myWorkProjectVisible = await page.locator('.workflow-project-group')
    .filter({ hasText: projectTitle }).isVisible().catch(() => false);
  await captureEvidenceScreenshot(page, `handoffs/${filenamePrefix}-my-work.jpg`);

  return { checkpoint, userKey, notificationProjectVisible, myWorkProjectVisible };
}

async function captureEvidenceScreenshot(page: Page, filename: string) {
  await expectDarkSurfaceTextReadable(page);
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({
    path: path.join(screenshotDirectory, filename),
    type: 'jpeg', quality: 82, fullPage: true, animations: 'disabled'
  });
}

async function searchMaterials(page: Page, projectTitle: string) {
  await page.getByPlaceholder('PJT 코드, 발주품목, 업체').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  const project = page.locator('.material-project-row').filter({ hasText: projectTitle });
  await expect(project).toBeVisible();
  if (await project.getAttribute('data-expanded') !== 'true') {
    await project.locator('.material-project-toggle').click();
  }
  await expect(project).toHaveAttribute('data-expanded', 'true');
}

async function openOperationalProject(page: Page, projectTitle: string) {
  const project = page.locator('.operational-project-row').filter({ hasText: projectTitle });
  await expect(project).toBeVisible();
  await project.click();
}

async function openInputSection(page: Page, title: string) {
  const section = page.locator('details.ds-input-section--collapsible').filter({ hasText: title });
  await expect(section).toBeVisible();
  if (await section.getAttribute('open') === null) await section.locator('summary').click();
  await expect(section).toHaveAttribute('open', '');
}

async function completeIqc(scope: Locator) {
  await scope.getByRole('button', { name: '검사 시작' }).click();
  await expect(scope.getByRole('heading', { name: '검사항목' })).toBeVisible();
  const cards = scope.locator('.iqc-item-card');
  for (let index = 0; index < await cards.count(); index += 1) {
    const pass = cards.nth(index).getByRole('button', { name: '적합', exact: true });
    if (await pass.count()) await pass.click();
  }
  const notes = scope.getByLabel('측정값·특이사항');
  if (await notes.count()) await notes.fill('외관과 치수 측정값 정상');
  await uploadRequiredIqcPhotos(scope, evidenceImage);
  await scope.getByRole('button', { name: '검사항목·사진 저장 후 최종확인' }).click();
  await scope.getByLabel('종합 판정 사유').fill('체크 항목과 외함 사진을 확인했습니다.');
  await scope.getByRole('button', { name: '합격 · 성적서 확정' }).click();
}

async function completeQualityStage(
  page: Page,
  stage: 'LQC' | 'OQC' | 'CustomerInspection' | 'FAT',
  stageLabel: string,
  projectId: string,
  panelId: string
) {
  await switchUser(page, 'dev-quality');
  await page.goto(`/quality/inspections?stage=${stage}&project=${projectId}&panel=${panelId}`);
  await expect(page.getByRole('heading', { name: `${stageLabel} 검사` })).toBeVisible();
  await page.getByRole('button', { name: `${stageLabel} 시작` }).click();
  const aggregate = stage === 'CustomerInspection' || stage === 'FAT';
  if (aggregate) {
    await expect(page.locator('.quality-aggregate-decision')).toContainText('패널 전체를 한 번에 판정');
    await expect(page.locator('.quality-item')).toHaveCount(0);
    await expect(page.getByRole('button', { name: '임시 저장' })).toHaveCount(0);
  } else {
    const items = page.locator('.quality-item');
    await expect(items.first()).toBeVisible();
    for (let index = 0; index < await items.count(); index += 1) {
      const item = items.nth(index);
      const pass = item.getByRole('button', { name: '적합', exact: true });
      if (await pass.count()) await pass.click();
      const text = item.getByPlaceholder('측정값·특이사항을 입력하세요.');
      if (await text.count()) await text.fill(`${stageLabel} 측정값 정상`);
    }
    await page.getByRole('button', { name: '임시 저장' }).click();
    await expect(page.getByText('검사 항목을 저장했습니다.')).toBeVisible();
  }
  if (aggregate) {
    const uploader = page.locator('.quality-photo-uploader');
    await uploader.locator('input[type="file"]').setInputFiles(evidenceImage);
    await uploader.getByPlaceholder('예: 배선 체결 상태').fill(`${stageLabel} 사진 증빙`);
    await uploader.getByRole('button', { name: '사진 등록' }).click();
    await expect(page.getByText('사진 증빙을 등록했습니다.')).toBeVisible();
  } else {
    const itemPhotoEditors = page.locator('.quality-item-photo');
    for (let index = 0; index < await itemPhotoEditors.count(); index += 1) {
      const editor = itemPhotoEditors.nth(index);
      await editor.locator('input[type="file"]').setInputFiles(evidenceImage);
      await editor.getByRole('button', { name: '이 항목에 사진 등록' }).click();
      await editor.locator('.quality-photo-list img').waitFor();
    }
  }
  await page.getByRole('button', { name: '판정 확정' }).click();
  const decision = page.getByRole('dialog');
  if (aggregate) {
    await decision.locator('button[data-result="passed"]').click();
  } else {
    await expect(decision.locator('.quality-decision-derived[data-result="passed"]')).toBeVisible();
  }
  await decision.getByLabel('판정 사유').fill(`${stageLabel} 기준 적합을 확인했습니다.`);
  await decision.getByRole('button', { name: '합격 확정 및 인계' }).click();
  await expect(page.locator('.quality-finalized-card')).toContainText('PASS');
}

async function completeLogisticsStage(
  page: Page,
  stage: 'packing' | 'departure' | 'delivery',
  projectId: string,
  needsAltText: boolean
) {
  await switchUser(page, 'dev-logistics');
  await page.goto(`/logistics?stage=${stage}&project=${projectId}`);
  const target = page.locator('.logistics-target-card').first();
  await expect(target).toBeVisible();
  await target.click();
  const selectionCount = page.locator('.logistics-queue > header > strong');
  await expect(selectionCount).toHaveText('1 선택');
  if (stage === 'packing') await page.getByLabel('포장 메모').fill('목재 포장과 방수 상태 확인');
  if (stage === 'departure') await page.getByLabel('출발일').fill(currentSeoulDate);
  await page.locator('.logistics-file-field input[type="file"]').setInputFiles(evidenceImage);
  if (needsAltText) await page.getByLabel('사진 설명').fill(`${logisticsStageLabel(stage)} 현장 증빙`);
  await expectInputFlowReadable(page, '.logistics-action-panel .ds-input-flow');
  await expectDarkSurfaceTextReadable(page);
  await captureEvidenceScreenshot(page, `logistics-${stage}-input-layout.jpg`);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(selectionCount).toHaveText('1 선택');
  await expectInputFlowReadable(page, '.logistics-action-panel .ds-input-flow');
  await expectNoHorizontalOverflow(page);
  await captureEvidenceScreenshot(page, `logistics-${stage}-input-layout-mobile.jpg`);
  await page.setViewportSize({ width: 1440, height: 1050 });
  await expect(selectionCount).toHaveText('1 선택');
  const finalizeButton = page.getByRole('button', { name: `${logisticsStageLabel(stage)} 저장 및 확정` });
  await expect(finalizeButton).toBeEnabled();
  await finalizeButton.click();
  await expect(page.getByText(new RegExp(`${logisticsStageLabel(stage)} 확정 완료`))).toBeVisible();
}

function logisticsStageLabel(stage: 'packing' | 'departure' | 'delivery') {
  return stage === 'packing' ? '포장' : stage === 'departure' ? '출발' : '납품';
}

async function expectInputFlowReadable(page: Page, selector: string) {
  const flow = page.locator(selector);
  await expect(flow).toBeVisible();
  await expect.poll(() => flow.locator('.ds-input-flow__header > div').evaluate((title) =>
    title.getBoundingClientRect().width)).toBeGreaterThan(120);
  const metrics = await flow.evaluate((flowElement) => {
    const header = flowElement.querySelector<HTMLElement>('.ds-input-flow__header');
    const title = flowElement.querySelector<HTMLElement>('.ds-input-flow__header > div');
    const steps = flowElement.querySelector<HTMLElement>('.ds-input-flow__header > ol');
    if (!header || !title || !steps) return null;
    return {
      headerWidth: header.getBoundingClientRect().width,
      titleWidth: title.getBoundingClientRect().width,
      stepsWidth: steps.getBoundingClientRect().width,
      headerScrollWidth: header.scrollWidth
    };
  });
  expect(metrics).not.toBeNull();
  expect(metrics!.titleWidth).toBeGreaterThan(120);
  expect(metrics!.stepsWidth).toBeLessThanOrEqual(metrics!.headerWidth + 1);
  expect(metrics!.headerScrollWidth).toBeLessThanOrEqual(metrics!.headerWidth + 1);
}

async function expectNoHorizontalOverflow(page: Page) {
  await expect.poll(() => page.evaluate(() =>
    Math.max(0, document.documentElement.scrollWidth - window.innerWidth))).toBe(0);
}

async function expectDarkSurfaceTextReadable(page: Page) {
  const unreadable = await page.evaluate(() => {
    const selectors = [
      '.production-control-version-list button.is-active',
      '.production-plan-set-tabs > button.active',
      '.production-plan-set-tabs > label.active',
      '.production-control-template-step > b',
      '.logistics-stage-switch button[aria-current="step"]',
      '.logistics-empty > span',
      '.logistics-project-heading > span',
      '.logistics-target-shape',
      '.logistics-summary-circle',
      '.logistics-confirm-box',
      '.iqc-report-panel > header > span',
      '.iqc-final-seal > i',
      '.iqc-legacy-report > span',
      '.department-workspace-heading > span',
      '.excel-export-button > span',
      '.account-photo-editor > span:last-child',
      '.form-template-catalog > button i'
    ];
    function luminance(value: string) {
      const channels = value.match(/\d+(?:\.\d+)?/gu)?.slice(0, 3).map(Number) ?? [255, 255, 255];
      const linear = channels.map((channel) => {
        const normalized = channel / 255;
        return normalized <= 0.03928 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2];
    }
    return Array.from(document.querySelectorAll<HTMLElement>(selectors.join(','))).flatMap((surface) => {
      const candidates = [surface, ...Array.from(surface.querySelectorAll<HTMLElement>('span,strong,small,b,em'))];
      const surfaceStyle = window.getComputedStyle(surface);
      const backgroundLuminance = luminance(surfaceStyle.backgroundColor);
      if (backgroundLuminance > 0.2) return [];
      return candidates.flatMap((candidate) => {
        const hasText = Array.from(candidate.childNodes).some((node) => node.nodeType === Node.TEXT_NODE && Boolean(node.textContent?.trim()));
        if (!hasText) return [];
        const candidateStyle = window.getComputedStyle(candidate);
        const backgroundChannels = candidateStyle.backgroundColor.match(/[\d.]+/gu) ?? [];
        const alpha = Number(backgroundChannels[3] ?? '1');
        const effectiveBackground = alpha > 0 ? luminance(candidateStyle.backgroundColor) : backgroundLuminance;
        const foregroundLuminance = luminance(candidateStyle.color);
        const contrast = (Math.max(effectiveBackground, foregroundLuminance) + 0.05)
          / (Math.min(effectiveBackground, foregroundLuminance) + 0.05);
        return contrast < 4.5 ? [`${surface.className || surface.tagName}:${candidate.tagName}`] : [];
      });
    });
  });
  expect(unreadable).toEqual([]);
}

async function captureProjectDetail(page: Page, projectId: string, filename: string) {
  await switchUser(page, 'dev-sales');
  await page.goto(`/projects/${projectId}`);
  await expect(page.locator('.account-identity-trigger')).toContainText('Dev Sales User');
  await expect(page.getByLabel('개발 사용자')).toHaveValue('dev-sales');
  await page.getByRole('tablist', { name: '프로젝트 상세 섹션' }).getByRole('tab', { name: '전체 흐름' }).click();
  await expect(page.locator('.workflow-stage-item')).toHaveCount(18);
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({
    path: path.join(screenshotDirectory, filename),
    type: 'jpeg', quality: 82, fullPage: true, animations: 'disabled'
  });
}

async function captureProjectDepartmentTabs(page: Page, projectId: string) {
  await switchUser(page, 'dev-sales');
  await page.goto(`/projects/${projectId}`);
  await page.getByRole('tablist', { name: '프로젝트 상세 섹션' }).getByRole('tab', { name: '전체 흐름' }).click();
  await expect(page.locator('.workflow-stage-item')).toHaveCount(18);
  const tabList = page.getByRole('tablist', { name: '프로젝트 상세 섹션' });
  const tabs = [
    { label: '전체 흐름', filename: '19-project-tab-workflow.jpg', kind: 'workflow', section: null, expectedText: '프로젝트 전체 흐름' },
    { label: '생산관리', filename: '20-project-tab-production.jpg', kind: 'planning', section: null, expectedText: '계획 항목과 일정' },
    { label: '설계', filename: '21-project-tab-design.jpg', kind: 'design', section: null, expectedText: '패널명' },
    { label: '구매', filename: '22-project-tab-procurement.jpg', kind: 'procurement', section: null, expectedText: '제어반 외함' },
    { label: '제조', filename: '23-project-tab-manufacturing.jpg', kind: 'panel', section: 'manufacturing', expectedText: '패널별 제조 착수' },
    { label: '품질', filename: '24-project-tab-quality.jpg', kind: 'panel', section: 'quality', expectedText: 'OQC 완료' },
    { label: '물류', filename: '25-project-tab-logistics.jpg', kind: 'panel', section: 'logistics', expectedText: '납품 완료' },
    { label: '영업', filename: '26-project-tab-sales.jpg', kind: 'sales', section: 'sales', expectedText: '정산·회계 발행 확인' }
  ] as const;

  for (const tab of tabs) {
    const button = tabList.getByRole('tab', { name: tab.label, exact: true });
    await expect(button).toHaveCount(1);
    await button.click();
    await expect(button).toHaveAttribute('aria-selected', 'true');
    await expect(page.locator('.project-detail-tab-content')).toBeVisible();
    await expect(page.locator('.project-detail-tab-content')).toContainText(tab.expectedText);
    if (tab.kind === 'panel') {
      await expect(page.locator('.project-department-section .project-department-metrics')).toBeVisible();
      await expect(page.locator('.project-panel-status-table')).toBeVisible();
      await expect(page.locator(`.project-department-section[data-department="${tab.section}"]`)).toContainText(tab.expectedText);
      const progressRendering = await page.locator('.project-panel-status-table .project-progress-meter').evaluateAll((meters) => {
        const completed = meters.filter((meter) => meter.querySelector('strong')?.textContent?.trim() === '100%');
        return {
          completedCount: completed.length,
          unfilledCompleted: completed.map((meter) => {
            const track = meter.querySelector('i');
            const fill = meter.querySelector('i > b');
            if (!track || !fill) return true;
            const fillStyle = window.getComputedStyle(fill);
            return fill.getBoundingClientRect().width < track.getBoundingClientRect().width - 2
              || fillStyle.backgroundColor === 'rgba(0, 0, 0, 0)';
          }).filter(Boolean).length
        };
      });
      expect(progressRendering.completedCount).toBeGreaterThan(0);
      expect(progressRendering.unfilledCompleted).toBe(0);
    } else if (tab.kind === 'sales') {
      await expect(page.locator('.project-department-section[data-department="sales"]')).toContainText(tab.expectedText);
    }
    await page.evaluate(async () => {
      await document.fonts.ready;
      window.scrollTo(0, 0);
      await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
    });
    await page.screenshot({
      path: path.join(screenshotDirectory, tab.filename),
      type: 'jpeg', quality: 86, fullPage: true, animations: 'disabled'
    });
  }

  await page.setViewportSize({ width: 390, height: 844 });
  for (const tab of tabs) {
    const button = tabList.getByRole('tab', { name: tab.label, exact: true });
    await button.click();
    await expect(page.locator('.project-detail-tab-content')).toContainText(tab.expectedText);
    await expect.poll(() => page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - window.innerWidth))).toBe(0);
    await captureEvidenceScreenshot(page, tab.filename.replace('.jpg', '-mobile.jpg'));
  }
  await page.setViewportSize({ width: 1440, height: 1050 });
}

async function captureProductionPlanningTabs(page: Page, projectId: string) {
  await switchUser(page, 'dev-production');
  await page.goto(`/projects/${projectId}`);
  await expect(page.locator('.account-identity-trigger')).toContainText('Dev Production Planning User');
  await expect(page.getByLabel('개발 사용자')).toHaveValue('dev-production');

  const tabList = page.getByRole('tablist', { name: '프로젝트 상세 섹션' });
  await tabList.getByRole('tab', { name: '전체 흐름' }).click();
  await expect(page.getByRole('heading', { name: '프로젝트 전체 흐름' })).toBeVisible();
  await expect(page.locator('.workflow-stage-item')).toHaveCount(18);
  await captureEvidenceScreenshot(page, '27-production-user-workflow.jpg');

  await tabList.getByRole('tab', { name: '생산관리' }).click();
  await expect(page.locator('.workflow-stage-item')).toHaveCount(0);
  await expect(page.getByRole('table', { name: '생산계획 항목' })).toBeVisible();
  await expect(page.getByRole('table', { name: '생산계획 캘린더 표' })).toBeVisible();
  await expect(page.getByLabel('담당자 지정 현황')).toBeVisible();
  await expect.poll(() => page.evaluate(() => {
    const schedule = document.querySelector('[aria-label="생산계획 일정"]');
    const assignees = document.querySelector('[aria-label="담당자 지정 현황"]');
    if (!schedule || !assignees) return false;
    return Boolean(schedule.compareDocumentPosition(assignees) & Node.DOCUMENT_POSITION_FOLLOWING);
  })).toBe(true);
  await captureEvidenceScreenshot(page, '28-production-user-planning.jpg');
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
