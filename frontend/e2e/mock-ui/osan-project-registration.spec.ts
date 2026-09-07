import { expect, type Page, type Route, test } from '@playwright/test';

const projectId = '91000000-0000-0000-0000-000000000001';
const cheongjuProjectId = '71000000-0000-0000-0000-000000000010';
const cheongjuPanelIds = [
  '72000000-0000-0000-0000-000000000001',
  '72000000-0000-0000-0000-000000000002'
];
const stepNames = ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'];

test('Cheongju and Osan share responsive project presentations while Osan preserves its registration contract', async ({ page }, testInfo) => {
  const consoleErrors: string[] = [];
  const requestFailures: string[] = [];
  const postedBodies: Array<Record<string, unknown>> = [];
  const unexpectedRequests: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('requestfailed', (request) => {
    if (request.failure()?.errorText !== 'net::ERR_ABORTED') {
      requestFailures.push(`${request.method()} ${new URL(request.url()).pathname}`);
    }
  });
  await installBackend(page, postedBodies, unexpectedRequests);
  await page.addInitScript(() => {
    if (!window.sessionStorage.getItem('emi.qms.business-unit')) {
      window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    }
  });

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/projects');
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  await expect(page.getByText('등록된 프로젝트가 없습니다.')).toBeVisible();
  const emptyCreateActions = page.getByRole('button', { name: '신규 프로젝트' });
  await expect(emptyCreateActions).toHaveCount(2);
  await emptyCreateActions.nth(1).click();

  const expectedFields = ['프로젝트 Title', '프로젝트 코드', '거래처', 'PO No', 'W/O No', '납기일', '제품명', '수량'];
  for (const field of expectedFields) {
    await expect(page.getByLabel(field, { exact: true })).toBeVisible();
  }
  await expect(page.locator('.osan-project-field')).toHaveCount(8);

  await page.getByLabel('프로젝트 Title').fill('  저장된 Title  ');
  await page.getByLabel('프로젝트 코드').fill('  AbC  001  ');
  await page.getByLabel('거래처').fill('  거래처  ');
  await page.getByLabel('PO No').fill('  001-PO/+  ');
  await page.getByLabel('W/O No').fill('  000-W/O  ');
  await page.getByLabel('납기일').fill('2026-12-31');
  await page.getByLabel('제품명').fill('  제품  이름  ');
  await page.getByLabel('수량').fill('2');
  await page.getByRole('button', { name: '프로젝트 등록' }).click();

  await expect(page.getByRole('heading', { name: '저장된 Title' })).toBeVisible();
  expect(postedBodies).toHaveLength(1);
  expect(postedBodies[0]).toMatchObject({
    title: '저장된 Title',
    projectCode: 'AbC  001',
    customerName: '거래처',
    poNumber: '001-PO/+',
    workOrderNumber: '000-W/O',
    deliveryDate: '2026-12-31',
    productName: '제품  이름',
    quantity: 2
  });
  expect(postedBodies[0].operationId).toMatch(/^[0-9a-f-]{36}$/i);

  const breadcrumbs = page.getByRole('navigation', { name: '현재 위치' });
  await expect(breadcrumbs).toBeVisible();
  const departmentTabs = page.getByRole('tablist', { name: '프로젝트 상세 섹션' });
  await expect(departmentTabs.getByRole('tab')).toHaveCount(1);
  const progressTab = departmentTabs.getByRole('tab', { name: '진행 관리' });
  await expect(progressTab).toHaveAttribute('aria-selected', 'true');
  await expect(progressTab).toHaveAttribute('aria-controls', 'osan-progress-panel');
  const progressPanel = page.getByRole('tabpanel', { name: '진행 관리' });
  await expect(progressPanel).toBeVisible();
  await expect(progressPanel).toHaveClass(/project-detail-tab-content/);
  await expect(progressPanel.locator('.project-department-section')).toHaveAttribute('data-department', 'manufacturing');
  await expect(progressPanel.locator('.project-department-metrics')).toBeVisible();
  await expect(page.locator('[data-presentation-contract="project-summary-v1"]')).toHaveAttribute('data-presentation-layout', 'desktop');
  await expect(progressPanel.locator('[data-presentation-contract="project-status-board-v1"]')).toHaveAttribute('data-presentation-layout', 'desktop');
  const desktopTargetTable = progressPanel.getByRole('table', { name: '진행 관리 대상 현황' });
  await expect(desktopTargetTable).toBeVisible();
  await expect(desktopTargetTable).toHaveClass(/project-panel-status-table/);
  const desktopTargetRows = desktopTargetTable.getByRole('row');
  await expect(desktopTargetRows).toHaveCount(3);
  await expect(desktopTargetRows.first().getByRole('columnheader')).toHaveCount(5);
  await expect(desktopTargetTable.locator('button')).toHaveCount(0);
  const osanDesktopTargetRow = desktopTargetRows.nth(1);
  expect(await osanDesktopTargetRow.evaluate((element) => element.tagName)).toBe('DIV');
  await expect(osanDesktopTargetRow).toHaveAttribute('data-interactive', 'false');
  await expect(osanDesktopTargetRow).not.toHaveAttribute('tabindex');
  expect(await osanDesktopTargetRow.evaluate((element) => getComputedStyle(element).cursor)).toBe('auto');
  const osanDesktopRowBackground = await osanDesktopTargetRow.evaluate((element) => getComputedStyle(element).backgroundColor);
  await osanDesktopTargetRow.hover();
  expect(await osanDesktopTargetRow.evaluate((element) => getComputedStyle(element).backgroundColor)).toBe(osanDesktopRowBackground);
  expect(await osanDesktopTargetRow.evaluate((element) => {
    (element as HTMLElement).focus();
    return document.activeElement === element;
  })).toBe(false);
  for (let index = 1; index <= 2; index += 1) {
    await expect(desktopTargetRows.nth(index).getByRole('cell')).toHaveCount(5);
    await expect(desktopTargetRows.nth(index)).toContainText('시작 전');
    await expect(desktopTargetRows.nth(index)).toContainText('0/7단계 완료');
    await expect(desktopTargetRows.nth(index)).toContainText('입고검사');
    await expect(desktopTargetRows.nth(index).locator('.project-progress-meter')).toHaveAttribute('aria-label', /진행률 0% \(0\/7\)$/);
  }
  await expect(progressPanel.locator('.project-department-records')).toHaveCount(0);
  for (const forbiddenAction of ['Pending', '보류', '중단', '취소']) {
    await expect(page.getByRole('button', { name: forbiddenAction, exact: true })).toHaveCount(0);
  }
  const osanDesktopSummaryDetails = page.locator('[data-presentation-contract="project-summary-v1"] details');
  await osanDesktopSummaryDetails.locator('summary').click();
  await expect(page.getByText('001-PO/+', { exact: true })).toBeVisible();
  await expect(page.getByText('000-W/O', { exact: true })).toBeVisible();
  const detailCode = page.locator('.project-summary-more dd.project-code-value');
  expect(await detailCode.textContent()).toBe('AbC  001');
  expect(await detailCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await detailCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(page.locator('.project-summary-primary .status-badge')).toHaveText('시작 전');
  await osanDesktopSummaryDetails.locator('summary').click();
  expect(await osanDesktopSummaryDetails.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(false);
  const osanDesktopSummaryContract = await projectSummaryContract(page);
  const osanDesktopStatusContract = await projectStatusBoardContract(page);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-detail-desktop-1440.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '저장된 Title' })).toBeVisible();
  await expect(breadcrumbs).toBeHidden();
  await expect(page.getByRole('button', { name: '← 프로젝트' })).toBeVisible();
  const mobileDetailCode = page.locator('.mobile-detail-hero .project-code-value');
  await expect(mobileDetailCode).toHaveText('AbC  001');
  expect(await mobileDetailCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await mobileDetailCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(page.locator('.mobile-detail-hero .status-badge')).toHaveText('시작 전');
  await expect(desktopTargetTable).toHaveCount(0);
  const mobileTargetCards = progressPanel.locator('.project-panel-status-cards');
  await expect(mobileTargetCards).toBeVisible();
  const mobileTargetItems = mobileTargetCards.locator('.project-panel-status-card');
  await expect(mobileTargetItems).toHaveCount(2);
  await expect(mobileTargetCards.locator('button')).toHaveCount(0);
  const osanMobileTarget = mobileTargetItems.first();
  expect(await osanMobileTarget.evaluate((element) => element.tagName)).toBe('ARTICLE');
  await expect(osanMobileTarget).toHaveAttribute('data-interactive', 'false');
  await expect(osanMobileTarget).not.toHaveAttribute('tabindex');
  expect(await osanMobileTarget.evaluate((element) => getComputedStyle(element).cursor)).toBe('auto');
  const osanMobileCardBackground = await osanMobileTarget.evaluate((element) => getComputedStyle(element).backgroundColor);
  await osanMobileTarget.hover();
  expect(await osanMobileTarget.evaluate((element) => getComputedStyle(element).backgroundColor)).toBe(osanMobileCardBackground);
  expect(await osanMobileTarget.evaluate((element) => {
    (element as HTMLElement).focus();
    return document.activeElement === element;
  })).toBe(false);
  await page.evaluate(() => window.scrollTo(0, 0));
  for (const target of await mobileTargetItems.all()) {
    await expect(target).toContainText('시작 전');
    await expect(target).toContainText('0/7단계 완료');
    await expect(target).toContainText('입고검사');
  }
  const osanMobileSummaryContract = await projectSummaryContract(page);
  const osanMobileStatusContract = await projectStatusBoardContract(page);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-detail-mobile-390.png'), fullPage: true });

  await page.getByRole('button', { name: '← 프로젝트' }).click();
  const mobileList = page.getByTestId('osan-project-list-mobile');
  await expect(mobileList).toBeVisible();
  await expect(mobileList).toHaveClass(/project-list-cards/);
  await expect(mobileList).toHaveClass(/project-list-mobile/);
  await expect(page.getByTestId('osan-project-list-desktop')).toBeHidden();
  await expect(mobileList.getByRole('article')).toHaveCount(1);
  await expect(mobileList.getByRole('article')).toHaveClass(/project-list-card/);
  const listCode = mobileList.locator('.project-code-value');
  await expect(listCode).toBeVisible();
  expect(await listCode.textContent()).toBe('AbC  001');
  expect(await listCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await listCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(mobileList.getByText('시작 전')).toBeVisible();
  await expect(mobileList.getByText('0%')).toBeVisible();
  const osanMobilePage = page.locator('[data-presentation-contract="project-list-page-v1"]');
  await expect(osanMobilePage).toHaveAttribute('data-presentation-layout', 'mobile');
  await expect(osanMobilePage).toContainText('납기를 먼저 보고 필요한 프로젝트를 선택하세요.');
  await expect(osanMobilePage).not.toContainText('병목');
  await expect(osanMobilePage.getByRole('button', { name: '+ 프로젝트', exact: true })).toBeVisible();
  await expect(osanMobilePage.locator(':scope > .mobile-filter-trigger')).toBeVisible();
  await expect(osanMobilePage.locator(':scope > .project-kpi-grid .dashboard-kpi-card')).toHaveCount(3);
  await expect(osanMobilePage.getByRole('tab')).toHaveCount(3);
  await expect(osanMobilePage.getByRole('tab', { name: '시작 전' })).toBeVisible();
  await expect(osanMobilePage.getByText('Excel')).toHaveCount(0);
  await expect(osanMobilePage.getByText('Pending')).toHaveCount(0);
  await expect(osanMobilePage.getByRole('checkbox')).toHaveCount(0);
  const osanMobilePageContract = await projectListPageContract(page);
  expect(osanMobilePageContract.structure.commonOrder).toEqual(['header', 'filter', 'kpi', 'tabs', 'list']);
  expect(osanMobilePageContract.structure.commonOrderValid).toBe(true);
  const osanMobileListContract = await projectListContract(page);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-list-mobile-390.png'), fullPage: true });

  await page.setViewportSize({ width: 1440, height: 900 });
  const desktopList = page.getByTestId('osan-project-list-desktop');
  await expect(desktopList).toBeVisible();
  await expect(desktopList).toHaveClass(/project-list-table/);
  await expect(desktopList).toHaveClass(/project-list-desktop/);
  await expect(mobileList).toBeHidden();
  await expect(desktopList.getByRole('row')).toHaveCount(2);
  await expect(desktopList.getByRole('columnheader')).toHaveCount(8);
  const desktopProjectRow = desktopList.getByRole('row', { name: '저장된 Title 상세 열기' });
  await expect(desktopProjectRow).toHaveClass(/project-list-row/);
  await expect(desktopProjectRow.getByRole('cell')).toHaveCount(8);
  await expect(desktopProjectRow.locator(':scope > .project-selection-cell')).toHaveCount(0);
  const desktopListCode = desktopProjectRow.locator('.project-code-value');
  expect(await desktopListCode.textContent()).toBe('AbC  001');
  expect(await desktopListCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await desktopListCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(desktopProjectRow.getByText('시작 전')).toBeVisible();
  await expect(desktopProjectRow.getByText('0%')).toBeVisible();
  const osanDesktopPage = page.locator('[data-presentation-contract="project-list-page-v1"]');
  await expect(osanDesktopPage).toHaveAttribute('data-presentation-layout', 'desktop');
  await expect(osanDesktopPage.locator(':scope > form.toolbar')).toBeVisible();
  await expect(osanDesktopPage.locator(':scope > .project-kpi-grid .dashboard-kpi-card')).toHaveCount(3);
  await expect(osanDesktopPage.getByRole('tab')).toHaveCount(3);
  const osanDesktopPageContract = await projectListPageContract(page);
  expect(osanDesktopPageContract.structure.commonOrder).toEqual(['header', 'filter', 'kpi', 'tabs', 'list']);
  expect(osanDesktopPageContract.structure.commonOrderValid).toBe(true);
  const osanDesktopListContract = await projectListContract(page);
  expect(osanDesktopListContract.geometry.rowHeight).toBeGreaterThanOrEqual(60);
  expect(osanDesktopListContract.geometry.headerBodyAligned).toBe(true);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-list-desktop-1440.png'), fullPage: true });

  await page.evaluate(() => window.sessionStorage.setItem('emi.qms.business-unit', 'CHEONGJU'));
  await page.goto('/projects');
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  const cheongjuDesktopList = page.getByTestId('project-list-desktop');
  await expect(cheongjuDesktopList).toBeVisible();
  const cheongjuDesktopProjectRow = cheongjuDesktopList.locator('[data-presentation-row="project"]');
  await expect(cheongjuDesktopProjectRow).toHaveCount(1);
  await expect(cheongjuDesktopProjectRow.locator(':scope > .project-selection-cell')).toHaveCount(1);
  const cheongjuDesktopPage = page.locator('[data-presentation-contract="project-list-page-v1"]');
  await expect(cheongjuDesktopPage.getByRole('button', { name: '신규 프로젝트', exact: true })).toBeVisible();
  await expect(cheongjuDesktopPage.getByRole('button', { name: '프로젝트 Excel 양식', includeHidden: true })).toHaveCount(1);
  await expect(cheongjuDesktopPage.getByRole('button', { name: '프로젝트 Excel 업로드', includeHidden: true })).toHaveCount(1);
  await expect(cheongjuDesktopPage.getByRole('button', { name: '선택 Excel 내보내기' })).toBeVisible();
  await expect(cheongjuDesktopPage.getByRole('tab', { name: '삭제 보관함' })).toBeVisible();
  const cheongjuDesktopPageContract = await projectListPageContract(page);
  expect(cheongjuDesktopPageContract).toEqual(osanDesktopPageContract);
  const cheongjuDesktopListContract = await projectListContract(page);
  expect(cheongjuDesktopListContract.geometry.headerBodyAligned).toBe(true);
  expect(cheongjuDesktopListContract).toEqual(osanDesktopListContract);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('cheongju-project-list-desktop-1440.png'), fullPage: true });

  await cheongjuDesktopProjectRow.click();
  await expect(page.getByRole('heading', { name: '청주 기준 프로젝트' })).toBeVisible();
  await page.getByRole('tab', { name: '제조', exact: true }).click();
  const cheongjuDesktopStatusTable = page.getByRole('table', { name: '제조 패널 현황' });
  await expect(cheongjuDesktopStatusTable).toBeVisible();
  await expect(page.locator('[data-presentation-contract="project-status-board-v1"] .project-department-metrics')).toBeVisible();
  const cheongjuDesktopStatusRow = cheongjuDesktopStatusTable.locator('[data-presentation-row="status"]').first();
  expect(await cheongjuDesktopStatusRow.evaluate((element) => element.tagName)).toBe('BUTTON');
  await expect(cheongjuDesktopStatusRow).toHaveAttribute('data-interactive', 'true');
  await expect(cheongjuDesktopStatusRow).toHaveAttribute('type', 'button');
  expect(await cheongjuDesktopStatusRow.evaluate((element) => (element as HTMLElement).tabIndex)).toBe(0);
  await expect(cheongjuDesktopStatusRow.getByRole('cell').nth(1).locator('strong')).toHaveText('P01');
  await expect(cheongjuDesktopStatusRow.getByRole('cell').nth(1).locator('small')).toHaveText('P01');
  const cheongjuDesktopSummaryDetails = page.locator('[data-presentation-contract="project-summary-v1"] details');
  expect(await cheongjuDesktopSummaryDetails.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(false);
  expect(await projectSummaryContract(page)).toEqual(osanDesktopSummaryContract);
  expect(await projectStatusBoardContract(page)).toEqual(osanDesktopStatusContract);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('cheongju-project-detail-desktop-1440.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('[data-presentation-contract="project-summary-v1"]')).toHaveAttribute('data-presentation-layout', 'mobile');
  await expect(page.locator('[data-presentation-contract="project-status-board-v1"]')).toHaveAttribute('data-presentation-layout', 'mobile');
  const cheongjuMobileStatusCard = page.locator('[data-presentation-contract="project-status-board-v1"] [data-presentation-row="status"]').first();
  expect(await cheongjuMobileStatusCard.evaluate((element) => element.tagName)).toBe('BUTTON');
  await expect(cheongjuMobileStatusCard).toHaveAttribute('data-interactive', 'true');
  await expect(cheongjuMobileStatusCard.locator('.project-panel-status-card-title strong')).toHaveText('패널명 미입력');
  expect(await projectSummaryContract(page)).toEqual(osanMobileSummaryContract);
  expect(await projectStatusBoardContract(page)).toEqual(osanMobileStatusContract);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('cheongju-project-detail-mobile-390.png'), fullPage: true });

  await page.getByRole('button', { name: '← 프로젝트' }).click();
  const cheongjuMobileList = page.getByTestId('project-list-mobile');
  await expect(cheongjuMobileList).toBeVisible();
  await expect(cheongjuMobileList.locator('[data-presentation-row="project"]')).toHaveCount(1);
  const cheongjuMobilePage = page.locator('[data-presentation-contract="project-list-page-v1"]');
  await expect(cheongjuMobilePage).toHaveAttribute('data-presentation-layout', 'mobile');
  await expect(cheongjuMobilePage.getByRole('button', { name: '+ 프로젝트', exact: true })).toBeVisible();
  await expect(cheongjuMobilePage.locator(':scope > .mobile-filter-trigger')).toBeVisible();
  const cheongjuMobilePageContract = await projectListPageContract(page);
  expect(cheongjuMobilePageContract).toEqual(osanMobilePageContract);
  expect(await projectListContract(page)).toEqual(osanMobileListContract);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('cheongju-project-list-mobile-390.png'), fullPage: true });

  expect(unexpectedRequests).toEqual([]);
  expect(consoleErrors).toEqual([]);
  expect(requestFailures).toEqual([]);
});

