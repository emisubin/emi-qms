import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), '../tasks/notice-board-001-screenshots');

test('TASK-NOTICE-BOARD-001: authenticated users share notices from Home on desktop and mobile', async ({ page, request }) => {
  const unique = Date.now();
  const apiTitle = `생산 일정 공지 ${unique}`;
  const apiBody = '이번 주 생산 계획 변경사항을 확인해 주세요.\n설계와 구매 담당자는 금요일까지 회신 바랍니다.';
  const apiNoticeId = await createNotice(request, apiTitle, apiBody, 'dev-sales');

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto('/');

  await expect(page.getByRole('heading', { name: '업무 홈' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '공지사항' })).toBeVisible();
  await expect(page.getByRole('button', { name: '공지 작성' })).toBeVisible();
  await expect(page.getByRole('button', { name: new RegExp(apiTitle) })).toContainText('Sales');
  await capture(page, '01-home-notice-desktop-1440.png');

  await page.getByRole('button', { name: '공지 전체 보기' }).click();
  await expect(page).toHaveURL(/\/notices$/);
  await expect(page.getByRole('heading', { name: '공지사항' })).toBeVisible();
  await expect(page.getByRole('button', { name: new RegExp(apiTitle) })).toBeVisible();
  await capture(page, '02-notice-list-desktop-1440.png');

  await page.goto('/');
  await page.getByRole('button', { name: '공지 작성' }).click();
  await expect(page).toHaveURL(/\/notices\?compose=1$/);
  await expect(page.getByRole('heading', { name: '공지 작성' })).toBeVisible();
  await capture(page, '03-notice-compose-desktop-1440.png');

  const uiTitle = `품질 검사 일정 ${unique}`;
  const uiBody = '수입검사 일정이 변경되었습니다.\n자재 도착 등록 후 품질팀에 전달해 주세요.';
  await page.getByRole('textbox', { name: /제목/ }).fill(uiTitle);
  await page.getByRole('textbox', { name: /내용/ }).fill(uiBody);
  await page.getByRole('button', { name: '공지 등록' }).click();
  await expect(page).toHaveURL(/\/notices\/[0-9a-f-]{36}$/i);
  await expect(page.getByRole('heading', { name: uiTitle })).toBeVisible();
  await expect(page.getByText('Dev Quality User · 품질')).toBeVisible();
  await expect(page.getByRole('button', { name: '내 공지 삭제' })).toBeVisible();
  await capture(page, '04-notice-detail-desktop-1440.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: '업무 홈' })).toBeVisible();
  await expect(page.getByRole('button', { name: new RegExp(uiTitle) })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '05-home-notice-mobile-390.png');

  await page.goto('/notices');
  await expect(page.getByRole('button', { name: new RegExp(uiTitle) })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '06-notice-list-mobile-390.png');

  await page.getByRole('button', { name: new RegExp(apiTitle) }).click();
  await expect(page).toHaveURL(`/notices/${apiNoticeId}`);
  await expect(page.getByText(apiBody.split('\n')[1])).toBeVisible();
  await expect(page.getByRole('button', { name: '내 공지 삭제' })).toHaveCount(0);
  await assertNoHorizontalOverflow(page);
  await capture(page, '07-notice-detail-mobile-390.png');
});

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

async function createNotice(request: APIRequestContext, title: string, body: string, developmentUserKey: string) {
  const response = await request.post(`${apiBaseUrl}/api/notices`, {
    headers: { 'X-Dev-User': developmentUserKey },
    data: { requestId: crypto.randomUUID(), title, body }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { noticeId: string }).noticeId;
}
