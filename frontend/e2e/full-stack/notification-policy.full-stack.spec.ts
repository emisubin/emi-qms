import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/notify-policy-001-screenshots');

test('notification policy settings expose only the confirmed choices in the isolated stack', async ({ page, request }) => {
  await fs.mkdir(screenshotDirectory, { recursive: true });

  const response = await request.get(`${apiBaseUrl}/api/my/notification-preferences`, {
    headers: { 'X-Dev-User': 'dev-sales' }
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  const preferences = await response.json() as {
    taxonomyVersion: string;
    items: Array<{ deliveryType: string; canChange: boolean }>;
  };
  expect(preferences.taxonomyVersion).toBe('2026-08-v1');
  expect(preferences.items).toHaveLength(5);
  expect(preferences.items.filter((item) => item.canChange).map((item) => item.deliveryType).sort()).toEqual([
    'DailyDigest',
    'DueSoonL0',
    'WorkItemCreated'
  ]);
  expect(preferences.items.filter((item) => !item.canChange).map((item) => item.deliveryType).sort()).toEqual([
    'OverdueL1',
    'UrgentBlocking'
  ]);

  expect(queryDatabase("select count(*)::text from information_schema.columns where table_name='work_items' and column_name in ('fallback_group_key','fallback_completed_by_user_id','fallback_auto_closed_at_utc');")).toBe('3');
  expect(queryDatabase("select count(*)::text from schema_migrations where version='0075_notification_policy_alignment';")).toBe('1');

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/notification-settings');
  await expect(page.getByRole('heading', { name: '내 알림 설정' })).toBeVisible();
  await expect(page.getByText('필수 Pending·프로젝트 알림은 해제할 수 없습니다.')).toBeVisible();
  await expect(page.locator('.notification-preference-card')).toHaveCount(5);
  await expect(page.getByText('필수', { exact: true })).toHaveCount(2);
  await expect(page.getByText('예정일 초과 L2')).toHaveCount(0);
  await expect(page.getByText('예정일 초과 L3')).toHaveCount(0);
  await page.screenshot({ path: path.join(screenshotDirectory, '01-notification-settings-desktop-1440.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '내 알림 설정' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  await page.screenshot({ path: path.join(screenshotDirectory, '02-notification-settings-mobile-390.png'), fullPage: true });
});

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
