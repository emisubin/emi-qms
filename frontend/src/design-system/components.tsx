import type { ReactNode } from 'react';

export function DsPageHeader({
  eyebrow,
  title,
  description,
  titleId,
  actions,
  className = ''
}: {
  eyebrow?: string;
  title: ReactNode;
  description?: ReactNode;
  titleId?: string;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <header className={`ds-page-header ${className}`.trim()}>
      <div className="ds-page-header__copy">
        {eyebrow ? <span className="ds-page-header__eyebrow">{eyebrow}</span> : null}
        <h2 id={titleId}>{title}</h2>
        {description ? <p>{description}</p> : null}
      </div>
      {actions ? <div className="ds-page-header__actions">{actions}</div> : null}
    </header>
  );
}

export function DsSurface({
  children,
  className = '',
  density = 'default',
  label,
  labelledBy
}: {
  children: ReactNode;
  className?: string;
  density?: 'default' | 'compact';
  label?: string;
  labelledBy?: string;
}) {
  return (
    <section
      className={`ds-surface ${className}`.trim()}
      data-density={density}
      aria-label={label}
      aria-labelledby={labelledBy}
    >
      {children}
    </section>
  );
}

export function DsToolbar({ children, className = '', label }: { children: ReactNode; className?: string; label?: string }) {
  return <div className={`ds-toolbar ${className}`.trim()} role="group" aria-label={label}>{children}</div>;
}

export function DsTabs({
  items,
  selectedKey,
  onSelect,
  label
}: {
  items: Array<{ key: string; label: string }>;
  selectedKey: string;
  onSelect: (key: string) => void;
  label: string;
}) {
  return (
    <div className="ds-tabs" role="tablist" aria-label={label}>
      {items.map((item) => (
        <button
          key={item.key}
          type="button"
          role="tab"
          aria-selected={item.key === selectedKey}
          onClick={() => onSelect(item.key)}
        >
          {item.label}
        </button>
      ))}
    </div>
  );
}

export function DsBadge({ children, tone = 'primary' }: { children: ReactNode; tone?: 'primary' | 'neutral' | 'success' | 'danger' }) {
  return <span className="ds-badge" data-tone={tone}>{children}</span>;
}
