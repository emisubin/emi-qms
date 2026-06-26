import { execFileSync } from 'node:child_process';
import { expect, type APIRequestContext, type Page, test } from '@playwright/test';

const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const apiBaseUrl = 'http://127.0.0.1:5081';

test('TASK-003B A: direct input allows duplicate names and distinguishes panels by No', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Direct ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-A-${unique}`, projectTitle, 'StretchWrap', 2);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await page.getByLabel('No.1 패널명').fill('PNL-1');
  await page.getByLabel('No.2 패널명').fill('PNL-1');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();

  await expect(page.getByText('패널정보를 저장했습니다.')).toBeVisible();
  expect(await page.getByText('동일 명칭 2면').count()).toBeGreaterThanOrEqual(2);
  expect(await queryDatabaseValue(`select count(*)::text from panel_placeholders where project_id = '${projectId}' and panel_name = 'PNL-1' and panel_info_completed and qr_eligible;`)).toBe('2');
  await page.reload();
  await openProject(page, projectTitle);
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
  await page.getByLabel('입력 단위').selectOption('Inch');
  await page.getByLabel('No.1 패널명').fill('WOOD-1-REV');
  await page.getByLabel('수정사유*').fill('패널명만 수정');
  await page.getByRole('button', { name: '직접 입력 저장' }).click();
  await expect(page.getByText('패널정보를 저장했습니다.')).toBeVisible();

  expect(await queryDatabaseValue(`select width_mm::text || ',' || height_mm::text || ',' || depth_mm::text from panel_placeholders where project_id = '${projectId}' and sequence_number = 1;`)).toBe('800.000,1800.000,400.000');
  expect(await queryDatabaseValue(`select count(*)::text from project_audit_events where project_id = '${projectId}' and field_name in ('WidthMm','HeightMm','DepthMm') and new_value in ('799.998','800.1');`)).toBe('0');
});

test('TASK-003B D: unauthorized and held projects block panel information writes', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Guard ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-D-${unique}`, projectTitle, 'StretchWrap', 1);
  const panel = await readPanelInformation(request, projectId);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: '직접 입력 저장' })).toBeDisabled();
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

