import type { ReactNode } from 'react';
import './osan-menu-heading.css';

/** Shared Osan menu title, description and action alignment. */
export function OsanMenuHeading({ title, description, actions, id }: {
  title: ReactNode; description: ReactNode; actions?: ReactNode; id?: string;
}) {
  return <header className="osan-menu-heading">
    <h1 id={id}>{title}</h1>
    {actions && <div className="osan-menu-heading-actions">{actions}</div>}
    <p className="osan-menu-heading-description">{description}</p>
  </header>;
}
