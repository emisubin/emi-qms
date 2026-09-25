import type { ReactNode } from 'react';

/** Shared semantics; existing list/home classes retain their approved visual layout. */
export function OsanKpiButton({ label, value, selected, onClick, disabled, className, valueClassName, valueAs: Value = 'strong', accessibleLabel }: {
  label: ReactNode; value: ReactNode; selected?: boolean; onClick: () => void;
  disabled?: boolean; className?: string; valueClassName?: string;
  valueAs?: 'strong' | 'b'; accessibleLabel?: string;
}) {
  return <button type="button" className={className} aria-label={accessibleLabel} aria-pressed={selected} disabled={disabled} onClick={onClick}>
    <span>{label}</span><Value className={valueClassName}>{value}</Value>
  </button>;
}