async function installBackend(page: Page, postedBodies: Array<Record<string, unknown>>, unexpectedRequests: string[]) {
  let projectCreated = false;
  await page.route('http://localhost:5080/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const businessUnit = request.headers()['x-qms-business-unit'] === 'CHEONGJU' ? 'CHEONGJU' : 'OSAN';
    if (path === '/health/ready') return fulfillJson(route, { status: 'ready', database: { reason: 'reachable' } });
    if (path === '/api/runtime-mode') {
      return fulfillJson(route, {
        mode: 'Development', reviewSafe: false, mutationAllowed: true, databaseReadOnly: false, ready: true
      });
    }
    if (path === '/api/me') return fulfillJson(route, currentUser(businessUnit));
    if (path === '/api/audit/site-access/signals') {
      return fulfillJson(route, {
        sessionId: '94000000-0000-0000-0000-000000000001',
        browserClientId: 'synthetic-browser',
        menuCode: 'Projects',
        startedAtUtc: '2026-09-07T00:00:00Z',
        lastSeenAtUtc: '2026-09-07T00:00:00Z'
      });
    }
    if (path === '/api/my-work/summary') {
      return fulfillJson(route, {
        requestedCount: 0,
        inProgressCount: 0,
        completedCount: 0,
        blockingCount: 0,
        assignedProjectCount: 0,
        assignedProjectBreakdown: []
      });
    }
    if (path === '/api/notifications/summary') return fulfillJson(route, { unreadCount: 0, blockingCount: 0 });
    if (path === '/api/form-templates/my-scope') {
      return fulfillJson(route, { canManage: false, isSystemAdministrator: false, domains: [] });
    }
    if (path === '/api/projects/summary') {
      return fulfillJson(route, {
        totalProjectCount: 1,
        activeProjectCount: 1,
        onHoldProjectCount: 0,
        completedProjectCount: 0,
        cancelledProjectCount: 0,
        qrEligiblePanelCount: 0,
        manufacturingCompletedCount: 0,
        inspectionCompletedCount: 0,
        manufacturingCompletedProjectCount: 0,
        inspectionCompletedProjectCount: 0
      });
    }
    if (path === '/api/projects') {
      return fulfillJson(route, { items: [cheongjuProject()], page: 1, pageSize: 20, totalCount: 1 });
    }
    if (path === `/api/projects/${cheongjuProjectId}`) return fulfillJson(route, cheongjuProjectDetail());
    if (path === `/api/projects/${cheongjuProjectId}/workflow`) return fulfillJson(route, cheongjuWorkflow());
    if (path === `/api/projects/${cheongjuProjectId}/panel-information`) return fulfillJson(route, cheongjuPanelInformation());
    if (path === `/api/projects/${cheongjuProjectId}/production-planning`) {
      return fulfillJson(route, {
        projectId: cheongjuProjectId,
        projectTitle: '청주 기준 프로젝트',
        projectCode: 'AbC  001',
        deliveryDate: '2026-12-31',
        modelVersion: 'LEGACY',
        planId: null,
        rowVersion: 0,
        planStatus: 'NotPlanned',
        planStatusLabel: '계획 전',
        productTypeId: null,
        templateId: null,
        productTypeCode: null,
        productTypeName: null,
        notes: null,
        manufacturingSteps: [],
        availableSources: [],
        items: [],
        assignees: [],
        assigneeCandidates: [],
        fallbacks: []
      });
    }
    if (path === `/api/projects/${cheongjuProjectId}/procurement`) {
      return fulfillJson(route, {
        projectId: cheongjuProjectId,
        projectTitle: '청주 기준 프로젝트',
        projectCode: 'AbC  001',
        projectDeliveryDate: '2026-12-31',
        iqcRoutingPolicy: 'CategoryBased',
        items: []
      });
    }
    if (path === '/api/manufacturing/queue') return fulfillJson(route, cheongjuManufacturingQueue());
    if (path.startsWith('/api/manufacturing/panels/')) {
      const panelId = path.split('/').at(-1) ?? cheongjuPanelIds[0];
      return fulfillJson(route, cheongjuManufacturingDetail(panelId));
    }
    if (path === '/api/osan/projects' && request.method() === 'GET') {
      return fulfillJson(route, { items: projectCreated ? [projectDetail()] : [] });
    }
    if (path === '/api/osan/projects' && request.method() === 'POST') {
      const body = request.postDataJSON() as Record<string, unknown>;
      postedBodies.push(body);
      projectCreated = true;
      return fulfillJson(route, {
        operationId: body.operationId,
        replayed: false,
        project: projectDetail()
      }, 201);
    }
    if (path === `/api/osan/projects/${projectId}`) return fulfillJson(route, projectDetail());
    unexpectedRequests.push(`${request.method()} ${path}`);
    return fulfillJson(route, { title: 'closed in synthetic scope' }, 404);
  });
}

