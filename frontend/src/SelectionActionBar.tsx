import { OsanButton } from './OsanButton';
import { useRef, useState } from 'react';
import { SelectionCheckbox } from './SelectedExcelExport';
import { dismissOnBackdrop } from './dialogBackdrop';
import './selection-action-bar.css';

export type SelectionAction = { id: string; label: string; disabled?: boolean; onClick: () => void };
type Props = {
  count: number;
  allSelected: boolean;
  onToggleAll: (checked: boolean) => void;
  onClear: () => void;
  actions: SelectionAction[];
  mobilePrimary?: { label: string; actionIds: string[] };
};

/** Responsive selection controls; the caller retains permission and mutation logic. */
export function SelectionActionBar({ count, allSelected, onToggleAll, onClear, actions, mobilePrimary }: Props) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [menu, setMenu] = useState<'primary' | 'more' | null>(null);
  const primaryActions = actions.filter(action => mobilePrimary?.actionIds.includes(action.id));
  const moreActions = actions.filter(action => !mobilePrimary?.actionIds.includes(action.id));
  const close = () => { dialog.current?.close(); setMenu(null); };
  const open = (next: 'primary' | 'more') => { setMenu(next); dialog.current?.showModal(); };
  const run = (action: SelectionAction) => { close(); action.onClick(); };
  return <div className="selection-action-bar" role="group" aria-label="목록 선택 작업">
    <label className="selection-action-all"><SelectionCheckbox checked={allSelected} indeterminate={count > 0 && !allSelected} label="현재 목록 전체 선택" onChange={onToggleAll} /><span>전체 선택</span></label>
    <span className="selection-action-count" aria-live="polite">{count}개 선택</span>
    <div className="selection-action-desktop">{actions.map(action => <OsanButton type="button" key={action.id} disabled={!count || action.disabled} onClick={action.onClick}>{action.label}</OsanButton>)}{count > 0 && <OsanButton type="button" onClick={onClear}>선택 해제</OsanButton>}</div>
    <div className="selection-action-mobile">
      {primaryActions.length > 0 && <OsanButton type="button" tone="soft" className="selection-action-primary" disabled={!count || primaryActions.every(action => action.disabled)} aria-haspopup="dialog" aria-expanded={menu === 'primary'} onClick={() => open('primary')}>{mobilePrimary?.label}</OsanButton>}
      <OsanButton type="button" disabled={!count} aria-haspopup="dialog" aria-expanded={menu === 'more'} onClick={() => open('more')}>더보기 <span aria-hidden="true">⋯</span></OsanButton>
    </div>
    <dialog ref={dialog} className="selection-action-dialog" aria-label={menu === 'primary' ? `${mobilePrimary?.label} 선택 작업` : '선택 항목 더보기'} onCancel={close} onClose={() => setMenu(null)} onClick={event => dismissOnBackdrop(event, close)}>
      <h2>{count}개 선택 · {menu === 'primary' ? mobilePrimary?.label : '더보기'}</h2>
      {(menu === 'primary' ? primaryActions : moreActions).map(action => <button type="button" key={action.id} disabled={!count || action.disabled} onClick={() => run(action)}>{action.label}</button>)}
      {menu === 'more' && <button type="button" disabled={!count} onClick={() => { close(); onClear(); }}>선택 해제</button>}
      <button type="button" className="selection-action-close" onClick={close}>닫기</button>
    </dialog>
  </div>;
}
