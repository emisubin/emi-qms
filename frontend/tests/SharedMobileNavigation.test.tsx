import { fireEvent, render, screen, within } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { SharedMobileNavigation } from '../src/SharedMobileNavigation';

it('links spaced department labels to one submenu and restores focus after child navigation', () => {
  const onSelect = vi.fn();
  render(<SharedMobileNavigation items={[{key:'인테리어 부스바',label:'인테리어 부스바',children:[{key:'production',label:'생산·출하',onSelect}]}]} />);
  const trigger = screen.getByRole('button',{name:'메뉴 열기'});
  fireEvent.click(trigger);
  const parent = screen.getByRole('button',{name:'인테리어 부스바'});
  expect(parent).toHaveFocus();
  fireEvent.click(parent);
  const id = parent.getAttribute('aria-controls')!;
  expect(id).not.toMatch(/\s/);
  expect(within(document.getElementById(id)!).getByRole('button',{name:'생산·출하'})).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button',{name:'생산·출하'}));
  expect(onSelect).toHaveBeenCalledOnce();
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  expect(trigger).toHaveFocus();
});

it('keeps keyboard focus in the open menu including footer controls and closes with Escape', () => {
  render(<SharedMobileNavigation items={[{key:'home',label:'홈'}]} footer={<select aria-label="검수 사용자"><option>사용자</option></select>} />);
  const trigger = screen.getByRole('button',{name:'메뉴 열기'});
  fireEvent.click(trigger);
  const home = screen.getByRole('button',{name:'홈'});
  fireEvent.keyDown(home,{key:'Tab',shiftKey:true});
  expect(trigger).toHaveFocus();
  fireEvent.keyDown(trigger,{key:'Tab'});
  expect(home).toHaveFocus();
  const select = screen.getByRole('combobox',{name:'검수 사용자'});
  select.focus();
  fireEvent.keyDown(select,{key:'Escape'});
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  expect(trigger).toHaveFocus();
});


it('disables preparing items and focuses the first available action', () => {
  const blocked = vi.fn();
  render(<SharedMobileNavigation items={[{key:'projects',label:'프로젝트',disabled:true,onSelect:blocked},{key:'g2',label:'G2'}]} />);
  fireEvent.click(screen.getByRole('button',{name:'메뉴 열기'}));
  const item = screen.getByRole('button',{name:'프로젝트 준비중'});
  expect(item).toBeDisabled();
  expect(within(item).getByText('준비중')).toHaveStyle({whiteSpace:'nowrap',flexShrink:'0'});
  expect(screen.getByRole('button',{name:'G2'})).toHaveFocus();
  fireEvent.click(item); expect(blocked).not.toHaveBeenCalled();
});
