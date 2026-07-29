import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { PendingInspectionContext } from '../src/PendingInspectionContext';

const pendingId = '91000000-0000-0000-0000-000000000099';

describe('PendingInspectionContext', () => {
  beforeEach(() => setRuntimeMutationAllowed(true));

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('shows action history and comments together and adds a reinspection comment', async () => {
    const postedBodies: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as { body: string };
        postedBodies.push(body.body);
        return json(detail([{ commentId: 'comment-2', body: body.body, createdByUserId: 'quality-user', createdByDisplayName: '품질 담당', createdAtUtc: '2026-07-21T02:00:00Z' }]));
      }
      return json(detail([{ commentId: 'comment-1', body: '단자 교체와 토크 재확인 완료', createdByUserId: 'materials-user', createdByDisplayName: '자재 담당', createdAtUtc: '2026-07-21T01:00:00Z' }]));
    }));

    render(<PendingInspectionContext pendingId={pendingId} developmentUserKey="dev-quality" />);

    expect(await screen.findByRole('heading', { name: '조치 내용과 재검사 코멘트' })).toBeInTheDocument();
    expect(screen.getAllByText('조치 완료 후 재검사 요청')).toHaveLength(2);
    expect(screen.getByText('단자 교체와 토크 재확인 완료')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('재검사 코멘트'), { target: { value: '교체 부위 외관과 체결 토크를 재확인했습니다.' } });
    fireEvent.click(screen.getByRole('button', { name: '코멘트 등록' }));

    expect(await screen.findByText('재검사 코멘트를 Pending 이력에 추가했습니다.')).toBeInTheDocument();
    expect(screen.getByText('교체 부위 외관과 체결 토크를 재확인했습니다.')).toBeInTheDocument();
    await waitFor(() => expect(postedBodies).toEqual(['교체 부위 외관과 체결 토크를 재확인했습니다.']));
  });
});

function detail(comments: Array<Record<string, unknown>>) {
  return {
    issue: {
      pendingId,
      issueNumber: 99,
      projectId: 'project-1',
      projectCode: 'PRJ-099',
      projectTitle: '재검사 순환 검증',
      targetType: 'Inspection',
      targetId: 'receipt-1',
      targetLabel: null,
      issueType: 'Nonconformance',
      issueTypeLabel: '부적합',
      title: 'IQC 부적합 · Contactor',
      description: '단자 외관 균열이 확인되었습니다.',
      status: 'ReinspectionRequested',
      statusLabel: '재검사 요청',
      priority: 'Urgent',
      priorityLabel: '긴급',
      actionDepartmentCode: 'materials',
      assigneeUserId: 'materials-user',
      assigneeDisplayName: '자재 담당',
      dueDate: null,
      isOverdue: false,
      version: 4,
      createdByUserId: 'quality-user',
      createdByDisplayName: '품질 담당',
      createdAtUtc: '2026-07-21T00:00:00Z',
      updatedAtUtc: '2026-07-21T01:30:00Z'
    },
    comments,
    history: [{
      historyId: 'history-1',
      eventType: 'StatusChanged',
      eventLabel: '상태 변경',
      fromStatus: 'InProgress',
      fromStatusLabel: '조치 중',
      toStatus: 'ReinspectionRequested',
      toStatusLabel: '재검사 요청',
      fromAssigneeDisplayName: '자재 담당',
      toAssigneeDisplayName: '자재 담당',
      reason: '조치 완료 후 재검사 요청',
      changedByUserId: 'materials-user',
      changedByDisplayName: '자재 담당',
      createdAtUtc: '2026-07-21T00:30:00Z'
    }],
    allowedTransitions: [],
    canComment: true,
    canAssign: false,
    reinspection: null
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
