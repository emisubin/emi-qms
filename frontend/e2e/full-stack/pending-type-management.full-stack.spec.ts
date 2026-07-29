import { expect, test, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/pending-type-001-screenshots');

test('TASK-PENDING-TYPE-001: administrator UI and adaptive read-only mobile cards', async ({ page }) => {
  const unique = String(Date.now()).slice(-6);
  const displayName = `설계 변경 ${unique}`;

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await page.goto('/admin/pending-types');
  await expect(page.getByRole('heading', { name: 'Pending 유형 관리' })).toBeVisible();
  await expect(page.getByText('시스템 4개 포함')).toBeVisible();
  await capture(page, '01-pending-type-management-desktop-1440.png');

  await page.getByRole('button', { name: '+ 사용자 유형 추가' }).click();
  const dialog = page.getByRole('dialog', { name: '사용자 유형 추가' });
  await expect(dialog).toBeVisible();
  await dialog.getByLabel('표시명 *').fill(displayName);
  await dialog.getByLabel('설명').fill('도면 승인 후 현장 반영이 필요한 변경 사항');
  await capture(page, '02-pending-type-create-dialog-desktop-1440.png');
  await dialog.getByRole('button', { name: '유형 추가' }).click();
  await expect(page.getByText('사용자 Pending 유형이 추가되었습니다.')).toBeVisible();
  await expect(page.getByText(displayName, { exact: true })).toBeVisible();
  await capture(page, '03-pending-type-custom-created-desktop-1440.png');

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto('/pending');
  await page.getByRole('button', { name: '+ Pending 등록' }).click();
  const pendingDialog = page.getByRole('dialog', { name: 'Pending 등록' });
  await expect(pendingDialog.getByRole('option', { name: displayName })).toHaveCount(1);
  await pendingDialog.getByLabel('유형 *').selectOption({ label: displayName });
  await capture(page, '04-pending-create-dynamic-type-desktop-1440.png');
  await pendingDialog.getByRole('button', { name: '닫기' }).click();

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/admin/pending-types');
  await expect(page.getByRole('heading', { name: 'Pending 유형 관리' })).toBeVisible();
  await expect(page.getByText('모바일은 조회 전용입니다.')).toBeVisible();
  await expect(page.getByRole('button', { name: '+ 사용자 유형 추가' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '편집' })).toHaveCount(0);
  await assertNoHorizontalOverflow(page);
  await capture(page, '05-pending-type-management-mobile-390.png');
});

test('TASK-PENDING-TYPE-001: API permission, CAS, activation, options and atomic reorder', async ({ request }) => {
  const adminHeaders = { 'X-Dev-User': 'dev-admin' };
  const qualityHeaders = { 'X-Dev-User': 'dev-quality' };
  const denied = await request.get(`${apiBaseUrl}/api/pending-types`, { headers: qualityHeaders });
  expect(denied.status()).toBe(403);

  const create = await request.post(`${apiBaseUrl}/api/pending-types`, {
    headers: adminHeaders,
    data: { displayName: `현장 확인 ${Date.now()}`, description: '현장 확인이 필요한 사용자 정의 유형' }
  });
  expect(create.ok()).toBeTruthy();
  let catalog = await create.json() as CatalogResponse;
  const custom = catalog.items.find((item) => !item.isSystem && item.displayName.startsWith('현장 확인'));
  expect(custom).toBeTruthy();
  expect(custom!.code).toMatch(/^CUSTOM_[A-F0-9]{32}$/);

  const update = await request.put(`${apiBaseUrl}/api/pending-types/${custom!.code}`, {
    headers: adminHeaders,
    data: { expectedRowVersion: custom!.rowVersion, displayName: `${custom!.displayName} 수정`, description: '수정된 사용자 정의 유형', isManualEnabled: true }
  });
  expect(update.ok()).toBeTruthy();
  catalog = await update.json() as CatalogResponse;
  const updated = catalog.items.find((item) => item.code === custom!.code)!;
  expect(updated.rowVersion).toBe(custom!.rowVersion + 1);

  const projectsResponse = await request.get(`${apiBaseUrl}/api/projects?pageSize=10`, { headers: qualityHeaders });
  expect(projectsResponse.ok()).toBeTruthy();
  const projects = await projectsResponse.json() as { items: Array<{ projectId: string }> };
  expect(projects.items.length).toBeGreaterThan(0);
  const createdPending = await request.post(`${apiBaseUrl}/api/pending`, {
    headers: qualityHeaders,
    data: {
      projectId: projects.items[0].projectId,
      issueType: updated.code,
      title: '사용자 유형 표시명 검증',
      description: '목록과 선택 Excel에서 관리자 표시명이 동일하게 노출되어야 합니다.',
      priority: 'Normal'
    }
  });
  expect(createdPending.ok()).toBeTruthy();
  const pendingDetail = await createdPending.json() as { issue: { pendingId: string; issueTypeLabel: string } };
  expect(pendingDetail.issue.issueTypeLabel).toBe(updated.displayName);
  const pendingList = await request.get(`${apiBaseUrl}/api/pending?issueType=${encodeURIComponent(updated.code)}`, { headers: qualityHeaders });
  expect(pendingList.ok()).toBeTruthy();
  const pendingListBody = await pendingList.json() as { items: Array<{ pendingId: string; issueTypeLabel: string }> };
  expect(pendingListBody.items.find((item) => item.pendingId === pendingDetail.issue.pendingId)?.issueTypeLabel).toBe(updated.displayName);
  const selectedExport = await request.post(`${apiBaseUrl}/api/data-exports/selected`, {
    headers: qualityHeaders,
    data: { screen: 'pending', ids: [pendingDetail.issue.pendingId], filters: { issueType: updated.code } }
  });
  expect(selectedExport.ok()).toBeTruthy();
  const workbookPath = path.join('/tmp', `pending-type-${Date.now()}.xlsx`);
  await fs.writeFile(workbookPath, await selectedExport.body());
  const workbookXml = execFileSync('unzip', ['-p', workbookPath], { encoding: 'utf8' });
  expect(workbookXml).toContain(updated.displayName);
  await fs.rm(workbookPath, { force: true });

  const stale = await request.put(`${apiBaseUrl}/api/pending-types/${updated.code}`, {
    headers: adminHeaders,
    data: { expectedRowVersion: custom!.rowVersion, displayName: '낡은 변경', description: null, isManualEnabled: true }
  });
  expect(stale.status()).toBe(409);

  const deactivate = await request.post(`${apiBaseUrl}/api/pending-types/${updated.code}/deactivate`, {
    headers: adminHeaders,
    data: { expectedRowVersion: updated.rowVersion }
  });
  expect(deactivate.ok()).toBeTruthy();
  catalog = await deactivate.json() as CatalogResponse;
  const inactive = catalog.items.find((item) => item.code === updated.code)!;
  expect(inactive.isActive).toBe(false);

  const manual = await request.get(`${apiBaseUrl}/api/pending-types/manual-options`, { headers: qualityHeaders });
  expect(manual.ok()).toBeTruthy();
  const manualOptions = await manual.json() as Array<{ code: string }>;
  expect(manualOptions.some((item) => item.code === inactive.code)).toBe(false);
  expect(manualOptions.some((item) => item.code === 'Other')).toBe(true);

  const filter = await request.get(`${apiBaseUrl}/api/pending-types/filter-options`, { headers: qualityHeaders });
  expect(filter.ok()).toBeTruthy();
  const filterOptions = await filter.json() as Array<{ code: string; isActive: boolean }>;
  expect(filterOptions.find((item) => item.code === inactive.code)?.isActive).toBe(false);

  const reversed = [...catalog.items].reverse();
  const reorder = await request.put(`${apiBaseUrl}/api/pending-types/reorder`, {
    headers: adminHeaders,
    data: { items: reversed.map((item, index) => ({ code: item.code, expectedRowVersion: item.rowVersion, newSortOrder: index + 1 })) }
  });
  expect(reorder.ok()).toBeTruthy();
  const reordered = await reorder.json() as CatalogResponse;
  expect(reordered.items.map((item) => item.code)).toEqual(reversed.map((item) => item.code));

  const staleReorder = await request.put(`${apiBaseUrl}/api/pending-types/reorder`, {
    headers: adminHeaders,
    data: { items: reversed.map((item, index) => ({ code: item.code, expectedRowVersion: item.rowVersion, newSortOrder: index + 1 })) }
  });
  expect(staleReorder.status()).toBe(409);
  expect(Number(queryDatabase(`select count(*) from pending_issue_type_audit_events where issue_type_code='${inactive.code}';`))).toBeGreaterThanOrEqual(4);
});

type CatalogResponse = {
  items: Array<{
    code: string;
    displayName: string;
    sortOrder: number;
    isSystem: boolean;
    isActive: boolean;
    rowVersion: number;
  }>;
};

async function capture(page: Page, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled', fullPage: true });
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
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
