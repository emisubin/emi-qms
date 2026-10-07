import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { OsanUserPermissionsPage } from '../src/OsanUserPermissionsPage';
import * as api from '../src/api';

vi.mock('../src/api', () => ({ fetchJson: vi.fn() }));

const items = [
  { userId: 'u1', displayName: '김영업', departmentCode: 'sales', departmentName: '영업', allowed: true, isAdministrator: false, version: 1 },
  { userId: 'u2', displayName: '박품질', departmentCode: 'quality', departmentName: '품질', allowed: false, isAdministrator: false, version: 0 },
  { userId: 'admin', displayName: '관리자', departmentCode: 'administration', departmentName: '관리', allowed: true, isAdministrator: true, version: 0 }
];

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.fetchJson).mockResolvedValue({ items });
});

it('개인별 프로젝트 생성 권한을 버전과 함께 일괄 저장한다', async () => {
  render(<OsanUserPermissionsPage developmentUserKey="admin" />);
  const table = await screen.findByRole('table', { name: '사용자별 프로젝트 생성 권한' });
  fireEvent.click(within(table).getByRole('checkbox', { name: '박품질 프로젝트 생성 허용' }));
  expect(screen.getByText(/1명 변경/)).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
  await waitFor(() => expect(api.fetchJson).toHaveBeenCalledWith(
    '/api/osan/admin/user-project-create-permissions',
    'admin',
    expect.objectContaining({
      method: 'PUT',
      body: JSON.stringify({ items: [{ userId: 'u2', allowed: true, expectedVersion: 0 }] })
    })));
});

it('관리자는 항상 허용으로 표시하고 변경 대상에서 제외한다', async () => {
  render(<OsanUserPermissionsPage />);
  const adminRow = (await screen.findByText('관리자')).closest('tr')!;
  expect(within(adminRow).getByText('항상 허용')).toBeInTheDocument();
  expect(within(adminRow).queryByRole('checkbox')).not.toBeInTheDocument();
});

it('이름·부서·권한 필터를 기존 공통 필터 도구에서 적용한다', async () => {
  render(<OsanUserPermissionsPage />);
  await screen.findByText('김영업');
  fireEvent.change(screen.getByLabelText('이름 검색'), { target: { value: '박' } });
  fireEvent.click(screen.getByRole('button', { name: '검색' }));
  expect(screen.getByText('박품질')).toBeInTheDocument();
  expect(screen.queryByText('김영업')).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '필터' }));
  fireEvent.click(screen.getByRole('button', { name: '프로젝트 생성 필터: 전체' }));
  fireEvent.click(screen.getByRole('checkbox', { name: '허용' }));
  fireEvent.click(screen.getByRole('button', { name: '적용' }));
  expect(screen.getByText('검색 결과가 없습니다.')).toBeInTheDocument();
});

it('변경 취소는 서버 요청 없이 원래 권한으로 되돌린다', async () => {
  render(<OsanUserPermissionsPage />);
  const checkbox = await screen.findByRole('checkbox', { name: '김영업 프로젝트 생성 허용' });
  fireEvent.click(checkbox);
  expect(checkbox).not.toBeChecked();
  fireEvent.click(screen.getByRole('button', { name: '취소' }));
  expect(checkbox).toBeChecked();
  expect(api.fetchJson).toHaveBeenCalledTimes(1);
});

it('동시 변경 충돌은 편집 내용을 버리고 최신 버전을 다시 불러올 수 있다', async () => {
  vi.mocked(api.fetchJson)
    .mockReset()
    .mockResolvedValueOnce({ items })
    .mockRejectedValueOnce(Object.assign(new Error('사용자 권한이 변경되었습니다.'), { status: 409 }))
    .mockResolvedValueOnce({ items: items.map(item => item.userId === 'u1' ? { ...item, version: 2 } : item) });
  render(<OsanUserPermissionsPage />);
  const checkbox = await screen.findByRole('checkbox', { name: '김영업 프로젝트 생성 허용' });
  fireEvent.click(checkbox);
  fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('화면에서 변경한 내용은 취소됩니다');
  fireEvent.click(screen.getByRole('button', { name: '최신 권한 다시 불러오기' }));
  await waitFor(() => expect(api.fetchJson).toHaveBeenCalledTimes(3));
  expect(await screen.findByRole('checkbox', { name: '김영업 프로젝트 생성 허용' })).toBeChecked();
});
