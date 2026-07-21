import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = '/tmp/workflow-continuity-001-screenshots';
const evidenceImage = path.resolve('src/assets/emi-logo.png');

test('WORKFLOW-CONTINUITY-001: arrival, IQC pending, automatic reinspection, and project tabs stay connected', async ({ page, request }) => {
  test.setTimeout(180_000);
  const unique = Date.now();
  const projectTitle = `IQC 연속흐름 ${unique}`;
  const projectCode = `CONT-${String(unique).slice(-8)}`;
  const projectId = await createProject(request, projectCode, projectTitle);
  const itemId = await createProcurementItem(request, projectId);
  const arrival = await registerArrival(request, itemId);

  expect(arrival.status).toBe('IqcRequested');
  expect(arrival.iqcAttemptId).toMatch(/^[0-9a-f-]{36}$/u);

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await page.goto(`/projects/${projectId}`);
  await expect(page.getByRole('tab', { name: '전체 흐름' })).toHaveAttribute('aria-selected', 'true');
  await expect(page).toHaveURL(new RegExp(`/projects/${projectId}$`, 'u'));
  await capture(page, '01-project-default-workflow-desktop.png');

  await page.getByRole('tab', { name: '자재' }).click();
  await expect(page.getByRole('tab', { name: '입고 관리' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByText('연속흐름 시험 자재').first()).toBeVisible();
  await expect(page.getByText('IQC 대기', { exact: true }).first()).toBeVisible();
  await capture(page, '02-material-receiving-tab-desktop.png');
  await page.getByRole('tab', { name: '키팅 관리' }).click();
  await expect(page.getByRole('tab', { name: '키팅 관리' })).toHaveAttribute('aria-selected', 'true');
  await capture(page, '03-material-kitting-tab-desktop.png');

  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto('/my-work');
  const firstWorkGroup = page.getByRole('region', { name: `${projectTitle} 내 업무` });
  await expect(firstWorkGroup).toContainText('IQC 판정');
  await firstWorkGroup.getByRole('button', { name: '이동', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/quality/iqc\\?project=${projectId}&request=${arrival.iqcAttemptId}$`, 'u'));
  const firstReport = page.locator('.material-action-drawer--iqc-report');
  await expect(firstReport).toContainText('연속흐름 시험 자재');
  await capture(page, '04-iqc-my-work-deep-link-desktop.png');

  await finalizeDetailedIqc(firstReport, 'Failed');
  await expect(firstReport.getByText('부적합 · 입고 차단', { exact: true })).toBeVisible();
  const firstAttempt = await findIqcAttempt(request, arrival.iqcAttemptId);
  expect(firstAttempt.pendingIssueId).toMatch(/^[0-9a-f-]{36}$/u);
  const pendingId = firstAttempt.pendingIssueId!;

  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await page.goto(`/pending/${pendingId}`);
  await expect(page.getByRole('button', { name: '조치 시작' })).toBeVisible();
  await page.getByLabel('처리 내용').fill('불량 자재를 격리하고 공급사 교환품을 확인합니다.');
  await page.getByRole('button', { name: '조치 시작' }).click();
  await expect(page.getByRole('button', { name: '조치 완료' })).toBeVisible();
  await page.getByLabel('처리 내용').fill('교환품 도착과 외관 상태를 확인하여 재검사를 요청합니다.');
  await page.getByRole('button', { name: '조치 완료' }).click();
  await expect(page.getByText('조치를 완료하고 품질 재검사 업무를 생성했습니다.')).toBeVisible();
  await expect(page.getByRole('button', { name: '종결', exact: true })).toHaveCount(0);
  await expect(page.locator('.pending-timeline')).toContainText('교환품 도착과 외관 상태를 확인하여 재검사를 요청합니다.');
  await capture(page, '05-pending-action-timeline-desktop.png');

  const reinspectionAttempt = await findRequestedReinspection(request, pendingId);
  const qualityWork = await listMyWork(request, 'dev-quality');
  const reinspectionWork = qualityWork.items.filter((item) => item.linkUrl === `/quality/iqc?request=${reinspectionAttempt.attemptId}`);
  expect(reinspectionWork).toHaveLength(1);
  expect(reinspectionWork[0].linkUrl).toBe(`/quality/iqc?request=${reinspectionAttempt.attemptId}`);
  const qualityNotifications = await listNotifications(request, 'dev-quality');
  expect(qualityNotifications.items.filter((item) => item.linkUrl === `/quality/iqc?request=${reinspectionAttempt.attemptId}`)).toHaveLength(1);

  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto('/notifications');
  await expect(page.getByText(projectTitle).first()).toBeVisible();
  await capture(page, '06-reinspection-notification-desktop.png');
  await page.goto('/my-work');
  const reinspectionGroup = page.getByRole('region', { name: `${projectTitle} 내 업무` });
  await expect(reinspectionGroup).toContainText('IQC 판정');
  await reinspectionGroup.getByRole('button', { name: '이동', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/quality/iqc\\?project=${projectId}&request=${reinspectionAttempt.attemptId}$`, 'u'));
  const reinspectionReport = page.locator('.material-action-drawer--iqc-report');
  await expect(reinspectionReport).toContainText('2차');
  await capture(page, '07-reinspection-my-work-deep-link-desktop.png');

  await finalizeDetailedIqc(reinspectionReport, 'Passed');
  await expect(reinspectionReport.getByText('IQC 합격', { exact: true })).toBeVisible();
  const closedPending = await getPending(request, pendingId);
  expect(closedPending.issue.status).toBe('Closed');
  const workflow = await getWorkflow(request, projectId);
  expect(workflow.stages.find((stage) => stage.stageCode === 'IQC')?.status).toBe('Completed');

  await page.goto(`/projects/${projectId}`);
  await expect(page.getByRole('tab', { name: '전체 흐름' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.locator('.workflow-stage-item').filter({ hasText: '수입검사' })).toContainText('완료');
  await capture(page, '08-iqc-completed-workflow-desktop.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}?section=materials`);
  await expect(page.getByRole('tab', { name: '입고 관리' })).toBeVisible();
  await expect(page.getByRole('tab', { name: '키팅 관리' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  await capture(page, '09-material-subtabs-mobile-390.png');
});

async function finalizeDetailedIqc(scope: Locator, result: 'Passed' | 'Failed') {
  await scope.getByRole('button', { name: '검사 시작' }).click();
  const cards = scope.locator('.iqc-item-card');
  await expect(cards).toHaveCount(6);
  const cardCount = await cards.count();
  for (let index = 0; index < cardCount; index += 1) {
    const card = cards.nth(index);
    const pass = card.getByRole('button', { name: '적합', exact: true });
    const fail = card.getByRole('button', { name: '부적합', exact: true });
    if (await pass.count()) {
      if (result === 'Failed' && index === 0) {
        await fail.click();
        const note = card.locator('input');
        if (await note.count()) await note.fill('외관 균열과 눌림 흔적을 사진으로 확인했습니다.');
      } else {
        await pass.click();
      }
    } else {
      const text = card.locator('textarea');
      if (await text.count()) await text.fill('측정값 정상 범위');
    }
  }
  await scope.getByRole('button', { name: '저장하고 사진 등록' }).click();
  await scope.locator('input[type="file"]').setInputFiles(evidenceImage);
  await scope.getByRole('button', { name: '사진 등록' }).click();
  await expect(scope.locator('.iqc-photo-evidence')).toHaveCount(1);
  await scope.getByRole('button', { name: '최종확인으로' }).click();
  await scope.getByLabel('종합 판정 사유').fill(result === 'Failed'
    ? '외관 균열과 눌림 흔적이 확인되어 조립 전 교환과 재검사가 필요합니다.'
    : '교환품의 외관, 수량, 식별 정보를 다시 확인했고 모든 항목이 적합합니다.');
  await scope.getByRole('button', { name: result === 'Failed' ? '부적합 · 입고 차단' : '합격 · 성적서 확정' }).click();
  await expect(scope.locator('.iqc-final-report')).toBeVisible();
}

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: devHeaders('dev-sales'),
    data: {
      customerName: 'Synthetic Customer', item: 'RPP', projectCode, projectTitle, panelCount: 2,
      deliveryDate: '2026-12-31', salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
      packagingMethod: 'WoodenCrate', salesAmount: 42000000, currencyCode: 'KRW',
      deliveryLocation: 'Synthetic Site', fatRequired: false
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}

async function createProcurementItem(request: APIRequestContext, projectId: string) {
  const response = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: devHeaders('dev-procurement'),
    data: {
      reason: '연속 흐름 검수용 발주',
      items: [{
        orderItem: '연속흐름 시험 자재', supplierName: 'Synthetic Vendor',
        orderDate: '2026-07-01', expectedReceiptDate: '2026-07-20',
        orderQuantity: 2, orderUnit: 'EA'
      }]
    }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json() as { items: Array<{ itemId: string }> }).items[0].itemId;
}

async function registerArrival(request: APIRequestContext, itemId: string) {
  const response = await request.post(`${apiBaseUrl}/api/materials/items/${itemId}/receipts`, {
    headers: devHeaders('dev-materials'),
    data: { quantity: 2, unit: 'EA', arrivalDate: '2026-07-18', note: '도착 즉시 IQC 연결 검수' }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { receiptId: string; iqcAttemptId: string; status: string };
}

async function findIqcAttempt(request: APIRequestContext, attemptId: string) {
  const response = await request.get(`${apiBaseUrl}/api/quality/iqc?includeDecided=true`, { headers: devHeaders('dev-quality') });
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { items: Array<{ attemptId: string; pendingIssueId: string | null }> };
  return body.items.find((item) => item.attemptId === attemptId)!;
}

async function findRequestedReinspection(request: APIRequestContext, pendingId: string) {
  const response = await request.get(`${apiBaseUrl}/api/quality/iqc?includeDecided=true`, { headers: devHeaders('dev-quality') });
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { items: Array<{ attemptId: string; pendingIssueId: string | null; status: string; attemptNumber: number }> };
  const attempt = body.items.find((item) => item.pendingIssueId === pendingId && item.status === 'Requested' && item.attemptNumber === 2);
  expect(attempt).toBeDefined();
  return attempt!;
}

async function listMyWork(request: APIRequestContext, user: string) {
  const response = await request.get(`${apiBaseUrl}/api/my-work`, { headers: devHeaders(user) });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { items: Array<{ linkUrl: string }> };
}

async function listNotifications(request: APIRequestContext, user: string) {
  const response = await request.get(`${apiBaseUrl}/api/notifications`, { headers: devHeaders(user) });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { items: Array<{ linkUrl: string | null }> };
}

async function getPending(request: APIRequestContext, pendingId: string) {
  const response = await request.get(`${apiBaseUrl}/api/pending/${pendingId}`, { headers: devHeaders('dev-procurement') });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { issue: { status: string } };
}

async function getWorkflow(request: APIRequestContext, projectId: string) {
  const response = await request.get(`${apiBaseUrl}/api/projects/${projectId}/workflow`, { headers: devHeaders('dev-quality') });
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as { stages: Array<{ stageCode: string; status: string }> };
}

async function capture(page: Page, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled', fullPage: true });
}

function devHeaders(user: string) {
  return { 'X-Dev-User': user };
}