function currentUser(selectedBusinessUnit: 'CHEONGJU' | 'OSAN') {
  const principal = {
    userId: '50000000-0000-0000-0000-000000000001',
    developmentUserKey: 'dev-admin',
    displayName: 'Synthetic Project Admin',
    email: null,
    authProvider: 'Dev',
    isActive: true,
    approvalPending: false,
    department: 'management-support',
    departmentName: '경영지원',
    profilePhotoVersion: null,
    roles: ['system-administrator']
  };
  return {
    ...principal,
    permissions: ['projects.read', 'Project.Create', 'Project.Read.All', 'Project.Deleted.Read'],
    projectAccess: [],
    isTestUserSwitch: false,
    testUserKey: null,
    canUseAdminTestUserSwitch: false,
    actualUser: principal,
    effectiveUser: principal,
    businessUnitAccess: {
      status: 'selected',
      selectedBusinessUnit,
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    }
  };
}

function projectDetail() {
  return {
    projectId,
    title: '저장된 Title',
    projectCode: 'AbC  001',
    customerName: '거래처',
    poNumber: '001-PO/+',
    workOrderNumber: '000-W/O',
    deliveryDate: '2026-12-31',
    productName: '제품  이름',
    quantity: 2,
    status: 'Active',
    createdAtUtc: '2026-09-07T00:00:00Z',
    targets: Array.from({ length: 2 }, (_, targetIndex) => ({
      targetId: `92000000-0000-0000-0000-${String(targetIndex + 1).padStart(12, '0')}`,
      sequenceNumber: targetIndex + 1,
      displayName: `제품  이름 ${targetIndex + 1}`,
      status: 'NotStarted',
      steps: stepNames.map((stepName, stepIndex) => ({
        stepId: `${93000000 + targetIndex}-${String(stepIndex + 1).padStart(4, '0')}-0000-0000-000000000001`,
        sequenceNumber: stepIndex + 1,
        stepCode: `STEP_${stepIndex + 1}`,
        stepName,
        status: 'NotStarted'
      }))
    }))
  };
}

