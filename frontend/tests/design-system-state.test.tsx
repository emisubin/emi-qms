import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { DsActionFeedback, DsDialog, DsEmptyState, DsStatePanel } from '../src/design-system';

describe('FABLE-P2 unified state panel', () => {
  it('announces error, forbidden and not-found messages through a single alert element', () => {
    for (const kind of ['error', 'forbidden', 'not-found'] as const) {
      const { unmount } = render(<DsStatePanel kind={kind} description={`${kind} 메시지`} />);
      const alert = screen.getByRole('alert');
      expect(alert).toHaveTextContent(`${kind} 메시지`);
      expect(alert).toHaveClass('error-text');
      expect(screen.queryByRole('status')).toBeNull();
      unmount();
    }
  });

  it('keeps the StateMessage default strings verbatim', () => {
    const { unmount } = render(<DsStatePanel kind="forbidden" description="권한이 없습니다." />);
    expect(screen.getByRole('alert')).toHaveTextContent('권한이 없습니다.');
    unmount();

    render(<DsStatePanel kind="not-found" description="대상을 찾을 수 없습니다." />);
    expect(screen.getByRole('alert')).toHaveTextContent('대상을 찾을 수 없습니다.');
  });

  it('exposes loading and empty panels as polite status regions', () => {
    const { unmount } = render(<DsStatePanel kind="loading" description="Loading" />);
    expect(screen.getByRole('status')).toHaveTextContent('Loading');
    expect(screen.queryByRole('alert')).toBeNull();
    unmount();

    render(<DsStatePanel kind="empty" title="항목 없음" description="등록된 항목이 없습니다." />);
    expect(screen.getByRole('status')).toHaveTextContent('등록된 항목이 없습니다.');
  });

  it('renders an action slot when provided', () => {
    const onRetry = vi.fn();
    render(
      <DsStatePanel
        kind="empty"
        description="비어 있습니다."
        action={<button type="button" onClick={onRetry}>다시 시도</button>}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: '다시 시도' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});

describe('FABLE-P2 shared action feedback', () => {
  it('maps error tone to an assertive alert and other tones to polite status', () => {
    const { unmount } = render(<DsActionFeedback tone="error" message="저장하지 못했습니다." />);
    const alert = screen.getByRole('alert');
    expect(alert).toHaveAttribute('data-tone', 'error');
    expect(alert).toHaveAttribute('aria-live', 'assertive');
    unmount();

    for (const tone of ['neutral', 'loading', 'success', 'partial', 'info'] as const) {
      const { unmount: done } = render(<DsActionFeedback tone={tone} message={`${tone} 메시지`} />);
      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('data-tone', tone);
      expect(status).toHaveAttribute('aria-live', 'polite');
      done();
    }
  });

  it('renders nothing without a message and merges legacy classes', () => {
    const { container, unmount } = render(<DsActionFeedback tone="success" message="" />);
    expect(container).toBeEmptyDOMElement();
    unmount();

    render(<DsActionFeedback tone="error" className="pending-full-field" message="필수 값입니다." />);
    expect(screen.getByRole('alert')).toHaveClass('action-feedback', 'pending-full-field');
  });
});

describe('FABLE-P3 shared dialog shell', () => {
  it('exposes a modal dialog named by aria-label', () => {
    render(
      <DsDialog label="Excel 업로드" onClose={() => undefined}>
        <section className="dialog"><p>내용</p></section>
      </DsDialog>
    );
    const dialog = screen.getByRole('dialog', { name: 'Excel 업로드' });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });

  it('supports aria-labelledby naming from a heading inside the dialog', () => {
    render(
      <DsDialog labelledBy="p3-dialog-title" onClose={() => undefined}>
        <section className="dialog"><h2 id="p3-dialog-title">Pending 등록</h2></section>
      </DsDialog>
    );
    expect(screen.getByRole('dialog', { name: 'Pending 등록' })).toBeInTheDocument();
  });

  it('closes on backdrop mousedown but not while closing is disabled', () => {
    const onClose = vi.fn();
    const { rerender } = render(
      <DsDialog label="확인" onClose={onClose}>
        <section className="dialog"><button type="button">내부 버튼</button></section>
      </DsDialog>
    );

    fireEvent.mouseDown(screen.getByRole('button', { name: '내부 버튼' }));
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.mouseDown(screen.getByRole('dialog', { name: '확인' }));
    expect(onClose).toHaveBeenCalledTimes(1);

    rerender(
      <DsDialog label="확인" onClose={onClose} closeDisabled>
        <section className="dialog"><button type="button">내부 버튼</button></section>
      </DsDialog>
    );
    fireEvent.mouseDown(screen.getByRole('dialog', { name: '확인' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});

describe('FABLE-P2 empty state delegation', () => {
  it('keeps the DsEmptyState contract: status role, texts and working actions', () => {
    const onPrimary = vi.fn();
    const onSecondary = vi.fn();
    render(
      <DsEmptyState
        title="입고 대상 없음"
        description="조건에 맞는 자재가 없습니다."
        primaryAction={{ label: '새로 등록', onClick: onPrimary }}
        secondaryAction={{ label: '조건 초기화', onClick: onSecondary }}
      />
    );

    expect(screen.getByRole('status')).toHaveTextContent('조건에 맞는 자재가 없습니다.');
    fireEvent.click(screen.getByRole('button', { name: '새로 등록' }));
    fireEvent.click(screen.getByRole('button', { name: '조건 초기화' }));
    expect(onPrimary).toHaveBeenCalledTimes(1);
    expect(onSecondary).toHaveBeenCalledTimes(1);
  });
});
