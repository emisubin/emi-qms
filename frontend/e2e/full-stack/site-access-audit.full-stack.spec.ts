import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/site-access-001-screenshots');

test('TASK-SITE-ACCESS-001: API, administrator audit UI and selected Excel stay consistent', async ({ page, request, context }) => {
  test.setTimeout(180_000);
  const browserClientId = '84000000-0000-4000-8000-000000000101';

  const anonymous = await request.post(`${apiBaseUrl}/api/audit/site-access/signals`, {
    data: { browserClientId, menuCode: 'Home' }
  });
  expect(anonymous.status()).toBe(401);

  const first = await request.post(`${apiBaseUrl}/api/audit/site-access/signals`, {
    headers: devHeaders('dev-sales'),
    data: { browserClientId, menuCode: 'Home' }
  });
  expect(first.ok()).toBeTruthy();
  const firstSession = await first.json() as SiteAccessSession;

  const next = await request.post(`${apiBaseUrl}/api/audit/site-access/signals`, {
    headers: devHeaders('dev-sales'),
    data: { browserClientId, menuCode: 'Projects' }
  });
  expect(next.ok()).toBeTruthy();
  const nextSession = await next.json() as SiteAccessSession;
  expect(nextSession.sessionId).toBe(firstSession.sessionId);
  expect(nextSession.created).toBe(false);

  const end = await request.post(`${apiBaseUrl}/api/audit/site-access/end`, {
    headers: devHeaders('dev-sales'),
    data: {
      sessionId: nextSession.sessionId,
      idempotencyReceipt: nextSession.idempotencyReceipt
    }
  });
  expect(end.status()).toBe(204);

  const deniedList = await request.get(
    `${apiBaseUrl}/api/admin/audit-events?eventType=SiteAccess&page=1&pageSize=50`,
    { headers: devHeaders('dev-sales') }
  );
  expect(deniedList.status()).toBe(403);

  const allowedList = await request.get(
    `${apiBaseUrl}/api/admin/audit-events?eventType=SiteAccess&page=1&pageSize=50`,
    { headers: devHeaders('dev-admin') }
  );
  expect(allowedList.ok()).toBeTruthy();
  const list = await allowedList.json() as AuditList;
  const target = list.items.find((item) => item.eventId === firstSession.sessionId);
  expect(target).toBeTruthy();
  expect(target!.siteAccessStatus).toBe('ExplicitLogout');
  expect(target!.menuCodes).toEqual(['Home', 'Projects']);
  expect(target!.menuLabels).toEqual(['홈', '프로젝트']);
  expect(list.coverage.siteAccessCoverageStartedAtUtc).toBeTruthy();
  expect(list.coverage.lastActivityNotice).toContain('실제 근무시간');

  const detail = await request.get(
    `${apiBaseUrl}/api/admin/audit-events/${firstSession.sessionId}?source=SiteAccess`,
    { headers: devHeaders('dev-admin') }
  );
  expect(detail.ok()).toBeTruthy();
  const detailBody = await detail.json() as { event: AuditItem };
  expect(detailBody.event.clientIp).toBeTruthy();
  expect(detailBody.event.browserFamily).toBe('Other');
  expect(detailBody.event.appAccessOutcome).toBe('Allowed');

  const selected = await request.post(`${apiBaseUrl}/api/data-exports/selected`, {
    headers: devHeaders('dev-admin'),
    data: {
      screen: 'admin-audit-events',
      ids: [firstSession.sessionId],
      filters: { eventType: 'SiteAccess' }
    }
  });
  expect(selected.ok()).toBeTruthy();
  const workbookPath = path.join('/tmp', `site-access-audit-${Date.now()}.xlsx`);
  await fs.writeFile(workbookPath, await selected.body());
  try {
    const workbookXml = execFileSync('unzip', ['-p', workbookPath], { encoding: 'utf8' });
    expect(workbookXml).toContain('사이트 마지막 활동');
    expect(workbookXml).toContain('사이트 접속 기록 시작');
    expect(workbookXml).toContain('실제 근무시간');
    expect(workbookXml).toContain('홈 → 프로젝트');
    expect(workbookXml).not.toMatch(/<f(?:\s|>)/);
  } finally {
    await fs.rm(workbookPath, { force: true });
  }

  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await page.goto('/admin/system/audit-events');
  await expect(page.getByRole('heading', { name: '전체 감사 이력' })).toBeVisible();
  await page.getByLabel('사건').selectOption('SiteAccess');
  await page.getByRole('button', { name: '조회', exact: true }).click();
  await expect(page.getByText('사이트 접속 기록 범위')).toBeVisible();
  await expect(page.getByText('사이트 접속', { exact: true }).first()).toBeVisible();
  await capture(page, '01-site-access-audit-desktop-1440.png');

  const desktopTable = page.locator('.audit-desktop-table');
  await desktopTable.getByRole('button').first().click();
  const detailPanel = page.getByLabel('감사 사건 상세');
  await expect(detailPanel).toBeVisible();
  await expect(detailPanel.getByText('접속 메뉴')).toBeVisible();
  await detailPanel.getByRole('button', { name: '닫기' }).click();

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByLabel('감사 이력 모바일 목록')).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '02-site-access-audit-mobile-390.png');

  await page.getByLabel('감사 이력 모바일 목록').getByRole('button', { name: '상세 보기' }).first().click();
  const mobileDetail = page.getByLabel('감사 사건 상세');
  await expect(mobileDetail).toBeVisible();
  await expect(mobileDetail.getByText('접속 메뉴')).toBeVisible();
  await capture(page, '03-site-access-detail-mobile-390.png');

  await page.evaluate(() => window.localStorage.removeItem('emi-pms.site-access.browser-client-id'));
  const signaledBrowserClientIds: string[] = [];
  const requestListener = (browserRequest: import('@playwright/test').Request) => {
    if (!browserRequest.url().endsWith('/api/audit/site-access/signals')) return;
    const body = browserRequest.postDataJSON() as { browserClientId?: string };
    if (body.browserClientId) signaledBrowserClientIds.push(body.browserClientId);
  };
  context.on('request', requestListener);
  const racePageA = await context.newPage();
  const racePageB = await context.newPage();
  await Promise.all([racePageA, racePageB].map((racePage) => racePage.addInitScript(() => {
    Object.defineProperty(navigator, 'locks', { configurable: true, value: undefined });
  })));
  await Promise.all([racePageA.goto('/'), racePageB.goto('/')]);
  await expect.poll(() => signaledBrowserClientIds.length).toBeGreaterThanOrEqual(2);
  expect(new Set(signaledBrowserClientIds).size).toBe(1);
  context.off('request', requestListener);
  await Promise.all([racePageA.close(), racePageB.close()]);

  await page.evaluate(async () => {
    window.localStorage.removeItem('emi-pms.site-access.browser-client-id');
    await new Promise<void>((resolve, reject) => {
      const request = indexedDB.deleteDatabase('emi-pms-site-access');
      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
      request.onblocked = () => reject(new Error('IndexedDB cleanup was blocked.'));
    });
  });
  const blockedStorageIds: string[] = [];
  const blockedStorageListener = (browserRequest: import('@playwright/test').Request) => {
    if (!browserRequest.url().endsWith('/api/audit/site-access/signals')) return;
    const body = browserRequest.postDataJSON() as { browserClientId?: string };
    if (body.browserClientId) blockedStorageIds.push(body.browserClientId);
  };
  context.on('request', blockedStorageListener);
  const blockedPageA = await context.newPage();
  const blockedPageB = await context.newPage();
  await Promise.all([blockedPageA, blockedPageB].map((blockedPage) => blockedPage.addInitScript(() => {
    const targetKey = 'emi-pms.site-access.browser-client-id';
    const originalGetItem = Storage.prototype.getItem;
    const originalSetItem = Storage.prototype.setItem;
    Storage.prototype.getItem = function getItem(key: string) {
      if (key === targetKey) throw new DOMException('blocked', 'SecurityError');
      return originalGetItem.call(this, key);
    };
    Storage.prototype.setItem = function setItem(key: string, value: string) {
      if (key === targetKey) throw new DOMException('blocked', 'SecurityError');
      return originalSetItem.call(this, key, value);
    };
  })));
  await Promise.all([blockedPageA.goto('/'), blockedPageB.goto('/')]);
  await expect.poll(() => blockedStorageIds.length).toBeGreaterThanOrEqual(2);
  expect(new Set(blockedStorageIds).size).toBe(1);
  context.off('request', blockedStorageListener);
  await Promise.all([blockedPageA.close(), blockedPageB.close()]);
});

type SiteAccessSession = {
  sessionId: string;
  idempotencyReceipt: string;
  created: boolean;
};

type AuditItem = {
  eventId: string;
  siteAccessStatus: string;
  menuCodes: string[];
  menuLabels: string[];
  clientIp: string | null;
  browserFamily: string | null;
  appAccessOutcome: string | null;
};

type AuditList = {
  items: AuditItem[];
  coverage: {
    siteAccessCoverageStartedAtUtc: string;
    lastActivityNotice: string;
  };
};

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey, 'User-Agent': 'site-access-full-stack-test' };
}

async function capture(page: import('@playwright/test').Page, filename: string) {
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  await page.screenshot({
    path: path.join(screenshotDirectory, filename),
    animations: 'disabled',
    fullPage: true
  });
}

async function assertNoHorizontalOverflow(page: import('@playwright/test').Page) {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth
  );
  expect(overflow).toBe(0);
}
