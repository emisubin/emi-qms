import { execFileSync } from 'node:child_process';
import { expect, type APIRequestContext, type Page, test } from '@playwright/test';

const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;

type ProductionPlanItemResponse = {
  itemId: string;
  templateStepId: string | null;
  stepName: string;
  sequenceNumber: number;
  isRequired: boolean;
  rowVersion: number;
  plannedDate: string | null;
  note: string | null;
};

type ProjectAssigneeResponse = {
  responsibilityType: string;
  assigneeId: string | null;
  rowVersion: number;
};

type ProductionPlanningResponse = {
  productTypeId: string | null;
  rowVersion: number;
  notes: string | null;
  items: ProductionPlanItemResponse[];
  assignees: ProjectAssigneeResponse[];
};

test('TASK-003B-1 A: read/detail split keeps detail fixed and edit page accepts duplicate names', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Direct ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-A-${unique}`, projectTitle, 'StretchWrap', 2);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await expect(page.getByRole('heading', { name: '설계' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toHaveCount(0);
  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  await page.getByLabel('No.1 패널명').fill('PNL-1');
  await page.getByLabel('No.2 패널명').fill('PNL-1');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  const duplicateDialog = page.getByTestId('duplicate-panel-name-dialog');
  await expect(duplicateDialog).toContainText('중복된 패널명이 있습니다.');
  await expect(duplicateDialog).toContainText('PNL-1: No.1, No.2');
  await duplicateDialog.getByRole('button', { name: '중복이어도 저장' }).click();

  await expect(page.getByRole('table', { name: '설계' })).toContainText('PNL-1');
  expect(await page.getByText('동일 명칭 2면').count()).toBeGreaterThanOrEqual(2);
  expect(await queryDatabaseValue(`select count(*)::text from panel_placeholders where project_id = '${projectId}' and panel_name = 'PNL-1' and panel_info_completed and qr_eligible;`)).toBe('2');
  await page.reload();
  const productTable = page.getByRole('table', { name: '설계' });
  await expect(productTable).toContainText('PNL-1');
  await expect(productTable).toContainText('동일 명칭 2면');
  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  await expect(page.getByLabel('No.1 패널명')).toHaveValue('PNL-1');
  await expect(page.getByLabel('No.2 패널명')).toHaveValue('PNL-1');
});

test('TASK-003B B: WoodenCrate name-only is QR eligible and unit switch does not drift size', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Drift ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-B-${unique}`, projectTitle, 'WoodenCrate', 1);
  const panel = await readPanelInformation(request, projectId);

  const nameOnly = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, {
    headers: { 'X-Dev-User': 'dev-design' },
    data: {
      panels: [{
        panelId: panel.panels[0].panelId,
        expectedPanelInfoVersion: panel.panels[0].panelInfoVersion,
        panelNameUpdate: { isChanged: true, value: 'WOOD-1' }
      }]
    }
  });
  expect(nameOnly.ok()).toBeTruthy();
  const snapshot = await readPanelInformation(request, projectId);
  expect(snapshot.panels[0].qrEligible).toBeTruthy();
  expect(snapshot.panels[0].panelInfoCompleted).toBeFalsy();

  const complete = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, {
    headers: { 'X-Dev-User': 'dev-design' },
    data: {
      panels: [{
        panelId: snapshot.panels[0].panelId,
        expectedPanelInfoVersion: snapshot.panels[0].panelInfoVersion,
        sizeUpdate: { isChanged: true, clear: false, inputUnit: 'Mm', width: 800, height: 1800, depth: 400 }
      }]
    }
  });
  expect(complete.ok()).toBeTruthy();

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  await page.getByLabel('입력 단위').selectOption('Inch');
  await page.getByLabel('No.1 패널명').fill('WOOD-1-REV');
  await page.getByLabel('수정사유*').fill('패널명만 수정');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  await expect(page.getByRole('table', { name: '설계' })).toContainText('WOOD-1-REV');

  expect(await queryDatabaseValue(`select width_mm::text || ',' || height_mm::text || ',' || depth_mm::text from panel_placeholders where project_id = '${projectId}' and sequence_number = 1;`)).toBe('800.000,1800.000,400.000');
  expect(await queryDatabaseValue(`select count(*)::text from project_audit_events where project_id = '${projectId}' and field_name in ('WidthMm','HeightMm','DepthMm') and new_value in ('799.998','800.1');`)).toBe('0');
});

test('TASK-003B-1 B: project detail summarizes QR, manufacturing, and inspection workflow counts', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Workflow Summary ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-WF-${unique}`, projectTitle, 'WoodenCrate', 10);
  queryDatabaseValue(`
    update panel_placeholders
    set workflow_stage = case sequence_number
        when 1 then 'BeforeManufacturing'
        when 2 then 'BeforeManufacturing'
        when 3 then 'ManufacturingInProgress'
        when 4 then 'ManufacturingInProgress'
        when 5 then 'ManufacturingCompleted'
        when 6 then 'ManufacturingCompleted'
        when 7 then 'InspectionInProgress'
        when 8 then 'InspectionCompleted'
        when 9 then 'PackingCompleted'
        else 'ShipmentCompleted'
      end,
      panel_name = case when sequence_number in (1, 3, 5, 7) then 'QR-' || sequence_number::text else null end
    where project_id = '${projectId}';
    select 'ok';
  `);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await openProject(page, projectTitle);

  const summary = page.locator('.project-workflow-summary');
  await expect(summary.locator('.status-chip').nth(0)).toContainText('QR 가능');
  await expect(summary.locator('.status-chip').nth(0)).toContainText('4/10');
  await expect(summary.locator('.status-chip').nth(1)).toContainText('제조 완료');
  await expect(summary.locator('.status-chip').nth(1)).toContainText('6/10');
  await expect(summary.locator('.status-chip').nth(2)).toContainText('검사 완료');
  await expect(summary.locator('.status-chip').nth(2)).toContainText('3/10');
  await expect(summary).not.toContainText('입력 완료');
  await expect(page.locator('.product-panel-table-head')).toHaveCSS('position', 'sticky');
  await expect(page.getByRole('table', { name: '설계' })).toContainText('납품 완료');
});

test('TASK-003B-1 UX: product panel and edit grid headers stay sticky on long lists', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Sticky Header ${unique}`;
  await createProjectByApi(request, `FS-3B-STICKY-${unique}`, projectTitle, 'WoodenCrate', 30);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await openProject(page, projectTitle);

  const header = page.locator('.product-panel-table-head');
  await expect(header).toContainText('No');
  await expect(header).toContainText('패널명');
  await expect(header).toContainText('사이즈');
  await expect(header).toContainText('패널정보');
  await expect(header).toContainText('QR');
  await expect(header).toContainText('상태');
  await expect(page.getByText('Placeholder')).toHaveCount(0);

  await page.evaluate(() => {
    const productHeader = document.querySelector('.product-panel-table-head');
    if (!productHeader) {
      throw new Error('product panel header not found');
    }

    const targetY = productHeader.getBoundingClientRect().top + window.scrollY + 300;
    window.scrollTo(0, targetY);
  });

  await expect(header).toBeVisible();
  const stickyBox = await header.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    const style = getComputedStyle(element);
    return {
      top: rect.top,
      bottom: rect.bottom,
      position: style.position,
      backgroundColor: style.backgroundColor,
      zIndex: Number(style.zIndex)
    };
  });

  expect(stickyBox.position).toBe('sticky');
  expect(stickyBox.top).toBeGreaterThanOrEqual(-1);
  expect(stickyBox.top).toBeLessThanOrEqual(1);
  expect(stickyBox.bottom).toBeGreaterThan(0);
  expect(stickyBox.backgroundColor).not.toBe('rgba(0, 0, 0, 0)');
  expect(stickyBox.zIndex).toBeGreaterThanOrEqual(1);

  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  const editHeader = page.locator('.panel-info-table-head');
  await expect(editHeader).toContainText('No');
  await expect(editHeader).toContainText('패널명');
  await expect(editHeader).toContainText('W');
  await expect(editHeader).toContainText('H');
  await expect(editHeader).toContainText('D');
  await expect(editHeader).toContainText('패널정보');
  await expect(editHeader).toContainText('QR');

  await page.evaluate(() => {
    const editGridHeader = document.querySelector('.panel-info-table-head');
    if (!editGridHeader) {
      throw new Error('panel information edit header not found');
    }

    const targetY = editGridHeader.getBoundingClientRect().top + window.scrollY + 300;
    window.scrollTo(0, targetY);
  });

  await expect(editHeader).toBeVisible();
  const editStickyBox = await editHeader.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    const style = getComputedStyle(element);
    return {
      top: rect.top,
      bottom: rect.bottom,
      position: style.position,
      backgroundColor: style.backgroundColor,
      zIndex: Number(style.zIndex)
    };
  });

  expect(editStickyBox.position).toBe('sticky');
  expect(editStickyBox.top).toBeGreaterThanOrEqual(-1);
  expect(editStickyBox.top).toBeLessThanOrEqual(1);
  expect(editStickyBox.bottom).toBeGreaterThan(0);
  expect(editStickyBox.backgroundColor).not.toBe('rgba(0, 0, 0, 0)');
  expect(editStickyBox.zIndex).toBeGreaterThanOrEqual(1);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.locator('[data-testid="panel-info-edit-mobile"]')).toBeVisible();
  await expect(page.locator('[data-testid="panel-info-edit-desktop"]')).toBeHidden();
  await expect(page.locator('[data-testid="panel-info-edit-card"]').first()).toContainText('No.1');
  await expect(page.locator('[data-testid="panel-info-edit-card"]').first().getByLabel('패널명')).toBeVisible();
});

