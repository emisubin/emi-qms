import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdaptiveLayoutProvider } from '../src/adaptive-layout';
import { setRuntimeMutationAllowed } from '../src/api';
import { LogisticsPage } from '../src/LogisticsPage';

const projectId = '91000000-0000-0000-0000-000000000010';
const panelId = '92000000-0000-0000-0000-000000000001';
const secondPanelId = '92000000-0000-0000-0000-000000000002';
const draftId = '93000000-0000-0000-0000-000000000001';

describe('LogisticsPage', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      value: vi.fn((query: string) => ({
        matches: query.includes('max-width'), media: query,
        addEventListener: vi.fn(), removeEventListener: vi.fn()
      }))
    });
    setRuntimeMutationAllowed(true);
  });

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('requires evidence first and finalizes packing with one save action', async () => {
    const requests: Array<Record<string, unknown>> = [];
    const calls: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/logistics/queue') return json(queue());
      if (url.pathname === '/api/logistics/packing-units') {
        calls.push('create');
        requests.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
        return json({ operationId: crypto.randomUUID(), projectId, targetId: draftId, stage: 'packing', status: 'Draft', version: 1, nextStage: 'evidence', replayed: false });
      }
      if (url.pathname === `/api/logistics/packing/${draftId}/evidence`) {
        calls.push('evidence');
        return json({ operationId: crypto.randomUUID(), projectId, targetId: draftId, stage: 'packing', status: 'Draft', version: 2, nextStage: 'confirm', replayed: false });
      }
      if (url.pathname === `/api/logistics/packing/${draftId}/finalize`) {
        calls.push('finalize');
        return json({ operationId: crypto.randomUUID(), projectId, targetId: draftId, stage: 'packing', status: 'Finalized', version: 3, nextStage: 'departure', replayed: false });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <LogisticsPage developmentUserKey="dev-logistics" canMutate initialStage="packing" initialProjectId={projectId} onLocationChange={vi.fn()} onBack={vi.fn()} />
      </AdaptiveLayoutProvider>
    );

    expect(await screen.findByRole('heading', { name: '포장 처리' })).toBeInTheDocument();
    expect(screen.getByText('처리 대기')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /P01/u }));
    const saveButton = screen.getByRole('button', { name: '포장 저장 및 확정' });
    expect(saveButton).toBeDisabled();
    fireEvent.change(screen.getByLabelText(/포장 사진/u), {
      target: { files: [new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'packing.png', { type: 'image/png' })] }
    });
    fireEvent.click(saveButton);

    expect(await screen.findByText(/포장 확정 완료/u)).toBeInTheDocument();
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(calls).toEqual(['create', 'evidence', 'finalize']);
    expect(requests[0].projectId).toBe(projectId);
    expect(requests[0].panelIds).toEqual([panelId]);
  });

  it('creates departure from only the panels selected by the user', async () => {
    const requests: Array<Record<string, unknown>> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/logistics/queue') {
        return json({
          stage: 'departure', todayCount: 2, blockedCount: 0, drafts: [],
          projects: [{
            projectId, projectCode: 'LOG-016', projectTitle: 'Panel Departure',
            items: [
              {
                targetId: panelId, targetType: 'Panel', displayCode: 'P01', title: 'Panel 01',
                supportingText: 'PU-001 · 포장 완료 · 출발 대기', panelIds: [panelId], panelCodes: ['P01'],
                version: 1, status: 'Requested', hasOpenPending: false, canMutate: true
              },
              {
                targetId: secondPanelId, targetType: 'Panel', displayCode: 'P02', title: 'Panel 02',
                supportingText: 'PU-001 · 포장 완료 · 출발 대기', panelIds: [secondPanelId], panelCodes: ['P02'],
                version: 1, status: 'Requested', hasOpenPending: false, canMutate: true
              }
            ]
          }]
        });
      }
      if (url.pathname === '/api/logistics/departure-batches') {
        requests.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
        return json({ operationId: crypto.randomUUID(), projectId, targetId: draftId, stage: 'departure', status: 'Draft', version: 1, nextStage: 'evidence', replayed: false });
      }
      if (url.pathname === `/api/logistics/departure/${draftId}/evidence`) {
        return json({ operationId: crypto.randomUUID(), projectId, targetId: draftId, stage: 'departure', status: 'Draft', version: 2, nextStage: 'confirm', replayed: false });
      }
      if (url.pathname === `/api/logistics/departure/${draftId}/finalize`) {
        return json({ operationId: crypto.randomUUID(), projectId, targetId: draftId, stage: 'departure', status: 'Finalized', version: 3, nextStage: 'delivery', replayed: false });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <LogisticsPage developmentUserKey="dev-logistics" canMutate initialStage="departure" initialProjectId={projectId} onLocationChange={vi.fn()} onBack={vi.fn()} />
      </AdaptiveLayoutProvider>
    );

    fireEvent.click(await screen.findByRole('button', { name: /P01/u }));
    expect(screen.getByRole('button', { name: /P02/u })).toHaveAttribute('aria-pressed', 'false');
    fireEvent.change(screen.getByLabelText(/상차 사진/u), {
      target: { files: [new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'departure.png', { type: 'image/png' })] }
    });
    fireEvent.click(screen.getByRole('button', { name: '출발 저장 및 확정' }));

    expect(await screen.findByText(/출발 확정 완료/u)).toBeInTheDocument();
    expect(requests).toHaveLength(1);
    expect(requests[0].panelIds).toEqual([panelId]);
    expect(requests[0]).not.toHaveProperty('unitIds');
  });

  it('recovers a draft from the queue when the original item and URL draft are unavailable', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/logistics/queue') return json({
        ...queue(),
        todayCount: 0,
        projects: [],
        drafts: [{
          targetId: draftId,
          projectId,
          projectCode: 'LOG-013A',
          projectTitle: 'Synthetic Logistics',
          stage: 'packing',
          displayCode: 'PU-001',
          version: 2,
          evidenceCount: 1,
          createdAtUtc: '2026-07-18T00:00:00Z'
        }]
      });
      if (url.pathname === `/api/logistics/packing/${draftId}`) return json({
        ...draft(),
        version: 2,
        evidence: [{
          evidenceId: '94000000-0000-0000-0000-000000000001', ownerType: 'PackingPhoto', displayName: 'packing-photo-1.png',
          normalizedMime: 'image/png', byteSize: 68, altText: '합성 포장 사진', createdAtUtc: '2026-07-18T00:00:00Z'
        }]
      });
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <LogisticsPage developmentUserKey="dev-logistics" canMutate initialStage="packing"
          initialProjectId={projectId} onLocationChange={vi.fn()} onBack={vi.fn()} />
      </AdaptiveLayoutProvider>
    );

    expect(await screen.findByText(/중간에 멈춘 물류 작업을 복구했습니다/u)).toBeInTheDocument();
    expect(screen.getByText(/등록 증빙 1개/u)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '포장 저장 및 확정' })).toBeEnabled();
    expect(screen.getByRole('button', { name: '임시 작업 취소' })).toBeInTheDocument();
  });
});

function draft() {
  return {
    targetId: draftId, projectId, stage: 'packing', displayCode: 'PU-001', status: 'Draft', version: 1,
    departureDate: null, panelIds: [panelId], unitIds: [], evidence: []
  };
}

function queue() {
  return {
    stage: 'packing', todayCount: 1, blockedCount: 0,
    drafts: [],
    projects: [{
      projectId, projectCode: 'LOG-013A', projectTitle: 'Synthetic Logistics',
      items: [{
        targetId: panelId, targetType: 'Panel', displayCode: 'P01', title: 'Main Panel',
        supportingText: '품질 완료 · 포장 대기', panelIds: [panelId], panelCodes: ['P01'],
        version: 0, status: 'Requested', hasOpenPending: false, canMutate: true
      }]
    }]
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
