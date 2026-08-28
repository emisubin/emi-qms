import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuditPage } from '../src/AuditPage';

describe('AuditPage', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('renders desktop and narrow-screen records and shows the linked login context', async () => {
    const eventId = '91000000-0000-0000-0000-000000000001';
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/admin/audit-events') {
        return json({
          items: [{
            eventId,
            source: 'Global',
            occurredAtUtc: '2026-08-28T01:00:00Z',
            eventType: 'MutationSucceeded',
            actorUserId: '92000000-0000-0000-0000-000000000001',
            actorDisplayName: '감사 사용자',
            actorDepartmentName: '품질',
            actualActorUserId: '92000000-0000-0000-0000-000000000099',
            actualActorDisplayName: '실제 관리자',
            domain: 'Quality',
            action: 'FinalizeInspection',
            targetType: 'panel_quality_reports',
            targetKey: '93000000-0000-0000-0000-000000000001',
            outcome: 'Succeeded',
            failureReason: null,
            reasonSummary: null,
            loginCorrelationId: '94000000-0000-0000-0000-000000000001',
            changeCount: 2,
            clientIp: null,
            browserFamily: null,
            osFamily: null,
            appAccessOutcome: null
          }],
          page: 1,
          pageSize: 50,
          totalCount: 1,
          summary: { totalEvents: 1, loginEvents: 0, successfulChanges: 1, failedChanges: 0, authorizationDenials: 0 },
          coverage: { coverageStartedAtUtc: '2026-08-28T00:00:00Z', completenessNotice: '2026-08-28 00:00:00 UTC 이후 전체 기록입니다.' },
          fromDate: '2026-07-30',
          toDate: '2026-08-28'
        });
      }
      if (url.pathname === `/api/admin/audit-events/${eventId}`) {
        return json({
          event: {
            eventId,
            source: 'Global',
            occurredAtUtc: '2026-08-28T01:00:00Z',
            eventType: 'MutationSucceeded',
            actorUserId: '92000000-0000-0000-0000-000000000001',
            actorDisplayName: '감사 사용자',
            actorDepartmentName: '품질',
            actualActorUserId: '92000000-0000-0000-0000-000000000099',
            actualActorDisplayName: '실제 관리자',
            domain: 'Quality',
            action: 'FinalizeInspection',
            targetType: 'panel_quality_reports',
            targetKey: '93000000-0000-0000-0000-000000000001',
            outcome: 'Succeeded',
            failureReason: null,
            reasonSummary: null,
            loginCorrelationId: '94000000-0000-0000-0000-000000000001',
            changeCount: 2,
            clientIp: null,
            browserFamily: null,
            osFamily: null,
            appAccessOutcome: null
          },
          changes: [{
            changeId: 1,
            rowAction: 'Update',
            targetType: 'panel_quality_reports',
            targetKey: '93000000-0000-0000-0000-000000000001',
            fieldCode: 'panel_quality_reports.status',
            projectionKind: 'ExactScalar',
            beforeValue: 'Draft',
            afterValue: 'Finalized',
            beforeLength: null,
            afterLength: null
          }],
          loginContext: {
            occurredAtUtc: '2026-08-28T00:30:00Z',
            clientIp: '192.0.2.10',
            browserFamily: 'Edge',
            osFamily: 'Windows',
            appAccessOutcome: 'Allowed'
          },
          valueNotice: '고정 형식 값만 변경 전후를 표시합니다.'
        });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(<AuditPage developmentUserKey="dev-admin" />);

    expect(await screen.findByRole('heading', { name: '전체 감사 이력' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: '입력값 확인' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: '동시 수정·상태 충돌' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: '중복 요청' })).not.toBeInTheDocument();
    expect(screen.getAllByText('감사 사용자').length).toBeGreaterThanOrEqual(2);
    const desktopTable = document.querySelector('.audit-desktop-table');
    expect(desktopTable).not.toBeNull();
    expect(within(desktopTable as HTMLElement).getByText('실제 사용자: 실제 관리자')).toBeInTheDocument();
    const mobileList = screen.getByLabelText('감사 이력 모바일 목록');
    expect(within(mobileList).getByText('실제 사용자: 실제 관리자')).toBeInTheDocument();
    expect(within(mobileList).getByText('상세 보기')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '상세 보기' }));
    const detail = await screen.findByLabelText('감사 사건 상세');
    expect(within(detail).getByText('실제 사용자')).toBeInTheDocument();
    expect(within(detail).getByText('실제 관리자')).toBeInTheDocument();
    expect(within(detail).getByText('연결 로그인')).toBeInTheDocument();
    expect(within(detail).getByText(/192\.0\.2\.10 · Edge · Windows/)).toBeInTheDocument();
    expect(within(detail).getByText('Draft')).toBeInTheDocument();
    expect(within(detail).getByText('Finalized')).toBeInTheDocument();
  });
});

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
