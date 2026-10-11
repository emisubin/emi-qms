import { useEffect, type RefObject } from 'react';
import './g2-mobile-input.css';

/** Keep floating input forms inside the visible area, including a software keyboard. */
export function useG2InputViewport(ref: RefObject<HTMLDialogElement | null>, open: boolean) {
  useEffect(() => {
    const element = ref.current;
    if (!open || !element) return;
    const viewport = window.visualViewport;
    const update = () => {
      element.style.setProperty('--g2-input-height', `${viewport?.height ?? window.innerHeight}px`);
      element.style.setProperty('--g2-input-top', `${viewport?.offsetTop ?? 0}px`);
    };
    update();
    viewport?.addEventListener('resize', update);
    viewport?.addEventListener('scroll', update);
    window.addEventListener('resize', update);
    return () => {
      viewport?.removeEventListener('resize', update);
      viewport?.removeEventListener('scroll', update);
      window.removeEventListener('resize', update);
      element.style.removeProperty('--g2-input-height');
      element.style.removeProperty('--g2-input-top');
    };
  }, [ref, open]);
}
