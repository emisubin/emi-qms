import { expect, test } from '@playwright/test';

const pendingUserId = '71000000-0000-0000-0000-000000000003';

test('three databases keep tab context, integrated user-access mutation locks, and reset isolated', async ({ browser }) => {
  const implicitContext = await browser.newContext();
  await implicitContext.addInitScript(() => {
    window.localStorage.setItem('emi-qms-development-user-key', 'dev-sales');
  });
  const implicitPage = await implicitContext.newPage();
  const implicitMeHeaders: Array<string | undefined> = [];
  implicitPage.on('request', (request) => {
    if (new URL(request.url()).pathname === '/api/me') {
      implicitMeHeaders.push(request.headers()['x-qms-business-unit']);
    }
  });
  await implicitPage.goto('/');
  await expect.poll(() => implicitPage.evaluate(() => window.sessionStorage.getItem('emi.qms.business-unit')))
    .toBe('CHEONGJU');
  await expect.poll(() => implicitMeHeaders.length).toBeGreaterThanOrEqual(2);
  expect(implicitMeHeaders[0]).toBeUndefined();
  expect(implicitMeHeaders.at(-1)).toBe('CHEONGJU');
  await implicitContext.close();

  const cheongjuContext = await browser.newContext();
  await cheongjuContext.addInitScript(() => {
    window.localStorage.setItem('emi-qms-development-user-key', 'dev-admin');
  });
  const cheongjuPage = await cheongjuContext.newPage();
  await cheongjuPage.goto('/');
  await expect(cheongjuPage.getByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).toBeVisible();
  await cheongjuPage.getByRole('button', { name: '청주 사업부로 이동' }).click();
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('CHEONGJU');

  await cheongjuPage.goto('/admin/business-unit-access');
  await expect(cheongjuPage.getByRole('heading', { name: '사용자 관리' })).toBeVisible();
  await cheongjuPage.waitForLoadState('networkidle');
  const pendingCard = cheongjuPage.locator('article').filter({ hasText: 'Synthetic Pending User' });
  await expect(pendingCard).toBeVisible();

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
  const cheongjuProfile = pendingCard.getByRole('group').filter({ hasText: '청주' });
  const cheongjuMembership = cheongjuProfile.getByRole('checkbox', { name: '소속·활성' });
  const saveMembership = pendingCard.getByRole('button', { name: '사용자 저장' });
  await expect(cheongjuMembership).not.toBeChecked();
  await cheongjuMembership.check();
  await cheongjuProfile.getByRole('combobox', { name: '부서' })
    .selectOption('10000000-0000-0000-0000-000000000005');
  await cheongjuProfile.getByRole('checkbox', { name: 'Quality User' }).check();
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
  await expect(osanPage.getByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).toBeVisible();
  await osanPage.getByRole('button', { name: '오산 사업부로 이동' }).click();
  await expect(osanPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('OSAN');
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('CHEONGJU');

  await cheongjuPage.evaluate(async () => {
    const api = await import('/src/api.ts');
    api.resetBusinessUnitRequestContext(true);
  });
  await expect(cheongjuPage.getByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).toBeVisible();
  expect(await cheongjuPage.evaluate(() => window.sessionStorage.getItem('emi.qms.business-unit'))).toBeNull();
  await cheongjuPage.getByRole('button', { name: '청주 사업부로 이동' }).click();

  await Promise.all([cheongjuContext.close(), osanContext.close()]);
});
