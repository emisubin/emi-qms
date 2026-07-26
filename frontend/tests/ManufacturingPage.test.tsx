import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdaptiveLayoutProvider } from '../src/adaptive-layout';
import { setRuntimeMutationAllowed } from '../src/api';
import { ManufacturingPage } from '../src/ManufacturingPage';

const projectId = '71000000-0000-0000-0000-000000000010';
const panelId = '72000000-0000-0000-0000-000000000001';
const secondPanelId = '72000000-0000-0000-0000-000000000002';
const executionId = '73000000-0000-0000-0000-000000000001';
const secondExecutionId = '73000000-0000-0000-0000-000000000002';

describe('ManufacturingPage', () => {
  beforeEach(() => {
    window.history.pushState(null, '', `/manufacturing/work?project=${projectId}&panel=${panelId}`);
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

  it('uses the mobile panel flow and reuses the start operation id after a failed request', async () => {
    const operationIds: string[] = [];
    let status: 'Ready' | 'InProgress' = 'Ready';
    let startAttempts = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/manufacturing/queue') return json(queue(status));
      if (url.pathname === `/api/manufacturing/panels/${panelId}`) return json(detail(status));
      if (url.pathname === '/api/manufacturing/action-departments') return json([]);
      if (url.pathname === '/api/manufacturing/executions/start') {
        startAttempts += 1;
        const body = JSON.parse(String(init?.body)) as { operationId: string; projectId: string; panelId: string };
        operationIds.push(body.operationId);
        if (startAttempts === 1) return json({ title: '현장 네트워크를 확인해 주세요.' }, 503);
        status = 'InProgress';
        return json({
          operationId: body.operationId,
          projectId,
          panelId,
          executionId,
          status,
          version: 1,
          checkedStepCount: 0,
          totalStepCount: 4,
          pendingId: null,
          pendingNumber: null,
          panelLqcWorkCreated: false,
          projectManufacturingCompleted: false,
          replayed: false
        });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <ManufacturingPage
          developmentUserKey="dev-manufacturing"
          canMutate
          initialProjectId={projectId}
          initialPanelId={panelId}
          onBack={vi.fn()}
          onOpenPending={vi.fn()}
        />
      </AdaptiveLayoutProvider>
    );

    const page = await screen.findByTestId('manufacturing-page');
    expect(page).toHaveClass('manufacturing-page--mobile');
    expect(screen.getByRole('heading', { name: '제조 작업' })).toBeInTheDocument();
    expect(screen.getByText('제조 투입 요청됨 · 키팅 미보고')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '제조 시작' }));
    expect(await screen.findByText('현장 네트워크를 확인해 주세요.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '제조 시작' }));

    expect(await screen.findByRole('button', { name: '1단계 확인' })).toBeInTheDocument();
    await waitFor(() => expect(operationIds).toHaveLength(2));
    expect(operationIds[0]).toMatch(/^[0-9a-f-]{36}$/u);
    expect(operationIds[1]).toBe(operationIds[0]);
  });

  it('shows execution state without mutation controls for a read-only user', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/manufacturing/queue') return json(queue('InProgress', false));
      if (url.pathname === `/api/manufacturing/panels/${panelId}`) return json(detail('InProgress', false));
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <ManufacturingPage
          developmentUserKey="dev-viewer"
          canMutate={false}
          initialProjectId={projectId}
          initialPanelId={panelId}
          onBack={vi.fn()}
          onOpenPending={vi.fn()}
        />
      </AdaptiveLayoutProvider>
    );

    expect(await screen.findByText(/조회 전용입니다/u)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '1단계 확인' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '작업 중단' })).not.toBeInTheDocument();
  });

  it('serializes rapid manufacturing step clicks before React can render the disabled state', async () => {
    let releaseStep: () => void = () => undefined;
    const stepGate = new Promise<void>((resolve) => {
      releaseStep = resolve;
    });
    let stepRequests = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/manufacturing/queue') return json(queue('InProgress'));
      if (url.pathname === `/api/manufacturing/panels/${panelId}`) return json(detail('InProgress'));
      if (url.pathname === '/api/manufacturing/action-departments') return json([]);
      if (url.pathname === `/api/manufacturing/executions/${executionId}/check-step`) {
        stepRequests += 1;
        await stepGate;
        return json({ projectId, panelId });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <ManufacturingPage
          developmentUserKey="dev-manufacturing"
          canMutate
          initialProjectId={projectId}
          initialPanelId={panelId}
          onBack={vi.fn()}
          onOpenPending={vi.fn()}
        />
      </AdaptiveLayoutProvider>
    );

    const stepButton = await screen.findByRole('button', { name: '1단계 확인' });
    act(() => {
      stepButton.click();
      stepButton.click();
      stepButton.click();
    });

    await waitFor(() => expect(stepRequests).toBe(1));
    expect(stepButton).toBeDisabled();
    expect(screen.getByText('제조 단계를 저장하는 중입니다. 완료될 때까지 잠시 기다려 주세요.')).toBeInTheDocument();

    releaseStep();
    expect(await screen.findByText('작업지시·도면 확인 단계를 확인했습니다.')).toBeInTheDocument();
    expect(stepRequests).toBe(1);
  });

  it('uses the Excel selection for an explicit multi-panel assembly confirmation', async () => {
    let capturedBody: {
      operationId: string;
      projectId: string;
      panels: Array<{ panelId: string; executionId: string; expectedVersion: number }>;
    } | null = null;
    const first = { ...panel('InProgress'), assemblyStepChecked: false, assemblyStepSequence: 3 };
    const second = {
      ...panel('InProgress'),
      panelId: secondPanelId,
      displayCode: 'PANEL-02',
      executionId: secondExecutionId,
      assemblyStepChecked: false,
      assemblyStepSequence: 3
    };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/manufacturing/queue') {
        return json({
          projects: [{
            projectId,
            projectCode: 'MFG-BATCH',
            projectTitle: '다중 패널 조립',
            readyCount: 0,
            inProgressCount: 2,
            blockedCount: 0,
            completedCount: 0,
            panels: [first, second]
          }]
        });
      }
      if (url.pathname === `/api/manufacturing/panels/${panelId}`) {
        return json({ ...detail('InProgress'), panel: first });
      }
      if (url.pathname === '/api/manufacturing/action-departments') return json([]);
      if (url.pathname === '/api/manufacturing/executions/assembly-batch') {
        capturedBody = JSON.parse(String(init?.body));
        return json({
          operationId: capturedBody!.operationId,
          projectId,
          completedPanelCount: 2,
          checkedStepCount: 6,
          replayed: false
        });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <ManufacturingPage
          developmentUserKey="dev-manufacturing"
          canMutate
          initialProjectId={projectId}
          initialPanelId={panelId}
          onBack={vi.fn()}
          onOpenPending={vi.fn()}
        />
      </AdaptiveLayoutProvider>
    );

    fireEvent.click(await screen.findByRole('checkbox', { name: 'PANEL-01 선택' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'PANEL-02 선택' }));
    fireEvent.click(screen.getByRole('button', { name: '선택 패널 조립 단계 완료 (2면)' }));

    expect(await screen.findByRole('heading', { name: '조립 단계 일괄 완료' })).toBeInTheDocument();
    expect(screen.getByText('2단계')).toBeInTheDocument();
    expect(screen.getByText('선택한 패널의 조립 단계만 완료 처리합니다. 다른 제조 단계는 그대로 유지됩니다.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '2면 조립 단계 완료' }));

    expect(await screen.findByText('2면의 조립 단계만 완료했습니다. 다른 제조 단계는 유지됩니다.')).toBeInTheDocument();
    expect(capturedBody).not.toBeNull();
    const submittedBody = capturedBody as unknown as {
      projectId: string;
      panels: Array<{ panelId: string; executionId: string; expectedVersion: number }>;
    };
    expect(submittedBody.projectId).toBe(projectId);
    expect(submittedBody.panels).toEqual(expect.arrayContaining([
      { panelId, executionId, expectedVersion: 1 },
      { panelId: secondPanelId, executionId: secondExecutionId, expectedVersion: 1 }
    ]));
    expect(screen.getByText('0개 선택')).toBeInTheDocument();
  });
});

