import type { ReactNode } from 'react';
import { OsanButton } from './OsanButton';

/** Shared photo-level action for new and retained record photos. */
export function OsanRecordPhoto({ children, action, onAction, disabled, excluded = false, name }: {
  children: ReactNode; action: string; onAction: () => void; disabled?: boolean; excluded?: boolean; name?: string;
}) {
  return <figure className="osan-record-photo" data-excluded={excluded || undefined}>
    <div className="osan-record-photo-image">{children}</div>
    {name && <figcaption>{name}</figcaption>}
    <div className="osan-record-photo-action">
      {excluded && <span>저장 시 제외</span>}
      <OsanButton type="button" disabled={disabled} onClick={onAction}>{action}</OsanButton>
    </div>
  </figure>;
}
