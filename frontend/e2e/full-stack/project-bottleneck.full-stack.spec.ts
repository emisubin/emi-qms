import { expect, test, type APIRequestContext } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;

test('TASK-007B: server sorting, Pending drill-down, and project bottleneck detail stay connected', async ({ page, request }) => {
  const unique = Date.now();
  const commonPrefix = `Bottleneck Synthetic ${unique}`;
  const blockedTitle = `${commonPrefix} Blocked`;
  const clearTitle = `${commonPrefix} Clear`;
  const clearProjectId = await createProject(request, `BOT-C-${unique}`, clearTitle, 2);
  const blockedProjectId = await createProject(request, `BOT-B-${unique}`, blockedTitle, 3);
  await createPending(request, blockedProjectId, `Urgent blocker ${unique}`);

  const projectsResponse = await request.get(
    `${apiBaseUrl}/api/projects?search=${encodeURIComponent(commonPrefix)}&pageSize=100`,
    { headers: { 'X-Dev-User': 'dev-quality' } }
  );
  expect(projectsResponse.ok()).toBeTruthy();
  const projects = await projectsResponse.json() as {
    items: Array<{
      projectId: string;
      bottleneck: {
        kind: string;
        label: string;
        nextAction: string;
        openPendingCount: number;
        urgentPendingCount: number;
        panelDistribution: Array<{ stageCode: string; panelCount: number }>;
      };
    }>;
  };
  expect(projects.items.map((project) => project.projectId)).toEqual([blockedProjectId, clearProjectId]);
  expect(projects.items[0].bottleneck).toMatchObject({
    kind: 'ProjectStage',
    label: '생산관리 단계',
    nextAction: 'Pending',
    openPendingCount: 1,
    urgentPendingCount: 1
  });
  expect(projects.items[0].bottleneck.panelDistribution).toHaveLength(7);

  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(commonPrefix);
  await page.getByRole('button', { name: '검색' }).click();

  const projectTable = page.getByRole('table', { name: '프로젝트 목록' });
  const blockedRow = projectTable.locator('.project-list-row').filter({ hasText: blockedTitle });
  const clearRow = projectTable.locator('.project-list-row').filter({ hasText: clearTitle });
  await expect(blockedRow).toContainText('병목 구간 · 생산관리 단계');
  await expect(blockedRow).toContainText('open 1 · 재검사 0 · 긴급 1');
  await expect(blockedRow.getByRole('button', { name: 'Pending 확인' })).toBeVisible();
  expect(await blockedRow.evaluate((node) => Array.from(node.parentElement?.children ?? []).indexOf(node))).toBeLessThan(
    await clearRow.evaluate((node) => Array.from(node.parentElement?.children ?? []).indexOf(node))
  );

  await blockedRow.getByRole('button', { name: 'Pending 확인' }).click();
  await expect(page).toHaveURL(new RegExp(`/pending\\?projectId=${blockedProjectId}$`));
  await expect(page.getByRole('button', { name: `Urgent blocker ${unique}` })).toBeVisible();

  await page.goto('/projects');
  await page.getByLabel('개발 사용자').selectOption('dev-quality');
  await page.getByPlaceholder('고객사, Item, PJT Code, PJT Title 검색').fill(blockedTitle);
  await page.getByRole('button', { name: '검색' }).click();
  await page.getByRole('table', { name: '프로젝트 목록' }).locator('.project-list-row').filter({ hasText: blockedTitle }).click();

  const bottleneckOverview = page.getByLabel('프로젝트 병목 현황');
  await expect(bottleneckOverview).toContainText('다음 확인 대상');
  await expect(bottleneckOverview).toContainText('생산관리 단계');
  await expect(bottleneckOverview).toContainText('열린 Pending을 먼저 표시합니다.');
  await expect(bottleneckOverview.getByRole('button', { name: '프로젝트 Pending 열기' })).toBeVisible();
  await expect(bottleneckOverview.getByRole('button', { name: /제조 전 패널 3면/ })).toBeVisible();
  await expect(bottleneckOverview.getByRole('button', { name: /납품 완료 패널 0면/ })).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('.app-shell')).toHaveAttribute('data-layout-mode', 'mobile');
  await expect(bottleneckOverview).toBeVisible();
  const overflowState = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    offenders: Array.from(document.querySelectorAll<HTMLElement>('body *'))
      .map((element) => {
        const rect = element.getBoundingClientRect();
        return {
          selector: `${element.tagName.toLowerCase()}.${element.className}`,
          left: Math.round(rect.left),
          right: Math.round(rect.right),
          width: Math.round(rect.width)
        };
      })
      .filter((item) => item.right > document.documentElement.clientWidth + 1)
      .slice(0, 12)
  }));
  expect(overflowState.overflow, JSON.stringify(overflowState.offenders)).toBe(0);
});

async function createProject(
  request: APIRequestContext,
  projectCode: string,
  projectTitle: string,
  panelCount: number
) {
  const response = await request.post(`${apiBaseUrl}/api/projects`, {
    headers: { 'X-Dev-User': 'dev-sales' },
    data: {
      customerName: 'Synthetic Customer',
      item: 'RPP',
      projectCode,
      projectTitle,
      panelCount,
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

async function createPending(request: APIRequestContext, projectId: string, title: string) {
  const response = await request.post(`${apiBaseUrl}/api/pending`, {
    headers: { 'X-Dev-User': 'dev-quality' },
    data: {
      projectId,
      issueType: 'ManufacturingStop',
      title,
      description: 'Synthetic TASK-007B blocker for isolated E2E validation.',
      priority: 'Urgent',
      actionDepartmentCode: 'production-planning'
    }
  });
  expect(response.ok()).toBeTruthy();
}