test('TASK-003B C: Excel preview/apply records linked audit metadata in the real UI', async ({ page, request }, testInfo) => {
  const unique = Date.now();
  const projectTitle = `TASK 003B Excel ${unique}`;
  const projectId = await createProjectByApi(request, `FS-3B-C-${unique}`, projectTitle, 'StretchWrap', 2);
  const panelInfo = await readPanelInformation(request, projectId);
  const seed = await request.patch(`${apiBaseUrl}/api/projects/${projectId}/panel-information`, {
    headers: { 'X-Dev-User': 'dev-design' },
    data: {
      panels: panelInfo.panels.map((panel, index) => ({
        panelId: panel.panelId,
        expectedPanelInfoVersion: panel.panelInfoVersion,
        panelNameUpdate: { isChanged: true, value: `EXCEL-${index + 1}` },
        sizeUpdate: { isChanged: true, clear: false, inputUnit: 'Mm', width: 700 + index, height: 1700 + index, depth: 300 + index }
      }))
    }
  });
  expect(seed.ok()).toBeTruthy();

  const workbookPath = testInfo.outputPath('panel-information-change.xlsx');
  writePanelInformationWorkbook(workbookPath, [
    ['1', '', 'EXCEL-1-REV', '31.5', '70.875', '15.75'],
    ['2', '', 'EXCEL-2', '27.598', '66.929', '11.85']
  ]);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
  await page.getByRole('button', { name: 'Excel 업로드' }).click();
  const excelDialog = page.getByRole('dialog', { name: 'Excel 업로드' });
  await page.locator('input[type="file"]').setInputFiles(workbookPath);
  await page.getByLabel('파일 단위').selectOption('Inch');
  await page.getByRole('button', { name: 'Preview' }).click();
  await expect(excelDialog.getByRole('button', { name: '전체 적용' })).toBeVisible();
  expect(await excelDialog.getByText('Changed').count()).toBeGreaterThanOrEqual(1);
  await expect(excelDialog.getByText('EXCEL-1-REV')).toBeVisible();
  await excelDialog.getByLabel('수정사유*').fill('Excel E2E audit');
  await excelDialog.getByRole('button', { name: '전체 적용' }).click();
  await expect(page.getByRole('button', { name: 'Excel 업로드' })).toBeVisible();
  expect(await queryDatabaseValue(`select count(*)::text from panel_information_excel_import_batches where project_id = '${projectId}';`)).toBe('1');
  await expect(page.getByText('입력 방식: Excel 입력').first()).toBeVisible();
  await expect(page.getByText('입력 파일: panel-information-change.xlsx').first()).toBeVisible();
  await expect(page.getByText('입력 단위: inch').first()).toBeVisible();
  await expect(page.getByText('입력값: 31.5 inch').first()).toBeVisible();
  await expect(page.getByText('저장값: 800.1 mm').first()).toBeVisible();
  await expect(page.getByText('수정사유: Excel E2E audit').first()).toBeVisible();
  await expect(page.getByText(/Dev Design User/).first()).toBeVisible();
  expect(await queryDatabaseValue(`select count(distinct import_batch_id)::text from project_audit_events where project_id = '${projectId}' and input_source = 'Excel';`)).toBe('1');
  await page.reload();
  await openProject(page, projectTitle);
  await expect(page.getByText('입력 방식: Excel 입력').first()).toBeVisible();
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
  await expect(page.getByRole('button', { name: /^P01 / })).toBeVisible();
  await expect(page.getByRole('button', { name: /^P04 / })).toBeVisible();
  await expect(page.getByText('KRW 1,250,000.5')).toBeVisible();

  const projectId = await findProjectId(projectTitle);
  expect(Number(await queryDatabaseValue(`select count(*) from panel_placeholders where project_id = '${projectId}' and status = 'Active';`))).toBe(4);

  await page.getByLabel('개발 사용자').selectOption('dev-design');
  await openProject(page, projectTitle);
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
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: /^P04 / })).toBeVisible();

  await page.getByRole('button', { name: '목록' }).click();
  await page.getByRole('button', { name: '신규 프로젝트' }).click();
  await fillProjectForm(page, `FS-DUP-${unique}`, ` task   003a full stack ${unique} `, '2');
  await page.getByRole('button', { name: '등록' }).click();
  await expect(page.getByText('동일한 PJT Title이 이미 존재합니다.')).toBeVisible();

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await openProject(page, projectTitle);
  await expect(page.getByRole('button', { name: /^P01 / })).toBeVisible();
  await expect(page.getByText('KRW 1,250,000.5')).toHaveCount(0);
  await expect(page.getByRole('button', { name: '수정' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '보류' })).toHaveCount(0);
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
  await expect(page.getByRole('button', { name: '수정' })).toHaveCount(0);
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
  await expect(page.locator('.status-badge', { hasText: 'OnHold' })).toBeVisible();
  expect(await queryDatabaseValue(`select status from projects where id = '${projectId}';`)).toBe('OnHold');

  await page.getByRole('button', { name: '보류 해제' }).click();
  await page.getByLabel('사유*').fill('Full-stack 보류 해제');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: 'Active' })).toBeVisible();

  await page.getByRole('button', { name: '수정' }).click();
  const panelCountInput = page.getByLabel('면수*');
  await expect(panelCountInput).toHaveValue('4');
  await panelCountInput.fill('6');
  await expect(panelCountInput).toHaveValue('6');
  await page.getByLabel('수정사유*').fill('Full-stack 면수 증가');
  await page.getByRole('button', { name: '저장' }).click();
  await expect(page.getByRole('button', { name: /^P05 / })).toBeVisible();
  await expect(page.getByRole('button', { name: /^P06 / })).toBeVisible();
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
  await expect(page.locator('.status-badge', { hasText: 'Cancelled' })).toBeVisible();

  await page.getByRole('button', { name: '목록' }).click();
  await page.getByRole('tab', { name: '취소' }).click();
  await openProject(page, projectTitle);

  await page.getByRole('button', { name: '재활성' }).click();
  await page.getByLabel('사유*').fill('Full-stack 재활성');
  await page.getByRole('button', { name: '확인' }).click();
  await expect(page.locator('.status-badge', { hasText: 'Active' })).toBeVisible();

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
  await page.getByRole('button', { name: projectTitle }).click();
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
