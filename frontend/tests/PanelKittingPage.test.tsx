import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdaptiveLayoutProvider } from '../src/adaptive-layout';
import { setRuntimeMutationAllowed } from '../src/api';
import { PanelKittingPage } from '../src/PanelKittingPage';

const projectId = '71000000-0000-0000-0000-000000000010';
const firstPanelId = '72000000-0000-0000-0000-000000000001';

describe('PanelKittingPage', () => {
  beforeEach(() => {
    window.history.pushState(null, '', '/materials/kitting');
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

  it('uses a mobile-first panel flow and reuses the operation id after a failed request', async () => {
    const operationIds: string[] = [];
    let completionAttempt = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/materials/kitting' && (!init?.method || init.method === 'GET')) {
        return json({
          projects: [{
            projectId,
            projectCode: 'KIT-010A',
            projectTitle: '모바일 패널 키팅',
            activeItemCount: 2,
            completedItemCount: 2,
            ready: true,
            pendingPanelCount: 2,
            completedPanelCount: 0,
            panels: [
              {
                panelId: firstPanelId,
                displayCode: 'PANEL-01',
                panelName: 'MCC-A',
                panelInfoCompleted: true,
                kittingCompleted: false,
                completedAtUtc: null,
                completedByDisplayName: null,
                selectable: true
              },
              {
                panelId: '72000000-0000-0000-0000-000000000002',
                displayCode: 'PANEL-02',
                panelName: 'MCC-B',
                panelInfoCompleted: true,
                kittingCompleted: false,
                completedAtUtc: null,
                completedByDisplayName: null,
                selectable: true
              }
            ]
          }]
        });
      }

      if (url.pathname === '/api/materials/kitting/complete') {
        completionAttempt += 1;
        const body = JSON.parse(String(init?.body)) as { operationId: string; panelIds: string[] };
        operationIds.push(body.operationId);
        expect(body.panelIds).toEqual([firstPanelId]);
        if (completionAttempt === 1) {
          return json({ title: '잠시 연결할 수 없습니다.' }, 503);
        }
        return json({
          operationId: body.operationId,
          completedPanelCount: 1,
          generatedWorkItemCount: 0,
          projectKittingCompleted: false,
          replayed: false
        });
      }

      return json({ title: 'not found' }, 404);
    }));

    render(
      <AdaptiveLayoutProvider>
        <PanelKittingPage
          developmentUserKey="dev-materials"
          canComplete
          initialPanelId={firstPanelId}
          onBack={vi.fn()}
          onOpenReceipts={vi.fn()}
        />
      </AdaptiveLayoutProvider>
    );

    const page = await screen.findByTestId('panel-kitting-page');
    expect(page).toHaveClass('panel-kitting-page--mobile');
    expect(screen.getByRole('heading', { name: '패널 키팅' })).toBeInTheDocument();
    expect(screen.getByText('전체 입고 완료 · 실제 키팅을 마친 패널만 알려 주세요.')).toBeInTheDocument();
    expect(screen.getByText('연결된 패널').closest('button')).toHaveAttribute('data-linked', 'true');

    fireEvent.click(screen.getByRole('button', { name: /PANEL-01.*MCC-A/u }));
    const completeButton = screen.getByRole('button', { name: '1면 키팅 완료 알림' });
    fireEvent.click(completeButton);
    expect(await screen.findByText(/잠시 연결할 수 없습니다.*잠시 후 다시 시도해 주세요/u)).toHaveAttribute('data-tone', 'error');

    fireEvent.click(screen.getByRole('button', { name: '1면 키팅 완료 알림' }));
    expect(await screen.findByText('1면의 키팅 완료 상태를 공유했습니다.')).toHaveAttribute('data-tone', 'success');
    await waitFor(() => expect(operationIds).toHaveLength(2));
    expect(operationIds[0]).toMatch(/^[0-9a-f-]{36}$/u);
    expect(operationIds[1]).toBe(operationIds[0]);
  });
});

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
