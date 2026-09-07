import { expect, test } from '@playwright/test';

const pendingUserId = '50000000-0000-0000-0000-000000000002';

test('three databases keep tab context, integrated user-access mutation locks, and reset isolated', async ({ browser }) => {
  const pendingContext = await browser.newContext();
  await pendingContext.addInitScript(() => {
    window.localStorage.setItem('emi-qms-development-user-key', 'dev-sales');
  });
  const pendingPage = await pendingContext.newPage();
  await pendingPage.goto('/');
  await expect(pendingPage.getByRole('heading', { name: '사용할 수 있는 사업부가 없습니다.' })).toBeVisible();
  await expect(pendingPage.getByRole('button', { name: /사업부로 이동/ })).toHaveCount(0);
  expect(await pendingPage.evaluate(() => window.sessionStorage.getItem('emi.qms.business-unit'))).toBeNull();
  await pendingContext.close();

  const cheongjuContext = await browser.newContext();
  await cheongjuContext.addInitScript(() => {
    window.localStorage.setItem('emi-qms-development-user-key', 'dev-admin');
  });
  const cheongjuPage = await cheongjuContext.newPage();
  await cheongjuPage.goto('/');
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('CHEONGJU');
  await expect(cheongjuPage.getByRole('button', { name: /사업부로 이동/ })).toHaveCount(0);

  await cheongjuPage.goto('/admin/business-unit-access');
  await expect(cheongjuPage.getByRole('heading', { name: '사용자 관리' })).toBeVisible();
  await cheongjuPage.waitForLoadState('networkidle');
  const pendingRow = cheongjuPage.getByRole('row').filter({ hasText: 'Synthetic Cheongju Approval' });
  await expect(pendingRow).toBeVisible();

  let markMutationStarted!: () => void;
  let releaseMutation!: () => void;
  const mutationStarted = new Promise<void>((resolve) => { markMutationStarted = resolve; });
  const mutationReleased = new Promise<void>((resolve) => { releaseMutation = resolve; });
  await cheongjuPage.route(`**/api/admin/user-access/users/${pendingUserId}/access`, async (route) => {
    const response = await route.fetch();
    markMutationStarted();
    await mutationReleased;
    await route.fulfill({ response });
  });
  const cheongjuMembership = pendingRow.getByRole('checkbox', { name: 'Synthetic Cheongju Approval 활성 상태' });
  const saveMembership = pendingRow.getByRole('button', { name: '승인' });
  await expect(cheongjuMembership).not.toBeChecked();
  await cheongjuMembership.check();
  await pendingRow.getByRole('combobox', { name: 'Synthetic Cheongju Approval 부서' })
    .selectOption('10000000-0000-0000-0000-000000000005');
  await expect(pendingRow.getByLabel('Synthetic Cheongju Approval 역할')).toContainText('Quality User');
  await expect(cheongjuMembership).toBeChecked();
  await expect(saveMembership).toBeEnabled();
  await saveMembership.click();
  await mutationStarted;
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toBeDisabled();
  releaseMutation();
  await expect(cheongjuPage.getByRole('status').filter({ hasText: '사용자 접근 정보를 저장했습니다.' })).toBeVisible();
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toBeEnabled();

  const osanContext = await browser.newContext();
  await osanContext.addInitScript(() => {
    window.localStorage.setItem('emi-qms-development-user-key', 'dev-admin');
  });
  const osanPage = await osanContext.newPage();
  await osanPage.goto('/');
  const osanSelector = osanPage.locator('select[aria-label="사업부 선택"]:visible');
  await expect(osanSelector).toHaveValue('CHEONGJU');
  await osanSelector.selectOption('OSAN');
  await expect(osanSelector).toHaveValue('OSAN');
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('CHEONGJU');

  await cheongjuPage.evaluate(async () => {
    const api = await import('/src/api.ts');
    api.resetBusinessUnitRequestContext(true);
  });
  await expect.poll(() => cheongjuPage.evaluate(() => window.sessionStorage.getItem('emi.qms.business-unit')))
    .toBe('CHEONGJU');
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('CHEONGJU');
  await expect(cheongjuPage.getByRole('button', { name: /사업부로 이동/ })).toHaveCount(0);

  await Promise.all([cheongjuContext.close(), osanContext.close()]);
});
