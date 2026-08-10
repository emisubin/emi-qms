import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type Page } from '@playwright/test';

const screenshotDirectory = '/tmp/emi-pms-teams-pwa-001';
const notificationId = '71000000-0000-0000-0000-000000000001';

test('Teams launcher stays small, preserves a notification deep link, and fits desktop/mobile', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/teams-launcher.html?notificationId=${notificationId}`);
  const appOrigin = new URL(page.url()).origin;

  await expect(page.getByRole('heading', { name: 'EMI PMS' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'EMI PMS 열기' })).toHaveAttribute(
    'href',
    `${appOrigin}/teams/activity/notifications/${notificationId}`
  );
  await expect(page.locator('script[src]')).toHaveCount(1);
  await expect(page.locator('script[src="/teams-launcher.js"]')).toHaveCount(1);
  await assertNoHorizontalOverflow(page);
  await capture(page, 'teams-launcher-desktop-1440.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.getByRole('heading', { name: 'EMI PMS' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, 'teams-launcher-mobile-390.png');

  await page.goto('/teams-launcher.html?notificationId=https%3A%2F%2Fexample.org%2Foutside');
  await expect(page.getByRole('link', { name: 'EMI PMS 열기' })).toHaveAttribute('href', `${appOrigin}/`);
});

test('iPhone receives one dismissible Home Screen guide without horizontal overflow', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window.navigator, 'userAgent', {
      configurable: true,
      value: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1'
    });
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  const dialog = page.getByRole('dialog', { name: 'iPhone 설치 안내' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText('홈 화면에 추가')).toBeVisible();
  await expect(dialog.getByText('웹 앱으로 열기')).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, 'pwa-iphone-guide-mobile-390.png');

  await dialog.getByRole('button', { name: '확인' }).click();
  await expect(dialog).toHaveCount(0);
  await page.reload();
  await expect(dialog).toHaveCount(0);
});

test('iPhone non-Safari browsers offer current-browser steps and a Safari copy fallback', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window.navigator, 'userAgent', {
      configurable: true,
      value: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 CriOS/140.0.0.0 Mobile/15E148 Safari/604.1'
    });
    Object.defineProperty(window.navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: async (value: string) => {
          (window as Window & { copiedInstallAddress?: string }).copiedInstallAddress = value;
        }
      }
    });
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  const dialog = page.getByRole('dialog', { name: 'iPhone 설치 안내' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('.pwa-install-note')).toContainText('현재 브라우저의 공유 메뉴에서 먼저');
  await dialog.getByRole('button', { name: 'PMS 주소 복사' }).click();
  await expect(dialog.getByRole('status')).toContainText('PMS 주소를 복사했습니다.');
  expect(await page.evaluate(() => (window as Window & { copiedInstallAddress?: string }).copiedInstallAddress)).toBe(`${new URL(page.url()).origin}/`);
  await assertNoHorizontalOverflow(page);
  await capture(page, 'pwa-iphone-other-browser-guide-mobile-390.png');
});

test('Android receives the one-tap install guide in the same wireframe shell', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window.navigator, 'userAgent', {
      configurable: true,
      value: 'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 Chrome/140.0.0.0 Mobile Safari/537.36'
    });
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  const dialog = page.getByRole('dialog', { name: 'Android 설치 안내' });
  await expect(dialog).toBeVisible();
  const installButton = dialog.getByRole('button', { name: 'EMI PMS 설치' });
  await expect(installButton).toBeDisabled();
  await expect(dialog).toContainText('설치 버튼을 준비하고 있습니다.');

  await page.evaluate(() => {
    const event = new Event('beforeinstallprompt') as Event & {
      prompt: () => Promise<void>;
      userChoice: Promise<{ outcome: 'dismissed'; platform: string }>;
    };
    event.prompt = async () => {};
    event.userChoice = Promise.resolve({ outcome: 'dismissed', platform: 'web' });
    window.dispatchEvent(event);
  });

  await expect(installButton).toBeEnabled();
  await expect(dialog).toContainText('설치를 누르면 브라우저의 설치 확인 창이 열립니다.');
  await assertNoHorizontalOverflow(page);
  await capture(page, 'pwa-android-guide-mobile-390.png');
});

async function assertNoHorizontalOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
}

async function capture(page: Page, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled' });
}
