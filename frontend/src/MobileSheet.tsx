import { useEffect, useId, useRef, type ReactNode, type RefObject } from 'react';

type MobileSheetProps = {
  open: boolean;
  title: string;
  eyebrow?: string;
  description?: string;
  onClose: () => void;
  triggerRef?: RefObject<HTMLButtonElement | null>;
  fullScreen?: boolean;
  children: ReactNode;
  footer?: ReactNode;
};

export function MobileSheet({
  open,
  title,
  eyebrow,
  description,
  onClose,
  triggerRef,
  fullScreen = false,
  children,
  footer
}: MobileSheetProps) {
  const titleId = useId();
  const sheetRef = useRef<HTMLElement>(null);
  const onCloseRef = useRef(onClose);

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const previousOverflow = document.body.style.overflow;
    const focusReturnTarget = triggerRef?.current;
    document.body.style.overflow = 'hidden';
    const focusTimer = window.setTimeout(() => {
      const preferred = sheetRef.current?.querySelector<HTMLElement>('[data-autofocus]');
      const first = preferred ?? focusableElements(sheetRef.current)[0];
      first?.focus();
    }, 0);

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault();
        onCloseRef.current();
        return;
      }

      if (event.key !== 'Tab') {
        return;
      }

      const focusable = focusableElements(sheetRef.current);
      if (focusable.length === 0) {
        event.preventDefault();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      window.clearTimeout(focusTimer);
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', handleKeyDown);
      window.setTimeout(() => focusReturnTarget?.focus(), 0);
    };
  }, [open, triggerRef]);

  if (!open) {
    return null;
  }

  return (
    <div
      className={fullScreen ? 'mobile-sheet-backdrop mobile-sheet-backdrop--full' : 'mobile-sheet-backdrop'}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <section
        ref={sheetRef}
        className={fullScreen ? 'mobile-sheet mobile-sheet--full' : 'mobile-sheet'}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <header className="mobile-sheet-header">
          <div>
            {eyebrow ? <p className="eyebrow">{eyebrow}</p> : null}
            <h2 id={titleId}>{title}</h2>
            {description ? <p>{description}</p> : null}
          </div>
          <button type="button" className="mobile-sheet-close" aria-label={`${title} 닫기`} onClick={onClose}>×</button>
        </header>
        <div className="mobile-sheet-body">{children}</div>
        {footer ? <footer className="mobile-sheet-footer">{footer}</footer> : null}
      </section>
    </div>
  );
}

function focusableElements(root: HTMLElement | null) {
  if (!root) {
    return [];
  }

  return Array.from(root.querySelectorAll<HTMLElement>(
    'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'
  ));
}
