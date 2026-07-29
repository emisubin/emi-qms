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
    maxTextLength: null,
    definitionKey: '82000000-0000-0000-0000-000000000001'
  }]
};

describe('FormTemplateManagementPage', () => {
  beforeEach(() => setRuntimeMutationAllowed(true));

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('edits and saves the one current inspection form without version controls', async () => {
    const calls: Array<{ path: string; method: string }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://localhost');
      const method = init?.method ?? 'GET';
      calls.push({ path: url.pathname, method });
      if (url.pathname === '/api/form-templates' && method === 'GET') {
        return json({ templates: [{ family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 }] });
      }
      if (url.pathname.endsWith('/current') && method === 'PUT') {
        return json(versionsResponse([{ ...activeVersion, rowVersion: 2 }]));
      }
      return json(versionsResponse([activeVersion]));
    }));

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator />);

    const editButton = await screen.findByRole('button', { name: '수정' });
    expect(editButton).toBeEnabled();
    expect(screen.queryByRole('button', { name: '저장' })).not.toBeInTheDocument();
    expect(screen.queryByText(/^v1/)).not.toBeInTheDocument();

    fireEvent.click(editButton);
    await waitFor(() => expect(screen.getByRole('button', { name: '저장' })).toBeEnabled());
    expect(screen.getByRole('textbox', { name: '항목명' })).toBeEnabled();

    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    await screen.findByText(/현재 양식을 저장했습니다/);
    expect(calls).toEqual(expect.arrayContaining([
      { path: '/api/form-templates/IqcReport/MATERIAL_IQC/current', method: 'GET' },
      { path: '/api/form-templates/IqcReport/MATERIAL_IQC/current', method: 'PUT' }
    ]));
  });

  it('edits one current production plan and matches each item to one result with a dropdown', async () => {
    const calls: Array<{ path: string; method: string; body: unknown }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://localhost');
      const method = init?.method ?? 'GET';
      calls.push({ path: url.pathname, method, body: init?.body ? JSON.parse(String(init.body)) : null });
      if (url.pathname === '/api/form-templates' && method === 'GET') {
        return json({ templates: [{ family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 }] });
      }
      if (url.pathname === '/api/production-control/templates' && method === 'GET') {
        return json(productionControlCatalog(false));
      }
      if (url.pathname === '/api/production-control/templates/planning/product-ul67/versions/planning-v1' && method === 'PUT') {
        return json(productionControlCatalog(false));
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

    const firstSummary = await screen.findByRole('button', { name: /자재 준비.*연결 실적/ });
    const secondSummary = screen.getByRole('button', { name: /제조 착수.*연결 실적/ });
    expect(firstSummary).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('textbox', { name: '1번 계획 항목명' })).not.toBeInTheDocument();
    expect(screen.queryByText(/^v1/)).not.toBeInTheDocument();
    expect(screen.queryByText(/^v2/)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '수정' }));
    expect(await screen.findByRole('textbox', { name: '1번 계획 항목명' })).toBeEnabled();
    expect(screen.queryByRole('textbox', { name: '2번 계획 항목명' })).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: '1번 연결할 실적' })).toBeEnabled();
    expect(screen.getByRole('option', { name: '외관 확인' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: '치수 검사' })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: '품질 · OQC 합격' })).toBeInTheDocument();
    fireEvent.change(screen.getByRole('combobox', { name: '1번 연결할 실적' }), { target: { value: 'OQC_PASSED:' } });

    fireEvent.click(secondSummary);
    expect(await screen.findByRole('textbox', { name: '2번 계획 항목명' })).toBeEnabled();
    expect(screen.queryByRole('textbox', { name: '1번 계획 항목명' })).not.toBeInTheDocument();

    fireEvent.change(screen.getByRole('combobox', { name: '2번 연결할 실적' }), { target: { value: 'LQC_PASSED:manufacturing-step-2' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    await screen.findByText(/생산계획 양식과 1:1 실적 연결을 저장했습니다/);

    const save = calls.find((call) => call.method === 'PUT' && call.path.endsWith('/planning-v1'));
    expect(save?.body).toMatchObject({
      expectedRowVersion: 1,
      items: [
        { connections: [{ sourceCode: 'OQC_PASSED', sourceDefinitionKey: null }] },
        { connections: [{ sourceCode: 'LQC_PASSED', sourceDefinitionKey: 'manufacturing-step-2' }] }
      ]
    });
  });

  it('validates manufacturing rows precisely and saves a replacement before relinking the production plan', async () => {
    const calls: Array<{ path: string; method: string }> = [];
    let manufacturingSaveCount = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://localhost');
      const method = init?.method ?? 'GET';
      calls.push({ path: url.pathname, method });
      if (url.pathname === '/api/form-templates' && method === 'GET') {
        return json({ templates: [{ family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 }] });
      }
      if (url.pathname === '/api/production-control/templates' && method === 'GET') {
        return json(productionControlCatalog(false));
      }
      if (url.pathname === '/api/production-control/templates/manufacturing/product-ul67/versions/manufacturing-v1' && method === 'PUT') {
        manufacturingSaveCount += 1;
        if (manufacturingSaveCount === 1) {
          return json({
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: { items: ['서버가 확인한 제조 단계 오류입니다.'] }
          }, 400);
        }
        return json(productionControlCatalogWithReplacedManufacturing());
      }
      return json(versionsResponse([activeVersion]));
    }));

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator />);

    fireEvent.click(await screen.findByRole('button', { name: /Item별 제조 양식/ }));
    expect(await screen.findByRole('combobox', { name: '적용 Item' })).toHaveValue('product-ul67');
    fireEvent.click(screen.getByRole('button', { name: '수정' }));
    expect(screen.queryByRole('combobox', { name: '1번 구분' })).not.toBeInTheDocument();
    expect(screen.getByText('등록한 모든 제조 단계를 패널 선택 일괄 완료에 사용할 수 있습니다.')).toBeInTheDocument();

    fireEvent.click(await screen.findByRole('button', { name: '저장' }));
    expect(await screen.findByText('서버가 확인한 제조 단계 오류입니다.')).toBeInTheDocument();
    expect(screen.queryByText('입력값을 확인해 주세요.')).not.toBeInTheDocument();

    fireEvent.click(screen.getAllByRole('button', { name: '삭제' })[0]);
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    expect(await screen.findByText(/연결이 끊긴 항목 1개를 다시 선택해 주세요/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /생산계획·실적 연결/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent('연결이 끊긴 생산계획 항목이 1개');
    expect(await screen.findByRole('button', { name: /제조 착수.*연결 재설정 필요/ })).toBeInTheDocument();
    expect(calls.filter((call) => call.path.endsWith('/manufacturing-v1') && call.method === 'PUT')).toHaveLength(2);
  });
});