test('TASK-003B-1 UX: mobile layouts use cards for detail, edit, and Excel preview', async ({ page, request }, testInfo) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Mobile Cards ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-MOBILE-${unique}`, projectTitle, 'WoodenCrate', 2);
  const panelInfo = await readPanelInformation(request, projectId);

  const seed = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, {
    headers: { 'X-Dev-User': 'dev-design' },
    data: {
      panels: [{
        panelId: panelInfo.panels[0].panelId,
        expectedPanelInfoVersion: panelInfo.panels[0].panelInfoVersion,
        panelNameUpdate: { isChanged: true, value: 'MOBILE-1' },
        sizeUpdate: { isChanged: true, clear: false, inputUnit: 'Mm', width: 800, height: 1800, depth: 400 }
      }]
    }
  });
  expect(seed.ok()).toBeTruthy();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);

  await expect(page.locator('[data-testid="project-panel-list-mobile"]')).toBeVisible();
  await expect(page.locator('[data-testid="project-panel-list-desktop"]')).toBeHidden();
  const firstProjectCard = page.locator('[data-testid="project-panel-card"]').first();
  await expect(firstProjectCard).toContainText('No.1');
  await expect(firstProjectCard).toContainText('MOBILE-1');
  await expect(firstProjectCard).toContainText('800 × 1800 × 400 mm');
  await expect(firstProjectCard).toContainText('입력 완료');
  await expect(firstProjectCard).toContainText('생성 가능');
  await expect(firstProjectCard).toContainText('제조 전');
  await expect(firstProjectCard).not.toContainText('BeforeManufacturing');
  await expect(page.getByText('Placeholder')).toHaveCount(0);

  const secondProjectCard = page.locator('[data-testid="project-panel-card"]').nth(1);
  await expect(secondProjectCard).toContainText('미입력');
  await expect(secondProjectCard.locator('.negative-text', { hasText: '생성 불가' })).toBeVisible();

  await firstProjectCard.getByRole('button', { name: '상세 보기' }).click();
  await expect(page.getByTestId('project-context-summary')).toContainText(projectTitle);
  await expect(page.getByTestId('project-context-summary')).toContainText(`FS-3B-MOBILE-${unique}`);
  await expect(page.getByLabel('패널 요약')).toContainText('No.1');
  await expect(page.getByLabel('패널 요약')).toContainText('MOBILE-1');
  await expect(page.getByText('W/H/D')).toHaveCount(0);
  await expect(page.getByText('QR 조건')).toHaveCount(0);
  await page.locator('.page-surface').filter({ hasText: '설계' }).getByRole('button', { name: '프로젝트' }).click();

  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  await expect(page.locator('[data-testid="panel-info-edit-mobile"]')).toBeVisible();
  await expect(page.locator('[data-testid="panel-info-edit-desktop"]')).toBeHidden();
  const firstEditCard = page.locator('[data-testid="panel-info-edit-card"]').first();
  await expect(firstEditCard).toContainText('No.1');
  await firstEditCard.getByLabel('패널명').fill('MOBILE-1-EDIT');
  await expect(firstEditCard.getByLabel('패널명')).toHaveValue('MOBILE-1-EDIT');

  const workbookPath = testInfo.outputPath('mobile-panel-preview.xlsx');
  writePanelInformationWorkbook(workbookPath, [
    ['2', 'M-20002', 'MOBILE-2', '', '', '']
  ]);

  await page.getByRole('button', { name: 'Excel 업로드' }).click();
  const excelDialog = page.getByRole('dialog', { name: 'Excel 업로드' });
  await page.locator('input[type="file"]').setInputFiles(workbookPath);
  await page.getByLabel('파일 단위').selectOption('Mm');
  await page.getByRole('button', { name: 'Preview' }).click();
  await expect(excelDialog.locator('.excel-preview-action-bar')).toBeVisible();
  await expect(excelDialog.getByRole('button', { name: 'Excel 저장' })).toBeVisible();
  await expect(excelDialog.locator('[data-testid="excel-preview-mobile"]')).toBeVisible();
  await expect(excelDialog.locator('[data-testid="excel-preview-desktop"]')).toBeHidden();
  await expect(excelDialog.locator('.excel-preview-card').first()).toContainText('No.2');
  await expect(excelDialog.locator('.excel-preview-card').first()).toContainText('MOBILE-2');
});

test('TASK-003B-1 UX: project list has all tab, sticky header, workflow status, progress, and mobile cards', async ({ page, request }) => {
  const unique = Date.now();
  const activeTitle = `TASK 003B List Active ${unique}`;
  const holdTitle = `TASK 003B List Hold ${unique}`;
  const completedTitle = `TASK 003B List Completed ${unique}`;
  const cancelledTitle = `TASK 003B List Cancelled ${unique}`;
  const deletedTitle = `TASK 003B List Deleted ${unique}`;

  const activeId = await createProjectByApi(request, `FS-LIST-A-${unique}`, activeTitle, 'WoodenCrate', 2);
  const holdId = await createProjectByApi(request, `FS-LIST-H-${unique}`, holdTitle, 'WoodenCrate', 1);
  const completedId = await createProjectByApi(request, `FS-LIST-COMP-${unique}`, completedTitle, 'WoodenCrate', 1);
  const cancelledId = await createProjectByApi(request, `FS-LIST-CAN-${unique}`, cancelledTitle, 'WoodenCrate', 1);
  const deletedId = await createProjectByApi(request, `FS-LIST-DEL-${unique}`, deletedTitle, 'WoodenCrate', 1);

  queryDatabaseValue(`
    update panel_placeholders
    set workflow_stage = case sequence_number
        when 1 then 'ManufacturingInProgress'
        else 'BeforeManufacturing'
      end
    where project_id = '${activeId}';
    update projects set status = 'Completed' where id = '${completedId}';
    select 'ok';
  `);

  expect((await request.post(`${apiBaseUrl}/api/projects/${holdId}/hold`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: { reason: '목록 보류' }
  })).ok()).toBeTruthy();
  expect((await request.post(`${apiBaseUrl}/api/projects/${cancelledId}/cancel`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: { reason: '목록 취소' }
  })).ok()).toBeTruthy();
  expect((await request.post(`${apiBaseUrl}/api/projects/${deletedId}/delete`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: { reason: '목록 삭제', confirmProjectTitle: deletedTitle }
  })).ok()).toBeTruthy();

  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  const desktopNavigation = page.getByRole('navigation', { name: '공통 메뉴' });
  await expect(desktopNavigation.getByRole('button', { name: '프로젝트' })).toHaveClass(/active/);
  await expect(desktopNavigation.getByRole('button', { name: '구매' })).toBeVisible();
  await expect(page.getByLabel('프로젝트 요약')).toContainText('전체 프로젝트');
  await expect(page.getByLabel('프로젝트 요약')).not.toContainText('QR 가능 패널');
  await expect(page.getByLabel('프로젝트 요약')).toContainText('제조 완료 프로젝트');
  await expect(page.getByLabel('프로젝트 요약')).toContainText('검사 완료 프로젝트');
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(`TASK 003B List`);
  await page.getByRole('button', { name: '검색' }).click();

  const tabNames = await page.getByRole('tab').allTextContents();
  expect(tabNames.slice(0, 5)).toEqual(['전체', '진행', '보류', '완료', '취소']);
  await expect(page.getByRole('tab', { name: '전체' })).toHaveAttribute('aria-selected', 'true');

  const projectTable = page.getByRole('table', { name: '프로젝트 목록' });
  await expect(projectTable).toContainText(activeTitle);
  await expect(projectTable).toContainText(holdTitle);
  await expect(projectTable).toContainText(completedTitle);
  await expect(projectTable).toContainText(cancelledTitle);
  await expect(projectTable).not.toContainText(deletedTitle);
  await expect(projectTable).toContainText('생산관리');
  await expect(projectTable).toContainText('6%');
  await expect(projectTable).not.toContainText('ManufacturingInProgress');
  await expect(projectTable).not.toContainText('13/26');

  const header = page.locator('.project-list-head');
  await expect(header).toContainText('프로젝트명');
  await expect(header).toContainText('고객사');
  await expect(header).toContainText('Code');
  await expect(header).toContainText('Item');
  await expect(header).toContainText('면수');
  await expect(header).toContainText('납기일');
  await expect(header).toContainText('상태');
  await expect(header).toContainText('진행률');
  await expect(header).toHaveCSS('position', 'sticky');

  await page.evaluate(() => {
    const projectHeader = document.querySelector('.project-list-head');
    if (!projectHeader) {
      throw new Error('project list header not found');
    }

    window.scrollTo(0, projectHeader.getBoundingClientRect().top + window.scrollY + 300);
  });
  await expect(header).toBeVisible();

  await page.getByRole('tab', { name: '진행' }).click();
  await expect(projectTable).toContainText(activeTitle);
  await expect(projectTable).not.toContainText(holdTitle);
  await page.getByRole('tab', { name: '취소' }).click();
  await expect(projectTable).toContainText(cancelledTitle);
  await expect(projectTable).not.toContainText(activeTitle);
  await expect(page.getByRole('tab', { name: '삭제 보관함' })).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole('tab', { name: '전체' }).click();
  await expect(page.locator('[data-testid="project-list-mobile"]')).toBeVisible();
  await expect(page.locator('[data-testid="project-list-desktop"]')).toHaveCount(0);
  const mobileCard = page.locator('[data-testid="project-list-card"]').filter({ hasText: activeTitle });
  await expect(mobileCard).toContainText('고객사');
  await expect(mobileCard).toContainText('Code');
  await expect(mobileCard).toContainText('Item');
  await expect(mobileCard).toContainText('면수');
  await expect(mobileCard).toContainText('납기일');
  await expect(mobileCard).toContainText('상태');
  await expect(mobileCard).toContainText('진행률');
  await expect(mobileCard).toContainText('생산관리');
  await expect(mobileCard).toContainText('6%');
});

test('TASK-004A project Excel import creates projects and panels', async ({ page }, testInfo) => {
  const unique = Date.now();
  const projectTitle = `TASK 004A Project Excel ${unique}`;
  const workbookPath = testInfo.outputPath('project-create.xlsx');
  writeProjectCreateWorkbook(workbookPath, [
    ['TEST CUSTOMER', 'UL67', `FS-4A-PROJ-${unique}`, projectTitle, '3', '2026-10-10', '목포장', '아니오', 'dev-sales', '', '', 'TEST LOCATION']
  ]);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await expect(page.getByRole('button', { name: '프로젝트 Excel 양식' })).toBeVisible();
  await page.getByRole('button', { name: '프로젝트 Excel 업로드' }).click();
  const excelDialog = page.getByRole('dialog', { name: '프로젝트 Excel 업로드' });
  await page.locator('input[type="file"]').setInputFiles(workbookPath);
  await excelDialog.getByRole('button', { name: 'Preview' }).click();
  await expect(excelDialog.locator('.excel-preview-action-bar')).toContainText('신규 1건');
  await expect(excelDialog).toContainText(projectTitle);
  await expect(excelDialog.getByRole('button', { name: 'Excel 저장' })).toBeEnabled();
  await excelDialog.getByRole('button', { name: 'Excel 저장' }).click();
  await expect(page.getByText('프로젝트 Excel을 저장했습니다.')).toBeVisible();

  await openProject(page, projectTitle);
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  expect(await queryDatabaseValue(`select count(*)::text from panel_placeholders where project_id = (select id from projects where project_title = '${projectTitle.replaceAll("'", "''")}') and status = 'Active';`)).toBe('3');
});

