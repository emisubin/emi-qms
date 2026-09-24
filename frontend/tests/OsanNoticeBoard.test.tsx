import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { OsanNoticeBoard, OsanNoticePopups } from '../src/OsanNoticeBoard';
import * as api from '../src/api';
import type { NoticeDetail } from '../src/notices';

vi.mock('../src/api', () => ({
  createNotice: vi.fn(), deleteNotice: vi.fn(), deleteNoticeAttachment: vi.fn(),
  downloadNoticeAttachment: vi.fn(), fetchJson: vi.fn(), getNotice: vi.fn(),
  listNotices: vi.fn(), updateNotice: vi.fn(), uploadNoticeAttachment: vi.fn()
}));

const detail: NoticeDetail = {
  noticeId: 'notice-a', title: '업데이트 안내', body: '변경된 기능', bodyFormat: 'PlainTextV1', version: 1,
  authorDisplayName: '작성자', authorDepartmentName: '제조', createdAtUtc: '2026-09-24T00:00:00Z',
  updatedAtUtc: null, canEdit: true, canDelete: true, attachments: []
};

beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(api.createNotice).mockResolvedValue(detail);
  vi.mocked(api.updateNotice).mockResolvedValue({ ...detail, version: 2 });
  vi.mocked(api.getNotice).mockResolvedValue(detail);
  vi.mocked(api.uploadNoticeAttachment).mockResolvedValue({} as Awaited<ReturnType<typeof api.uploadNoticeAttachment>>);
  vi.mocked(api.fetchJson).mockResolvedValue({});
});

it('공지 저장 후 두 번째 첨부 실패 시 본문과 성공한 첨부를 반복하지 않는다', async () => {
  const onOpen = vi.fn();
  vi.mocked(api.uploadNoticeAttachment)
    .mockResolvedValueOnce({} as Awaited<ReturnType<typeof api.uploadNoticeAttachment>>)
    .mockRejectedValueOnce(new Error('첨부 저장 실패'));
  render(<OsanNoticeBoard userKey="user-a" compose admin={false} enabled onList={vi.fn()} onOpen={onOpen} onCompose={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('제목'), { target: { value: '업데이트 안내' } });
  fireEvent.change(screen.getByLabelText('내용'), { target: { value: '변경된 기능' } });
  fireEvent.change(screen.getByLabelText('첨부파일'), { target: { files: [
    new File(['first'], 'first.pdf', { type: 'application/pdf' }),
    new File(['second'], 'second.pdf', { type: 'application/pdf' })
  ] } });
  fireEvent.click(screen.getByRole('button', { name: '저장' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('첨부 저장 실패');
  expect(screen.getByText('second.pdf')).toBeInTheDocument();
  expect(screen.queryByText('first.pdf')).not.toBeInTheDocument();
  expect(api.createNotice).toHaveBeenCalledOnce();
  expect(onOpen).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: '저장' }));
  await waitFor(() => expect(onOpen).toHaveBeenCalledWith('notice-a'));
  expect(api.createNotice).toHaveBeenCalledOnce();
  expect(api.updateNotice).toHaveBeenCalledWith('user-a', 'notice-a', expect.objectContaining({ expectedVersion: 1 }));
  expect(api.uploadNoticeAttachment).toHaveBeenCalledTimes(3);
  expect(vi.mocked(api.uploadNoticeAttachment).mock.calls.map(([, , file]) => file.name))
    .toEqual(['first.pdf', 'second.pdf', 'second.pdf']);
});

it('상세를 읽으면 계정 읽음을 기록하고 관리자의 팝업 설정을 버전으로 갱신한다', async () => {
  const settings = { version: 1, pinned: false, popupEnabled: false, popupVersion: 0 };
  vi.mocked(api.fetchJson).mockImplementation(async (url, _user, init) => url.endsWith('/settings')
    ? init?.method === 'PUT' ? { ...settings, version: 2, popupEnabled: true, popupVersion: 1 } : settings
    : {});
  render(<OsanNoticeBoard userKey="admin" noticeId="notice-a" admin enabled onList={vi.fn()} onOpen={vi.fn()} onCompose={vi.fn()} />);
  await screen.findByRole('heading', { name: '업데이트 안내' });
  await waitFor(() => expect(api.fetchJson).toHaveBeenCalledWith('/api/osan/notices/notice-a/read', 'admin', { method: 'POST' }));
  fireEvent.click(screen.getByText('관리자 설정'));
  fireEvent.click(screen.getByRole('checkbox', { name: '팝업 표시' }));
  await waitFor(() => expect(vi.mocked(api.fetchJson).mock.calls.some(([url, , init]) => url.endsWith('/settings') && init?.method === 'PUT')).toBe(true));
  const call = vi.mocked(api.fetchJson).mock.calls.find(([url, , init]) => url.endsWith('/settings') && init?.method === 'PUT')!;
  expect(JSON.parse(call[2]!.body as string)).toMatchObject({ expectedVersion: 1, pinned: false, popupEnabled: true, reannounce: false });
  await waitFor(() => expect(screen.getByRole('checkbox', { name: '팝업 표시' })).toBeChecked());
});

it('팝업 청구에 성공한 공지만 표시하고 사용자의 확인 뒤 다시 띄우지 않는다', async () => {
  const first = { noticeId: 'notice-a', title: '첫 공지', body: '첫 내용', popupVersion: 1 };
  const second = { noticeId: 'notice-b', title: '두 번째 공지', body: '두 번째 내용', popupVersion: 2 };
  let listed = false;
  vi.mocked(api.fetchJson).mockImplementation(async url => {
    if (url === '/api/osan/notices/popups') {
      if (listed) return { items: [] };
      listed = true;
      return { items: [first, second] };
    }
    return { claimed: url.includes('/notice-b/') };
  });
  const props = { userKey: 'user-a', scope: 'user-a', onOpen: vi.fn() };
  const view = render(<OsanNoticePopups {...props} />);
  expect(await screen.findByRole('heading', { name: '두 번째 공지' })).toBeInTheDocument();
  expect(screen.queryByText('첫 공지')).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '확인' }));
  expect(screen.queryByRole('dialog', { name: '공지사항' })).not.toBeInTheDocument();
  view.unmount();
  render(<OsanNoticePopups {...props} />);
  await waitFor(() => expect(api.fetchJson).toHaveBeenCalledTimes(4));
  expect(screen.queryByRole('dialog', { name: '공지사항' })).not.toBeInTheDocument();
  expect(vi.mocked(api.fetchJson).mock.calls.filter(([url]) => url.includes('/claim'))).toHaveLength(2);
});

it('계정을 바꾸면 이전 계정의 열린 공지를 즉시 닫는다', async () => {
  vi.mocked(api.fetchJson).mockImplementation(async (url, userKey) =>
    url === '/api/osan/notices/popups'
      ? { items: userKey === 'user-a' ? [{ noticeId: 'notice-a', title: '이전 계정 공지', body: '이전 내용', popupVersion: 1 }] : [] }
      : { claimed: true });
  const props = { scope: 'OSAN', onOpen: vi.fn() };
  const { rerender } = render(<OsanNoticePopups {...props} userKey="user-a" />);
  expect(await screen.findByRole('heading', { name: '이전 계정 공지' })).toBeInTheDocument();
  rerender(<OsanNoticePopups {...props} userKey="user-b" />);
  await waitFor(() => expect(vi.mocked(api.fetchJson).mock.calls).toContainEqual(['/api/osan/notices/popups', 'user-b']));
  expect(screen.queryByRole('dialog', { name: '공지사항' })).not.toBeInTheDocument();
});