function versionsResponse(versions: Array<typeof activeVersion>) {
  return { family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', versions };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
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
      { definitionKey: 'manufacturing-step-1', displayOrder: 1, label: '작업지시 확인' },
      { definitionKey: 'manufacturing-step-2', displayOrder: 2, label: '조립' }
    ]
  };
  const planItems = [
    {
      definitionKey: 'plan-step-1',
      displayOrder: 1,
      label: '자재 준비',
      isRequired: true,
      connections: [{ sourceCode: 'PURCHASE_ORDERED', sourceDefinitionKey: null }]
    },
    {
      definitionKey: 'plan-step-2',
      displayOrder: 2,
      label: '제조 착수',
      isRequired: true,
      connections: [
        { sourceCode: 'MANUFACTURING_STEP_COMPLETED', sourceDefinitionKey: 'manufacturing-step-1' },
        { sourceCode: 'LQC_PASSED', sourceDefinitionKey: 'manufacturing-step-1' }
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
      { code: 'PURCHASE_ORDERED', departmentLabel: '구매', label: '발주 완료', requiresManufacturingDefinition: false, definitionKind: 'None', definitions: [] },
      { code: 'MANUFACTURING_STEP_COMPLETED', departmentLabel: '제조', label: '제조 단계 완료', requiresManufacturingDefinition: true, definitionKind: 'Manufacturing', definitions: [] },
      { code: 'LQC_PASSED', departmentLabel: '품질', label: 'LQC 합격', requiresManufacturingDefinition: true, definitionKind: 'Manufacturing', definitions: [] },
      { code: 'IQC_PASSED', departmentLabel: '품질', label: 'IQC 합격', requiresManufacturingDefinition: false, definitionKind: 'Iqc', definitions: [{ definitionKey: 'iqc-step-1', label: '외관 확인' }] },
      { code: 'OQC_PASSED', departmentLabel: '품질', label: 'OQC 합격', requiresManufacturingDefinition: false, definitionKind: 'None', definitions: [] }
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

function productionControlCatalogWithReplacedManufacturing() {
  const catalog = productionControlCatalog(false);
  catalog.items[0].manufacturingVersions[0] = {
    ...catalog.items[0].manufacturingVersions[0],
    rowVersion: 2,
    items: [
      { definitionKey: 'manufacturing-step-2', displayOrder: 1, label: '조립' }
    ]
  };
  return catalog;
}
