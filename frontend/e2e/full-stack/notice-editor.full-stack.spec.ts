import { expect, test, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const screenshotDirectory = path.resolve(process.cwd(), '../tasks/notice-editor-001-screenshots');

test('TASK-NOTICE-EDITOR-001: author formats, edits and shares an attachment', async ({ page }) => {
  const unique = Date.now();
  const title = `시범 운영 자료 ${unique}`;
  const changedTitle = `시범 운영 자료 수정 ${unique}`;
  const bodyValue = '중요 일정과 첨부자료를 확인해 주세요.';

  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto('/notices?compose=1');
  await page.getByRole('textbox', { name: /제목/ }).fill(title);
  const body = page.getByRole('textbox', { name: /내용/ });
  await body.fill(bodyValue);
  await body.evaluate((element) => {
    const textarea = element as HTMLTextAreaElement;
    textarea.focus();
    textarea.setSelectionRange(0, 2);
  });
  await page.getByRole('button', { name: '선택한 글씨 굵게' }).click();
  await expect(body).toHaveValue(`**중요**${bodyValue.slice(2)}`);
  await page.getByLabel(/첨부파일/).setInputFiles({
    name: 'notice-guide.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.7\nsynthetic notice guide')
  });
  await capture(page, '01-compose-bold-attachment-desktop-1440.png');

  await page.getByRole('button', { name: '공지 등록' }).click();
  await expect(page).toHaveURL(/\/notices\/[0-9a-f-]{36}$/i);
  await expect(page.getByRole('heading', { name: title })).toBeVisible();
  await expect(page.getByText('중요', { exact: true })).toHaveCSS('font-weight', /^(700|[89]00)$/);
  await expect(page.getByRole('button', { name: /notice-guide\.pdf/ })).toBeVisible();
  await capture(page, '02-detail-bold-attachment-desktop-1440.png');

  await page.getByRole('button', { name: '공지 수정' }).click();
  await page.getByRole('textbox', { name: /제목/ }).fill(changedTitle);
  await page.getByRole('button', { name: '수정 저장' }).click();
  await expect(page.getByRole('heading', { name: changedTitle })).toBeVisible();
  await expect(page.getByText(/수정/)).toBeVisible();
  await capture(page, '03-edited-detail-desktop-1440.png');

  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: /notice-guide\.pdf/ }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('notice-guide.pdf');

  const noticeUrl = page.url();
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto(noticeUrl);
  await expect(page.getByRole('button', { name: '공지 수정' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /notice-guide\.pdf/ })).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(noticeUrl);
  await expect(page.getByRole('heading', { name: changedTitle })).toBeVisible();
  await expect(page.getByRole('button', { name: /notice-guide\.pdf/ })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '04-reader-detail-mobile-390.png');
});

async function capture(page: Page, filename: string) {
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.evaluate(async () => {
    await document.fonts.ready;
    window.scrollTo(0, 0);
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled', fullPage: true });
}

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}