function cheongjuProject() {
  return {
    projectId: cheongjuProjectId,
    customerName: '거래처',
    item: '제품  이름',
    projectCode: 'AbC  001',
    projectTitle: '청주 기준 프로젝트',
    activePanelCount: 2,
    deliveryDate: '2026-12-31',
    salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
    salesOwnerName: '합성 영업 담당자',
    packagingMethod: 'WoodenCrate',
    deliveryLocation: null,
    fatRequired: false,
    status: 'Active',
    projectWorkStatus: 'BeforeManufacturing',
    projectProgressPercent: 0,
    createdAt: '2026-09-07T00:00:00Z',
    updatedAt: '2026-09-07T00:00:00Z'
  };
}

function cheongjuProjectDetail() {
  return {
    ...cheongjuProject(),
    lseTaskNumber: null,
    qrEligibleCount: 0,
    manufacturingCompletedCount: 0,
    inspectionCompletedCount: 0,
    manufacturingStepCount: 7,
    oqcStepCount: 7,
    statusReason: null
  };
}

function cheongjuWorkflow() {
  return {
    projectId: cheongjuProjectId,
    generatedWorkItemCount: 0,
    requiredStageCount: 17,
    completedRequiredStageCount: 0,
    progressPercent: 0,
    currentStageCode: 'ProductionPlanning',
    currentStageName: '생산계획·담당자',
    currentDepartmentCode: 'production-planning',
    currentDepartmentLabel: '생산관리',
    stages: []
  };
}

