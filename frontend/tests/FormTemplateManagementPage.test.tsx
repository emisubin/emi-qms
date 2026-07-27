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
