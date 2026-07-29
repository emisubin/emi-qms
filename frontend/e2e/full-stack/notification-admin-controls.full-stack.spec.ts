import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/notify-admin-controls-screenshots');

test('notification administrator audit and failed reprocess use the real isolated stack', async ({ page, request }) => {
  test.setTimeout(180_000);
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await createPreferenceAudit(request);
  const failedDeliveryId = seedFailedDelivery();

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');

  await page.goto('/admin/system/notification-preference-audit');
  await expect(page.getByRole('heading', { name: '알림 설정 변경 이력' })).toBeVisible();
  await expect(page.locator('.audit-desktop-table').getByText('사용자 직접 변경', { exact: true }).first()).toBeVisible();
  await capture(page, '01-notification-preference-audit-desktop-1440.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '알림 설정 변경 이력' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '02-notification-preference-audit-mobile-390.png');

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/admin/system/notification-deliveries?status=Failed');
  await expect(page.getByRole('heading', { name: '알림 발송 상태' })).toBeVisible();
  const failedCheckbox = page.getByLabel('격리 E2E 최종 실패 알림 선택');
  await expect(failedCheckbox).toBeVisible();
  await failedCheckbox.check();
  await page.getByRole('button', { name: '최종 실패 재처리' }).click();
  await expect(page.getByRole('region', { name: '최종 실패 알림 재처리' })).toBeVisible();
  await capture(page, '03-failed-notification-reprocess-desktop-1440.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await assertNoHorizontalOverflow(page);
  await capture(page, '04-failed-notification-reprocess-mobile-390.png');

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.getByLabel('재처리 사유').fill('격리 E2E에서 장애 원인을 확인하고 안전하게 다시 처리합니다.');
  await page.getByLabel('외부 provider에 이미 전달되었을 가능성과 중복 위험을 확인했습니다.').check();
  await page.getByRole('button', { name: '새 generation 시작' }).click();
  await expect(page.getByText('1건을 새 generation으로 재처리 대기열에 등록했습니다.')).toBeVisible();

  await page.goto(`/admin/system/notification-deliveries/${failedDeliveryId}`);
  await expect(page.getByRole('heading', { name: '알림 발송 상세' })).toBeVisible();
  await expect(page.getByText('G2', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: '수동 재처리 이력' })).toBeVisible();
  await capture(page, '05-reprocessed-notification-detail-desktop-1440.png');
});

async function createPreferenceAudit(request: APIRequestContext) {
  const initialResponse = await request.get(`${apiBaseUrl}/api/my/notification-preferences`, {
    headers: devHeaders('dev-sales')
  });
  expect(initialResponse.ok(), await initialResponse.text()).toBeTruthy();
  const initial = await initialResponse.json() as {
    version: number;
    items: Array<{ deliveryType: string; channel: string; enabled: boolean; canChange: boolean }>;
  };
  const target = initial.items.find((item) => item.deliveryType === 'DueSoonL0' && item.canChange);
  expect(target).toBeTruthy();
  const saveResponse = await request.put(`${apiBaseUrl}/api/my/notification-preferences`, {
    headers: devHeaders('dev-sales'),
    data: {
      expectedVersion: initial.version,
      items: [{ deliveryType: target!.deliveryType, channel: target!.channel, enabled: !target!.enabled }]
    }
  });
  expect(saveResponse.ok(), await saveResponse.text()).toBeTruthy();
}

function seedFailedDelivery() {
  const deliveryId = '79000000-0000-0000-0000-000000000901';
  queryDatabase(`
    insert into notification_deliveries (
      id, channel, delivery_type, status, attempt_count, generation_attempt_count,
      current_generation, error_code, error_message, dedupe_key, display_title,
      display_recipient_name, display_channel_target, next_attempt_at_utc
    ) values (
      '${deliveryId}', 'Mail', 'ManualTest', 'Failed', 3, 3, 1,
      'ProviderTimeout', '외부 메일 provider 응답 제한시간을 초과했습니다.',
      'notification-admin-controls-failed', '격리 E2E 최종 실패 알림',
      '영업 담당자', 'sales@example.invalid', now()
    );
  `);
  return deliveryId;
}

async function capture(page: Page, filename: string) {
  await page.screenshot({
    path: path.join(screenshotDirectory, filename),
    animations: 'disabled',
    fullPage: true
  });
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
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