function cheongjuPanelInformation() {
  return {
    projectId: cheongjuProjectId,
    projectStatus: 'Active',
    packagingMethod: 'WoodenCrate',
    activePanelCount: 2,
    panelInfoCompletedCount: 0,
    panelInfoPendingCount: 2,
    qrEligibleCount: 0,
    manufacturingCompletedCount: 0,
    inspectionCompletedCount: 0,
    duplicatePanelNameGroupCount: 0,
    supportsPanelGrouping: true,
    projectPanelInformationCompleted: false,
    panelInformationStatusMessage: null,
    panels: cheongjuPanelIds.map((panelId, index) => ({
      panelId,
      projectId: cheongjuProjectId,
      sequenceNumber: index + 1,
      panelNumber: `No.${index + 1}`,
      displayCode: `P0${index + 1}`,
      panelName: index === 0 ? null : `제품  이름 ${index + 1}`,
      drawingNumber: null,
      displayName: index === 0 ? `No.${index + 1} · 패널명 미입력` : `No.${index + 1} · 제품  이름 ${index + 1}`,
      widthMm: null,
      heightMm: null,
      depthMm: null,
      panelStatus: 'Active',
      workflowStage: 'BeforeManufacturing',
      panelInfoCompleted: false,
      qrEligible: false,
      hasDuplicateName: false,
      duplicateNameCount: 0,
      panelGroupNumber: null,
      panelInfoVersion: 0,
      createdAt: '2026-09-07T00:00:00Z',
      updatedAt: '2026-09-07T00:00:00Z',
      panelInfoUpdatedAtUtc: null,
      panelInfoUpdatedByUserId: null,
      panelInfoUpdatedByUserName: null
    }))
  };
}

