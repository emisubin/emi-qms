import { execFileSync } from 'node:child_process';
import { expect, type APIRequestContext, type Page, test } from '@playwright/test';

const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const apiBaseUrl = 'http://127.0.0.1:5081';

test('TASK-003B-1 A: read/detail split keeps detail fixed and edit page accepts duplicate names', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Direct ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-A-${unique}`, projectTitle, 'StretchWrap', 2);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await expect(page.getByText('제품·패널 목록')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toHaveCount(0);
  await page.getByRole('button', { name: '패널정보 수정' }).click();
  await page.getByLabel('No.1 패널명').fill('PNL-1');
  await page.getByLabel('No.2 패널명').fill('PNL-1');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();

  await expect(page.getByText('패널정보를 저장했습니다.')).toBeVisible();
  expect(await page.getByText('동일 명칭 2면').count()).toBeGreaterThanOrEqual(2);
  expect(await queryDatabaseValue(`select count(*)::text from panel_placeholders where project_id = '${projectId}' and panel_name = 'PNL-1' and panel_info_completed and qr_eligible;`)).toBe('2');
  await page.reload();
  await page.getByRole('button', { name: '상세' }).click();
  const productTable = page.getByRole('table', { name: '제품·패널 목록' });
  await expect(productTable).toContainText('PNL-1');
  await expect(productTable).toContainText('동일 명칭 2면');
  await page.getByRole('button', { name: '패널정보 수정' }).click();
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
  await page.getByRole('button', { name: '패널정보 수정' }).click();
  await page.getByLabel('입력 단위').selectOption('Inch');
  await page.getByLabel('No.1 패널명').fill('WOOD-1-REV');
  await page.getByLabel('수정사유*').fill('패널명만 수정');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  await expect(page.getByText('패널정보를 저장했습니다.')).toBeVisible();

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
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('출하 완료');
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
  await expect(header).toContainText('제품정보');
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

  await page.getByRole('button', { name: '패널정보 수정' }).click();
  const editHeader = page.locator('.panel-info-table-head');
  await expect(editHeader).toContainText('No');
  await expect(editHeader).toContainText('패널명');
  await expect(editHeader).toContainText('W');
  await expect(editHeader).toContainText('H');
  await expect(editHeader).toContainText('D');
  await expect(editHeader).toContainText('제품정보');
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

  await page.getByRole('button', { name: '패널정보 수정' }).click();
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
  await expect(projectTable).toContainText('제조 중');
  await expect(projectTable).toContainText('10%');
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
  await expect(mobileCard).toContainText('제조 중');
  await expect(mobileCard).toContainText('10%');
});

test('TASK-003B D: unauthorized and held projects block panel information writes', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Guard ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-D-${unique}`, projectTitle, 'StretchWrap', 1);
  const panel = await readPanelInformation(request, projectId);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await openProject(page, projectTitle);
  await expect(page.getByText('제품·패널 목록')).toBeVisible();
  await expect(page.getByRole('button', { name: '패널정보 수정' })).toHaveCount(0);
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
  await page.getByRole('button', { name: '패널정보 수정' }).click();
  await page.getByLabel('No.1 패널명').fill('GROUP-1');
  await page.getByLabel('No.1 W').fill('800');
  await page.getByLabel('No.1 H').fill('1800');
  await page.getByLabel('No.1 D').fill('400');
  await page.getByLabel('No.2 패널명').fill('GROUP-2');
  await page.getByLabel('No.2 W').fill('900');
  await page.getByLabel('No.2 H').fill('2200');
  await page.getByLabel('No.2 D').fill('300');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  await expect(page.getByText('패널정보를 저장했습니다.')).toBeVisible();

  await page.getByRole('button', { name: '상세' }).click();
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
  await page.getByRole('button', { name: '패널정보 수정' }).click();
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

  await page.getByRole('button', { name: '상세' }).click();
  const partialTable = page.getByRole('table', { name: '제품·패널 목록' });
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
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('제조 전');
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('미입력');
  await expect(page.getByText('KRW 1,250,000.5')).toBeVisible();

  const projectId = await findProjectId(projectTitle);
  expect(Number(await queryDatabaseValue(`select count(*) from panel_placeholders where project_id = '${projectId}' and status = 'Active';`))).toBe(4);

  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: 'Excel 양식 다운로드' })).toHaveCount(0);
  await page.getByRole('button', { name: '패널정보 수정' }).click();
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
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('4');

  await page.getByRole('button', { name: '목록' }).click();
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, `FS-DUP-${unique}`, ` task   003a full stack ${unique} `, '2');
  await page.getByRole('button', { name: '등록' }).click();
  await expect(page.getByText('동일한 PJT Title이 이미 존재합니다.')).toBeVisible();

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await openProject(page, projectTitle);
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('미입력');
  await expect(page.getByText('KRW 1,250,000.5')).toHaveCount(0);
  await expect(page.getByRole('button', { name: '수정', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '보류' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '패널정보 수정' })).toHaveCount(0);
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
  await expect(page.getByRole('button', { name: '패널정보 수정' })).toHaveCount(0);
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
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('5');
  await expect(page.getByRole('table', { name: '제품·패널 목록' })).toContainText('6');
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
  await expect(page.getByRole('button', { name: '삭제' })).toHaveCount(0);

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
  await page.getByLabel('Item*').fill('Control Panel');
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
      item: 'Control Panel',
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
