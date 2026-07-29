import { render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NotificationPreferenceAuditPage } from '../src/NotificationPreferenceAuditPage';

describe('NotificationPreferenceAuditPage', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('renders shared summary, desktop row and mobile card from the admin audit endpoint', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      expect(url.pathname).toBe('/api/admin/notification-preference-audit');
      expect(url.searchParams.get('pageSize')).toBe('50');
      return json({
        items: [{
          auditEventId: '91000000-0000-0000-0000-000000000001',
          occurredAtUtc: '2026-07-20T02:30:00Z',
          targetDisplayName: '영업 담당자',
          targetDepartmentName: '영업',
          targetIsActive: true,
          actorDisplayName: '시스템 관리자',
          actorDepartmentName: '관리',
          actorIsActive: true,
          action: 'AdminSave',
          actionLabel: '관리자 대리 변경',
          deliveryType: 'DueSoonL0',
          deliveryTypeLabel: '예정일 임박 D-1',
          channel: 'TeamsDirectMessage',
          channelLabel: 'Teams 개인 알림',
          oldValue: true,
          newValue: false,
          changeLabel: '켬 → 끔',
          resultingVersion: 3
        }],
        page: 1,
        pageSize: 50,
        totalCount: 1,
        summary: { totalChanges: 1, userChanges: 0, adminChanges: 1, turnedOffChanges: 1 },
        identityNotice: '사용자명과 부서는 변경 당시 snapshot이 아니라 현재 계정 정보입니다.',
        fromDate: '2026-06-21',
        toDate: '2026-07-20'
      });
    }));

    render(<NotificationPreferenceAuditPage developmentUserKey="dev-admin" />);

    expect(await screen.findByRole('heading', { name: '알림 설정 변경 이력' })).toBeInTheDocument();
    const summary = screen.getByLabelText('변경 이력 요약');
    expect(within(summary).getByText('전체 변경')).toBeInTheDocument();
    expect(within(summary).getAllByText('1')).toHaveLength(3);
    expect(screen.getByText(/현재 계정 정보/)).toBeInTheDocument();
    expect(screen.getAllByText('관리자 대리 변경').length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByText('켬 → 끔').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByRole('checkbox', { name: '현재 목록 전체 선택' })).toBeInTheDocument();
  });
});

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}
