import { expect, type Page, type Route, test } from '@playwright/test';

const projectId = '91000000-0000-0000-0000-000000000001';
const cheongjuProjectId = '71000000-0000-0000-0000-000000000010';
const cheongjuPanelIds = [
  '72000000-0000-0000-0000-000000000001',
  '72000000-0000-0000-0000-000000000002'
];
const stepNames = ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'];

test('Osan shares its page frame and preserves registration and target navigation while Cheongju keeps its presentations', async ({ page }, testInfo) => {
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

  const overview = page.getByRole('region', { name: '프로젝트 기본 정보' });
  await expect(overview).toBeVisible();
  await expect(overview.getByRole('table')).toHaveCount(0);
  await expect(page.getByRole('tablist', { name: '프로젝트 상세 섹션' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '진행 현황 열기' })).toHaveCount(0);
  const progressPanel = page.locator('#osan-progress-panel');
  await expect(progressPanel).toBeVisible();
  await expect(progressPanel).toHaveClass(/project-detail-tab-content/);
  await expect(progressPanel.locator('.project-department-section')).toHaveAttribute('data-department', 'manufacturing');
  await expect(progressPanel.locator('.project-department-metrics')).toBeVisible();
  await expect(progressPanel.locator('[data-presentation-contract="project-status-board-v1"]')).toHaveAttribute('data-presentation-layout', 'desktop');
  const desktopTargetTable = progressPanel.getByRole('table', { name: '진행 관리 대상 현황' });
  await expect(desktopTargetTable).toBeVisible();
  await expect(desktopTargetTable).toHaveClass(/project-panel-status-table/);
  const desktopTargetRows = desktopTargetTable.getByRole('row');
  await expect(desktopTargetRows).toHaveCount(3);
  await expect(desktopTargetRows.first().getByRole('columnheader')).toHaveCount(5);
  await expect(desktopTargetTable.locator('button')).toHaveCount(2);
  const osanDesktopTargetRow = desktopTargetRows.nth(1);
  expect(await osanDesktopTargetRow.evaluate((element) => element.tagName)).toBe('BUTTON');
  await expect(osanDesktopTargetRow).toHaveAttribute('data-interactive', 'true');
  await expect(osanDesktopTargetRow).toHaveAttribute('type', 'button');
  expect(await osanDesktopTargetRow.evaluate((element) => (element as HTMLElement).tabIndex)).toBe(0);
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
  await expect(overview.getByText('001-PO/+', { exact: true })).toBeVisible();
  await expect(overview.getByText('000-W/O', { exact: true })).toBeVisible();
  await expect(overview.getByText('거래처', { exact: true })).toHaveCount(2);
  await expect(overview.getByText('제품 이름', { exact: true })).toBeVisible();
  await expect(overview.getByText('2개', { exact: true })).toBeVisible();
  await expect(overview.getByText('2026-12-31', { exact: true })).toBeVisible();
  const detailCode = overview.locator('.project-code-value');
  expect(await detailCode.textContent()).toBe('AbC  001');
  expect(await detailCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await detailCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(overview.locator('.osan-detail-status')).toHaveText('시작 전');
  const osanDesktopStatusContract = await projectStatusBoardContract(page);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-detail-desktop-1440.png'), fullPage: true });

  await osanDesktopTargetRow.press('Enter');
  await expect(page).toHaveURL(`/progress?projectId=${projectId}&targetId=${projectDetail().targets[0].targetId}`);
  await expect(page.getByRole('region', { name: '오산 진행 상세' })).toBeVisible();
  await expect(page.locator('.osan-progress-target-trigger')).toHaveText('제품 이름 1');
  await page.goto(`/projects/${projectId}`);
  await expect(overview).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '저장된 Title' })).toBeVisible();
  await expect(page.getByRole('button', { name: '목록으로' })).toBeVisible();
  const mobileDetailCode = overview.locator('.project-code-value');
  await expect(mobileDetailCode).toHaveText('AbC  001');
  expect(await mobileDetailCode.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('break-spaces');
  expect(await mobileDetailCode.evaluate((element) => getComputedStyle(element).textTransform)).toBe('none');
  await expect(overview.locator('.osan-detail-status')).toHaveText('시작 전');
  await expect(overview.getByText('001-PO/+', { exact: true })).toBeVisible();
  await expect(overview.getByText('000-W/O', { exact: true })).toBeVisible();
  await expect(desktopTargetTable).toHaveCount(0);
  const mobileTargetCards = progressPanel.locator('.project-panel-status-cards');
  await expect(mobileTargetCards).toBeVisible();
  const mobileTargetItems = mobileTargetCards.locator('.project-panel-status-card');
  await expect(mobileTargetItems).toHaveCount(2);
  await expect(mobileTargetCards.locator('button')).toHaveCount(2);
  const osanMobileTarget = mobileTargetItems.first();
  expect(await osanMobileTarget.evaluate((element) => element.tagName)).toBe('BUTTON');
  await expect(osanMobileTarget).toHaveAttribute('data-interactive', 'true');
  expect(await osanMobileTarget.evaluate((element) => (element as HTMLElement).tabIndex)).toBe(0);
  await page.evaluate(() => window.scrollTo(0, 0));
  for (const target of await mobileTargetItems.all()) {
    await expect(target).toContainText('시작 전');
    await expect(target).toContainText('0/7단계 완료');
    await expect(target).toContainText('입고검사');
  }
  const osanMobileStatusContract = await projectStatusBoardContract(page);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-detail-mobile-390.png'), fullPage: true });

  await osanMobileTarget.click();
  await expect(page).toHaveURL(`/progress?projectId=${projectId}&targetId=${projectDetail().targets[0].targetId}`);
  await expect(page.locator('.osan-progress-target-trigger')).toHaveText('제품 이름 1');
  await page.goto(`/projects/${projectId}`);
  await page.getByRole('button', { name: '목록으로' }).click();
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
  await expect(mobileList.locator('.mobile-detail-list dt')).toHaveText(['거래처', 'Code', '제품명', '수량', '납기일', '상태', '진행률']);
  const osanMobilePage = page.locator('[data-presentation-contract="osan-list-frame"]');
  await expect(osanMobilePage.getByRole('heading', { name: '프로젝트', exact: true })).toBeVisible();
  await expect(osanMobilePage.getByRole('button', { name: '신규 프로젝트', exact: true })).toBeVisible();
  await expect(osanMobilePage.getByRole('textbox', { name: '프로젝트 검색' })).toBeVisible();
  await expect(osanMobilePage.getByLabel('프로젝트 요약').locator(':scope > div')).toHaveText(['전체1', '시작 전1', '진행 중0', '완료0']);
  await osanMobilePage.getByRole('button', { name: '필터', exact: true }).click();
  await expect(osanMobilePage.getByRole('combobox', { name: '상태' })).toHaveValue('All');
  await expect(osanMobilePage.getByLabel('시작일')).toBeVisible();
  await expect(osanMobilePage.getByLabel('종료일')).toBeVisible();
  await osanMobilePage.getByLabel('시작일').fill('2027-01-01');
  await expect(page.getByText('조건에 맞는 프로젝트가 없습니다.')).toBeVisible();
  await osanMobilePage.getByRole('button', { name: '초기화', exact: true }).click();
  await expect(mobileList).toBeVisible();
  await osanMobilePage.getByRole('combobox', { name: '상태' }).selectOption('Completed');
  await expect(page.getByText('조건에 맞는 프로젝트가 없습니다.')).toBeVisible();
  await osanMobilePage.getByRole('button', { name: '초기화', exact: true }).click();
  await expect(mobileList).toBeVisible();
  await osanMobilePage.getByRole('button', { name: '닫기', exact: true }).click();
  for (const forbiddenText of ['Excel', 'Pending', '병목']) await expect(osanMobilePage.getByText(forbiddenText)).toHaveCount(0);
  await expect(osanMobilePage.getByRole('checkbox')).toHaveCount(0);
  const osanMobilePageContract = await osanListFrameContract(page);
  expect(osanMobilePageContract.order).toEqual(['title', 'description', 'kpi', 'filter', 'listHeading']);
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
  const osanDesktopPage = page.locator('[data-presentation-contract="osan-list-frame"]');
  await expect(osanDesktopPage.getByRole('textbox', { name: '프로젝트 검색' })).toBeVisible();
  await expect(osanDesktopPage.getByLabel('프로젝트 요약').locator(':scope > div')).toHaveCount(4);
  const osanDesktopPageContract = await osanListFrameContract(page);
  expect(osanDesktopPageContract.order).toEqual(osanMobilePageContract.order);
  const osanDesktopListContract = await projectListContract(page);
  expect(osanDesktopListContract.geometry.rowHeight).toBeGreaterThanOrEqual(60);
  expect(osanDesktopListContract.geometry.headerBodyAligned).toBe(true);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-project-list-desktop-1440.png'), fullPage: true });

  for (const [path, title] of [['/', '오산 홈'], ['/progress', '진행 현황']] as const) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: title, exact: true })).toBeVisible();
    await expect(page.getByRole('list', { name: '프로젝트 진행 목록' })).toBeVisible();
    expect(await osanListFrameContract(page)).toEqual(osanDesktopPageContract);
    expect(await hasHorizontalOverflow(page)).toBe(false);
  }

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
  expect(cheongjuDesktopPageContract.structure.commonOrder).toEqual(['header', 'filter', 'kpi', 'tabs', 'list']);
  expect(cheongjuDesktopPageContract.structure.commonOrderValid).toBe(true);
  const cheongjuDesktopListContract = await projectListContract(page);
  expect(cheongjuDesktopListContract.geometry.headerBodyAligned).toBe(true);
  expect(cheongjuDesktopListContract.structure).toEqual(osanDesktopListContract.structure);
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
  expect(await projectSummaryContract(page)).toMatchObject({ contract: 'project-summary-v1', layout: 'desktop', structure: { item: ['DIV', 'DT', 'DD'] } });
  expect((await projectStatusBoardContract(page)).structure).toEqual(osanDesktopStatusContract.structure);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('cheongju-project-detail-desktop-1440.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('[data-presentation-contract="project-summary-v1"]')).toHaveAttribute('data-presentation-layout', 'mobile');
  await expect(page.locator('[data-presentation-contract="project-status-board-v1"]')).toHaveAttribute('data-presentation-layout', 'mobile');
  const cheongjuMobileStatusCard = page.locator('[data-presentation-contract="project-status-board-v1"] [data-presentation-row="status"]').first();
  expect(await cheongjuMobileStatusCard.evaluate((element) => element.tagName)).toBe('BUTTON');
  await expect(cheongjuMobileStatusCard).toHaveAttribute('data-interactive', 'true');
  await expect(cheongjuMobileStatusCard.locator('.project-panel-status-card-title strong')).toHaveText('패널명 미입력');
  expect(await projectSummaryContract(page)).toMatchObject({ contract: 'project-summary-v1', layout: 'mobile', structure: { item: ['DIV', 'DT', 'DD'] } });
  expect((await projectStatusBoardContract(page)).structure).toEqual(osanMobileStatusContract.structure);
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
  expect(cheongjuMobilePageContract.structure.commonOrder).toEqual(['header', 'filter', 'kpi', 'tabs', 'list']);
  expect(cheongjuMobilePageContract.structure.commonOrderValid).toBe(true);
  expect((await projectListContract(page)).structure).toEqual(osanMobileListContract.structure);
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('cheongju-project-list-mobile-390.png'), fullPage: true });

  expect(unexpectedRequests).toEqual([]);
  expect(consoleErrors).toEqual([]);
  expect(requestFailures).toEqual([]);
});

