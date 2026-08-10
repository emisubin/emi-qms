import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdaptiveLayoutProvider } from '../src/adaptive-layout';
import { setRuntimeMutationAllowed } from '../src/api';
import { QualityInspectionsPage } from '../src/QualityInspectionsPage';
import type { QualityInspectionDetail, QualityInspectionPanel, QualityInspectionQueueResponse } from '../src/qualityInspections';

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
    let reconciliationCalls = 0;
    const operationIds: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/quality/inspections/reconcile') {
        reconciliationCalls += 1;
        return json({
          recoveredLqcHandoffCount: 0,
          recoveredOqcHandoffCount: 1,
          recoveredInspectionHandoffCount: 0,
          recoveredPackingHandoffCount: 0,
          unresolvedAssigneeCount: 0
        });
      }
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
    expect(screen.getByRole('heading', { name: 'LQC 검사' })).toBeInTheDocument();
    expect(screen.getByText('검사 항목을 불러오고 판정 근거를 기록합니다.')).toBeInTheDocument();
    expect(await screen.findByText('누락된 품질 후속 업무 1건을 자동으로 복구했습니다.')).toBeInTheDocument();
    expect(reconciliationCalls).toBe(1);

    fireEvent.click(screen.getByRole('button', { name: 'LQC 시작' }));
    expect(await screen.findByText('현장 연결을 확인해 주세요.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'LQC 시작' }));

    expect(await screen.findByText('도면·작업기준 일치')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: '적합' })).toHaveLength(2);
    await waitFor(() => expect(operationIds).toHaveLength(2));
    expect(operationIds[0]).toMatch(/^[0-9a-f-]{36}$/u);
    expect(operationIds[1]).toBe(operationIds[0]);
  });

  it('finalizes checklist responses atomically, shows the server error in the dialog, and blocks duplicate retry clicks', async () => {
    let finalized = false;
    let finalizeCalls = 0;
    let responseSaveCalls = 0;
    const operationIds: string[] = [];
    const submittedResponseCounts: number[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/quality/inspections/reconcile') {
        return json({
          recoveredLqcHandoffCount: 0,
          recoveredOqcHandoffCount: 0,
          recoveredInspectionHandoffCount: 0,
          recoveredPackingHandoffCount: 0,
          unresolvedAssigneeCount: 0
        });
      }
      if (url.pathname === '/api/quality/inspections/queue') return json(queue(true));
      if (url.pathname === `/api/quality/inspections/panels/${panelId}`) {
        return json({
          ...detail(true),
          reportStatus: finalized ? 'Finalized' : 'Draft',
          result: finalized ? 'Passed' : null,
          reason: finalized ? '검사 기준 적합' : null,
          pdfStatus: finalized ? 'Ready' : null
        });
      }
      if (url.pathname === '/api/quality/inspections/action-departments') return json([]);
      if (url.pathname.endsWith('/responses')) {
        responseSaveCalls += 1;
        return json({ title: '판정 확정에서 별도 저장 요청을 보내면 안 됩니다.' }, 500);
      }
      if (url.pathname === `/api/quality/inspections/reports/${reportId}/finalize`) {
        finalizeCalls += 1;
        const body = JSON.parse(String(init?.body)) as {
          operationId: string;
          responses: unknown[];
        };
        operationIds.push(body.operationId);
        submittedResponseCounts.push(body.responses.length);
        if (finalizeCalls === 1) {
          return json({
            title: '입력값을 확인해 주세요.',
            errors: { result: ['서버에서 판정 조건을 다시 확인해 주세요.'] }
          }, 400);
        }
        finalized = true;
        return json({
          operationId: body.operationId,
          projectId,
          panelId,
          stageCode: 'LQC',
          attemptId,
          reportId,
          status: 'Passed',
          version: 2,
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

    const passButtons = await screen.findAllByRole('button', { name: '적합' });
    for (const button of passButtons) fireEvent.click(button);
    fireEvent.click(screen.getByRole('button', { name: '판정 확정' }));
    const dialog = screen.getByRole('dialog');
    expect(screen.getByText('모든 검사 항목 적합')).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: '부적합' })).not.toBeInTheDocument();
    const submit = screen.getByRole('button', { name: '합격 확정 및 인계' });
    fireEvent.click(submit);

    await waitFor(() => expect(finalizeCalls).toBe(1), { timeout: 5_000 });
    expect(await screen.findByText(
      '서버에서 판정 조건을 다시 확인해 주세요.',
      undefined,
      { timeout: 5_000 }
    )).toBeInTheDocument();
    expect(dialog).toBeInTheDocument();
    expect(responseSaveCalls).toBe(0);

    fireEvent.click(submit);
    fireEvent.click(submit);
    await waitFor(() => expect(finalizeCalls).toBe(2));
    expect(await screen.findByText('PASS')).toBeInTheDocument();
    expect(submittedResponseCounts).toEqual([2, 2]);
    expect(operationIds[1]).toBe(operationIds[0]);
  });

  it('shows only the failed reinspection item and can request another action without choosing a department again', async () => {
    const pendingId = '87000000-0000-0000-0000-000000000001';
    const finalizeBodies: Array<Record<string, unknown>> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/quality/inspections/reconcile') {
        return json({
          recoveredLqcHandoffCount: 0,
          recoveredOqcHandoffCount: 0,
          recoveredInspectionHandoffCount: 0,
          recoveredPackingHandoffCount: 0,
          unresolvedAssigneeCount: 0
        });
      }
      if (url.pathname === '/api/quality/inspections/queue') {
        const response = queue(true);
        response.projects[0].panels[0] = {
          ...response.projects[0].panels[0],
          pendingId,
          pendingNumber: 14,
          actionDepartmentCode: 'manufacturing'
        };
        return json(response);
      }
      if (url.pathname === `/api/quality/inspections/panels/${panelId}`) {
        const response = detail(true);
        response.panel = { ...response.panel, pendingId, pendingNumber: 14, actionDepartmentCode: 'manufacturing' };
        response.items = [{
          itemId: '86000000-0000-0000-0000-000000000001',
          itemCode: 'DRAWING_STANDARD',
          displayOrder: 1,
          label: '도면·작업기준 일치',
          guidance: '도면과 기준을 대조해 주세요.',
          responseType: 'Check',
          isRequired: true,
          maxTextLength: null,
          isAvailable: true,
          availabilityMessage: null,
          isReinspectionTarget: true,
          previousFailureEvidence: '도면 체결 기준이 일치하지 않았습니다.'
        }];
        return json(response);
      }
      if (url.pathname === '/api/quality/inspections/action-departments') return json([]);
      if (url.pathname === `/api/pending/${pendingId}`) return json({ title: 'not found' }, 404);
      if (url.pathname === `/api/quality/inspections/reports/${reportId}/finalize`) {
        const finalizeBody = JSON.parse(String(init?.body)) as Record<string, unknown>;
        finalizeBodies.push(finalizeBody);
        return json({
          operationId: String(finalizeBody.operationId),
          projectId,
          panelId,
          stageCode: 'LQC',
          attemptId,
          reportId,
          status: 'Failed',
          version: 2,
          pendingId,
          pendingNumber: 14,
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

    expect(await screen.findByText('이 항목만 재검사')).toBeInTheDocument();
    expect(screen.getByText('도면 체결 기준이 일치하지 않았습니다.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '부적합' }));
    fireEvent.click(screen.getByRole('button', { name: '판정 확정' }));
    const dialog = screen.getByRole('dialog');
    expect(screen.getByText('부적합 항목 확인')).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: '합격' })).not.toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText('조치 내용을 확인한 결과와 재검사 판정 근거를 입력하세요.'), {
      target: { value: '재검사에서도 동일한 기준 미달이 확인되어 추가 재조치가 필요합니다.' }
    });
    const submit = screen.getByRole('button', { name: '불합격 · 재조치 요청' });
    expect(submit).toBeEnabled();
    fireEvent.click(submit);
    await waitFor(() => expect(finalizeBodies).toHaveLength(1));
    expect(finalizeBodies[0].actionDepartmentCode).toBe('manufacturing');
    expect((finalizeBodies[0].responses as unknown[])).toHaveLength(1);
  });
});

