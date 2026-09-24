import { expect, it, vi } from 'vitest';
import { getOsanDashboard } from '../src/osanDashboard';
import * as api from '../src/api';

vi.mock('../src/api', () => ({ fetchJson: vi.fn() }));

it('복수 조건과 납기 범위 및 50개 페이지를 서버 쿼리로 전달한다', async () => {
  vi.mocked(api.fetchJson).mockResolvedValue({ items: [] });
  const signal = new AbortController().signal;
  await getOsanDashboard('user', {
    search: 'panel', customers: ['고객 A', '고객 B'], statuses: ['NotStarted', 'InProgress'],
    dueFrom: '2026-09-01', dueTo: '2026-09-30', kpi: 'OpenIssue', page: 2, view: 'home'
  }, signal);
  const [path, userKey, init] = vi.mocked(api.fetchJson).mock.calls[0];
  const url = new URL(path, 'https://local.test');
  expect(url.searchParams.getAll('customer')).toEqual(['고객 A', '고객 B']);
  expect(url.searchParams.getAll('status')).toEqual(['NotStarted', 'InProgress']);
  expect(url.searchParams.get('dueFrom')).toBe('2026-09-01');
  expect(url.searchParams.get('dueTo')).toBe('2026-09-30');
  expect(url.searchParams.get('kpi')).toBe('OpenIssue');
  expect(url.searchParams.get('pageSize')).toBe('50');
  expect(url.searchParams.get('page')).toBe('2');
  expect(url.searchParams.get('view')).toBe('home');
  expect(userKey).toBe('user');
  expect(init).toEqual({ signal });
});