function cheongjuManufacturingPanel(panelId: string) {
  const index = Math.max(cheongjuPanelIds.indexOf(panelId), 0);
  return {
    panelId,
    displayCode: `P0${index + 1}`,
    panelName: index === 0 ? null : `제품  이름 ${index + 1}`,
    workflowStage: 'BeforeManufacturing',
    workItemId: null,
    workItemStatus: 'Requested',
    executionId: null,
    status: 'Ready',
    version: 1,
    checkedStepCount: 0,
    totalStepCount: 7,
    activePendingId: null,
    activePendingNumber: null,
    actionDepartmentCode: null,
    startedAtUtc: null,
    completedAtUtc: null,
    canMutate: false
  };
}

function cheongjuManufacturingQueue() {
  return {
    projects: [{
      projectId: cheongjuProjectId,
      projectCode: 'AbC  001',
      projectTitle: '청주 기준 프로젝트',
      readyCount: 2,
      inProgressCount: 0,
      blockedCount: 0,
      completedCount: 0,
      panels: cheongjuPanelIds.map(cheongjuManufacturingPanel)
    }]
  };
}

function cheongjuManufacturingDetail(panelId: string) {
  return {
    panel: cheongjuManufacturingPanel(panelId),
    steps: stepNames.map((stepName, index) => ({
      stepId: `${panelId}-${index + 1}`,
      sequenceNumber: index + 1,
      stepName,
      checked: false,
      checkedByDisplayName: null,
      checkedAtUtc: null
    })),
    events: []
  };
}

