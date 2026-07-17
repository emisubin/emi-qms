import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdaptiveLayoutProvider } from '../src/adaptive-layout';
import { setRuntimeMutationAllowed } from '../src/api';
import { QualityInspectionsPage } from '../src/QualityInspectionsPage';

const projectId = '81000000-0000-0000-0000-000000000010';
const panelId = '82000000-0000-0000-0000-000000000001';
const attemptId = '83000000-0000-0000-0000-000000000001';
const reportId = '84000000-0000-0000-0000-000000000001';

describe('QualityInspectionsPage', () => {
  beforeEach(() => {
    window.history.pushState(null, '', `/quality/inspections?stage=LQC&project=${projectId}&panel=${panelId}`);
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      value: vi.fn((query: string) => ({
        matches: query.includes('max-width'),
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn()
      }))
    });
    setRuntimeMutationAllowed(true);
  });

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('uses a mobile-specific inspection flow and reuses the start operation after a network failure', async () => {
    let started = false;
    let attempts = 0;
    const operationIds: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/quality/inspections/queue') return json(queue(started));
      if (url.pathname === `/api/quality/inspections/panels/${panelId}`) return json(detail(started));
      if (url.pathname === '/api/quality/inspections/action-departments') return json([]);
      if (url.pathname === '/api/quality/inspections/start') {
        attempts += 1;
        const body = JSON.parse(String(init?.body)) as { operationId: string };
        operationIds.push(body.operationId);
        if (attempts === 1) return json({ title: '현장 연결을 확인해 주세요.' }, 503);
        started = true;
        return json({
          operationId: body.operationId,
          projectId,
          panelId,
          stageCode: 'LQC',
          attemptId,
          reportId,
          status: 'InProgress',
          version: 1,
          pendingId: null,
          pendingNumber: null,
          nextStageCode: null,
          replayed: false
        });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <QualityInspectionsPage
          developmentUserKey="dev-quality"
          canInspect
          initialStage="LQC"
          initialProjectId={projectId}
          initialPanelId={panelId}
          onOpenIqc={vi.fn()}
          onBack={vi.fn()}
          onOpenPending={vi.fn()}
        />
      </AdaptiveLayoutProvider>
    );

    const page = await screen.findByTestId('quality-inspection-page');
    expect(page).toHaveClass('quality-inspection-page--mobile');
    expect(screen.getByRole('heading', { name: '품질 검사' })).toBeInTheDocument();
    expect(screen.getByText('검사 항목을 불러오고 판정 근거를 기록합니다.')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'LQC 시작' }));
    expect(await screen.findByText('현장 연결을 확인해 주세요.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'LQC 시작' }));

    expect(await screen.findByText('도면·작업기준 일치')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: '적합' })).toHaveLength(2);
    await waitFor(() => expect(operationIds).toHaveLength(2));
    expect(operationIds[0]).toMatch(/^[0-9a-f-]{36}$/u);
    expect(operationIds[1]).toBe(operationIds[0]);
  });
});

function queue(started: boolean) {
  const panel = panelSummary(started);
  return {
    projects: [{
      projectId,
      projectCode: 'QLT-012A',
      projectTitle: '모바일 패널 품질검사',
      fatRequired: false,
      readyCount: started ? 0 : 1,
      inProgressCount: started ? 1 : 0,
      blockedCount: 0,
      completedCount: 0,
      panels: [panel]
    }]
  };
}

function panelSummary(started: boolean) {
  return {
    panelId,
    displayCode: 'PANEL-Q01',
    panelName: 'MCC QUALITY',
    workflowStage: 'InspectionInProgress',
    stageCode: 'LQC',
    stageLabel: 'LQC',
    workItemId: '85000000-0000-0000-0000-000000000001',
    workItemStatus: started ? 'InProgress' : 'Requested',
    attemptId: started ? attemptId : null,
    attemptNumber: started ? 1 : 0,
    status: started ? 'InProgress' : 'Ready',
    version: started ? 1 : 0,
    pendingId: null,
    pendingNumber: null,
    actionDepartmentCode: null,
    canMutate: true
  };
}

function detail(started: boolean) {
  return {
    panel: panelSummary(started),
    reportId: started ? reportId : null,
    reportStatus: started ? 'Draft' : null,
    reportVersion: started ? 1 : null,
    result: null,
    reason: null,
    pdfStatus: null,
    items: started ? [
      { itemId: '86000000-0000-0000-0000-000000000001', itemCode: 'DRAWING_STANDARD', displayOrder: 1, label: '도면·작업기준 일치', guidance: '도면과 기준을 대조해 주세요.', responseType: 'Check', isRequired: true, maxTextLength: null },
      { itemId: '86000000-0000-0000-0000-000000000002', itemCode: 'ASSEMBLY', displayOrder: 2, label: '조립 상태', guidance: '조립 상태를 확인해 주세요.', responseType: 'Check', isRequired: true, maxTextLength: null },
      { itemId: '86000000-0000-0000-0000-000000000003', itemCode: 'NOTES', displayOrder: 3, label: '추가 메모', guidance: null, responseType: 'Text', isRequired: false, maxTextLength: 1000 }
    ] : [],
    responses: [],
    photos: [],
    history: started ? [{ attemptId, attemptNumber: 1, status: 'InProgress', pendingId: null, pendingNumber: null, completedAtUtc: null }] : []
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
