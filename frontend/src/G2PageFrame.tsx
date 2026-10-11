import type { ReactNode } from 'react';
import { OsanMenuHeading } from './OsanMenuHeading';
import './g2-page-frame.css';

/** Shared G2 page chrome. Business content and controls remain with each menu. */
export function G2PageFrame({ id, title, description, actions, children, fitMobile = false }: {
  id: string; title: string; description: string; actions?: ReactNode; children: ReactNode; fitMobile?: boolean;
}) {
  return <section id={id} className={`g2-page-frame${fitMobile ? ' g2-page-frame-fit' : ''}`} aria-labelledby={`${id}-title`}>
    <OsanMenuHeading id={`${id}-title`} title={title} description={description} actions={actions} />
    <div className="g2-page-content">{children}</div>
  </section>;
}
