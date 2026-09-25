import { OsanButton } from './OsanButton';
import type { ReactNode } from 'react';

/** Shares tab interaction while the owning page retains its approved appearance. */
export function OsanTabs<Key extends string>({ items, value, onChange, label, className, as: Element = 'div' }: {
  items: readonly { value: Key; label: ReactNode }[];
  value: Key;
  onChange: (value: Key) => void;
  label: string;
  className?: string;
  as?: 'div' | 'nav';
}) {
  return <Element className={className} role="tablist" aria-label={label}>
    {items.map((item, index) => <button key={item.value} type="button" role="tab"
      aria-selected={value === item.value} tabIndex={value === item.value ? 0 : -1}
      onClick={() => onChange(item.value)}
      onKeyDown={event => {
        const next = event.key === 'ArrowRight' ? (index + 1) % items.length
          : event.key === 'ArrowLeft' ? (index + items.length - 1) % items.length
          : event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1 : undefined;
        if (next === undefined) return;
        event.preventDefault();
        onChange(items[next].value);
        event.currentTarget.parentElement?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[next]?.focus();
      }}>{item.label}</button>)}
  </Element>;
}

/** A compact in-place state: no added wrapper, spacing, or popup styling. */
export function OsanInlineState({ kind, children, className, onRetry }: {
  kind: 'loading' | 'error' | 'empty';
  children: ReactNode;
  className?: string;
  onRetry?: () => void;
}) {
  return <p className={className} role={kind === 'error' ? 'alert' : kind === 'loading' ? 'status' : undefined}>
    {children}{onRetry && <> <OsanButton onClick={onRetry}>다시 시도</OsanButton></>}
  </p>;
}
