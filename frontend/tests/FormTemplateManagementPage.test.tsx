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

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator domains={['Quality', 'Manufacturing', 'ProductionPlanning']} />);

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

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator domains={['Quality', 'Manufacturing', 'ProductionPlanning']} />);

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
    expect(screen.getByRole('option', { name: '전체 구매품' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: '외함' })).toBeInTheDocument();
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

  it('manages each Item LQC status and form while disabling that Item result source', async () => {
    const calls: Array<{ path: string; method: string }> = [];
    let lqcOperational = false;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://localhost');
      const method = init?.method ?? 'GET';
      calls.push({ path: url.pathname, method });
      if (url.pathname === '/api/form-templates' && method === 'GET') {
        return json({ templates: [{
          family: 'PanelQualityStage',
          templateKey: 'LQC',
          displayName: 'Item별 LQC 검사',
          domain: 'Quality',
          activeVersionNumber: 1,
          activatedAtUtc: activeVersion.activatedAtUtc,
          draftCount: 0
        }] });
      }
      if (url.pathname === '/api/form-templates/lqc-items' && method === 'GET') {
        return json(lqcItemCatalog(lqcOperational));
      }
      if (url.pathname.endsWith('/operating-status') && method === 'PUT') {
        lqcOperational = JSON.parse(String(init?.body)).isOperational;
        return json(lqcItemCatalog(lqcOperational));
      }
      if (url.pathname.endsWith('/lqc-items/product-ul67/current') && method === 'PUT') {
        return json(lqcItemCatalog(lqcOperational, 'Item별 저장 검사 항목'));
      }
      if (url.pathname === '/api/production-control/templates' && method === 'GET') {
        return json(productionControlCatalog(false, false));
      }
      return json({ ...versionsResponse([activeVersion]), family: 'PanelQualityStage', templateKey: 'LQC', displayName: 'Item별 LQC 검사' });
    }));

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator domains={['Quality', 'Manufacturing', 'ProductionPlanning']} />);

    expect(await screen.findByRole('button', { name: /Item별 LQC 검사.*Item별 운영 상태.*검사 항목/ })).toBeInTheDocument();
    expect(await screen.findByText(/이미 만들어진 프로젝트에는 영향을 주지 않습니다/)).toBeInTheDocument();
    const switchControl = screen.getByRole('switch');
    expect(switchControl).not.toBeChecked();
    fireEvent.click(switchControl);
    await screen.findByText(/UL67 LQC를 운영 중으로 변경했습니다/);
    expect(calls).toContainEqual({ path: '/api/form-templates/lqc-items/product-ul67/operating-status', method: 'PUT' });

    expect(screen.getByRole('button', { name: '수정' })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: '수정' }));
    fireEvent.change(screen.getByRole('textbox', { name: '항목명' }), { target: { value: 'Item별 저장 검사 항목' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    await screen.findByText(/LQC 검사 항목을 저장했습니다/);
    expect(calls).toContainEqual({ path: '/api/form-templates/lqc-items/product-ul67/current', method: 'PUT' });

    fireEvent.click(screen.getByRole('button', { name: /생산계획·실적 연결/ }));
    expect(await screen.findByText(/기존 LQC 연결 이력은 유지되며 새 연결은 제조 단계 완료/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '수정' }));
    fireEvent.click(screen.getByRole('button', { name: /제조 착수.*연결 실적/ }));
    expect(screen.getByRole('group', { name: /품질 · LQC 합격 · 운영 중지/ })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    expect(await screen.findByText(/기존 연결 이력은 유지되며 새 연결에는 사용할 수 없습니다/)).toBeInTheDocument();
    expect(calls.some((call) => call.method === 'PUT' && call.path.includes('/planning/'))).toBe(false);
  });

  it('configures IQC mode and detailed items for each purchase category', async () => {
    const calls: Array<{ path: string; method: string; body: Record<string, unknown> | null }> = [];
    let category = materialCategoryIqcItem();
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://localhost');
      const method = init?.method ?? 'GET';
      const body = init?.body ? JSON.parse(String(init.body)) as Record<string, unknown> : null;
      calls.push({ path: url.pathname, method, body });
      if (url.pathname === '/api/form-templates' && method === 'GET') {
        return json({ templates: [{ family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 }] });
      }
      if (url.pathname === '/api/form-templates/material-category-iqc' && method === 'GET') {
        return json({ canManage: true, items: [category] });
      }
      if (url.pathname.endsWith('/current') && method === 'PUT') {
        category = { ...category, settingRowVersion: category.settingRowVersion + 1, templateVersionNumber: category.templateVersionNumber + 1, templateRowVersion: 2, items: body?.items as typeof activeVersion.items };
        return json({ canManage: true, items: [category] });
      }
      if (url.pathname.endsWith('/setting') && method === 'PUT') {
        category = { ...category, settingRowVersion: category.settingRowVersion + 1, isEnabled: body?.isEnabled as boolean, decisionMode: body?.decisionMode as 'ScanBased' | 'Detailed' };
        return json({ canManage: true, items: [category] });
      }
      return json(versionsResponse([activeVersion]));
    }));

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator domains={['Quality', 'Manufacturing', 'ProductionPlanning']} />);
    fireEvent.click(await screen.findByRole('button', { name: /구매품별 IQC 양식/ }));
    expect(await screen.findByRole('heading', { name: '기타 수입검사' })).toBeInTheDocument();
    expect(screen.getByText(/이미 저장된 구매품과 시작된 검사에는 영향을 주지 않습니다/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('switch'));
    fireEvent.change(screen.getByRole('combobox', { name: '검사 방식' }), { target: { value: 'Detailed' } });
    expect(screen.getByRole('alert')).toHaveTextContent('검사 항목을 1개 이상 먼저 저장');
    expect(screen.getByRole('button', { name: '설정 저장' })).toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: '수정' }));
    fireEvent.click(screen.getByRole('button', { name: '항목 추가' }));
    fireEvent.change(screen.getByRole('textbox', { name: '항목명' }), { target: { value: '단자 외관 확인' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    await screen.findByText(/상세 검사 항목을 저장했습니다/);

    fireEvent.click(screen.getByRole('switch'));
    fireEvent.change(screen.getByRole('combobox', { name: '검사 방식' }), { target: { value: 'Detailed' } });
    fireEvent.click(screen.getByRole('button', { name: '설정 저장' }));
    await screen.findByText(/IQC 설정을 저장했습니다/);

    expect(calls.find((call) => call.path.endsWith('/current') && call.method === 'PUT')?.body).toMatchObject({
      expectedTemplateRowVersion: 1,
      items: [{ label: '단자 외관 확인', responseType: 'Check' }]
    });
    expect(calls.find((call) => call.path.endsWith('/setting') && call.method === 'PUT')?.body).toEqual({
      isEnabled: true,
      decisionMode: 'Detailed',
      expectedRowVersion: 2
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

    render(<FormTemplateManagementPage developmentUserKey="dev-admin" isSystemAdministrator domains={['Quality', 'Manufacturing', 'ProductionPlanning']} />);

    fireEvent.click(await screen.findByRole('button', { name: /Item별 제조 양식/ }));
    expect(await screen.findByRole('combobox', { name: '적용 Item' })).toHaveValue('product-ul67');
    fireEvent.click(screen.getByRole('button', { name: '수정' }));
    const saveButton = await screen.findByRole('button', { name: '저장' });
    expect(saveButton).toBeEnabled();
    expect(screen.queryByRole('combobox', { name: '1번 구분' })).not.toBeInTheDocument();
    expect(screen.getByText('등록한 모든 제조 단계를 패널 선택 일괄 완료에 사용할 수 있습니다.')).toBeInTheDocument();

    fireEvent.click(saveButton);
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

  it('shows only quality forms and allows the quality department head to change LQC operation', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input), 'http://localhost');
      if (url.pathname === '/api/form-templates') {
        return json({ templates: [
          { family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 },
          { family: 'PanelQualityStage', templateKey: 'LQC', displayName: 'Item별 LQC 검사', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 },
          { family: 'PanelQualityStage', templateKey: 'OQC', displayName: 'OQC 자체검수', domain: 'Quality', activeVersionNumber: 1, activatedAtUtc: activeVersion.activatedAtUtc, draftCount: 0 }
        ] });
      }
      if (url.pathname === '/api/form-templates/lqc-items') return json(lqcItemCatalog(true));
      return json(versionsResponse([activeVersion]));
    }));

    render(<FormTemplateManagementPage developmentUserKey="quality-head" isSystemAdministrator={false} domains={['Quality']} />);

    const navigation = await screen.findByRole('navigation', { name: '양식 종류' });
    expect(navigation).toHaveTextContent('자재 수입검사');
    expect(navigation).toHaveTextContent('Item별 LQC 검사');
    expect(navigation).toHaveTextContent('OQC 자체검수');
    expect(navigation).toHaveTextContent('구매품별 IQC 양식');
    expect(navigation).toHaveTextContent('구매품 구분 관리');
    expect(navigation).not.toHaveTextContent('Item별 제조 양식');
    expect(navigation).not.toHaveTextContent('생산계획·실적 연결');

    fireEvent.click(screen.getByRole('button', { name: /Item별 LQC 검사/ }));
    expect(await screen.findByRole('switch')).toBeEnabled();
    expect(screen.queryByText(/운영 상태 변경 권한 없음/)).not.toBeInTheDocument();
  });

  it('shows and enables only manufacturing and planning forms for the production department head', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input), 'http://localhost');
      if (url.pathname === '/api/form-templates') return json({ templates: [] });
      if (url.pathname === '/api/production-control/templates') return json(productionControlCatalog(false));
      return json({ title: 'not found' }, 404);
    }));

    render(<FormTemplateManagementPage
      developmentUserKey="production-head"
      isSystemAdministrator={false}
      domains={['Manufacturing', 'ProductionPlanning']}
    />);

    const navigation = await screen.findByRole('navigation', { name: '양식 종류' });
    expect(navigation).toHaveTextContent('Item별 제조 양식');
    expect(navigation).toHaveTextContent('생산계획·실적 연결');
    expect(navigation).not.toHaveTextContent('자재 수입검사');
    expect(navigation).not.toHaveTextContent('구매품별 IQC 양식');
    expect(navigation).not.toHaveTextContent('구매품 구분 관리');
    expect(await screen.findByRole('heading', { name: '생산계획·실적 연결' })).toBeInTheDocument();
    expect(screen.getByText('편집 가능')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Item별 제조 양식/ }));
    expect(await screen.findByRole('heading', { name: 'Item별 제조 항목' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '수정' })).toBeEnabled();
  });
});