test('Osan Excel previews errors and registers from the shared project menu on desktop and mobile', async ({ page }, testInfo) => {
  const unexpected: string[] = [];
  const consoleErrors: string[] = [];
  page.on('pageerror', error => consoleErrors.push(error.message));
  await installBackend(page, [], unexpected);
  await page.addInitScript(() => window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN'));
  let applied = false;
  let invalid = true;
  let releaseApply!: () => void;
  await page.route('**/api/osan/projects', route => fulfillJson(route, { items: applied ? [projectDetail()] : [] }));
  await page.route('**/api/osan/projects/import/*', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/template')) return route.fulfill({ status: 200, contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', body: 'synthetic template response' });
    if (path.endsWith('/preview')) return fulfillJson(route, {
      fileSha256: 'synthetic-file-hash', totalRowCount: 1, totalQuantity: 2, errorCount: invalid ? 1 : 0, errors: [],
      rows: [{ rowNumber: 2, ...projectDetail(), errors: invalid ? ['이미 등록된 프로젝트 코드입니다.'] : [] }]
    });
    if (path.endsWith('/apply')) {
      expect(route.request().postData()).toContain('synthetic-file-hash');
      expect(route.request().postData()).toContain('operationId');
      await new Promise<void>(resolve => { releaseApply = resolve; });
      applied = true;
      return fulfillJson(route, { operationId: 'synthetic-operation', replayed: false, createdCount: 1, projectIds: [projectId] });
    }
    throw new Error(`Unexpected import route: ${path}`);
  });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/projects');
  await page.getByRole('button', { name: '엑셀 업로드', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: '오산 프로젝트 엑셀 업로드' });
  const downloadPromise = page.waitForEvent('download');
  await dialog.getByRole('button', { name: '엑셀 양식 다운로드' }).click();
  expect((await downloadPromise).suggestedFilename()).toBe('EMI_오산_프로젝트_등록양식.xlsx');
  await dialog.getByLabel('작성한 엑셀 파일').setInputFiles({ name: 'projects.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: Buffer.from('synthetic workbook') });
  await dialog.getByRole('button', { name: '내용 미리보기' }).click();
  await expect(dialog.getByRole('list', { name: '입력 오류 목록' })).toContainText('2행: 이미 등록된 프로젝트 코드입니다.');
  await expect(dialog.getByRole('button', { name: '1개 프로젝트 등록' })).toBeDisabled();
  await page.screenshot({ path: testInfo.outputPath('osan-excel-error-desktop.png'), fullPage: true });
  invalid = false;
  await dialog.getByLabel('작성한 엑셀 파일').setInputFiles({ name: 'corrected.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: Buffer.from('corrected synthetic workbook') });
  await expect(dialog.getByRole('button', { name: '1개 프로젝트 등록' })).toHaveCount(0);
  await dialog.getByRole('button', { name: '내용 미리보기' }).click();
  await expect(dialog.getByRole('button', { name: '1개 프로젝트 등록' })).toBeEnabled();
  expect(await dialog.locator('tbody td').nth(1).textContent()).toBe('AbC  001');
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-excel-ready-mobile.png'), fullPage: true });
  await dialog.getByRole('button', { name: '1개 프로젝트 등록' }).focus();
  await page.keyboard.press('Enter');
  await expect(dialog.getByRole('button', { name: '등록 중…' })).toBeDisabled();
  await page.keyboard.press('Tab');
  expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);
  await page.keyboard.press('Escape');
  await expect(dialog).toBeVisible();
  releaseApply();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole('status').filter({ hasText: '1개 프로젝트를 등록했습니다.' })).toBeVisible();
  await expect(page.getByTestId('osan-project-list-mobile')).toBeVisible();
  expect(await hasHorizontalOverflow(page)).toBe(false);
  await page.screenshot({ path: testInfo.outputPath('osan-excel-success-mobile.png'), fullPage: true });
  expect(unexpected).toEqual([]);
  expect(consoleErrors).toEqual([]);
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
    if (path === '/api/osan/dashboard') {
      return fulfillJson(route, {
        summary: { totalCount: 1, notStartedCount: 1, inProgressCount: 0, completedCount: 0 },
        items: [{ ...projectDetail(), progressPercent: 0, stages: stepNames.map((stepName, index) => ({
          sequenceNumber: index + 1, stepCode: `STEP_${index + 1}`, stepName,
          completedTargetCount: 0, totalTargetCount: 2
        })) }],
        totalCount: 1, page: 1, pageSize: 11
      });
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
    if (path === `/api/osan/projects/${projectId}/progress`) return fulfillJson(route, projectDetail());
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
    status: 'NotStarted',
    completedStepCount: 0,
    totalStepCount: 14,
    createdAtUtc: '2026-09-07T00:00:00Z',
    targets: Array.from({ length: 2 }, (_, targetIndex) => ({
      targetId: `92000000-0000-0000-0000-${String(targetIndex + 1).padStart(12, '0')}`,
      sequenceNumber: targetIndex + 1,
      displayName: `제품  이름 ${targetIndex + 1}`,
      status: 'NotStarted',
      version: 1,
      startedAtUtc: null, startedByUserId: null, startedByDisplayName: null,
      steps: stepNames.map((stepName, stepIndex) => ({
        stepId: `${93000000 + targetIndex}-${String(stepIndex + 1).padStart(4, '0')}-0000-0000-000000000001`,
        sequenceNumber: stepIndex + 1,
        stepCode: `STEP_${stepIndex + 1}`,
        stepName,
        status: 'NotStarted',
        startedAtUtc: null, completedAtUtc: null, completedByUserId: null, completedByDisplayName: null,
        canCompleteIndividual: stepIndex < 6, canCompleteBatch: stepIndex < 6,
        guidanceDescription: null, guidancePhotos: [], photos: []
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

function osanListFrameContract(page: Page) {
  return page.locator('[data-presentation-contract="osan-list-frame"]').evaluate(root => {
    const sections = new Map<string, string>([
      ['h1', 'title'], ['.osan-dashboard-description', 'description'],
      ['.osan-dashboard-summary', 'kpi'], ['.osan-dashboard-toolbar', 'filter'],
      ['.osan-list-heading', 'listHeading']
    ]);
    const elements = [...sections].map(([selector]) => root.querySelector(`:scope > ${selector}`) as HTMLElement);
    return {
      order: Array.from(root.children).flatMap(element => [...sections].filter(([selector]) => element.matches(selector)).map(([, name]) => name)),
      geometry: elements.map(element => ({ y: element.getBoundingClientRect().y, height: element.getBoundingClientRect().height })),
      labels: Array.from(root.querySelectorAll('.osan-dashboard-summary > div > span')).map(element => element.textContent)
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