function projectListContract(page: Page) {
  return page.locator('[data-presentation-contract="project-list-v1"]').evaluate((root) => {
    const layout = root.getAttribute('data-presentation-layout');
    const container = root.querySelector(layout === 'mobile' ? '.project-list-mobile' : '.project-list-desktop') as HTMLElement;
    const row = root.querySelector('[data-presentation-row="project"]') as HTMLElement;
    const style = getComputedStyle(row);
    const rootStyle = getComputedStyle(root);
    const header = layout === 'desktop' ? root.querySelector('.project-list-head') as HTMLElement : null;
    const headerCells = header ? Array.from(header.querySelectorAll<HTMLElement>(':scope > [role="columnheader"]')) : [];
    const rowCells = layout === 'desktop' ? Array.from(row.querySelectorAll<HTMLElement>(':scope > [role="cell"]')) : [];
    const columnGeometry = (elements: HTMLElement[]) => {
      const origin = elements[0]?.getBoundingClientRect().left ?? 0;
      return elements.map((element) => {
        const rect = element.getBoundingClientRect();
        return { left: Math.round(rect.left - origin), width: Math.round(rect.width) };
      });
    };
    const headerColumnGeometry = columnGeometry(headerCells);
    const bodyColumnGeometry = columnGeometry(rowCells);
    const commonCells = layout === 'mobile'
      ? Array.from(row.querySelectorAll('.mobile-detail-list > div')).slice(0, 1).map((element) => [element.tagName, element.querySelector('dt')?.tagName, element.querySelector('dd')?.tagName])
      : rowCells.map((element) => [element.tagName, element.className]);
    return {
      contract: root.getAttribute('data-presentation-contract'),
      layout,
      columnCount: root.getAttribute('data-presentation-column-count'),
      structure: {
        root: root.className,
        container: container.className.replace(' selectable', ''),
        row: row.className,
        commonCells,
        mobileHeader: layout === 'mobile' ? row.querySelector('.subsection-header')?.className : null,
        mobileTitle: layout === 'mobile' ? row.querySelector('.project-card-title-row')?.className : null,
        mobileDetails: layout === 'mobile' ? row.querySelector('.mobile-detail-list')?.className : null
      },
      styles: {
        rootDisplay: rootStyle.display,
        rootGap: rootStyle.gap,
        headerGridColumns: header ? getComputedStyle(header).gridTemplateColumns : null,
        rowGridColumns: layout === 'desktop' ? style.gridTemplateColumns : null,
        rowDisplay: style.display,
        rowGap: style.gap,
        rowBorder: style.border,
        rowBorderRadius: style.borderRadius,
        rowPadding: style.padding,
        rowBackground: style.backgroundColor
      },
      geometry: {
        rootWidth: Math.round(root.getBoundingClientRect().width),
        rowWidth: Math.round(row.getBoundingClientRect().width),
        rowHeight: layout === 'desktop' ? Math.round(row.getBoundingClientRect().height) : null,
        headerColumnGeometry: layout === 'desktop' ? headerColumnGeometry : null,
        bodyColumnGeometry: layout === 'desktop' ? bodyColumnGeometry : null,
        headerBodyAligned: layout === 'desktop'
          ? headerColumnGeometry.length === bodyColumnGeometry.length
            && headerColumnGeometry.every((cell, index) => (
              Math.abs(cell.left - bodyColumnGeometry[index].left) <= 1
              && Math.abs(cell.width - bodyColumnGeometry[index].width) <= 1
            ))
          : null
      }
    };
  });
}

