import { expect, type APIRequestContext } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;

type LqcItemTemplate = {
  productTypeId: string;
  productTypeCode: string;
  isOperational: boolean;
  settingRowVersion: number;
};

/**
 * Legacy LQC regression scenarios opt their item in before project creation.
 * The project then captures the same immutable LQC snapshot as production.
 */
export async function ensureLqcOperational(
  request: APIRequestContext,
  productTypeCode = 'RPP'
) {
  const catalogResponse = await request.get(`${apiBaseUrl}/api/form-templates/lqc-items`, {
    headers: devAdminHeaders()
  });
  expect(catalogResponse.ok(), await catalogResponse.text()).toBeTruthy();

  const catalog = await catalogResponse.json() as { items: LqcItemTemplate[] };
  const item = catalog.items.find((candidate) => candidate.productTypeCode === productTypeCode);
  expect(item, `LQC item setting not found: ${productTypeCode}`).toBeDefined();
  if (!item || item.isOperational) return;

  const updateResponse = await request.put(
    `${apiBaseUrl}/api/form-templates/lqc-items/${item.productTypeId}/operating-status`,
    {
      headers: devAdminHeaders(),
      data: { isOperational: true, expectedRowVersion: item.settingRowVersion }
    }
  );
  expect(updateResponse.ok(), await updateResponse.text()).toBeTruthy();
}

function devAdminHeaders() {
  return { 'X-Dev-User': 'dev-admin' };
}
