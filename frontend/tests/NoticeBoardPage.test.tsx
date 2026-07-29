import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { NoticeBoardPage } from '../src/NoticeBoardPage';

const noticeId = '81000000-0000-0000-0000-000000000001';

describe('NoticeBoardPage', () => {
  beforeEach(() => {
    setRuntimeMutationAllowed(true);
  });

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('shows a compact notice list and opens the selected notice', async () => {
    const onOpenNotice = vi.fn();
    vi.stubGlobal('fetch', vi.fn(async () => json(listResponse())));
    renderPage({ onOpenNotice });

    const list = await screen.findByLabelText('공지 목록');
    const item = within(list).getByRole('button', { name: /7월 생산 일정 안내/ });
    expect(item).toHaveTextContent('영업');
    expect(item).toHaveTextContent('생산 계획 변경사항');
    fireEvent.click(item);
    expect(onOpenNotice).toHaveBeenCalledWith(noticeId);
  });

  it('keeps empty fields focused and reports plain validation messages', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json(listResponse())));
    renderPage({ compose: true });

    fireEvent.click(await screen.findByRole('button', { name: '공지 등록' }));
    expect(await screen.findByText('제목을 입력해 주세요.')).toBeInTheDocument();
    expect(screen.getByText('내용을 입력해 주세요.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /제목/ })).toHaveFocus();
  });

  it('creates a notice with a UUID request id and keeps the authenticated author server-owned', async () => {
    const calls: Array<{ path: string; method: string; body?: Record<string, unknown> }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://localhost');
      calls.push({
        path: url.pathname,
        method: init?.method ?? 'GET',
        body: init?.body ? JSON.parse(String(init.body)) as Record<string, unknown> : undefined
      });
      if (url.pathname === '/api/notices' && init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as { title: string; body: string };
        return json(detailResponse(body.title, body.body));
      }
      return json(listResponse());
    }));
    const onOpenNotice = vi.fn();
    renderPage({ compose: true, onOpenNotice });

    fireEvent.change(screen.getByRole('textbox', { name: /제목/ }), { target: { value: '설비 점검 안내' } });
    fireEvent.change(screen.getByRole('textbox', { name: /내용/ }), { target: { value: '금요일 오후 설비 점검을 진행합니다.' } });
    fireEvent.click(screen.getByRole('button', { name: '공지 등록' }));

    await waitFor(() => expect(onOpenNotice).toHaveBeenCalledWith(noticeId));
    const createCall = calls.find((call) => call.method === 'POST');
    expect(createCall?.body?.requestId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
    expect(createCall?.body).toEqual(expect.objectContaining({ title: '설비 점검 안내', body: '금요일 오후 설비 점검을 진행합니다.' }));
    expect(createCall?.body).not.toHaveProperty('authorDisplayName');
    expect(createCall?.body).not.toHaveProperty('authorDepartmentName');
  });
});

function renderPage(overrides: Partial<Parameters<typeof NoticeBoardPage>[0]> = {}) {
  const props: Parameters<typeof NoticeBoardPage>[0] = {
    developmentUserKey: 'dev-sales',
    mutationEnabled: true,
    onOpenHome: vi.fn(),
    onOpenList: vi.fn(),
    onOpenNotice: vi.fn(),
    onOpenCompose: vi.fn(),
    ...overrides
  };
  return render(<NoticeBoardPage {...props} />);
}

function listResponse() {
  return {
    items: [{
      noticeId,
      title: '7월 생산 일정 안내',
      preview: '생산 계획 변경사항을 확인해 주세요.',
      authorDisplayName: '영업 담당자',
      authorDepartmentName: '영업',
      createdAtUtc: '2026-07-21T01:00:00Z',
      canDelete: true
    }],
    totalCount: 1,
    page: 1,
    pageSize: 20
  };
}

function detailResponse(title = '7월 생산 일정 안내', body = '생산 계획 변경사항을 확인해 주세요.') {
  return {
    noticeId,
    title,
    body,
    authorDisplayName: '영업 담당자',
    authorDepartmentName: '영업',
    createdAtUtc: '2026-07-21T01:00:00Z',
    canDelete: true
  };
}

function json(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
}
