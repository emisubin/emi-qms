import { expect, test } from '@playwright/test';

const backendUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const requestHeaders = (businessUnit: 'CHEONGJU' | 'OSAN') => ({
  'X-Dev-User': businessUnit === 'OSAN' ? 'dev-sales' : 'dev-admin',
  'X-Qms-Business-Unit': businessUnit
});

test('isolated three-database runtime creates, lists, and reads an Osan project without changing Cheongju', async ({ page, request }) => {
  const consoleErrors: string[] = [];
  const requestFailures: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('requestfailed', (failedRequest) => {
    if (failedRequest.failure()?.errorText !== 'net::ERR_ABORTED') {
      requestFailures.push(`${failedRequest.method()} ${new URL(failedRequest.url()).pathname}`);
    }
  });

  await page.addInitScript(() => {
    window.localStorage.setItem('emi-qms-development-user-key', 'dev-sales');
  });

  const cheongjuBefore = await request.get(`${backendUrl}/api/projects`, {
    headers: requestHeaders('CHEONGJU')
  });
  expect(cheongjuBefore.status()).toBe(200);
  const cheongjuBeforeBody = await cheongjuBefore.text();

  const rejectedCheongjuCreate = await request.post(`${backendUrl}/api/osan/projects`, {
    headers: requestHeaders('CHEONGJU'),
    data: {
      title: 'Rejected Cheongju Project',
      projectCode: 'CJ-REJECTED',
      customerName: 'Synthetic Customer',
      poNumber: null,
      workOrderNumber: null,
      deliveryDate: '2026-10-31',
      productName: 'Synthetic Product',
      quantity: 1,
      operationId: '11111111-1111-1111-1111-111111111111'
    }
  });
  expect(rejectedCheongjuCreate.status()).toBe(403);

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '오산 홈' })).toBeVisible();

  await page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '프로젝트' }).click();
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  await expect(page.getByText('등록된 프로젝트가 없습니다.')).toBeVisible();
  await page.getByRole('button', { name: '신규 프로젝트' }).first().click();

  await page.getByLabel('장비명').fill('  오산 통합 프로젝트  ');
  await page.getByLabel('프로젝트 코드').fill('  OSAN  001  ');
  await page.getByLabel('거래처').fill('  테스트 거래처  ');
  await page.getByLabel('PO No').fill('  001-PO/+  ');
  await page.getByLabel('W/O No').fill('  000-W/O  ');
  await page.getByLabel('납기일').fill('2026-10-31');
  await page.getByLabel('part분류').fill('  전원장치 A  ');
  await page.getByLabel('수량').fill('2');
  await page.getByRole('button', { name: '프로젝트 등록' }).click();

  await expect(page.getByRole('heading', { name: '오산 통합 프로젝트' })).toBeVisible();
  const summary = page.getByRole('region', { name: '프로젝트 기본 정보' });
  await expect(summary).toBeVisible();
  await expect(summary.getByRole('table')).toHaveCount(0);
  await expect(page.getByRole('tablist', { name: '프로젝트 상세 섹션' })).toHaveCount(0);
  await expect(summary.getByText('OSAN  001', { exact: true })).toBeVisible();
  await expect(page.getByText('001-PO/+', { exact: true })).toBeVisible();
  await expect(page.getByText('000-W/O', { exact: true })).toBeVisible();
  const targetTable = page.getByRole('table', { name: '진행 관리 대상 현황' });
  const targetRows = targetTable.getByRole('row');
  await expect(targetRows).toHaveCount(3);
  for (const target of [targetRows.nth(1), targetRows.nth(2)]) {
    await expect(target).toContainText('시작 전');
    await expect(target).toContainText('0/7단계 완료');
  }

  await targetRows.nth(1).press('Enter');
  await expect(page).toHaveURL(/\/progress\?projectId=[^&]+&targetId=/);
  await expect(page.getByRole('navigation', { name: '진행 단계' })).toBeVisible();
  await page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '프로젝트' }).click();
  await expect(page.getByRole('heading', { name: '프로젝트 목록' })).toBeVisible();
  const projectRow = page.getByTestId('osan-project-list-desktop').getByRole('row', { name: '오산 통합 프로젝트 상세 열기' });
  await expect(projectRow.locator('.project-code-value')).toHaveText('OSAN  001');

  const osanList = await request.get(`${backendUrl}/api/osan/projects`, {
    headers: requestHeaders('OSAN')
  });
  expect(osanList.status()).toBe(200);
  const osanItems = (await osanList.json()) as { items: Array<{ projectId: string; projectCode: string }> };
  expect(osanItems.items).toHaveLength(1);
  expect(osanItems.items[0].projectCode).toBe('OSAN  001');

  const osanDetail = await request.get(`${backendUrl}/api/osan/projects/${osanItems.items[0].projectId}`, {
    headers: requestHeaders('OSAN')
  });
  expect(osanDetail.status()).toBe(200);
  const detail = (await osanDetail.json()) as { targets: Array<{ steps: unknown[] }> };
  expect(detail.targets).toHaveLength(2);
  expect(detail.targets.every((target) => target.steps.length === 7)).toBe(true);

  const cheongjuAfter = await request.get(`${backendUrl}/api/projects`, {
    headers: requestHeaders('CHEONGJU')
  });
  expect(cheongjuAfter.status()).toBe(200);
  expect(await cheongjuAfter.text()).toBe(cheongjuBeforeBody);
  expect(consoleErrors).toEqual([]);
  expect(requestFailures).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});