test('TASK-004A A/D/G: procurement direct input, material receipt, permissions, and mobile cards', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 004A Direct ${unique}`;
  const projectId = await createProjectByApi(request, `FS-4A-DIRECT-${unique}`, projectTitle, 'StretchWrap', 1);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await openProject(page, projectTitle);
  await page.getByRole('tab', { name: '구매' }).click();
  await expect(page.getByRole('button', { name: '구매정보 수정' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toHaveCount(0);
  await page.getByRole('button', { name: '구매정보 수정' }).click();
  await expect(page.getByTestId('project-context-summary')).toContainText(projectTitle);
  await expect(page.getByTestId('project-context-summary')).toContainText(`FS-4A-DIRECT-${unique}`);
  await expect(page.getByTestId('project-context-summary')).toContainText('2026-10-10');
  await expect(page.getByRole('table', { name: '구매정보 수정' })).toBeVisible();
  const editTable = page.getByRole('table', { name: '구매정보 수정' });
  const editRows = editTable.locator('.procurement-table-row.editable');
  const initialEditRowCount = await editRows.count();
  await page.getByRole('button', { name: '행 추가' }).click();
  try {
    await expect.poll(async () => editRows.count(), { timeout: 5_000 }).toBeGreaterThan(initialEditRowCount);
  } catch {
    await page.getByRole('button', { name: '행 추가' }).click();
    await expect.poll(async () => editRows.count(), { timeout: 10_000 }).toBeGreaterThan(initialEditRowCount);
  }
  const editRow = editRows.nth(initialEditRowCount);
  await expect(editRow.locator('input').first()).toBeVisible({ timeout: 15_000 });
  const editInputs = editRow.locator('input');
  await editInputs.nth(0).fill('4W');
  await editInputs.nth(4).fill('2026-07-10');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('tab', { name: '구매' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByRole('table', { name: '구매정보' })).toContainText('4W');
  expect(await queryDatabaseValue(`select count(*)::text from project_procurement_items where project_id = '${projectId}' and standard_lead_time = '4W';`)).toBe('1');

  const procurementTable = page.getByRole('table', { name: '구매정보' });
  await expect(procurementTable).toContainText('4W');
  await expect(procurementTable).not.toContainText('예정일까지');
  await expect(procurementTable).not.toContainText('D-');
  await expect(procurementTable.locator('input')).toHaveCount(0);
  await expect(procurementTable).not.toContainText('입고지연');
  await expect(procurementTable).not.toContainText('미입고');

  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await openProject(page, projectTitle);
  await page.getByRole('tab', { name: '구매' }).click();
  await expect(page.getByRole('button', { name: '구매정보 수정' })).toHaveCount(0);
  await page.getByRole('navigation', { name: '공통 메뉴' }).first().getByRole('button', { name: '자재' }).click();
  await page.getByPlaceholder('프로젝트 또는 발주품목 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  const receiptGroup = page.locator('.material-receipt-group').filter({ hasText: projectTitle });
  await expect(receiptGroup).toContainText(projectTitle);
  const receiptsTable = receiptGroup.getByRole('table', { name: /자재 입고 처리/ });
  const receiptCheckbox = receiptsTable.locator('input[type="checkbox"]').first();
  await receiptCheckbox.click();
  await expect(receiptCheckbox).toBeChecked();
  await page.getByLabel('수정사유').fill('자재 입고 확인');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  expect(await queryDatabaseValue(`select receipt_completed::text from project_procurement_items where project_id = '${projectId}' limit 1;`)).toBe('true');

  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await page.getByRole('button', { name: '구매' }).click();
  await expect(page.getByRole('heading', { name: '구매' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '구매' })).toHaveClass(/active/);
  await expect(page.getByLabel('구매 요약')).toContainText('입고대기품목');
  await expect(page.getByLabel('구매 요약')).toContainText('입고완료품목');
  await expect(page.getByLabel('구매 요약')).toContainText('입고예정일 경과 품목');
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toBeVisible();
  const procurementProjects = page.getByRole('table', { name: '구매 프로젝트 목록' });
  await expect(procurementProjects).toContainText(projectTitle);
  const procurementProjectRow = procurementProjects.locator('.procurement-project-row').filter({ hasText: projectTitle });
  await procurementProjectRow.click();
  const expandedProcurement = page.getByRole('region', { name: `${projectTitle} 구매정보` });
  await expect(expandedProcurement).toBeVisible();
  await expect(expandedProcurement).toContainText('4W');
  const rowBox = await procurementProjectRow.boundingBox();
  const expandedBox = await expandedProcurement.boundingBox();
  expect(rowBox).not.toBeNull();
  expect(expandedBox).not.toBeNull();
  expect(expandedBox!.y).toBeGreaterThan(rowBox!.y);
  await procurementProjectRow.click();
  await expect(expandedProcurement).toHaveCount(0);

  const salesProcurementWrite = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/procurement`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: { items: [{ orderItem: 'blocked' }] }
  });
  expect(salesProcurementWrite.status()).toBe(403);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await openProject(page, projectTitle);
  await page.getByRole('tab', { name: '구매' }).click();
  await expect(page.locator('[data-testid="procurement-mobile"]')).toBeVisible();
  await expect(page.locator('[data-testid="procurement-mobile"]')).toContainText('4W');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBeTruthy();
  await page.getByRole('button', { name: '구매정보 수정' }).click();
  await expect(page.locator('[data-testid="procurement-mobile"]')).toBeVisible();
});

