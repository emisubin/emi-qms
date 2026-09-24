import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { OsanCustomerMatch } from '../src/OsanCustomerMatch';
import { fetchJson } from '../src/api';

vi.mock('../src/api', () => ({ fetchJson: vi.fn() }));
const first = { customerId: 'customer-a', name: '한빛전자' };
const second = { customerId: 'customer-b', name: '한빛전자 연구소' };

function ControlledMatch({ initial = '한빛' }: { initial?: string }) {
  const [value, setValue] = useState(initial);
  const [customerId, setCustomerId] = useState<string>();
  return <><OsanCustomerMatch value={value} customerId={customerId} userKey="user-a"
    onChange={(name, id) => { setValue(name); setCustomerId(id); }} />
    <output aria-label="연결 ID">{customerId ?? '미연결'}</output></>;
}

beforeEach(() => vi.resetAllMocks());

it('고객사를 입력한 뒤 비동기 목록이 도착해도 단일 후보에 자동 연결한다', async () => {
  let resolve!: (value: { items: typeof first[] }) => void;
  vi.mocked(fetchJson).mockImplementation(() => new Promise(done => { resolve = done; }));
  render(<ControlledMatch />);
  expect(screen.getByLabelText('연결 ID')).toHaveTextContent('미연결');
  await act(async () => resolve({ items: [first] }));
  await waitFor(() => expect(screen.getByLabelText('연결 ID')).toHaveTextContent('customer-a'));
  expect(screen.getByText('연결된 고객사: 한빛전자')).toBeInTheDocument();
  expect(fetchJson).toHaveBeenCalledWith('/api/osan/customers', 'user-a', { signal: expect.any(AbortSignal) });
});

it('복수 후보는 임의 연결하지 않고 명시 선택하며, 다시 입력하면 이전 연결을 해제한다', async () => {
  vi.mocked(fetchJson).mockResolvedValue({ items: [first, second] });
  render(<ControlledMatch />);
  expect(await screen.findByText('여러 고객사가 일치합니다. 고객사를 선택해 주세요.')).toBeInTheDocument();
  expect(screen.getByLabelText('연결 ID')).toHaveTextContent('미연결');
  fireEvent.click(screen.getByRole('button', { name: '일치하는 고객사 2개 확인' }));
  fireEvent.click(screen.getByRole('button', { name: '한빛전자 연구소' }));
  expect(screen.getByLabelText('연결 ID')).toHaveTextContent('customer-b');
  expect(screen.getByLabelText('고객사')).toHaveValue('한빛전자 연구소');
  fireEvent.change(screen.getByLabelText('고객사'), { target: { value: '없는 고객' } });
  expect(screen.getByLabelText('연결 ID')).toHaveTextContent('미연결');
  expect(screen.getByText('등록된 고객사와 연결해야 저장할 수 있습니다.')).toBeInTheDocument();
});


it('삭제된 기존 고객사는 유지하며 이름을 편집했다 되돌려도 기존 연결을 복구한다', async () => {
  vi.mocked(fetchJson).mockResolvedValue({ items: [] });
  function Existing() {
    const [name, setName] = useState(first.name);
    const [id, setId] = useState<string | undefined>(first.customerId);
    return <OsanCustomerMatch value={name} customerId={id} retainedCustomer={first} onChange={(value, next) => { setName(value); setId(next); }} />;
  }
  render(<Existing />);
  expect(await screen.findByText('기존 고객사 유지: 한빛전자')).toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('고객사'), { target: { value: '다른 고객사' } });
  expect(screen.getByText('등록된 고객사와 연결해야 저장할 수 있습니다.')).toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('고객사'), { target: { value: first.name } });
  expect(screen.getByText('기존 고객사 유지: 한빛전자')).toBeInTheDocument();
});