function versionsResponse(versions: Array<typeof activeVersion>) {
  return { family: 'IqcReport', templateKey: 'MATERIAL_IQC', displayName: '자재 수입검사', domain: 'Quality', versions };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function productionControlCatalog(withDraft: boolean, lqcOperational = true) {
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
      { code: 'PURCHASE_ORDERED', departmentLabel: '구매', label: '발주 완료', requiresManufacturingDefinition: false, definitionKind: 'MaterialCategory', definitions: [{ definitionKey: 'material-category-1', label: '외함' }], isOperational: true, operationalMessage: null },
      { code: 'MANUFACTURING_STEP_COMPLETED', departmentLabel: '제조', label: '제조 단계 완료', requiresManufacturingDefinition: true, definitionKind: 'Manufacturing', definitions: [], isOperational: true, operationalMessage: null },
      { code: 'LQC_PASSED', departmentLabel: '품질', label: 'LQC 합격', requiresManufacturingDefinition: true, definitionKind: 'Manufacturing', definitions: [], isOperational: lqcOperational, operationalMessage: lqcOperational ? null : 'LQC는 현재 운영 중지 상태입니다. 기존 연결 이력은 유지되며 새 연결에는 사용할 수 없습니다.' },
      { code: 'IQC_PASSED', departmentLabel: '품질', label: 'IQC 합격', requiresManufacturingDefinition: false, definitionKind: 'Iqc', definitions: [{ definitionKey: 'iqc-step-1', label: '외관 확인' }], isOperational: true, operationalMessage: null },
      { code: 'OQC_PASSED', departmentLabel: '품질', label: 'OQC 합격', requiresManufacturingDefinition: false, definitionKind: 'None', definitions: [], isOperational: true, operationalMessage: null }
    ],
    items: [{
      productTypeId: 'product-ul67',
      productTypeCode: 'UL67',
      productTypeName: 'UL67',
      lqcOperational,
      manufacturingVersions: [manufacturingVersion],
      planVersions: withDraft ? [draftPlan, activePlan] : [activePlan]
    }]
  };
}

function lqcItemCatalog(isOperational: boolean, label = '외관 확인') {
  return {
    canManageItems: true,
    canChangeOperatingStatus: true,
    items: [{
      productTypeId: 'product-ul67',
      productTypeCode: 'UL67',
      productTypeName: 'UL67',
      isOperational,
      settingRowVersion: isOperational ? 2 : 1,
      templateVersionId: 'lqc-template-ul67',
      templateVersionNumber: 1,
      templateRowVersion: 2,
      items: [{ ...activeVersion.items[0], label }]
    }]
  };
}

function materialCategoryIqcItem() {
  return {
    materialCategoryId: '67000000-0000-0000-0000-000000000005',
    materialCategoryCode: 'OTHER',
    materialCategoryName: '기타',
    isCategoryActive: true,
    isEnabled: false,
    decisionMode: 'ScanBased' as 'ScanBased' | 'Detailed',
    settingRowVersion: 1,
    templateVersionId: 'category-iqc-v1',
    templateVersionNumber: 1,
    templateRowVersion: 1,
    items: [] as typeof activeVersion.items
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
