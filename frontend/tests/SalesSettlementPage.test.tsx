import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdaptiveLayoutProvider } from '../src/adaptive-layout';
import { setRuntimeMutationAllowed } from '../src/api';
import { SalesSettlementPage } from '../src/SalesSettlementPage';

const projectId = '91000000-0000-0000-0000-000000000014';

describe('SalesSettlementPage', () => {
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

  it('shows the compact mobile gates and completes through the explicit final confirmation', async () => {
    const requests: Array<{ path: string; body: Record<string, unknown> }> = [];
    let completed = false;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/settlement` && (!init?.method || init.method === 'GET')) {
        return json(detail(completed));
      }
      if (url.pathname.endsWith('/complete')) {
        requests.push({ path: url.pathname, body: JSON.parse(String(init?.body)) as Record<string, unknown> });
        completed = true;
        return json({ operationId: crypto.randomUUID(), projectId, settlementId: crypto.randomUUID(), status: 'Completed', version: 1, replayed: false });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <SalesSettlementPage developmentUserKey="dev-sales" projectId={projectId} onBack={vi.fn()} onOpenPending={vi.fn()} />
      </AdaptiveLayoutProvider>
    );

    expect(await screen.findByRole('heading', { name: '정산하고 완료하기' })).toBeInTheDocument();
    expect(screen.getByText('2/2')).toBeInTheDocument();
    expect(screen.getByText('0건')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(/발행일/u), { target: { value: '2026-07-18' } });
    fireEvent.click(screen.getByRole('button', { name: '최종 완료 확인' }));
    expect(screen.getByText('정말 최종 완료할까요?')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '정산·프로젝트 완료' }));

    expect(await screen.findByRole('heading', { name: '프로젝트 완료 내역' })).toBeInTheDocument();
    expect(screen.getByText('프로젝트가 최종 완료되었습니다.')).toBeInTheDocument();
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0].body.expectedVersion).toBe(0);
    expect(requests[0].body.invoiceIssuedDate).toBe('2026-07-18');
    expect(requests[0].body.operationId).toEqual(expect.any(String));
  });

  it('keeps open Pending as an aggregate and routes to the existing project filter', async () => {
    const onOpenPending = vi.fn();
    vi.stubGlobal('fetch', vi.fn(async () => json({ ...detail(false), openPendingCount: 2, noOpenPending: false, canComplete: false })));

    render(
      <AdaptiveLayoutProvider>
        <SalesSettlementPage developmentUserKey="dev-sales" projectId={projectId} onBack={vi.fn()} onOpenPending={onOpenPending} />
      </AdaptiveLayoutProvider>
    );

    expect(await screen.findByText('2건')).toBeInTheDocument();
    expect(screen.queryByText(/Pending 원문/u)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '목록 열기' }));
    expect(onOpenPending).toHaveBeenCalledOnce();
    expect(screen.getByRole('button', { name: '최종 완료 확인' })).toBeDisabled();
  });
});

function detail(completed: boolean) {
  return {
    projectId, projectCode: 'SET-014A', projectTitle: '합성 정산 프로젝트',
    projectStatus: completed ? 'Completed' : 'Active', settlementStatus: completed ? 'Completed' : 'NotStarted', version: completed ? 1 : 0,
    activePanelCount: 2, deliveredPanelCount: 2, openPendingCount: 0,
    invoiceIssuedDate: completed ? '2026-07-18' : null, invoiceNumber: completed ? 'SYNTH-001' : null, note: null,
    completedAtUtc: completed ? '2026-07-18T01:00:00Z' : null, completedByName: completed ? '합성 영업 담당' : null,
    allPanelsDelivered: true, noOpenPending: true, invoiceIssued: completed,
    canComplete: !completed, canMutate: !completed, pendingLink: `/pending?projectId=${projectId}`
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
