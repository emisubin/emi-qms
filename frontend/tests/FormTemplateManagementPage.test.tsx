import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { FormTemplateManagementPage } from '../src/FormTemplateManagementPage';

const activeVersion = {
  versionId: '81000000-0000-0000-0000-000000000001',
  versionNumber: 1,
  displayName: '자재 수입검사 v1',
  lifecycleStatus: 'Active' as const,
  rowVersion: 1,
  activatedAtUtc: '2026-07-01T00:00:00Z',
  archivedAtUtc: null,
  items: [{
    itemId: '82000000-0000-0000-0000-000000000001',
    itemCode: 'VISUAL_CHECK',
    displayOrder: 1,
    label: '외관 확인',
    guidance: '외관 상태를 확인해 주세요.',
    responseType: 'Check' as const,
    isRequired: true,
    requiresPhoto: false,
    maxTextLength: null
  }]
};

describe('FormTemplateManagementPage', () => {
  beforeEach(() => setRuntimeMutationAllowed(true));

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('keeps edit and save visible and opens a safe draft before saving', async () => {
    const calls: Array<{ path: string; method: string }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://localhost');
      const method = init?.method ?? 'GET';
      calls.push({ path: url.pathname, method });
      if (url.pathname === '/api/form-templates' && method === 'GET') {
        return json({ templates: [{ family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 }] });
      }
      if (url.pathname.endsWith('/versions') && method === 'POST') {
        return json(versionsResponse([draftVersion(), activeVersion]));
      }
      if (url.pathname.endsWith('/items') && method === 'PUT') {
        return json(versionsResponse([draftVersion(3), activeVersion]));
      }
      return json(versionsResponse([activeVersion]));
    }));

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator />);

    const editButton = await screen.findByRole('button', { name: '편집' });
    const saveButton = screen.getByRole('button', { name: '저장' });
    expect(editButton).toBeEnabled();
    expect(saveButton).toBeDisabled();

    fireEvent.click(editButton);
    await waitFor(() => expect(screen.getByRole('button', { name: '저장' })).toBeEnabled());
    expect(screen.getByRole('textbox', { name: '항목명' })).toBeEnabled();

    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    await screen.findByText('초안 항목을 저장했습니다.');
    expect(calls).toEqual(expect.arrayContaining([
      { path: '/api/form-templates/IqcReport/MATERIAL_IQC/versions', method: 'POST' },
      { path: '/api/form-templates/IqcReport/MATERIAL_IQC/versions/81000000-0000-0000-0000-000000000002/items', method: 'PUT' }
    ]));
  });

  it('lists production control templates inside the catalog and expands one plan item at a time', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://localhost');
      const method = init?.method ?? 'GET';
      if (url.pathname === '/api/form-templates' && method === 'GET') {
        return json({ templates: [{ family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 }] });
      }
      if (url.pathname === '/api/production-control/templates' && method === 'GET') {
        return json(productionControlCatalog(false));
      }
      if (url.pathname.endsWith('/production-control/templates/planning/product-ul67/drafts') && method === 'POST') {
        return json(productionControlCatalog(true));
      }
      return json(versionsResponse([activeVersion]));
    }));

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator />);

    const catalog = await screen.findByRole('navigation', { name: '양식 종류' });
    expect(catalog).toHaveTextContent('Item별 제조 양식');
    expect(catalog).toHaveTextContent('생산계획·실적 연결');
    expect(screen.queryByRole('tablist', { name: '양식 관리 영역' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /생산계획·실적 연결/ }));
    expect(await screen.findByRole('combobox', { name: '적용 Item' })).toHaveValue('product-ul67');

    const firstSummary = await screen.findByRole('button', { name: /자재 준비.*실적 연결 1개/ });
    const secondSummary = screen.getByRole('button', { name: /제조 착수.*실적 연결 2개/ });
    expect(firstSummary).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('textbox', { name: '1번 계획 항목명' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '편집' }));
    expect(await screen.findByRole('textbox', { name: '1번 계획 항목명' })).toBeEnabled();
    expect(screen.queryByRole('textbox', { name: '2번 계획 항목명' })).not.toBeInTheDocument();

    fireEvent.click(secondSummary);
    expect(await screen.findByRole('textbox', { name: '2번 계획 항목명' })).toBeEnabled();
    expect(screen.queryByRole('textbox', { name: '1번 계획 항목명' })).not.toBeInTheDocument();
  });
});

function draftVersion(rowVersion = 2) {
  return {
    ...activeVersion,
    versionId: '81000000-0000-0000-0000-000000000002',
    versionNumber: 2,
    displayName: '자재 수입검사 v2',
    lifecycleStatus: 'Draft' as const,
    rowVersion,
    activatedAtUtc: null
  };
}

function versionsResponse(versions: Array<typeof activeVersion | ReturnType<typeof draftVersion>>) {
  return { family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', versions };
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function productionControlCatalog(withDraft: boolean) {
  const manufacturingVersion = {
    versionId: 'manufacturing-v1',
    versionNumber: 1,
    lifecycleStatus: 'Active' as const,
    rowVersion: 1,
    activatedAtUtc: '2026-07-01T00:00:00Z',
    archivedAtUtc: null,
    items: [
      { definitionKey: 'manufacturing-step-1', displayOrder: 1, label: '작업지시 확인', stepRole: 'General' as const },
      { definitionKey: 'manufacturing-step-2', displayOrder: 2, label: '조립', stepRole: 'Assembly' as const }
    ]
  };
  const planItems = [
    {
      definitionKey: 'plan-step-1',
      displayOrder: 1,
      label: '자재 준비',
      isRequired: true,
      connections: [{ sourceCode: 'PROCUREMENT_ORDERED', sourceDefinitionKey: null }]
    },
    {
      definitionKey: 'plan-step-2',
      displayOrder: 2,
      label: '제조 착수',
      isRequired: true,
      connections: [
        { sourceCode: 'MANUFACTURING_STEP_COMPLETED', sourceDefinitionKey: 'manufacturing-step-1' },
        { sourceCode: 'LQC_STEP_PASSED', sourceDefinitionKey: 'manufacturing-step-1' }
      ]
    }
  ];
  const activePlan = {
    versionId: 'planning-v1',
    versionNumber: 1,
    lifecycleStatus: 'Active' as const,
    rowVersion: 1,
    activatedAtUtc: '2026-07-01T00:00:00Z',
    archivedAtUtc: null,
    items: planItems
  };
  const draftPlan = {
    ...activePlan,
    versionId: 'planning-v2',
    versionNumber: 2,
    lifecycleStatus: 'Draft' as const,
    rowVersion: 2,
    activatedAtUtc: null
  };
  return {
    canManageManufacturing: true,
    canManageProductionPlanning: true,
    sources: [
      { code: 'PROCUREMENT_ORDERED', departmentLabel: '구매', label: '발주 완료', requiresManufacturingDefinition: false },
      { code: 'MANUFACTURING_STEP_COMPLETED', departmentLabel: '제조', label: '제조 단계 완료', requiresManufacturingDefinition: true },
      { code: 'LQC_STEP_PASSED', departmentLabel: '품질', label: 'LQC 합격', requiresManufacturingDefinition: true }
    ],
    items: [{
      productTypeId: 'product-ul67',
      productTypeCode: 'UL67',
      productTypeName: 'UL67',
      manufacturingVersions: [manufacturingVersion],
      planVersions: withDraft ? [draftPlan, activePlan] : [activePlan]
    }]
  };
}
