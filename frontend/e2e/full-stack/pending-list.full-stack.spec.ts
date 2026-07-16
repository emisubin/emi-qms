import { expect, test, type APIRequestContext } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const productionUserId = '50000000-0000-0000-0000-000000000003';

test('TASK-007A Pending: create, assign, act, reinspect, close, and audit', async ({ page, request }) => {
  const unique = Date.now();
  const projectTitle = `Pending Synthetic ${unique}`;
  const projectId = await createProject(request, `PEND-${unique}`, projectTitle);

  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto('/pending');
  await expect(page.getByRole('heading', { name: 'Pending List' })).toBeVisible();
  await page.getByRole('button', { name: '+ Pending 등록' }).click();
  await page.getByLabel('프로젝트 *').selectOption(projectId);
  await page.getByLabel('유형 *').selectOption('ManufacturingStop');
  await page.getByLabel('긴급도 *').selectOption('Urgent');
  await page.getByLabel('조치 담당').selectOption(productionUserId);
  await page.getByLabel('조치 기한').fill('2026-12-31');
  await page.getByLabel('제목 *').fill('제조 치수 확인 전 작업 중단');
  await page.getByLabel('상세 내용 *').fill('도면 기준과 현장 측정값 차이를 확인할 때까지 제조 작업을 중단합니다.');
  await page.getByRole('button', { name: 'Pending 등록', exact: true }).click();

  await expect(page.getByText('Pending이 등록되었습니다.')).toBeVisible();
  await page.getByRole('button', { name: '제조 치수 확인 전 작업 중단' }).click();
  await expect(page.getByRole('heading', { name: '제조 치수 확인 전 작업 중단' })).toBeVisible();
  await expect(page.getByText('조치 요청', { exact: true })).toBeVisible();
  const detailUrl = page.url();

  const myWorkResponse = await request.get(`${apiBaseUrl}/api/my-work?status=Requested`, {
    headers: { 'X-Dev-User': 'dev-production' }
  });
  expect(myWorkResponse.ok()).toBeTruthy();
  const myWork = await myWorkResponse.json() as { items: Array<{ workItemId: string; title: string; linkUrl: string }> };
  const pendingWorkTitle = 'Pending 조치 · 제조 치수 확인 전 작업 중단';
  const pendingWorkItem = myWork.items.find((item) => item.title === pendingWorkTitle);
  expect(pendingWorkItem).toBeDefined();
  expect(pendingWorkItem?.linkUrl).toBe(new URL(detailUrl).pathname);
  const directStart = await request.post(`${apiBaseUrl}/api/my-work/${pendingWorkItem!.workItemId}/start`, {
    headers: { 'X-Dev-User': 'dev-production' }
  });
  expect(directStart.status()).toBe(409);

  await page.getByLabel('개발 사용자').selectOption('dev-production');
  await page.goto('/my-work');
  const pendingWorkRow = page.getByRole('row').filter({ hasText: pendingWorkTitle });
  await expect(pendingWorkRow.getByRole('button', { name: 'Pending 열기' })).toBeVisible();
  await expect(pendingWorkRow.getByRole('button', { name: '작업 완료' })).toHaveCount(0);
  await pendingWorkRow.getByRole('button', { name: 'Pending 열기' }).click();
  await expect(page.getByText('조치 요청', { exact: true })).toBeVisible();
  await page.getByLabel('상태 변경 사유').fill('도면과 측정값 비교를 시작합니다.');
  await page.getByRole('button', { name: '조치 시작' }).click();
  await expect(page.getByText('조치 중', { exact: true })).toBeVisible();
  await page.getByLabel('새 코멘트').fill('현장 측정값을 재확인했고 설계 기준과 비교 중입니다.');
  await page.getByRole('button', { name: '코멘트 등록' }).click();
  await expect(page.getByText('현장 측정값을 재확인했고 설계 기준과 비교 중입니다.')).toBeVisible();
  await page.getByLabel('상태 변경 사유').fill('조치 완료 후 재검사를 요청합니다.');
  await page.getByRole('button', { name: '재검사 요청' }).click();
  await expect(page.getByText('재검사 요청 상태로 변경되었습니다.')).toBeVisible();
  await expect(page.locator('.pending-detail-header .status-badge').filter({ hasText: '재검사 요청' })).toBeVisible();

  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto(detailUrl);
  await page.getByLabel('상태 변경 사유').fill('재검사 적합을 확인했습니다.');
  await page.getByRole('button', { name: '종결' }).click();
  await expect(page.getByText('종결 상태로 변경되었습니다.')).toBeVisible();
  await expect(page.locator('.pending-detail-header .status-badge').filter({ hasText: '종결' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '변경 이력' })).toBeVisible();

  const viewerMutation = await request.post(`${apiBaseUrl}/api/pending`, {
    headers: { 'X-Dev-User': 'dev-viewer' },
    data: {
      projectId,
      issueType: 'Other',
      title: '권한 차단 확인',
      description: '읽기 전용 사용자의 생성 요청은 서버에서 거부되어야 합니다.',
      priority: 'Normal'
    }
  });
  expect(viewerMutation.status()).toBe(403);

  const adminMutation = await request.post(`${apiBaseUrl}/api/pending`, {
    headers: { 'X-Dev-User': 'dev-admin' },
    data: {
      projectId,
      issueType: 'Other',
      title: '관리자 우회 차단 확인',
      description: '관리자는 감사 조회만 가능하고 업무 생성은 거부되어야 합니다.',
      priority: 'Normal'
    }
  });
  expect(adminMutation.status()).toBe(403);
});

test('TASK-007A Pending: 390px workspace has no page overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.goto('/pending');
  await expect(page.getByRole('heading', { name: 'Pending List' })).toBeVisible();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
});

async function createProject(request: APIRequestContext, projectCode: string, projectTitle: string) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      customerName: 'Synthetic Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount: 1,
      deliveryDate: '2026-12-31',
      salesOwnerUserId: '50000000-0000-0000-0000-000000000002',
      packagingMethod: 'WoodenCrate',
      salesAmount: 1000,
      currencyCode: 'KRW',
      deliveryLocation: 'Synthetic Site',
      fatRequired: false
    }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { projectId: string }).projectId;
}
