import { StrictMode } from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { ProductionControlTemplateWorkspace } from '../src/ProductionControlTemplateWorkspace';
import { getProductionControlTemplateCatalog, saveProductionControlManufacturingCurrent } from '../src/api';
import type { ProductionControlTemplateCatalog } from '../src/productionControlTemplates';

vi.mock('../src/api', () => ({
  getProductionControlTemplateCatalog: vi.fn(),
  ensureProductionControlCurrent: vi.fn(),
  saveProductionControlManufacturingCurrent: vi.fn(),
  saveProductionControlPlanCurrent: vi.fn()
}));

const catalog: ProductionControlTemplateCatalog = {
  canManageManufacturing: true,
  canManageProductionPlanning: true,
  sources: [],
  items: [{
    productTypeId: 'one', productTypeCode: 'IEC', productTypeName: 'IEC', lqcOperational: true,
    planVersions: [],
    manufacturingVersions: [{
      versionId: 'v1', versionNumber: 1, lifecycleStatus: 'Active', rowVersion: 1,
      activatedAtUtc: null, archivedAtUtc: null,
      items: [{ definitionKey: 'one', displayOrder: 1, label: '기존 항목' }]
    }]
  }]
};

function pending() {
  let resolve!: (value: ProductionControlTemplateCatalog) => void;
  const promise = new Promise<ProductionControlTemplateCatalog>(done => { resolve = done; });
  return { promise, resolve };
}

beforeEach(() => vi.resetAllMocks());

it('StrictMode mounts issue only the current request and preserve fast editing', async () => {
  vi.mocked(getProductionControlTemplateCatalog).mockResolvedValue(catalog);
  render(<StrictMode>
    <ProductionControlTemplateWorkspace developmentUserKey="admin" domain="manufacturing" />
  </StrictMode>);
  fireEvent.click(await screen.findByRole('button', { name: '수정' }));
  fireEvent.change(screen.getByRole('textbox', { name: '1번 제조 항목' }), { target: { value: '입력 유지' } });
  await act(async () => { await Promise.resolve(); });
  expect(getProductionControlTemplateCatalog).toHaveBeenCalledTimes(1);
  expect(screen.getByRole('button', { name: '저장' })).toBeEnabled();
  expect(screen.getByRole('textbox', { name: '1번 제조 항목' })).toHaveValue('입력 유지');
});

it('late previous-user catalog cannot reset the new user editing state', async () => {
  const old = pending();
  vi.mocked(getProductionControlTemplateCatalog).mockImplementation(key =>
    key === 'old' ? old.promise : Promise.resolve(catalog));
  const view = render(<ProductionControlTemplateWorkspace developmentUserKey="old" domain="manufacturing" />);
  await waitFor(() => expect(getProductionControlTemplateCatalog).toHaveBeenCalledWith('old'));
  view.rerender(<ProductionControlTemplateWorkspace developmentUserKey="new" domain="manufacturing" />);
  fireEvent.click(await screen.findByRole('button', { name: '수정' }));
  fireEvent.change(screen.getByRole('textbox', { name: '1번 제조 항목' }), { target: { value: '새 사용자 입력' } });
  await act(async () => old.resolve({ ...catalog, canManageManufacturing: false }));
  expect(screen.getByRole('button', { name: '저장' })).toBeEnabled();
  expect(screen.getByRole('textbox', { name: '1번 제조 항목' })).toHaveValue('새 사용자 입력');
});

it('late manufacturing response cannot replace the selected planning form', async () => {
  const old = pending();
  const planning: ProductionControlTemplateCatalog = {
    ...catalog,
    items: [{
      ...catalog.items[0],
      planVersions: [{
        versionId: 'p1', versionNumber: 1, lifecycleStatus: 'Active', rowVersion: 1,
        activatedAtUtc: null, archivedAtUtc: null,
        items: [{ definitionKey: 'plan', displayOrder: 1, label: '계획', isRequired: true, connections: [] }]
      }]
    }]
  };
  vi.mocked(getProductionControlTemplateCatalog).mockReturnValueOnce(old.promise).mockResolvedValueOnce(planning);
  const view = render(<ProductionControlTemplateWorkspace developmentUserKey="admin" domain="manufacturing" />);
  await waitFor(() => expect(getProductionControlTemplateCatalog).toHaveBeenCalledTimes(1));
  view.rerender(<ProductionControlTemplateWorkspace developmentUserKey="admin" domain="planning" />);
  fireEvent.click(await screen.findByRole('button', { name: '수정' }));
  await act(async () => old.resolve({ ...catalog, canManageProductionPlanning: false }));
  expect(screen.getByRole('button', { name: '저장' })).toBeEnabled();
  expect(screen.getByRole('region', { name: '생산계획 연결 양식' })).toBeInTheDocument();
});

it('successful explicit save still resets editing to the saved catalog', async () => {
  vi.mocked(getProductionControlTemplateCatalog).mockResolvedValue(catalog);
  vi.mocked(saveProductionControlManufacturingCurrent).mockResolvedValue(catalog);
  render(<ProductionControlTemplateWorkspace developmentUserKey="admin" domain="manufacturing" />);
  fireEvent.click(await screen.findByRole('button', { name: '수정' }));
  fireEvent.click(screen.getByRole('button', { name: '저장' }));
  expect(await screen.findByRole('button', { name: '수정' })).toBeEnabled();
  expect(screen.getByRole('textbox', { name: '1번 제조 항목' })).toBeDisabled();
  expect(saveProductionControlManufacturingCurrent).toHaveBeenCalledTimes(1);
});