test('TASK-004A B/C/E: procurement Excel matching, apply, reupload changed preview, and admin history', async ({ page, request }, testInfo) => {
  const unique = Date.now();
  const projectTitle = `TASK 004A Excel ${unique}`;
  const projectId = await createProjectByApi(request, `FS-4A-EXCEL-${unique}`, projectTitle, 'StretchWrap', 1);
  const firstWorkbook = testInfo.outputPath('procurement-first.xlsx');
  writeProcurementWorkbook(firstWorkbook, [
    [projectTitle, `FS-4A-EXCEL-${unique}`, '4W', 'MCCB', '', 'Owner A', '2026-07-01', '2026-07-10', 'First', 'Y'],
    ['', '', '5W', 'Cable', '', 'Owner B', '2026-07-02', '2026-07-11', '', '']
  ]);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await openProject(page, projectTitle);
  await page.getByRole('tab', { name: '구매' }).click();
  await page.getByRole('button', { name: '구매정보 수정' }).click();
  await page.getByRole('button', { name: 'Excel 업로드' }).click();
  const excelDialog = page.getByRole('dialog', { name: '구매 Excel 업로드' });
  await page.locator('input[type="file"]').setInputFiles(firstWorkbook);
  await page.getByRole('button', { name: 'Preview' }).click();
  await expect(excelDialog).toContainText('저장 가능한 데이터 목록 2건');
  await expect(excelDialog.getByRole('table', { name: '저장 가능한 데이터 목록' })).toContainText('Excel 행');
  await expect(excelDialog.getByRole('table', { name: '저장 가능한 데이터 목록' })).toContainText('발주품목');
  await expect(excelDialog.getByRole('table', { name: '저장 가능한 데이터 목록' }).getByText('결과')).toHaveCount(0);
  await expect(excelDialog.getByRole('table', { name: '저장 불가능한 데이터 목록' })).toContainText('Excel 행');
  await expect(excelDialog.getByRole('table', { name: '저장 불가능한 데이터 목록' })).toContainText('통상납기');
  await expect(excelDialog.getByRole('table', { name: '저장 불가능한 데이터 목록' }).getByText('해결 방법')).toHaveCount(0);
  await expect(excelDialog).not.toContainText(/오류 \d+건/);
  await expect(excelDialog).not.toContainText(/확인 필요 \d+건/);
  await expect(excelDialog).not.toContainText('저장할 수 없는 행');
  await expect(excelDialog).toContainText('매칭 완료');
  await expect(excelDialog.locator('.excel-preview-action-bar')).toHaveCSS('position', 'sticky');
  await expect(excelDialog.locator('.excel-preview-grid.saveable .excel-preview-head')).toHaveCSS('position', 'sticky');
  await excelDialog.locator('.dialog').evaluate((element) => { element.scrollTop = 300; });
  const actionBarBox = await excelDialog.locator('.excel-preview-action-bar').boundingBox();
  const saveableHeaderBox = await excelDialog.locator('.excel-preview-grid.saveable .excel-preview-head').boundingBox();
  expect(actionBarBox).not.toBeNull();
  expect(saveableHeaderBox).not.toBeNull();
  expect(saveableHeaderBox!.y).toBeGreaterThanOrEqual(actionBarBox!.y + actionBarBox!.height - 1);
  await excelDialog.getByRole('button', { name: '저장 가능한 항목 적용' }).click();
  await expect(page.getByRole('table', { name: '구매정보' })).toContainText('MCCB');
  expect(await queryDatabaseValue(`select count(*)::text from project_procurement_items where project_id = '${projectId}';`)).toBe('2');
  expect(await queryDatabaseValue(`select count(*)::text from project_audit_events where project_id = '${projectId}' and entity_type = 'ProcurementItem' and field_name = 'ShipmentText';`)).toBe('0');

  const secondWorkbook = testInfo.outputPath('procurement-second.xlsx');
  writeProcurementWorkbook(secondWorkbook, [
    [projectTitle, `FS-4A-EXCEL-${unique}`, '4W', 'MCCB', '', 'Owner A', '2026-07-01', '2026-07-10', 'First changed', 'Y'],
    ['', '', '6W', 'New item', '', 'Owner C', '2026-07-03', '2026-07-12', 'New', 'N']
  ]);

  await page.getByRole('button', { name: '구매정보 수정' }).click();
  await page.getByRole('button', { name: 'Excel 업로드' }).click();
  const reuploadDialog = page.getByRole('dialog', { name: '구매 Excel 업로드' });
  await page.locator('input[type="file"]').setInputFiles(secondWorkbook);
  await page.getByRole('button', { name: 'Preview' }).click();
  await expect(reuploadDialog).toContainText('저장 가능한 데이터 목록 2건');
  await expect(reuploadDialog).toContainText('저장 불가능한 데이터 목록 0건');
  await reuploadDialog.getByLabel('수정사유*').fill('Excel 변경분 적용');
  await reuploadDialog.getByRole('button', { name: '저장 가능한 항목 적용' }).click();
  await expect(page.getByRole('table', { name: '구매정보' })).toContainText('First changed');
  expect(await queryDatabaseValue(`select issue_note from project_procurement_items where project_id = '${projectId}' and order_item = 'MCCB';`)).toBe('First changed');
  expect(await queryDatabaseValue(`select count(*)::text from project_procurement_items where project_id = '${projectId}' and order_item = 'Cable';`)).toBe('1');

  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await openProject(page, projectTitle);
  await expect(page.getByRole('heading', { name: '전체 이력' })).toBeVisible();
  await expect(page.getByText('Excel 입력').first()).toBeVisible();
});

