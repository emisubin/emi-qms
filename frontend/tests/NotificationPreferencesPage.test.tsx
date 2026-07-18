import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { NotificationPreferencesPage } from '../src/NotificationPreferencesPage';
import type { NotificationPreferenceResponse } from '../src/notificationPreferences';

describe('NotificationPreferencesPage', () => {
  beforeEach(() => setRuntimeMutationAllowed(true));

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('shows only three editable mobile-friendly controls and saves then resets preferences', async () => {
    const requests: Array<{ method: string; path: string; body?: unknown }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      const method = init?.method ?? 'GET';
      const body = init?.body ? JSON.parse(String(init.body)) : undefined;
      requests.push({ method, path: url.pathname, body });
      if (method === 'PUT') return json(response(1, false, true));
      if (method === 'POST') return json(response(2, true, false));
      return json(response(0, true, false));
    }));

    render(
      <NotificationPreferencesPage
        developmentUserKey="dev-sales"
        onBack={vi.fn()}
      />
    );

    expect(await screen.findByRole('heading', { name: '내 알림 설정' })).toBeInTheDocument();
    expect(screen.getByText('인앱 알림은 항상 저장되고 통합 채널 공지는 조직 공지로 유지됩니다.')).toBeInTheDocument();
    expect(screen.getAllByRole('switch')).toHaveLength(3);
    expect(screen.getAllByText((_, element) => (
      element?.tagName === 'SMALL'
      && element.textContent?.includes('업무상 필수 알림은 해제할 수 없습니다.') === true
    ))).toHaveLength(4);

    const dailyCard = screen.getByText('일일 업무 요약').closest('article');
    const dailyToggle = dailyCard?.querySelector<HTMLInputElement>('input[role="switch"]');
    expect(dailyToggle).not.toBeNull();
    fireEvent.click(dailyToggle!);
    fireEvent.click(screen.getByRole('button', { name: '설정 저장' }));

    await screen.findByText('알림 설정을 저장했습니다.');
    const put = requests.find((request) => request.method === 'PUT');
    expect(put?.path).toBe('/api/my/notification-preferences');
    expect(put?.body).toMatchObject({ expectedVersion: 0 });
    expect((put?.body as { items: unknown[] }).items).toHaveLength(3);

    fireEvent.click(screen.getByRole('button', { name: '기본값 복원' }));
    await screen.findByText('기본 알림 설정으로 복원했습니다.');
    expect(requests.some((request) => request.method === 'POST' && request.path.endsWith('/reset'))).toBe(true);
  });

  it('uses the admin support endpoint for a selected active user', async () => {
    let requestedUrl = '';
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      requestedUrl = String(input);
      return json(response(0, true, false));
    });
    vi.stubGlobal('fetch', fetchMock);

    render(
      <NotificationPreferencesPage
        developmentUserKey="dev-admin"
        targetUserId="50000000-0000-0000-0000-000000000002"
        onBack={vi.fn()}
      />
    );

    expect(await screen.findByRole('heading', { name: '사용자 알림 설정 지원' })).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(requestedUrl).toContain('/api/admin/users/50000000-0000-0000-0000-000000000002/notification-preferences');
  });
});

function response(version: number, isDefault: boolean, dailyDisabled: boolean): NotificationPreferenceResponse {
  return {
    userId: '50000000-0000-0000-0000-000000000002',
    userDisplayName: '검수 사용자',
    taxonomyVersion: '2026-07-v1',
    version,
    isDefault,
    changed: version > 0,
    items: [
      item('WorkItemCreated', 'TeamsDirectMessage', '자동 단계 업무 생성', 'Teams 개인 알림', true, true, false),
      item('DueSoonL0', 'TeamsDirectMessage', '예정일 임박 D-1', 'Teams 개인 알림', true, true, false),
      item('DailyDigest', 'Mail', '일일 업무 요약', '메일', !dailyDisabled, true, dailyDisabled),
      item('UrgentBlocking', 'Mail', '긴급·차단', '메일', true, false, false),
      item('OverdueL1', 'TeamsDirectMessage', '예정일 초과 L1', 'Teams 개인 알림 · 메일', true, false, false),
      item('OverdueL2', 'TeamsDirectMessage', '예정일 초과 L2', 'Teams 개인 알림', true, false, false),
      item('OverdueL3', 'Mail', '예정일 초과 L3', '메일', true, false, false)
    ]
  };
}

function item(
  deliveryType: string,
  channel: string,
  eventLabel: string,
  channelLabel: string,
  enabled: boolean,
  canChange: boolean,
  isOverridden: boolean
) {
  return {
    deliveryType,
    channel,
    eventLabel,
    channelLabel,
    description: `${eventLabel} 설명`,
    enabled,
    canChange,
    isOverridden,
    lockReason: canChange ? null : '업무상 필수 알림은 해제할 수 없습니다.'
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
