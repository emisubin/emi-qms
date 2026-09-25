import { fireEvent, render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SelectionActionBar } from '../src/SelectionActionBar';

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
});

describe('SelectionActionBar', () => {
  it('separates primary HOLD actions from QR and clears selection without executing another action', () => {
    const hold = vi.fn(), qr = vi.fn(), clear = vi.fn();
    render(<SelectionActionBar count={2} allSelected={false} onToggleAll={vi.fn()} onClear={clear}
      mobilePrimary={{ label: 'HOLD', actionIds: ['hold'] }} actions={[
        { id: 'qr', label: 'QR 출력', onClick: qr }, { id: 'hold', label: 'HOLD 지정', onClick: hold }
      ]} />);
    fireEvent.click(screen.getByRole('button', { name: /^HOLD$/ }));
    const menu = screen.getByRole('dialog');
    expect(within(menu).queryByText('QR 출력')).toBeNull();
    fireEvent.click(within(menu).getByText('HOLD 지정'));
    expect(hold).toHaveBeenCalledOnce();
    expect(menu).not.toHaveAttribute('open');
    fireEvent.click(screen.getByRole('button', { name: /더보기/ }));
    expect(within(menu).queryByText('HOLD 지정')).toBeNull();
    fireEvent.click(within(menu).getByText('선택 해제'));
    expect(clear).toHaveBeenCalledOnce();
    expect(qr).not.toHaveBeenCalled();
  });
  it('does not offer hidden privileged actions and disables actions for empty selection', () => {
    render(<SelectionActionBar count={0} allSelected={false} onToggleAll={vi.fn()} onClear={vi.fn()}
      actions={[{ id: 'qr', label: 'QR 출력', onClick: vi.fn() }]} />);
    expect(screen.queryByRole('button', { name: 'HOLD' })).toBeNull();
    expect(screen.getByRole('button', { name: 'QR 출력' })).toBeDisabled();
    expect(screen.getByRole('button', { name: /더보기/ })).toBeDisabled();
  });
});
