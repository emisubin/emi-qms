import type { ReactNode } from 'react';

export function DsBreadcrumbs({
  items,
  current,
  className = ''
}: {
  items: Array<{ label: string; onClick?: () => void }>;
  current: string;
  className?: string;
}) {
  return (
    <nav className={`ds-breadcrumbs ${className}`.trim()} aria-label="현재 위치">
      <ol>
        {items.map((item) => (
          <li key={item.label}>
            {item.onClick
              ? <button type="button" onClick={item.onClick}>{item.label}</button>
              : <span>{item.label}</span>}
          </li>
        ))}
        <li aria-current="page"><strong>{current}</strong></li>
      </ol>
    </nav>
  );
}

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

export function DsReadOnlyBanner({
  title = '조회 전용입니다.',
  description,
  kind = 'permission',
  action
}: {
  title?: ReactNode;
  description: ReactNode;
  kind?: 'permission' | 'prerequisite';
  action?: ReactNode;
}) {
  return (
    <aside className="ds-readonly-banner" data-kind={kind} role="note">
      <span className="ds-readonly-banner__mark" aria-hidden="true">{kind === 'permission' ? '보기' : '대기'}</span>
      <div>
        <strong>{title}</strong>
        <p>{description}</p>
      </div>
      {action ? <div className="ds-readonly-banner__action">{action}</div> : null}
    </aside>
  );
}

export function DsEmptyState({
  title,
  description,
  primaryAction,
  secondaryAction
}: {
  title: ReactNode;
  description: ReactNode;
  primaryAction?: { label: string; onClick: () => void };
  secondaryAction?: { label: string; onClick: () => void };
}) {
  return (
    <div className="ds-empty-state" role="status">
      <span aria-hidden="true">0</span>
      <div>
        <strong>{title}</strong>
        <p>{description}</p>
      </div>
      {primaryAction || secondaryAction ? (
        <div>
          {secondaryAction ? <button type="button" onClick={secondaryAction.onClick}>{secondaryAction.label}</button> : null}
          {primaryAction ? <button type="button" className="primary-button" onClick={primaryAction.onClick}>{primaryAction.label}</button> : null}
        </div>
      ) : null}
    </div>
  );
}

export function DsSecondaryTools({
  children,
  label = '추가 기능'
}: {
  children: ReactNode;
  label?: string;
}) {
  return (
    <details className="ds-secondary-tools">
      <summary>{label}</summary>
      <div>{children}</div>
    </details>
  );
}

export function DsSelectionModeBar({
  active,
  label,
  selectedCount,
  onStart,
  onCancel,
  children
}: {
  active: boolean;
  label: string;
  selectedCount: number;
  onStart: () => void;
  onCancel: () => void;
  children?: ReactNode;
}) {
  if (!active) {
    return <button type="button" className="ds-selection-mode-trigger" onClick={onStart}>{label}</button>;
  }

  return (
    <div className="ds-selection-mode-bar" role="region" aria-label={`${label} 모드`}>
      <div>
        <strong>{label} 모드</strong>
        <span>{selectedCount > 0 ? `${selectedCount}개 선택됨` : '처리할 항목을 선택하세요.'}</span>
      </div>
      <div>
        {children}
        <button type="button" onClick={onCancel}>선택 종료</button>
      </div>
    </div>
  );
}

export function DsInputFlow({
  children,
  className = '',
  title = '업무 입력',
  description = '대상을 확인하고 값을 입력한 뒤 저장하세요.'
}: {
  children: ReactNode;
  className?: string;
  title?: ReactNode;
  description?: ReactNode;
}) {
  return (
    <section className={`ds-input-flow ${className}`.trim()}>
      <header className="ds-input-flow__header">
        <div>
          <span>빠른 입력</span>
          <h3>{title}</h3>
          <p>{description}</p>
        </div>
        <ol aria-label="입력 순서">
          <li><b>1</b><span>대상 확인</span></li>
          <li><b>2</b><span>값 입력</span></li>
          <li><b>3</b><span>저장</span></li>
        </ol>
      </header>
      <div className="ds-input-flow__body">{children}</div>
    </section>
  );
}

export function DsInputSection({
  children,
  className = '',
  description,
  number,
  title,
  actions,
  collapsible = false
}: {
  children: ReactNode;
  className?: string;
  description?: ReactNode;
  number: number | string;
  title: ReactNode;
  actions?: ReactNode;
  collapsible?: boolean;
}) {
  if (collapsible) {
    return (
      <details className={`ds-input-section ds-input-section--collapsible ${className}`.trim()}>
        <summary className="ds-input-section__header">
          <b aria-hidden="true">{String(number).padStart(2, '0')}</b>
          <div>
            <h4>{title}</h4>
            {description ? <p>{description}</p> : null}
          </div>
          <span className="ds-input-section__toggle" aria-hidden="true">열기</span>
        </summary>
        <div className="ds-input-section__content">{children}</div>
      </details>
    );
  }

  return (
    <section className={`ds-input-section ${className}`.trim()}>
      <header className="ds-input-section__header">
        <b aria-hidden="true">{String(number).padStart(2, '0')}</b>
        <div>
          <h4>{title}</h4>
          {description ? <p>{description}</p> : null}
        </div>
        {actions ? <div className="ds-input-section__actions">{actions}</div> : null}
      </header>
      <div className="ds-input-section__content">{children}</div>
    </section>
  );
}

export function DsChoiceGroup<T extends string>({
  label,
  options,
  value,
  onChange,
  disabled = false,
  className = ''
}: {
  label: string;
  options: Array<{ value: T; label: ReactNode; description?: ReactNode; disabled?: boolean }>;
  value: T;
  onChange: (value: T) => void;
  disabled?: boolean;
  className?: string;
}) {
  return (
    <div className={`ds-choice-group ${className}`.trim()} role="group" aria-label={label}>
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={value === option.value}
          disabled={disabled || option.disabled}
          onClick={() => onChange(option.value)}
        >
          <strong>{option.label}</strong>
          {option.description ? <small>{option.description}</small> : null}
        </button>
      ))}
    </div>
  );
}

export function DsActionBar({
  children,
  className = '',
  description,
  feedback
}: {
  children: ReactNode;
  className?: string;
  description?: ReactNode;
  feedback?: ReactNode;
}) {
  return (
    <footer className={`ds-action-bar ${className}`.trim()}>
      <div className="ds-action-bar__copy">
        <strong>입력 내용을 확인했나요?</strong>
        {description ? <span>{description}</span> : null}
        {feedback ? <div className="ds-action-bar__feedback">{feedback}</div> : null}
      </div>
      <div className="ds-action-bar__buttons">{children}</div>
    </footer>
  );
}
