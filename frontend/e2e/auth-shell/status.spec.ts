import { expect, test } from '@playwright/test';
for (const state of ['loading', 'reauth', 'pending', 'error']) {
  test(`@desktop @mobile approved ${state} layout remains readable`, async ({ page }, testInfo) => {
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(`/e2e/auth-shell/status.html?state=${state}`);
    await expect(page.locator('main')).toHaveAttribute('data-auth-state', state);
    await expect(page.locator('main')).toHaveAttribute('data-auth-layout', 'login');
    await page.evaluate(() => document.fonts.ready);
    if (state === 'loading') {
      await expect(page.getByRole('status', { name: '로그인 확인 중' })).toBeVisible();
      await expect(page.getByRole('button')).toHaveCount(0);
    } else {
      const button = page.getByRole('button', { name: state === 'pending' ? '로그아웃' : '로그인', exact: true });
      await expect(button).toBeVisible();
      const message = page.locator('.auth-status-message');
      await expect(message).toHaveText(state === 'pending' ? '관리자 승인을 기다리고 있습니다.' :
        state === 'reauth' ? '인증이 필요합니다.' : '로그인을 완료하지 못했습니다. 다시 시도해 주세요.');
      const buttonBox = (await button.boundingBox())!;
      const messageBox = (await message.boundingBox())!;
      const panelBox = (await page.locator('.auth-gate-panel').boundingBox())!;
      expect(messageBox.y).toBeGreaterThanOrEqual(buttonBox.y + buttonBox.height);
      expect(messageBox.y + messageBox.height).toBeLessThanOrEqual(panelBox.y + panelBox.height);
      expect(messageBox.x).toBeGreaterThanOrEqual(panelBox.x);
      expect(messageBox.x + messageBox.width).toBeLessThanOrEqual(panelBox.x + panelBox.width + 1);
      await button.focus();
      await expect(button).toBeFocused();
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(errors).toEqual([]);
    await page.screenshot({ path: testInfo.outputPath(`${state}.png`), fullPage: true });
  });
}