function queue(status: 'Ready' | 'InProgress', canMutate = true) {
  return {
    projects: [{
      projectId,
      projectCode: 'MFG-011A',
      projectTitle: '모바일 제조 실행',
      readyCount: status === 'Ready' ? 1 : 0,
      inProgressCount: status === 'InProgress' ? 1 : 0,
      blockedCount: 0,
      completedCount: 0,
      panels: [panel(status, canMutate)]
    }]
  };
}

function panel(status: 'Ready' | 'InProgress', canMutate = true) {
  return {
    panelId,
    displayCode: 'PANEL-01',
    panelName: 'MCC-A',
    workflowStage: status === 'Ready' ? 'BeforeManufacturing' : 'ManufacturingInProgress',
    workItemId: '74000000-0000-0000-0000-000000000001',
    workItemStatus: status === 'Ready' ? 'Requested' : 'InProgress',
    executionId: status === 'Ready' ? null : executionId,
    status,
    version: status === 'Ready' ? 0 : 1,
    checkedStepCount: 0,
    totalStepCount: status === 'Ready' ? 0 : 4,
    activePendingId: null,
    activePendingNumber: null,
    actionDepartmentCode: null,
    startedAtUtc: status === 'Ready' ? null : '2026-07-17T01:00:00Z',
    completedAtUtc: null,
    canMutate,
    assemblyStepChecked: status === 'InProgress' ? false : null,
    assemblyStepSequence: status === 'InProgress' ? 3 : null
  };
}

function detail(status: 'Ready' | 'InProgress', canMutate = true) {
  return {
    panel: panel(status, canMutate),
    steps: status === 'Ready' ? [] : [
      { stepId: '75000000-0000-0000-0000-000000000001', sequenceNumber: 1, stepName: '작업지시·도면 확인', checked: false, checkedByDisplayName: null, checkedAtUtc: null },
      { stepId: '75000000-0000-0000-0000-000000000002', sequenceNumber: 2, stepName: '자재·부품 확인', checked: false, checkedByDisplayName: null, checkedAtUtc: null },
      { stepId: '75000000-0000-0000-0000-000000000003', sequenceNumber: 3, stepName: '제조 작업 수행', checked: false, checkedByDisplayName: null, checkedAtUtc: null },
      { stepId: '75000000-0000-0000-0000-000000000004', sequenceNumber: 4, stepName: '자체 확인', checked: false, checkedByDisplayName: null, checkedAtUtc: null }
    ],
    events: status === 'Ready' ? [] : [{
      eventId: '76000000-0000-0000-0000-000000000001',
      eventType: 'Started',
      eventLabel: '제조 시작',
      stopReasonCode: null,
      stopDescription: null,
      pendingId: null,
      actorDisplayName: '제조 담당',
      createdAtUtc: '2026-07-17T01:00:00Z'
    }]
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