function queue(started: boolean): QualityInspectionQueueResponse {
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

function panelSummary(started: boolean): QualityInspectionPanel {
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

function detail(started: boolean): QualityInspectionDetail {
  return {
    panel: panelSummary(started),
    decisionMode: 'Checklist',
    reportId: started ? reportId : null,
    reportStatus: started ? 'Draft' : null,
    reportVersion: started ? 1 : null,
    result: null,
    reason: null,
    pdfStatus: null,
    items: started ? [
      { itemId: '86000000-0000-0000-0000-000000000001', itemCode: 'DRAWING_STANDARD', displayOrder: 1, label: '도면·작업기준 일치', guidance: '도면과 기준을 대조해 주세요.', responseType: 'Check', isRequired: true, maxTextLength: null, isAvailable: true, availabilityMessage: null, isReinspectionTarget: false, previousFailureEvidence: null },
      { itemId: '86000000-0000-0000-0000-000000000002', itemCode: 'ASSEMBLY', displayOrder: 2, label: '조립 상태', guidance: '조립 상태를 확인해 주세요.', responseType: 'Check', isRequired: true, maxTextLength: null, isAvailable: true, availabilityMessage: null, isReinspectionTarget: false, previousFailureEvidence: null },
      { itemId: '86000000-0000-0000-0000-000000000003', itemCode: 'NOTES', displayOrder: 3, label: '추가 메모', guidance: null, responseType: 'Text', isRequired: false, maxTextLength: 1000, isAvailable: true, availabilityMessage: null, isReinspectionTarget: false, previousFailureEvidence: null }
    ] : [],
    responses: [],
    photos: [],
    history: started ? [{ attemptId, attemptNumber: 1, status: 'InProgress', pendingId: null, pendingNumber: null, completedAtUtc: null }] : []
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
