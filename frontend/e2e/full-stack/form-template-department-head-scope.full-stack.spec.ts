import { expect, test, type Page } from '@playwright/test';

test('TASK-ADMIN-003 Change 002: department heads see only their assigned form workspaces', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');

  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await expect(page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '양식 관리' })).toBeVisible();
  await page.goto('/form-templates');
  await expect(page.getByRole('heading', { name: '양식 관리' })).toBeVisible();
  const qualityCatalog = page.getByRole('navigation', { name: '양식 종류' });
  await expect(qualityCatalog.getByRole('button', { name: /자재 수입검사/ })).toBeVisible();
  await expect(qualityCatalog.getByRole('button', { name: /Item별 LQC 검사/ })).toBeVisible();
  await expect(qualityCatalog.getByRole('button', { name: /OQC 자체검수/ })).toBeVisible();
  await expect(qualityCatalog.getByRole('button', { name: /구매품별 IQC 양식/ })).toBeVisible();
  await expect(qualityCatalog.getByRole('button', { name: /구매품 구분 관리/ })).toBeVisible();
  await expect(qualityCatalog.getByRole('button', { name: /Item별 제조 양식/ })).toHaveCount(0);
  await expect(qualityCatalog.getByRole('button', { name: /생산계획·실적 연결/ })).toHaveCount(0);
  await qualityCatalog.getByRole('button', { name: /Item별 LQC 검사/ }).click();
  await expect(page.getByRole('switch')).toBeEnabled();

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await expect(page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '양식 관리' })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: '양식 관리' })).toHaveCount(0);

  await page.getByLabel('개발 사용자').selectOption('dev-production');
  await expect(page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '양식 관리' })).toBeVisible();
  await page.goto('/form-templates');
  await expect(page.getByRole('heading', { name: '양식 관리' })).toBeVisible();
  const productionCatalog = page.getByRole('navigation', { name: '양식 종류' });
  await expect(productionCatalog.getByRole('button', { name: /Item별 제조 양식/ })).toBeVisible();
  await expect(productionCatalog.getByRole('button', { name: /생산계획·실적 연결/ })).toBeVisible();
  await expect(productionCatalog.getByRole('button', { name: /자재 수입검사/ })).toHaveCount(0);
  await expect(productionCatalog.getByRole('button', { name: /구매품별 IQC 양식/ })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: '생산계획·실적 연결' })).toBeVisible();
  await expect(page.getByText('편집 가능')).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(page.getByRole('heading', { name: '양식 관리' })).toBeVisible();
  await assertNoHorizontalOverflow(page);
});

async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => ({
    documentWidth: document.documentElement.scrollWidth,
    viewportWidth: document.documentElement.clientWidth
  }));
  expect(overflow.documentWidth).toBeLessThanOrEqual(overflow.viewportWidth + 1);
}
