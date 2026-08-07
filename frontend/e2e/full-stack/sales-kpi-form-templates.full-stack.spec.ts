import { expect, test, type Page, type Route } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const screenshotDirectory = path.resolve(process.cwd(), '../tasks/design-000-screenshots');

test('DESIGN-000 + SALES-KPI-001 Change 002: token foundation and adaptive decision chart', async ({ page }) => {
  await page.route('**/api/sales/kpi**', mockSalesKpi);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: '연간 매출 성과' })).toBeVisible();
  await expect(page.getByRole('img', { name: '월별 확정 매출과 목표 막대, 달성률 선 비교' })).toBeVisible();
  await capture(page, '01-sales-home-desktop-1440.png');

  await page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '영업' }).click();
  await expect(page).toHaveURL(/\/sales/);
  await expect(page.getByRole('heading', { name: '연간 매출 성과' })).toBeVisible();
  await expect(page.getByText('₩1,140,000,000')).toBeVisible();
  await capture(page, '02-sales-kpi-desktop-1440.png');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/sales?year=2026&currency=KRW');
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('group', { name: '12개월 확정 매출·목표·달성률 그래프' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '03-sales-kpi-mobile-390.png');

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '연간 매출 성과' })).toBeVisible();
  await expect(page.getByRole('group', { name: '12개월 확정 매출·목표·달성률 그래프' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await capture(page, '04-sales-home-mobile-390.png');

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await page.goto('/form-templates');
  await expect(page.getByRole('heading', { name: '양식 관리' })).toBeVisible();
  await expect(page.getByRole('button', { name: '부서장 지정' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '자재 수입검사' })).toBeVisible();
  await expect(page.getByRole('button', { name: '수정' })).toBeVisible();
  await expect(page.getByRole('button', { name: '저장' })).toHaveCount(0);
  await page.getByRole('button', { name: '수정' }).click();
  await expect(page.getByRole('button', { name: '저장' })).toBeVisible();
  await expect(page.getByRole('button', { name: '저장' })).toBeEnabled();
  await capture(page, '05-form-templates-desktop-1440.png');

  await page.getByRole('button', { name: '취소' }).click();
  await page.getByRole('button', { name: /Item별 제조 양식/ }).click();
  await expect(page.getByRole('combobox', { name: '적용 Item' })).toBeVisible();
  await page.getByRole('button', { name: '수정' }).click();
  const productionSaveButton = page.getByRole('button', { name: '저장' });
  await expect(productionSaveButton).toBeVisible();
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(productionSaveButton).toBeVisible();
  await expect(productionSaveButton).toBeEnabled();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/form-templates');
  await expect(page.getByRole('heading', { name: '양식 관리' })).toBeVisible();
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await assertNoHorizontalOverflow(page);
  await capture(page, '06-form-templates-mobile-390.png');
});

async function mockSalesKpi(route: Route) {
  const url = new URL(route.request().url());
  if (/\/api\/sales\/kpi\/months\/\d+$/.test(url.pathname)) {
    const month = Number(url.pathname.split('/').at(-1));
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        year: 2026,
        month,
        currency: 'KRW',
        projects: [
          { projectId: '71000000-0000-0000-0000-000000000010', projectCode: 'EMI-2607-A', projectName: '데이터센터 배전반', invoiceIssuedDate: `2026-${String(month).padStart(2, '0')}-18`, amount: month * 10000000 }
        ]
      })
    });
    return;
  }
  if (url.pathname === '/api/sales/kpi') {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        year: 2026,
        currency: 'KRW',
        defaultCurrency: 'KRW',
        availableYears: [2026, 2025],
        availableCurrencies: ['KRW', 'USD'],
        months: [55, 62, 78, 83, 91, 104, 96, 108, 112, 120, 116, 115].map((amount, index) => ({
          month: index + 1,
          revenueAmount: amount * 1000000,
          targetAmount: (85 + index * 3) * 1000000,
          settlementCount: 2 + index % 3
        })),
        kpi: {
          currentMonthRevenue: 96000000,
          revenueTotal: 1140000000,
          targetTotal: 1218000000,
          registeredTargetMonthCount: 12,
          achievementRate: 93.6,
          remainingTargetAmount: 78000000,
          exceededTargetAmount: 0
        },
        pipeline: { amount: 486000000, projectCount: 7 },
        missingAmountCount: 0
      })
    });
    return;
  }
  await route.continue();
}

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