function projectListPageContract(page: Page) {
  return page.locator('[data-presentation-contract="project-list-page-v1"]').evaluate((root) => {
    const layout = root.getAttribute('data-presentation-layout');
    const header = root.querySelector(':scope > .page-header') as HTMLElement;
    const filter = root.querySelector(layout === 'mobile' ? ':scope > .mobile-filter-trigger' : ':scope > form.toolbar') as HTMLElement;
    const kpis = root.querySelector(':scope > .project-kpi-grid') as HTMLElement;
    const tabs = root.querySelector(':scope > .tab-row') as HTMLElement;
    const list = root.querySelector(':scope > [data-presentation-contract="project-list-v1"]') as HTMLElement;
    const rootStyle = getComputedStyle(root);
    const filterStyle = getComputedStyle(filter);
    const kpiStyle = getComputedStyle(kpis);
    const tabsStyle = getComputedStyle(tabs);
    const commonSections = [header, filter, kpis, tabs, list];
    const sectionNames = new Map<Element, string>([
      [header, 'header'],
      [filter, 'filter'],
      [kpis, 'kpi'],
      [tabs, 'tabs'],
      [list, 'list']
    ]);
    const commonOrder = Array.from(root.children)
      .map((element) => sectionNames.get(element))
      .filter((value): value is string => Boolean(value));
    return {
      contract: root.getAttribute('data-presentation-contract'),
      layout,
      structure: {
        root: [root.tagName, root.className],
        header: [header.tagName, header.className],
        filter: [filter.tagName, filter.className],
        kpis: [kpis.tagName, kpis.className],
        tabs: [tabs.tagName, tabs.className],
        list: [list.tagName, list.className],
        commonOrder,
        commonOrderValid: commonSections.every((section, index) => (
          index === 0 || section.compareDocumentPosition(commonSections[index - 1]) === Node.DOCUMENT_POSITION_PRECEDING
        ))
      },
      styles: {
        rootDisplay: rootStyle.display,
        rootGap: rootStyle.gap,
        rootPadding: rootStyle.padding,
        filterDisplay: filterStyle.display,
        filterGap: filterStyle.gap,
        filterPadding: filterStyle.padding,
        filterBorder: filterStyle.border,
        filterBorderRadius: filterStyle.borderRadius,
        kpiDisplay: kpiStyle.display,
        kpiGap: kpiStyle.gap,
        tabsDisplay: tabsStyle.display,
        tabsGap: tabsStyle.gap,
        tabsPadding: tabsStyle.padding,
        tabsBorder: tabsStyle.border
      }
    };
  });
}

function projectSummaryContract(page: Page) {
  return page.locator('[data-presentation-contract="project-summary-v1"]').evaluate((root) => {
    const layout = root.getAttribute('data-presentation-layout');
    const grid = (layout === 'mobile' ? root : root.querySelector('.project-summary-primary')) as HTMLElement;
    const item = grid.querySelector(':scope > div') as HTMLElement;
    const gridStyle = getComputedStyle(grid);
    const itemStyle = getComputedStyle(item);
    return {
      contract: root.getAttribute('data-presentation-contract'),
      layout,
      structure: {
        rootTag: root.tagName,
        rootClass: root.className,
        gridTag: grid.tagName,
        gridClass: grid.className,
        item: [item.tagName, item.querySelector('dt')?.tagName, item.querySelector('dd')?.tagName]
      },
      styles: {
        gridDisplay: gridStyle.display,
        gridColumns: gridStyle.gridTemplateColumns,
        gridGap: gridStyle.gap,
        gridMargin: gridStyle.margin,
        itemBorder: itemStyle.border,
        itemBorderRadius: itemStyle.borderRadius,
        itemPadding: itemStyle.padding,
        itemBackground: itemStyle.backgroundColor,
        itemMinHeight: itemStyle.minHeight
      },
      geometry: {
        rootWidth: Math.round(root.getBoundingClientRect().width),
        itemWidth: Math.round(item.getBoundingClientRect().width),
        itemHeight: Math.round(item.getBoundingClientRect().height)
      }
    };
  });
}

function projectStatusBoardContract(page: Page) {
  return page.locator('[data-presentation-contract="project-status-board-v1"]').evaluate((root) => {
    const layout = root.getAttribute('data-presentation-layout');
    const collection = root.querySelector(layout === 'mobile' ? '.project-panel-status-cards' : '.project-panel-status-table') as HTMLElement;
    const row = collection.querySelector('[data-presentation-row="status"]') as HTMLElement;
    const rootStyle = getComputedStyle(root);
    const rowStyle = getComputedStyle(row);
    return {
      contract: root.getAttribute('data-presentation-contract'),
      layout,
      structure: {
        rootClass: root.className,
        headerClass: root.querySelector('.subsection-header')?.className,
        metricsClass: root.querySelector('.project-department-metrics')?.className,
        collectionClass: collection.className,
        rowClass: row.className,
        rowChildren: Array.from(row.children).map((child) => child.className).filter((className) => className !== '')
      },
      styles: {
        rootBorder: rootStyle.border,
        rowDisplay: rowStyle.display,
        rowColumns: rowStyle.gridTemplateColumns,
        rowGap: rowStyle.gap,
        rowBorder: rowStyle.border,
        rowBorderRadius: rowStyle.borderRadius,
        rowPadding: rowStyle.padding,
        rowBackground: rowStyle.backgroundColor,
        rowBoxShadow: rowStyle.boxShadow,
        rowMinHeight: rowStyle.minHeight,
        rowFontFamily: rowStyle.fontFamily,
        rowFontSize: rowStyle.fontSize,
        rowFontWeight: rowStyle.fontWeight,
        rowLineHeight: rowStyle.lineHeight
      },
      geometry: {
        rootWidth: Math.round(root.getBoundingClientRect().width),
        rowWidth: Math.round(row.getBoundingClientRect().width),
        rowHeight: layout === 'desktop' ? Math.round(row.getBoundingClientRect().height) : null
      }
    };
  });
}

function fulfillJson(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    contentType: 'application/json',
    headers: {
      'Access-Control-Allow-Origin': 'http://127.0.0.1:5173',
      'Access-Control-Allow-Headers': 'Content-Type, X-Dev-User, X-Qms-Business-Unit'
    },
    body: JSON.stringify(body)
  });
}

function hasHorizontalOverflow(page: Page) {
  return page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
}
