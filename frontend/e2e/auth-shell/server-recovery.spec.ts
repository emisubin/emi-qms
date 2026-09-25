import { expect, test } from '@playwright/test';

test('@desktop @mobile server expiry reloads the document once, preserving query and fragment', async ({ page }) => {
  let documentRequests = 0;
  const mutations: string[] = [];
  page.on('request', (request) => {
    if (request.isNavigationRequest() && request.frame() === page.mainFrame()) documentRequests++;
    if (!['GET', 'HEAD', 'OPTIONS'].includes(request.method())) mutations.push(request.method());
  });
  await page.goto('/e2e/auth-shell/status.html?state=server-recovery&projectId=synthetic#stage-3');
  // A failed return must stop at manual recovery rather than reload forever.
  await expect(page.getByRole('button', { name: '로그인', exact: true })).toBeVisible();
  expect(documentRequests).toBe(2);
  expect(new URL(page.url()).searchParams.get('projectId')).toBe('synthetic');
  expect(new URL(page.url()).hash).toBe('#stage-3');
  expect(mutations).toEqual([]);
});
