import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OsanNotificationSettings } from '../src/OsanNotificationSettings';
import { getOsanNotificationPreferences, saveOsanNotificationPreferences } from '../src/osanNotificationPreferences';

vi.mock('../src/osanNotificationPreferences', async () => {
  const actual = await vi.importActual<typeof import('../src/osanNotificationPreferences')>('../src/osanNotificationPreferences');
  return { ...actual, getOsanNotificationPreferences: vi.fn(), saveOsanNotificationPreferences: vi.fn() };
});

const response = {
  version: 3,
  items: [
    { kind: 'ProjectCreated', label: '프로젝트 생성', mailEnabled: true, pushEnabled: true },
    { kind: 'StepCompleted', label: 'Gate 완료', mailEnabled: true, pushEnabled: true },
    { kind: 'StepRejected', label: '진행단계 반려', mailEnabled: true, pushEnabled: true },
    { kind: 'StepEdited', label: '진행단계 수정 완료', mailEnabled: true, pushEnabled: true },
    { kind: 'StepIssueRegistered', label: '공정 이상 발생', mailEnabled: true, pushEnabled: true },
    { kind: 'StepIssueResolved', label: '이상 조치 완료', mailEnabled: true, pushEnabled: true },
    { kind: 'ProjectCompleted', label: '프로젝트 완료', mailEnabled: true, pushEnabled: true }
  ],
  stepCompletedStages: ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장']
    .map((label, index) => ({ sequence: index + 1, label, mailEnabled: true, pushEnabled: true }))
};

describe('OsanNotificationSettings', () => {
  afterEach(() => vi.clearAllMocks());

  it('preserves stage choices while a parent channel is off and saves the full draft', async () => {
    vi.mocked(getOsanNotificationPreferences).mockResolvedValue(structuredClone(response));
    vi.mocked(saveOsanNotificationPreferences).mockResolvedValue({ ...structuredClone(response), version: 4 });
    const close = vi.fn();
    render(<OsanNotificationSettings contextKey="OSAN:user" mutationAllowed developmentUserKey="dev-sales" onClose={close} />);

    const dialog = await screen.findByRole('dialog', { name: '오산 알림 설정' });
    fireEvent.click(within(dialog).getByRole('switch', { name: 'Gate 완료 메일' }));
    fireEvent.click(within(dialog).getByRole('button', { name: /단계별 상세 설정/ }));
    expect(await within(dialog).findByText(/메일 전체 수신 꺼짐/)).toBeInTheDocument();
    expect(within(dialog).getByRole('switch', { name: '입고검사 메일' })).toBeDisabled();
    expect(within(dialog).getByRole('switch', { name: '입고검사 메일' })).toHaveAttribute('aria-checked', 'true');
    fireEvent.click(within(dialog).getByRole('button', { name: '저장' }));

    await waitFor(() => expect(saveOsanNotificationPreferences).toHaveBeenCalledTimes(1));
    const request = vi.mocked(saveOsanNotificationPreferences).mock.calls[0][1];
    expect(request.expectedVersion).toBe(3);
    expect(request.items.find((item) => item.kind === 'StepCompleted')?.mailEnabled).toBe(false);
    expect(request.stepCompletedStages.every((stage) => stage.mailEnabled)).toBe(true);
    expect(close).toHaveBeenCalled();
  });

  it('discards a changed draft on cancel without saving', async () => {
    vi.mocked(getOsanNotificationPreferences).mockResolvedValue(structuredClone(response));
    const close = vi.fn();
    render(<OsanNotificationSettings contextKey="OSAN:user" mutationAllowed developmentUserKey="dev-sales" onClose={close} />);
    const dialog = await screen.findByRole('dialog', { name: '오산 알림 설정' });
    fireEvent.click(within(dialog).getByRole('switch', { name: '프로젝트 생성 푸시' }));
    fireEvent.click(within(dialog).getByRole('button', { name: '취소' }));
    expect(saveOsanNotificationPreferences).not.toHaveBeenCalled();
    expect(close).toHaveBeenCalledOnce();
  });

  it('keeps the draft open and announces a save failure', async () => {
    vi.mocked(getOsanNotificationPreferences).mockResolvedValue(structuredClone(response));
    vi.mocked(saveOsanNotificationPreferences).mockRejectedValue(new Error('network'));
    render(<OsanNotificationSettings contextKey="OSAN:user" mutationAllowed developmentUserKey="dev-sales" onClose={vi.fn()} />);
    const dialog = await screen.findByRole('dialog', { name: '오산 알림 설정' });
    fireEvent.click(within(dialog).getByRole('switch', { name: '프로젝트 완료 메일' }));
    fireEvent.click(within(dialog).getByRole('button', { name: '저장' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('알림 설정을 저장하지 못했습니다.');
    expect(within(dialog).getByRole('switch', { name: '프로젝트 완료 메일' })).toHaveAttribute('aria-checked', 'false');
  });
});
