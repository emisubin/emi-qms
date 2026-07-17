import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdaptiveLayoutProvider } from '../src/adaptive-layout';
import { setRuntimeMutationAllowed } from '../src/api';
import { LogisticsPage } from '../src/LogisticsPage';

const projectId = '91000000-0000-0000-0000-000000000010';
const panelId = '92000000-0000-0000-0000-000000000001';
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

  it('shows the mobile action priority and creates a same-project packing draft', async () => {
    const requests: Array<Record<string, unknown>> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/logistics/queue') return json(queue());
      if (url.pathname === '/api/logistics/packing-units') {
        requests.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
        return json({ operationId: crypto.randomUUID(), projectId, targetId: draftId, stage: 'packing', status: 'Draft', version: 1, nextStage: 'evidence', replayed: false });
      }
      if (url.pathname === `/api/logistics/packing/${draftId}`) return json(draft());
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <LogisticsPage developmentUserKey="dev-logistics" canMutate initialStage="packing" onLocationChange={vi.fn()} onBack={vi.fn()} />
      </AdaptiveLayoutProvider>
    );

    expect(await screen.findByRole('heading', { name: '물류 실행' })).toBeInTheDocument();
    expect(screen.getByText('처리 대기')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /P01/u }));
    fireEvent.click(screen.getByRole('button', { name: '포장 묶음 시작' }));

    expect(await screen.findByText(/draft를 만들었습니다/u)).toBeInTheDocument();
    expect(screen.getByText('포장 사진')).toBeInTheDocument();
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0].projectId).toBe(projectId);
    expect(requests[0].panelIds).toEqual([panelId]);
  });

  it('recovers a draft from its URL when the original queue item is no longer available', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/logistics/queue') return json({ ...queue(), todayCount: 0, projects: [] });
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
        <LogisticsPage developmentUserKey="dev-logistics" canMutate initialStage="packing" initialDraftId={draftId}
          onLocationChange={vi.fn()} onBack={vi.fn()} />
      </AdaptiveLayoutProvider>
    );

    expect(await screen.findByText(/진행 중인 draft를 복구했습니다/u)).toBeInTheDocument();
    expect(screen.getByText(/등록 증빙 1개/u)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '포장 확정' })).toBeEnabled();
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