test('TASK-004A deleted archive restore returns a project to the normal list', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 004A Restore ${unique}`;
  const projectId = await createProjectByApi(request, `FS-4A-RESTORE-${unique}`, projectTitle, 'StretchWrap', 1);
  const deleteResponse = await request.post(`${apiBaseUrl}/api/projects/${projectId}/delete`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: { reason: '복구 E2E 준비', confirmProjectTitle: projectTitle }
  });
  expect(deleteResponse.ok()).toBeTruthy();

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await page.getByRole('tab', { name: '삭제 보관함' }).click();
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: '복구' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '완전 삭제' })).toHaveCount(0);

  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await page.getByRole('tab', { name: '삭제 보관함' }).click();
  await openProject(page, projectTitle);
  await page.getByRole('button', { name: '복구' }).click();
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  await expect(page.getByRole('table', { name: '프로젝트 목록' })).toContainText(projectTitle);
  expect(await queryDatabaseValue(`select deleted_at_utc is null from projects where id = '${projectId}';`)).toBe('t');
});

test('TASK-005A production planning page, project section, edit, permissions, and mobile cards', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 005A Plan ${unique}`;
  const projectId = await createProjectByApi(request, `FS-5A-PLAN-${unique}`, projectTitle, 'WoodenCrate', 2);
  expect(queryDatabaseValue(`
    with upserted as (
      insert into system_holidays (holiday_date, name, country_code, source, source_key, is_active)
      values (date '2026-07-03', '공식 대체공휴일', 'KR', 'E2E', '20260703:공식 대체공휴일', true)
      on conflict (country_code, holiday_date, source_key) do update
      set name = excluded.name,
          is_active = true,
          updated_at_utc = now()
      returning id
    )
    select count(*)::text from upserted;
  `)).toBe('1');

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-production');
  const navigation = page.getByRole('navigation', { name: '공통 메뉴' }).first();
  await expect(navigation.getByRole('button', { name: '생산관리' })).toBeVisible();
  await navigation.getByRole('button', { name: '생산관리' }).click();
  await expect(navigation.getByRole('button', { name: '생산관리' })).toHaveClass(/active/);
  await expect(page.getByLabel('생산계획 요약')).toContainText('생산계획 미등록');
  await expect(page.getByRole('button', { name: 'Excel 양식 다운로드' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toBeVisible();
  await page.getByRole('button', { name: 'Excel 업로드' }).click();
  const productionExcelDialog = page.getByRole('dialog', { name: '생산계획 Excel 업로드' });
  await expect(productionExcelDialog.locator('input[type="file"]')).toBeVisible();
  await expect(productionExcelDialog.getByRole('button', { name: 'Preview' })).toBeVisible();
  await productionExcelDialog.getByRole('button', { name: '닫기' }).click();
  await expect(productionExcelDialog).toHaveCount(0);
  await page.getByPlaceholder('프로젝트명, 고객사, Code, Item 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  const productionTable = page.getByRole('table', { name: '생산계획 프로젝트 목록' });
  await expect(productionTable).toContainText(projectTitle);
  await productionTable.locator('.production-project-row').filter({ hasText: projectTitle }).click();
  const expandedPlan = page.getByLabel('선택 프로젝트 생산계획');
  await expect(expandedPlan.getByRole('button', { name: '프로젝트 상세에서 보기' })).toBeVisible();
  await expect(expandedPlan.getByRole('button', { name: '생산계획 수정' })).toBeVisible();
  await expect(expandedPlan).toContainText('Item');
  await expect(expandedPlan).toContainText('계획 상태');
  await expect(expandedPlan).not.toContainText('알림 기준');
  await expect(expandedPlan).not.toContainText('fallback');
  await expect(expandedPlan.getByLabel('품질 담당자')).toContainText('IQC 수입검사');
  await expect(expandedPlan.getByLabel('품질 담당자')).toContainText('전진검수/FAT');
  await expect(expandedPlan).toContainText('자재 입고');
  await expect(expandedPlan.getByRole('table', { name: '생산계획 캘린더 표' })).toHaveCount(0);

  await expandedPlan.getByRole('button', { name: '생산계획 수정' }).click();
  await expect(page.getByTestId('project-context-summary')).toContainText(projectTitle);
  await expect(page.getByRole('heading', { name: '생산계획 수정' })).toBeVisible();
  await expect(page.getByText('부서별 담당자')).toBeVisible();
  await expect(page.getByText('품질 검사 담당자')).toBeVisible();
  const assigneeEditSection = page.locator('section.subsection').filter({ has: page.getByRole('heading', { name: '프로젝트 담당자 지정' }) });
  await expect(assigneeEditSection).not.toContainText('비고');
  await expect(assigneeEditSection).not.toContainText('fallback');
  await expect(assigneeEditSection).not.toContainText('알림 기준');
  await expect(page.getByRole('heading', { name: '영업' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '설계' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '생산관리' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '구매' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '자재' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '제조' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '물류' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'IQC 수입검사' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'LQC' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'OQC 자체검수' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '전진검수/FAT' })).toBeVisible();
  await expect(page.getByLabel('영업 담당자 지정')).toHaveAttribute('data-tone', 'sales');
  await expect(page.getByLabel('품질 검사 담당자').locator('[data-tone="quality"]')).toHaveCount(4);
  const planEditTable = page.getByRole('table', { name: '생산계획 수정' });
  await expect(planEditTable.locator('input[name="items[0].stepName"]')).toHaveValue('자재 입고');
  await expect(planEditTable).not.toContainText('No');
  await expect(page.getByRole('button', { name: 'Excel 양식 다운로드' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toBeVisible();
  await page.getByRole('button', { name: 'Excel 업로드' }).click();
  const projectProductionExcelDialog = page.getByRole('dialog', { name: '생산계획 Excel 업로드' });
  await expect(projectProductionExcelDialog).toContainText(`현재 프로젝트: ${projectTitle}`);
  await expect(projectProductionExcelDialog.locator('input[type="file"]')).toBeVisible();
  await expect(projectProductionExcelDialog.getByRole('button', { name: 'Preview' })).toBeVisible();
  await projectProductionExcelDialog.getByRole('button', { name: '닫기' }).click();
  await expect(projectProductionExcelDialog).toHaveCount(0);
  await planEditTable.locator('input[type="date"]').nth(0).fill('2026-07-01');
  await planEditTable.locator('input[type="date"]').nth(1).fill('2026-07-15');
  await planEditTable.locator('input[type="date"]').nth(2).fill('2026-07-31');
  await planEditTable.locator('input[type="date"]').nth(3).fill('2026-08-15');
  await page.getByLabel('구매 정').selectOption('50000000-0000-0000-0000-000000000011');
  await page.getByLabel('생산관리 정').selectOption('50000000-0000-0000-0000-000000000003');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  await expect(page.getByRole('tab', { name: '생산관리' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByText('계획 완료').first()).toBeVisible();
  const planItemsTable = page.getByRole('table', { name: '생산계획 항목' });
  await expect(planItemsTable).not.toContainText('No');
  const calendarTable = page.getByRole('table', { name: '생산계획 캘린더 표' });
  await expect(calendarTable.getByRole('columnheader', { name: /^7\/3\b/ })).toHaveClass(/calendar-red-day/);
  await expect(page.getByText('공식 대체공휴일')).toBeVisible();
  const calendarWrap = page.locator('.production-calendar-table-wrap').first();
  const stageHeader = calendarTable.locator('thead th.production-calendar-stage-cell');
  const stageCell = calendarTable.locator('tbody th.production-calendar-stage-cell').first();
  const dateCell = calendarTable.locator('thead th.production-calendar-date-cell').nth(10);
  const beforeSticky = {
    header: await stageHeader.boundingBox(),
    cell: await stageCell.boundingBox(),
    date: await dateCell.boundingBox()
  };
  await calendarWrap.evaluate((element) => {
    element.scrollLeft = 520;
  });
  await page.waitForTimeout(100);
  const afterSticky = {
    header: await stageHeader.boundingBox(),
    cell: await stageCell.boundingBox(),
    date: await dateCell.boundingBox()
  };
  expect(beforeSticky.header).not.toBeNull();
  expect(beforeSticky.cell).not.toBeNull();
  expect(beforeSticky.date).not.toBeNull();
  expect(afterSticky.header).not.toBeNull();
  expect(afterSticky.cell).not.toBeNull();
  expect(afterSticky.date).not.toBeNull();
  expect(Math.abs(afterSticky.header!.x - beforeSticky.header!.x)).toBeLessThanOrEqual(2);
  expect(Math.abs(afterSticky.cell!.x - beforeSticky.cell!.x)).toBeLessThanOrEqual(2);
  expect(afterSticky.date!.x).toBeLessThan(beforeSticky.date!.x - 100);
  const calendarWidths = await calendarTable.evaluate((table) => {
    const dateHeaders = Array.from(table.querySelectorAll('thead th.production-calendar-date-cell')).slice(0, 8);
    return {
      stageHeaderWidth: table.querySelector('thead th.production-calendar-stage-cell')?.getBoundingClientRect().width ?? 0,
      stageCellWidth: table.querySelector('tbody th.production-calendar-stage-cell')?.getBoundingClientRect().width ?? 0,
      dateWidths: dateHeaders.map((cell) => Math.round(cell.getBoundingClientRect().width)),
      pageOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
    };
  });
  expect(Math.abs(calendarWidths.stageHeaderWidth - calendarWidths.stageCellWidth)).toBeLessThanOrEqual(1);
  expect(new Set(calendarWidths.dateWidths).size).toBe(1);
  expect(calendarWidths.pageOverflow).toBeFalsy();
  await expect(page.getByLabel('담당자 지정 현황')).toContainText('Dev Procurement User');
  await expect(page.getByLabel('영업 담당자')).toContainText('정');
  await expect(page.getByLabel('영업 담당자')).toContainText('부');
  expect(await queryDatabaseValue(`select count(*)::text from project_production_plan_items where production_plan_id = (select id from project_production_plans where project_id = '${projectId}') and planned_date is not null;`)).toBe('4');

  const salesForbidden = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/production-planning`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      productTypeId: null,
      expectedRowVersion: 0,
      notes: null,
      reason: null,
      items: [],
      assignees: []
    }
  });
  expect(salesForbidden.status()).toBe(403);

  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await page.goto('/production-planning');
  await expect(page.getByLabel('생산계획 요약')).toContainText('생산계획 미등록');
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Excel 양식 다운로드' })).toHaveCount(0);
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await openProject(page, projectTitle);
  await page.getByRole('tab', { name: '생산관리' }).click();
  await expect(page.getByRole('button', { name: '생산계획 수정' })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: '전체 이력' })).toBeVisible();
  await expect(page.getByText('생산계획 · 대상').first()).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByLabel('개발 사용자').selectOption('dev-production');
  await page.goto('/production-planning');
  await page.getByPlaceholder('프로젝트명, 고객사, Code, Item 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  await expect(page.locator('.production-planning-mobile .procurement-project-card').filter({ hasText: projectTitle })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBeTruthy();
});

test('TASK-006A UAT regression: my-work data, procurement settings route, and workflow order are wired', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 006A UAT Regression ${unique}`;
  const projectCode = `FS-6A-UAT-${unique}`;
  const projectId = await createProjectByApi(request, projectCode, projectTitle, 'WoodenCrate', 1);
  await queryDatabaseValue(`
    insert into project_assignees (project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc)
    values ('${projectId}', 'SalesPrimary', '${salesOwnerId}', '${salesOwnerId}', now())
    on conflict (project_id, responsibility_type) do update
    set assigned_user_id = excluded.assigned_user_id,
        assigned_by_user_id = excluded.assigned_by_user_id,
        assigned_at_utc = excluded.assigned_at_utc;
    select 'ok';
  `);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await page.getByRole('navigation', { name: '공통 메뉴' }).first().getByRole('button', { name: '내 업무' }).click();
  await expect(page.getByRole('heading', { name: '내 업무' })).toBeVisible();
  await expect
    .poll(
      async () =>
        page
          .locator('.workflow-kpi-grid .dashboard-kpi-card strong')
          .evaluateAll((elements) => elements.map((element) => element.textContent?.trim() ?? '')),
      { timeout: 15_000 },
    )
    .not.toContain('-');
  const kpiValues = await page.locator('.workflow-kpi-grid .dashboard-kpi-card strong').evaluateAll((elements) => elements.map((element) => element.textContent?.trim() ?? ''));
  expect(kpiValues).not.toContain('-');
  expect(kpiValues.every((value) => /^\d+$/.test(value))).toBeTruthy();
  await expect(page.locator('.workflow-kpi-grid')).toContainText('담당 프로젝트');

  await page.getByRole('button', { name: '담당 프로젝트' }).click();
  await expect(page.getByText('대상을 찾을 수 없습니다.')).toHaveCount(0);
  await expect(page.locator('.workflow-project-group').filter({ hasText: projectTitle })).toBeVisible();
  await expect(page.locator('.workflow-project-group').filter({ hasText: projectTitle })).toContainText('영업 정');

  await page.goto(`/projects/${projectId}`);
  const workflowStages = page.locator('.workflow-stage-item');
  await expect(workflowStages.nth(0)).toContainText('영업 / 프로젝트 생성');
  await expect(workflowStages.nth(1)).toContainText('생산관리 / 생산계획·담당자');
  await expect(workflowStages.nth(2)).toContainText('설계');
  await expect(workflowStages.nth(2)).not.toContainText('설계 / 패널명·사이즈');

  const planResponse = await request.get(`${apiBaseUrl}/api/projects/${projectId}/production-planning`, {
    headers: { 'X-Dev-User': 'dev-production' }
  });
  expect(planResponse.ok()).toBeTruthy();
  const plan = await planResponse.json() as ProductionPlanningResponse;
  const saveAssignees = async () => {
    const currentResponse = await request.get(`${apiBaseUrl}/api/projects/${projectId}/production-planning`, {
      headers: { 'X-Dev-User': 'dev-production' }
    });
    const current = await currentResponse.json() as ProductionPlanningResponse;
    const designAssignee = current.assignees.find((item) => item.responsibilityType === 'DesignPrimary');
    const procurementAssignee = current.assignees.find((item) => item.responsibilityType === 'ProcurementPrimary');
    const patch = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/production-planning`, {
      headers: { 'X-Dev-User': 'dev-production' },
      data: {
        productTypeId: current.productTypeId,
        expectedRowVersion: current.rowVersion,
        notes: current.notes,
        reason: 'E2E 확장 담당자 지정',
        items: current.items.map((item) => ({
          itemId: item.itemId,
          templateStepId: item.templateStepId,
          stepName: item.stepName,
          sequenceNumber: item.sequenceNumber,
          isRequired: item.isRequired,
          expectedRowVersion: item.rowVersion,
          plannedDate: item.plannedDate,
          note: item.note,
          isDeleted: false
        })),
        assignees: [
          {
            responsibilityType: 'DesignPrimary',
            assigneeId: designAssignee?.assigneeId ?? null,
            expectedRowVersion: designAssignee?.rowVersion ?? 0,
            assignedUserId: '50000000-0000-0000-0000-000000000010',
            note: 'E2E 설계'
          },
          {
            responsibilityType: 'ProcurementPrimary',
            assigneeId: procurementAssignee?.assigneeId ?? null,
            expectedRowVersion: procurementAssignee?.rowVersion ?? 0,
            assignedUserId: '50000000-0000-0000-0000-000000000011',
            note: 'E2E 구매'
          }
        ]
      }
    });
    expect(patch.ok()).toBeTruthy();
  };
  expect(plan.assignees.map((item) => item.responsibilityType)).toContain('DesignPrimary');
  await saveAssignees();
  const workCountAfterFirstSave = await queryDatabaseValue(`select count(*)::text from work_items where project_id = '${projectId}' and workflow_stage_code in ('DesignPanelInfo','ProcurementInfo');`);
  await saveAssignees();
  expect(await queryDatabaseValue(`select count(*)::text from work_items where project_id = '${projectId}' and workflow_stage_code in ('DesignPanelInfo','ProcurementInfo');`)).toBe(workCountAfterFirstSave);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await page.getByRole('navigation', { name: '공통 메뉴' }).first().getByRole('button', { name: '내 업무' }).click();
  await expect(page.locator('.workflow-project-group').filter({ hasText: projectTitle })).toContainText('패널명, 사이즈 입력');

  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await page.getByRole('navigation', { name: '공통 메뉴' }).first().getByRole('button', { name: '내 업무' }).click();
  await expect(page.locator('.workflow-project-group').filter({ hasText: projectTitle })).toContainText('구매정보 입력');

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-procurement');
  await page.getByRole('navigation', { name: '공통 메뉴' }).first().getByRole('button', { name: '구매' }).click();
  await expect(page.getByRole('heading', { name: '구매' })).toBeVisible();
  await page.getByRole('button', { name: '구매 필수 항목 설정' }).click();
  await expect(page.getByText('대상을 찾을 수 없습니다.')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: '구매 필수 항목 설정' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'UL67' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'TEST-TYPE' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '행 추가' })).toBeVisible();

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await expect(page.getByLabel('Item*').locator('option')).toHaveText(['Item 선택', 'UL67', 'UL891', 'UL508A', 'IEC', 'LLP', 'RPP']);

  await page.getByLabel('개발 사용자').selectOption('dev-production');
  await page.getByRole('navigation', { name: '공통 메뉴' }).first().getByRole('button', { name: '생산관리' }).click();
  await page.getByRole('button', { name: '생산계획 단계 설정' }).click();
  await expect(page.getByRole('tab', { name: 'TEST-TYPE' })).toHaveCount(0);
});

test('TASK-003B D: unauthorized and held projects block panel information writes', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Guard ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-D-${unique}`, projectTitle, 'StretchWrap', 1);
  const panel = await readPanelInformation(request, projectId);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await openProject(page, projectTitle);
  await expect(page.getByRole('heading', { name: '설계' })).toBeVisible();
  await expect(page.getByRole('button', { name: '패널명·사이즈 수정' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toHaveCount(0);
  const forbidden = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, {
    headers: { 'X-Dev-User': 'dev-manufacturing' },
    data: {
      panels: [{
        panelId: panel.panels[0].panelId,
        expectedPanelInfoVersion: panel.panels[0].panelInfoVersion,
        panelNameUpdate: { isChanged: true, value: 'BLOCKED' }
      }]
    }
  });
  expect(forbidden.status()).toBe(403);

  const hold = await request.post(`${apiBaseUrl}/api/projects/${projectId}/hold`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: { reason: 'E2E hold' }
  });
  expect(hold.ok()).toBeTruthy();
  const blockedByStatus = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, {
    headers: { 'X-Dev-User': 'dev-design' },
    data: {
      panels: [{
        panelId: panel.panels[0].panelId,
        expectedPanelInfoVersion: panel.panels[0].panelInfoVersion,
        panelNameUpdate: { isChanged: true, value: 'ON-HOLD' }
      }]
    }
  });
  expect(blockedByStatus.status()).toBe(409);
});

test('TASK-003B-1 D: admin grouped history summarizes one direct bulk save', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Grouped History ${unique}`;
  await createProjectByApi(request, `FS-3B-G-${unique}`, projectTitle, 'WoodenCrate', 2);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  await page.getByLabel('No.1 패널명').fill('GROUP-1');
  await page.getByLabel('No.1 W').fill('800');
  await page.getByLabel('No.1 H').fill('1800');
  await page.getByLabel('No.1 D').fill('400');
  await page.getByLabel('No.2 패널명').fill('GROUP-2');
  await page.getByLabel('No.2 W').fill('900');
  await page.getByLabel('No.2 H').fill('2200');
  await page.getByLabel('No.2 D').fill('300');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  await expect(page.getByRole('table', { name: '설계' })).toContainText('GROUP-1');

  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await openProject(page, projectTitle);
  await expect(page.getByRole('heading', { name: '전체 이력' })).toBeVisible();
  await expect(page.getByText('직접 입력 · 대상 패널 2면').first()).toBeVisible();
  await expect(page.getByText('변경항목 8건').first()).toBeVisible();
  await page.getByText('변경 상세').first().click();
  await expect(page.getByText('No.1 · GROUP-1').first()).toBeVisible();
  await expect(page.getByText('No.2 · GROUP-2').first()).toBeVisible();
  await expect(page.getByText('WidthMm: → 800').first()).toBeVisible();
});

test('TASK-003B-1 C: partial Excel preview/apply skips blank rows and admin sees grouped audit', async ({ page, request }, testInfo) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Partial Excel ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-C-${unique}`, projectTitle, 'WoodenCrate', 3);

  const workbookPath = testInfo.outputPath('panel-information-change.xlsx');
  writePanelInformationWorkbook(workbookPath, [
    ['1', '10001', 'PNL-1', '800', '1800', '400'],
    ['2', '', '', '', '', ''],
    ['3', '10003', 'PNL-3', '', '', '']
  ]);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  await expect(page.locator('.panel-info-table-head')).toHaveCSS('position', 'sticky');
  await page.getByRole('button', { name: 'Excel 업로드' }).click();
  const excelDialog = page.getByRole('dialog', { name: 'Excel 업로드' });
  await page.locator('input[type="file"]').setInputFiles(workbookPath);
  await page.getByLabel('파일 단위').selectOption('Mm');
  await page.getByRole('button', { name: 'Preview' }).click();
  await expect(excelDialog.locator('.excel-preview-action-bar')).toHaveCSS('position', 'sticky');
  await expect(excelDialog.getByRole('button', { name: 'Excel 저장' })).toBeVisible();
  expect(await excelDialog.getByText('New').count()).toBeGreaterThanOrEqual(2);
  const desktopPreview = excelDialog.locator('[data-testid="excel-preview-desktop"]');
  await expect(desktopPreview.getByText('Skipped')).toBeVisible();
  await expect(desktopPreview.getByText('PNL-1')).toBeVisible();
  await expect(desktopPreview.getByText('PNL-3')).toBeVisible();
  await excelDialog.getByRole('button', { name: 'Excel 저장' }).click();
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toBeVisible();
  expect(await queryDatabaseValue(`select count(*)::text from panel_information_excel_import_batches where project_id = '${projectId}';`)).toBe('1');
  expect(await queryDatabaseValue(`select panel_name from panel_placeholders where project_id = '${projectId}' and sequence_number = 2;`)).toBe('');
  expect(await queryDatabaseValue(`select panel_name from panel_placeholders where project_id = '${projectId}' and sequence_number = 3;`)).toBe('PNL-3');
  expect(await queryDatabaseValue(`select width_mm::text from panel_placeholders where project_id = '${projectId}' and sequence_number = 1;`)).toBe('800.000');
  expect(await queryDatabaseValue(`select skipped_panel_count::text from panel_information_excel_import_batches where project_id = '${projectId}';`)).toBe('1');
  expect(await queryDatabaseValue(`select count(distinct import_batch_id)::text from project_audit_events where project_id = '${projectId}' and input_source = 'Excel';`)).toBe('1');

  const partialTable = page.getByRole('table', { name: '설계' });
  await expect(partialTable).toContainText('PNL-1');
  await expect(partialTable).toContainText('미입력');
  await expect(partialTable).toContainText('PNL-3');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await openProject(page, projectTitle);
  await expect(page.getByRole('heading', { name: '전체 이력' })).toBeVisible();
  await expect(page.getByText('Excel 입력 · 대상 패널 2면').first()).toBeVisible();
  await expect(page.getByText('입력 파일: panel-information-change.xlsx').first()).toBeVisible();
  await expect(page.getByText('변경항목 5건').first()).toBeVisible();
});

test('full-stack: project registration, permissions, status, and panel count use the real backend and PostgreSQL', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003A Full Stack ${unique}`;

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();

  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, `FS-${unique}`, projectTitle, '4');
  await page.getByRole('button', { name: '등록' }).click();

  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
  await expect(page.locator('dd').filter({ hasText: /^목포장$/ })).toBeVisible();
  await expect(page.getByRole('table', { name: '설계' })).toContainText('제조 전');
  await expect(page.getByRole('table', { name: '설계' })).toContainText('미입력');
  await expect(page.getByText('KRW 1,250,000.5')).toBeVisible();

  const projectId = await findProjectId(projectTitle);
  expect(Number(await queryDatabaseValue(`select count(*) from panel_placeholders where project_id = '${projectId}' and status = 'Active';`))).toBe(4);

  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: 'Excel 양식 다운로드' })).toHaveCount(0);
  await page.getByRole('button', { name: '패널명·사이즈 수정' }).click();
  const [mmTemplateDownload] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: 'Excel 양식 다운로드' }).click()
  ]);
  expect(mmTemplateDownload.suggestedFilename()).toMatch(/_Panel_Information_mm\.xlsx$/);
  await page.getByLabel('입력 단위').selectOption('Inch');
  const [inchTemplateDownload] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: 'Excel 양식 다운로드' }).click()
  ]);
  expect(inchTemplateDownload.suggestedFilename()).toMatch(/_Panel_Information_inch\.xlsx$/);

  await page.reload();
  await page.getByRole('button', { name: '상세' }).click();
  await expect(page.getByRole('table', { name: '설계' })).toContainText('4');

  await page.getByRole('button', { name: '목록' }).click();
  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, `FS-DUP-${unique}`, ` task   003a full stack ${unique} `, '2');
  await page.getByRole('button', { name: '등록' }).click();
  await expect(page.getByText('동일한 PJT Title이 이미 존재합니다.')).toBeVisible();

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await openProject(page, projectTitle);
  await expect(page.getByRole('table', { name: '설계' })).toContainText('미입력');
  await expect(page.getByText('KRW 1,250,000.5')).toHaveCount(0);
  await expect(page.getByRole('button', { name: '수정', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '보류' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '패널명·사이즈 수정' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Excel 양식 다운로드' })).toHaveCount(0);
  const manufacturingTemplateDownload = await request.get(`${apiBaseUrl}/api/projects/${projectId}/panel-information/import/template?unit=mm`, {
    headers: { 'X-Dev-User': 'dev-manufacturing' }
  });
  expect(manufacturingTemplateDownload.status()).toBe(403);
  const manufacturingDetail = await request.get(`${apiBaseUrl}/api/projects/${projectId}`, {
    headers: { 'X-Dev-User': 'dev-manufacturing' }
  });
  expect(manufacturingDetail.ok()).toBeTruthy();
  const manufacturingJson = await manufacturingDetail.json() as Record<string, unknown>;
  expect(manufacturingJson).not.toHaveProperty('salesAmount');
  expect(manufacturingJson).not.toHaveProperty('currencyCode');

  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await openProject(page, projectTitle);
  await expect(page.getByText('KRW 1,250,000.5')).toBeVisible();
  await expect(page.getByRole('button', { name: '수정', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '패널명·사이즈 수정' })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: '전체 이력' })).toBeVisible();
  await expect(page.getByRole('button', { name: '보류' })).toHaveCount(0);
  const adminWrite = await request.post(`${apiBaseUrl}/api/projects/${projectId}/hold`, {
    headers: { 'X-Dev-User': 'dev-admin' },
    data: { reason: '관리자 직접 쓰기 차단' }
  });
  expect(adminWrite.status()).toBe(403);

  await page.getByLabel('개발 사용자').selectOption('dev-sales');
  await openProject(page, projectTitle);
  await page.getByRole('button', { name: '보류' }).click();
  await page.getByLabel('사유*').fill('Full-stack 보류');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: '보류' })).toBeVisible();
  expect(await queryDatabaseValue(`select status from projects where id = '${projectId}';`)).toBe('OnHold');

  await page.getByRole('button', { name: '보류 해제' }).click();
  await page.getByLabel('사유*').fill('Full-stack 보류 해제');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: '진행' })).toBeVisible();

  await page.getByRole('button', { name: '수정', exact: true }).click();
  const panelCountInput = page.getByLabel('면수*');
  await expect(panelCountInput).toHaveValue('4');
  await panelCountInput.fill('6');
  await expect(panelCountInput).toHaveValue('6');
  await page.getByLabel('수정사유*').fill('Full-stack 면수 증가');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('table', { name: '설계' })).toContainText('5');
  await expect(page.getByRole('table', { name: '설계' })).toContainText('6');
  expect(Number(await queryDatabaseValue(`select count(*) from panel_placeholders where project_id = '${projectId}' and status = 'Active';`))).toBe(6);

  const stalePanelChange = await request.post(`${apiBaseUrl}/api/projects/${projectId}/change-panel-count`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      panelCount: 7,
      expectedActivePanelCount: 4,
      cancelPanelIds: [],
      reason: '오래된 화면 기준 요청'
    }
  });
  expect(stalePanelChange.status()).toBe(409);

  await page.getByRole('button', { name: '취소' }).click();
  await page.getByLabel('사유*').fill('Full-stack 취소');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: '취소' })).toBeVisible();

  await page.getByRole('button', { name: '목록' }).click();
  await page.getByRole('tab', { name: '취소' }).click();
  await openProject(page, projectTitle);

  await page.getByRole('button', { name: '재활성' }).click();
  await page.getByLabel('사유*').fill('Full-stack 재활성');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: '진행' })).toBeVisible();

  await page.getByRole('button', { name: '삭제' }).click();
  await page.getByLabel('삭제 사유*').fill('Full-stack 오등록 정리');
  await page.getByLabel('PJT Title 확인 입력*').fill(projectTitle);
  await page.getByRole('dialog', { name: '프로젝트 삭제' }).getByRole('button', { name: '삭제', exact: true }).click();

  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  await page.getByRole('tab', { name: '삭제 보관함' }).click();
  await openProject(page, projectTitle);
  await expect(page.locator('dd').filter({ hasText: /^Full-stack 오등록 정리$/ })).toBeVisible();
  expect(await queryDatabaseValue(`select count(*)::text from projects where id = '${projectId}' and deleted_at_utc is not null;`)).toBe('1');

  await page.getByRole('button', { name: '목록' }).click();
  await page.getByRole('tab', { name: '진행' }).click();
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, `FS-REUSE-${unique}`, projectTitle, '1');
  await page.getByRole('button', { name: '등록' }).click();
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();

  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  await expect(page.getByRole('tab', { name: '삭제 보관함' })).toBeVisible();
  await page.getByRole('tab', { name: '삭제 보관함' }).click();
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: '삭제', exact: true })).toHaveCount(0);

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  await expect(page.getByRole('tab', { name: '삭제 보관함' })).toHaveCount(0);
  const manufacturingDelete = await request.post(`${apiBaseUrl}/api/projects/${projectId}/delete`, {
    headers: { 'X-Dev-User': 'dev-manufacturing' },
    data: { reason: '제조 삭제 차단', confirmProjectTitle: projectTitle }
  });
  expect(manufacturingDelete.status()).toBe(403);
});

async function fillProjectForm(page: Page, projectCode: string, projectTitle: string, panelCount: string) {
  await page.getByLabel('고객사*').fill('EMI Full Stack Customer');
  await page.getByLabel('Item*').selectOption('UL67');
  await page.getByLabel('PJT Code*').fill(projectCode);
  await page.getByLabel('PJT Title*').fill(projectTitle);
  await page.getByLabel('면수*').fill(panelCount);
  await page.getByLabel('납기일*').fill('2026-10-10');
  await page.getByLabel('영업담당자*').selectOption(salesOwnerId);
  await page.getByLabel('포장방식*').selectOption('WoodenCrate');
  await page.getByLabel('판매금액').fill('1250000.5');
}

async function createProjectByApi(
  request: APIRequestContext,
  projectCode: string,
  projectTitle: string,
  packagingMethod: 'WoodenCrate' | 'StretchWrap' | 'HeavyDutyBox',
  panelCount: number
) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      customerName: 'EMI Full Stack Customer',
      item: 'UL67',
      projectCode,
      projectTitle,
      panelCount,
      deliveryDate: '2026-10-10',
      salesOwnerUserId: salesOwnerId,
      packagingMethod,
      salesAmount: null,
      currencyCode: null,
      deliveryLocation: null
    }
  });
  expect(response.status()).toBe(201);
  const json = await response.json() as { projectId: string };
  return json.projectId;
}

async function readPanelInformation(request: APIRequestContext, projectId: string) {
  const response = await request.get(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, {
    headers: { 'X-Dev-User': 'dev-design' }
  });
  expect(response.ok()).toBeTruthy();
  return await response.json() as {
    panels: Array<{
      panelId: string;
      panelInfoVersion: number;
      panelInfoCompleted: boolean;
      qrEligible: boolean;
    }>;
  };
}

async function openProject(page: Page, projectTitle: string) {
  await page.getByRole('heading', { name: '프로젝트 목록' }).waitFor();
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(projectTitle);
  await page.getByRole('button', { name: '검색' }).click();
  const projectEntry = page.locator('.project-list-row, .project-list-card').filter({ hasText: projectTitle });
  await expect(projectEntry).toBeVisible();
  const desktopRow = page.locator('.project-list-row').filter({ hasText: projectTitle });
  if (await desktopRow.count() > 0) {
    await desktopRow.click();
  } else {
    await page.locator('.project-list-card').filter({ hasText: projectTitle }).getByRole('button', { name: '상세 보기' }).click();
  }
  await expect(page.getByRole('heading', { name: projectTitle })).toBeVisible();
}

async function findProjectId(projectTitle: string) {
  const normalizedTitle = projectTitle.trim().replace(/\s+/g, ' ').toUpperCase();
  return queryDatabaseValue(`select id::text from projects where project_title_normalized = '${normalizedTitle.replaceAll("'", "''")}';`);
}

function queryDatabaseValue(sql: string) {
  const databaseName = process.env.E2E_DATABASE_NAME ?? 'emi_qms_e2e';
  const databaseHost = process.env.DATABASE_HOST ?? 'localhost';
  const databasePort = process.env.DATABASE_PORT ?? '5432';
  const databaseUser = process.env.DATABASE_USER ?? 'emi_qms';
  const databasePassword = requireEnv('DATABASE_PASSWORD');

  if (commandExists('psql')) {
    return execFileSync(
      'psql',
      [
        '--host',
        databaseHost,
        '--port',
        databasePort,
        '--username',
        databaseUser,
        '--dbname',
        databaseName,
        '--no-psqlrc',
        '--tuples-only',
        '--no-align',
        '--set',
        'ON_ERROR_STOP=1',
        '--command',
        sql
      ],
      {
        encoding: 'utf8',
        env: { ...process.env, PGPASSWORD: databasePassword }
      }
    ).trim();
  }

  return execFileSync(
    'docker',
    [
      'compose',
      '--env-file',
      `${repositoryRoot()}/.env`,
      '-f',
      `${repositoryRoot()}/infrastructure/docker-compose.yml`,
      'exec',
      '-T',
      postgresComposeService(),
      'psql',
      '--username',
      databaseUser,
      '--dbname',
      databaseName,
      '--no-psqlrc',
      '--tuples-only',
      '--no-align',
      '--set',
      'ON_ERROR_STOP=1',
      '--command',
      sql
    ],
    { encoding: 'utf8' }
  ).trim();
}

function writePanelInformationWorkbook(filePath: string, rows: string[][]) {
  const rowXml = [
    ['No', '도번', 'panel name', 'w', 'h', 'd'],
    ...rows
  ].map((row, rowIndex) => {
    const cells = row.map((value, columnIndex) => {
      const reference = `${String.fromCharCode('A'.charCodeAt(0) + columnIndex)}${rowIndex + 1}`;
      return `<c r="${reference}" t="inlineStr"><is><t>${escapeXml(value)}</t></is></c>`;
    }).join('');
    return `<row r="${rowIndex + 1}">${cells}</row>`;
  }).join('');
  const script = `
import sys, zipfile
path = sys.argv[1]
sheet = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${rowXml}</sheetData></worksheet>'''
with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
    z.writestr('[Content_Types].xml', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>''')
    z.writestr('_rels/.rels', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>''')
    z.writestr('xl/workbook.xml', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Panel Information" sheetId="1" r:id="rId1"/></sheets></workbook>''')
    z.writestr('xl/_rels/workbook.xml.rels', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>''')
    z.writestr('xl/worksheets/sheet1.xml', sheet)
`;
  execFileSync('python3', ['-c', script, filePath]);
}

function writeProcurementWorkbook(filePath: string, rows: string[][]) {
  const allRows = [
    ['PS 사업부 PJT 발주 관리', '', '', '', '', '', '', '', '', ''],
    ['', '', '', '', '', '', '', '', '', ''],
    ['PJT', 'PJT CODE', '통상납기', '발주품목', '업체', '기술 담당자', '발주일', '입고예정일', '이슈사항', '입고 완료'],
    ...rows
  ];
  const rowXml = allRows.map((row, rowIndex) => {
    const cells = row.map((value, columnIndex) => {
      const reference = `${String.fromCharCode('A'.charCodeAt(0) + columnIndex)}${rowIndex + 1}`;
      return `<c r="${reference}" t="inlineStr"><is><t>${escapeXml(value)}</t></is></c>`;
    }).join('');
    return `<row r="${rowIndex + 1}">${cells}</row>`;
  }).join('');
  const script = `
import sys, zipfile
path = sys.argv[1]
sheet = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${rowXml}</sheetData></worksheet>'''
with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
    z.writestr('[Content_Types].xml', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>''')
    z.writestr('_rels/.rels', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>''')
    z.writestr('xl/workbook.xml', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Procurement Plan" sheetId="1" r:id="rId1"/></sheets></workbook>''')
    z.writestr('xl/_rels/workbook.xml.rels', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>''')
    z.writestr('xl/worksheets/sheet1.xml', sheet)
`;
  execFileSync('python3', ['-c', script, filePath]);
}

function writeProjectCreateWorkbook(filePath: string, rows: string[][]) {
  const allRows = [
    ['프로젝트 Excel 등록', '', '', '', '', '', '', '', '', '', '', ''],
    ['', '', '', '', '', '', '', '', '', '', '', ''],
    ['고객사', 'Item', 'PJT Code', '프로젝트명', '면수', '납기일', '포장방식', 'FAT 필요 여부', '영업담당자', '판매금액', '통화', '납품장소'],
    ...rows
  ];
  const rowXml = allRows.map((row, rowIndex) => {
    const cells = row.map((value, columnIndex) => {
      const reference = `${String.fromCharCode('A'.charCodeAt(0) + columnIndex)}${rowIndex + 1}`;
      return `<c r="${reference}" t="inlineStr"><is><t>${escapeXml(value)}</t></is></c>`;
    }).join('');
    return `<row r="${rowIndex + 1}">${cells}</row>`;
  }).join('');
  const script = `
import sys, zipfile
path = sys.argv[1]
sheet = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${rowXml}</sheetData></worksheet>'''
with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
    z.writestr('[Content_Types].xml', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>''')
    z.writestr('_rels/.rels', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>''')
    z.writestr('xl/workbook.xml', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Projects" sheetId="1" r:id="rId1"/></sheets></workbook>''')
    z.writestr('xl/_rels/workbook.xml.rels', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>''')
    z.writestr('xl/worksheets/sheet1.xml', sheet)
`;
  execFileSync('python3', ['-c', script, filePath]);
}

function escapeXml(value: string) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&apos;');
}

function postgresComposeService() {
  return execFileSync(
    'docker',
    [
      'compose',
      '--env-file',
      `${repositoryRoot()}/.env`,
      '-f',
      `${repositoryRoot()}/infrastructure/docker-compose.yml`,
      'config',
      '--services'
    ],
    { encoding: 'utf8' }
  )
    .split('\n')
    .find((service) => service.trim() === 'postgres') ?? 'postgres';
}

function repositoryRoot() {
  return execFileSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8' }).trim();
}

function commandExists(command: string) {
  try {
    execFileSync('bash', ['-lc', `command -v ${command}`], { stdio: 'ignore' });
    return true;
  } catch {
    return false;
  }
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is required for full-stack E2E.`);
  }

  return value;
}
