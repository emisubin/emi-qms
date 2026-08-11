import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/pwa-push-001-screenshots');

test('PWA push settings stay self-service and provider-safe in the isolated stack', async ({ page, request }) => {
  await fs.mkdir(screenshotDirectory, { recursive: true });

  const configurationResponse = await request.get(`${apiBaseUrl}/api/my/web-push`, {
    headers: { 'X-Dev-User': 'dev-sales' }
  });
  expect(configurationResponse.ok(), await configurationResponse.text()).toBeTruthy();
  expect(await configurationResponse.json()).toEqual({
    enabled: true,
    dryRun: true,
    configured: true,
    publicKey: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
    activeDeviceCount: 0,
    lastChangedAtUtc: null
  });

  const firstSubscription = await request.put(`${apiBaseUrl}/api/my/web-push/subscriptions`, {
    headers: devHeaders('dev-sales'),
    data: {
      endpoint: 'https://push.example.test/e2e-device-one',
      keys: {
        p256dh: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
        auth: 'AAAAAAAAAAAAAAAAAAAAAA'
      }
    }
  });
  expect(firstSubscription.ok(), await firstSubscription.text()).toBeTruthy();
  const secondSubscription = await request.put(`${apiBaseUrl}/api/my/web-push/subscriptions`, {
    headers: devHeaders('dev-sales'),
    data: {
      endpoint: 'https://push.example.test/e2e-device-two',
      keys: {
        p256dh: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
        auth: 'AAAAAAAAAAAAAAAAAAAAAA'
      }
    }
  });
  expect(secondSubscription.ok(), await secondSubscription.text()).toBeTruthy();

  seedRecipientNotification();
  await expect.poll(
    () => queryDatabase("select count(*)::text from notification_deliveries where channel='WebPush' and status='DryRunSent';"),
    { timeout: 30_000 }
  ).toBe('2');

  const deactivateCurrent = await request.post(`${apiBaseUrl}/api/my/web-push/subscriptions/deactivate-current`, {
    headers: devHeaders('dev-sales'),
    data: { endpoint: 'https://push.example.test/e2e-device-one', reason: 'UserRequest' }
  });
  expect(deactivateCurrent.ok(), await deactivateCurrent.text()).toBeTruthy();
  expect((await deactivateCurrent.json() as { activeDeviceCount: number }).activeDeviceCount).toBe(1);

  const deactivateAll = await request.post(`${apiBaseUrl}/api/my/web-push/subscriptions/deactivate-all`, {
    headers: devHeaders('dev-sales')
  });
  expect(deactivateAll.ok(), await deactivateAll.text()).toBeTruthy();
  expect((await deactivateAll.json() as { activeDeviceCount: number }).activeDeviceCount).toBe(0);

  const workerResponse = await request.get('/web-push-service-worker.js');
  expect(workerResponse.ok()).toBeTruthy();
  const worker = await workerResponse.text();
  expect(worker).toContain("addEventListener('push'");
  expect(worker).toContain("addEventListener('notificationclick'");
  expect(worker).not.toContain("addEventListener('fetch'");

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/notification-settings');
  await expect(page.getByRole('heading', { name: '내 알림 설정' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '기기 푸시 알림' })).toBeVisible();
  await expect(page.getByText('먼저 EMI PMS를 홈 화면에 설치한 뒤 설치된 앱에서 이 설정을 열어 주세요.')).toBeVisible();
  await expect(page.getByRole('button', { name: '설정 저장' })).toBeVisible();
  await page.screenshot({ path: path.join(screenshotDirectory, '01-self-settings-desktop-1440.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '기기 푸시 알림' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  await page.screenshot({ path: path.join(screenshotDirectory, '02-self-settings-mobile-390.png'), fullPage: true });

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await page.goto('/admin/users/50000000-0000-0000-0000-000000000002/notification-settings');
  await expect(page.getByRole('heading', { name: '사용자 알림 설정 지원' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '기기 푸시 알림' })).toHaveCount(0);
});

function seedRecipientNotification() {
  queryDatabase(`
    with inserted as (
      insert into notifications (
        id, notification_type, severity, title, message,
        idempotency_key, visibility_scope, source_kind, created_at_utc
      ) values (
        '79000000-0000-0000-0000-000000000974', 'Info', 'Info',
        '격리 PWA 푸시 알림', '상세 내용은 인앱에서만 확인합니다.',
        'pwa-push-e2e-recipient', 'RecipientOnly', 'Automatic', now()
      ) returning id
    )
    insert into notification_recipients (notification_id, user_id, created_at_utc)
    select id, '50000000-0000-0000-0000-000000000002', now() from inserted;
  `);
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}

function queryDatabase(sql: string) {
  return execFileSync(
    'docker',
    [
      'compose',
      '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'),
      '--file', requireEnv('E2E_COMPOSE_FILE'),
      'exec',
      '-T', requireEnv('E2E_POSTGRES_SERVICE'),
      'psql',
      '--username', requireEnv('E2E_DATABASE_USER'),
      '--dbname', requireEnv('E2E_DATABASE_NAME'),
      '--no-psqlrc',
      '--tuples-only',
      '--no-align',
      '--set', 'ON_ERROR_STOP=1',
      '--command', sql
    ],
    { encoding: 'utf8' }
  ).trim().split('\n').at(-1)?.trim() ?? '';
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for isolated full-stack validation.`);
  return value;
}
