import { useState } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { OsanInlineState, OsanTabs } from '../src/OsanUiPrimitives';

it('선택 콜백과 방향키 이동을 공유하면서 기존 라벨과 클래스를 유지한다', () => {
  const change = vi.fn();
  function Tabs() {
    const [value, setValue] = useState('work');
    return <OsanTabs as="nav" className="mobile-home-tabs" label="업무 화면" value={value}
      items={[{ value: 'work', label: <>내 업무 <b>3</b></> }, { value: 'news', label: '소식' }, { value: 'due', label: '납기' }]}
      onChange={next => { change(next); setValue(next); }} />;
  }
  render(<Tabs />);
  const work = screen.getByRole('tab', { name: '내 업무 3' });
  const news = screen.getByRole('tab', { name: '소식' });
  const due = screen.getByRole('tab', { name: '납기' });
  expect(screen.getByRole('tablist')).toHaveClass('mobile-home-tabs');
  expect(work).toHaveAttribute('tabindex', '0');
  expect(news).toHaveAttribute('tabindex', '-1');
  fireEvent.keyDown(work, { key: 'ArrowRight' });
  expect(change).toHaveBeenLastCalledWith('news');
  expect(news).toHaveFocus();
  expect(news).toHaveAttribute('aria-selected', 'true');
  expect(work).toHaveAttribute('tabindex', '-1');
  fireEvent.keyDown(news, { key: 'End' });
  expect(due).toHaveFocus();
  fireEvent.keyDown(due, { key: 'ArrowRight' });
  expect(work).toHaveFocus();
  fireEvent.keyDown(work, { key: 'ArrowLeft' });
  expect(due).toHaveFocus();
  fireEvent.keyDown(due, { key: 'Home' });
  expect(work).toHaveFocus();
  const count = change.mock.calls.length;
  fireEvent.keyDown(work, { key: 'Escape' });
  expect(change).toHaveBeenCalledTimes(count);
  fireEvent.click(news);
  expect(change).toHaveBeenLastCalledWith('news');
  expect(news).toHaveAttribute('aria-selected', 'true');
});

it('간결한 오류 안내는 오류 문구와 재시도 콜백을 유지한다', () => {
  const retry = vi.fn();
  const { rerender } = render(<OsanInlineState kind="error" className="home-error" onRetry={retry}>불러오지 못했습니다.</OsanInlineState>);
  expect(screen.getByRole('alert')).toHaveClass('home-error');
  expect(screen.getByRole('alert').tagName).toBe('P');
  fireEvent.click(screen.getByRole('button', { name: '다시 시도' }));
  expect(retry).toHaveBeenCalledTimes(1);
  rerender(<OsanInlineState kind="loading" className="empty">불러오는 중…</OsanInlineState>);
  expect(screen.getByRole('status')).toHaveTextContent('불러오는 중…');
  expect(screen.queryByRole('button')).not.toBeInTheDocument();
});
