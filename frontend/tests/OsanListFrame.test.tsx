import { fireEvent, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { OsanListFrame } from '../src/OsanListFrame';

const customers = ['한빛전자', '미래물산', '새봄전자'];

function FrameHarness({ initialCustomers = [], onCustomersApplied = vi.fn() }: {
  initialCustomers?: string[]; onCustomersApplied?: (values: string[]) => void;
}) {
  const [selectedCustomers, setSelectedCustomers] = useState(initialCustomers);
  const [statuses, setStatuses] = useState<string[]>([]);
  return <div className="app-shell">
    <OsanListFrame title="진행 현황" description="설명" counts={[3, 1, 1, 1]}
      search="" onSearchChange={vi.fn()} onSearch={vi.fn()}
      statuses={statuses} onStatusesChange={setStatuses} onReset={vi.fn()}
      selectedCustomers={selectedCustomers} customers={customers} onCustomersChange={values => {
        setSelectedCustomers(values);
        onCustomersApplied(values);
      }}
      dueFrom="" dueTo="" onDueChange={vi.fn()} kpi={null} onKpiChange={vi.fn()}>
      <div>목록</div>
    </OsanListFrame>
  </div>;
}

function openCustomerDropdown() {
  fireEvent.click(screen.getByRole('button', { name: /^필터/ }));
  fireEvent.click(screen.getByRole('button', { name: /고객사 필터/ }));
  return screen.getByRole('dialog', { name: '고객사 선택' });
}

describe('OsanListFrame 다중 선택 필터', () => {
  it('검색 결과만 전체 선택하고 적용할 때 기존 선택과 함께 반영한다', () => {
    const applied = vi.fn();
    render(<FrameHarness initialCustomers={['미래물산']} onCustomersApplied={applied} />);
    const dialog = openCustomerDropdown();

    fireEvent.change(within(dialog).getByRole('searchbox', { name: '고객사 검색' }), { target: { value: '전자' } });
    expect(within(dialog).getAllByRole('checkbox')).toHaveLength(2);
    fireEvent.click(within(dialog).getByRole('button', { name: '검색 결과 전체 선택' }));
    within(dialog).getAllByRole('checkbox').forEach(checkbox => expect(checkbox).toBeChecked());
    fireEvent.click(within(dialog).getByRole('button', { name: '적용' }));

    expect(applied).toHaveBeenCalledWith(['미래물산', '한빛전자', '새봄전자']);
    expect(screen.getByRole('button', { name: '고객사 필터: 3개 선택' })).toHaveAttribute('aria-expanded', 'false');
  });

  it('취소·외부 클릭·Escape는 임시 선택을 버리고 Escape는 트리거로 초점을 돌린다', () => {
    const applied = vi.fn();
    render(<FrameHarness onCustomersApplied={applied} />);
    let dialog = openCustomerDropdown();
    fireEvent.click(within(dialog).getByRole('checkbox', { name: '한빛전자' }));
    fireEvent.click(within(dialog).getByRole('button', { name: '취소' }));

    fireEvent.click(screen.getByRole('button', { name: '고객사 필터: 전체' }));
    dialog = screen.getByRole('dialog', { name: '고객사 선택' });
    expect(within(dialog).getByRole('checkbox', { name: '한빛전자' })).not.toBeChecked();
    fireEvent.click(within(dialog).getByRole('checkbox', { name: '미래물산' }));
    fireEvent.mouseDown(document.body);

    const trigger = screen.getByRole('button', { name: '고객사 필터: 전체' });
    fireEvent.click(trigger);
    dialog = screen.getByRole('dialog', { name: '고객사 선택' });
    expect(within(dialog).getByRole('checkbox', { name: '미래물산' })).not.toBeChecked();
    fireEvent.click(within(dialog).getByRole('checkbox', { name: '새봄전자' }));
    fireEvent.keyDown(document, { key: 'Escape' });

    expect(screen.queryByRole('dialog', { name: '고객사 선택' })).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
    expect(applied).not.toHaveBeenCalled();
  });

  it('전체 선택 해제 후 빈 배열을 적용하면 전체 조건으로 표시한다', () => {
    const applied = vi.fn();
    render(<FrameHarness initialCustomers={customers} onCustomersApplied={applied} />);
    const dialog = openCustomerDropdown();

    fireEvent.click(within(dialog).getByRole('button', { name: '검색 결과 선택 해제' }));
    within(dialog).getAllByRole('checkbox').forEach(checkbox => expect(checkbox).not.toBeChecked());
    expect(within(dialog).getByRole('status')).toHaveTextContent('선택 없음은 전체로 적용됩니다.');
    fireEvent.click(within(dialog).getByRole('button', { name: '적용' }));

    expect(applied).toHaveBeenCalledWith([]);
    expect(screen.getByRole('button', { name: '고객사 필터: 전체' })).toBeInTheDocument();
  });
});
