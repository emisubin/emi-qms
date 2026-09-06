import { expect, test } from '@playwright/test';

const adminUserId = '50000000-0000-0000-0000-000000000001';
const salesUserId = '50000000-0000-0000-0000-000000000002';
const backendUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;

test('three databases keep tab context, stale reads, mutation locks, reset, and revocation isolated', async ({ browser }) => {
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
  const cheongjuPage = await cheongjuContext.newPage();
  await cheongjuPage.goto('/');
  await expect(cheongjuPage.getByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).toBeVisible();
  await cheongjuPage.getByRole('button', { name: '청주 사업부로 이동' }).click();
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('CHEONGJU');

  await cheongjuPage.goto('/admin/business-unit-access');
  await expect(cheongjuPage.getByRole('heading', { name: '사업부 소속 관리' })).toBeVisible();
  await cheongjuPage.waitForLoadState('networkidle');
  const salesCard = cheongjuPage.locator('article').filter({ hasText: 'Synthetic Sales User' });
  await expect(salesCard).toBeVisible();

  let markMutationStarted!: () => void;
  let releaseMutation!: () => void;
  const mutationStarted = new Promise<void>((resolve) => { markMutationStarted = resolve; });
  const mutationReleased = new Promise<void>((resolve) => { releaseMutation = resolve; });
  await cheongjuPage.route(`**/api/admin/business-unit-access/users/${salesUserId}/memberships`, async (route) => {
    const response = await route.fetch();
    markMutationStarted();
    await mutationReleased;
    await route.fulfill({ response });
  });
  const osanMembership = salesCard.getByRole('checkbox', { name: '오산' });
  const saveMembership = salesCard.getByRole('button', { name: '소속 저장' });
  await expect(osanMembership).not.toBeChecked();
  await osanMembership.check();
  await expect(osanMembership).toBeChecked();
  await expect(saveMembership).toBeEnabled();
  await saveMembership.click();
  await mutationStarted;
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toBeDisabled();
  releaseMutation();
  await expect(cheongjuPage.getByRole('status').filter({ hasText: '사업부 소속을 저장했습니다.' })).toBeVisible();
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toBeEnabled();

  const osanContext = await browser.newContext();
  const osanPage = await osanContext.newPage();
  await osanPage.goto('/');
  await expect(osanPage.getByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).toBeVisible();
  await osanPage.getByRole('button', { name: '오산 사업부로 이동' }).click();
  await expect(osanPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('OSAN');
  await expect(cheongjuPage.locator('select[aria-label="사업부 선택"]:visible')).toHaveValue('CHEONGJU');

  await Promise.all([
    cheongjuPage.goto('/admin/users'),
    osanPage.goto('/admin/users')
  ]);
  await expect(cheongjuPage.getByText('Cheongju Boundary Profile')).toBeVisible();
  await expect(cheongjuPage.getByText('Osan Boundary Profile')).toHaveCount(0);
  await expect(osanPage.getByText('Osan Boundary Profile')).toBeVisible();
  await expect(osanPage.getByText('Cheongju Boundary Profile')).toHaveCount(0);

  let markSlowReadStarted!: () => void;
  let releaseSlowRead!: () => void;
  const slowReadStarted = new Promise<void>((resolve) => { markSlowReadStarted = resolve; });
  const slowReadReleased = new Promise<void>((resolve) => { releaseSlowRead = resolve; });
  let holdNextCheongjuRead = true;
  await cheongjuPage.route('**/api/admin/users', async (route) => {
    if (holdNextCheongjuRead
      && route.request().method() === 'GET'
      && route.request().headers()['x-qms-business-unit'] === 'CHEONGJU') {
      holdNextCheongjuRead = false;
      const response = await route.fetch();
      markSlowReadStarted();
      await slowReadReleased;
      try {
        await route.fulfill({ response });
      } catch {
        // The application aborts this route when the tab changes business context.
      }
      return;
    }
    await route.continue();
  });
  await cheongjuPage.getByRole('button', { name: '새로고침' }).click();
  await slowReadStarted;
  await cheongjuPage.locator('select[aria-label="사업부 선택"]:visible').selectOption('OSAN');
  await expect(cheongjuPage.getByRole('heading', { name: '오산 사업부 홈' })).toBeVisible();
  releaseSlowRead();
  await cheongjuPage.goto('/admin/users');
  await expect(cheongjuPage.getByText('Osan Boundary Profile')).toBeVisible();
  await expect(cheongjuPage.getByText('Cheongju Boundary Profile')).toHaveCount(0);

  await cheongjuPage.evaluate(async () => {
    const api = await import('/src/api.ts');
    api.resetBusinessUnitRequestContext(true);
  });
  await expect(cheongjuPage.getByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).toBeVisible();
  expect(await cheongjuPage.evaluate(() => window.sessionStorage.getItem('emi.qms.business-unit'))).toBeNull();
  await cheongjuPage.getByRole('button', { name: '청주 사업부로 이동' }).click();

  const revoke = await cheongjuContext.request.put(
    `${backendUrl}/api/admin/business-unit-access/users/${adminUserId}/memberships`,
    {
      headers: {
        'X-Dev-User': 'dev-admin',
        'X-Qms-Business-Unit': 'CHEONGJU'
      },
      data: { businessUnitCodes: ['CHEONGJU'] }
    });
  expect(revoke.status()).toBe(200);

  await expect(osanPage.getByText('Osan Boundary Profile')).toBeVisible();
  await osanPage.getByRole('button', { name: '새로고침' }).click();
  await expect(osanPage.getByRole('heading', { name: '이 계정으로 선택할 수 없는 사업부입니다.' })).toBeVisible();
  expect(await osanPage.evaluate(() => window.sessionStorage.getItem('emi.qms.business-unit'))).toBeNull();
  await expect(osanPage.getByText('Osan Boundary Profile')).toHaveCount(0);
  await osanPage.getByRole('button', { name: '청주 사업부로 이동' }).click();
  await expect.poll(() => osanPage.evaluate(() => window.sessionStorage.getItem('emi.qms.business-unit')))
    .toBe('CHEONGJU');
  await expect(osanPage.getByRole('heading', { name: '이 계정으로 선택할 수 없는 사업부입니다.' })).toHaveCount(0);

  await Promise.all([cheongjuContext.close(), osanContext.close()]);
});
