import { forwardRef, type ButtonHTMLAttributes } from 'react';
import './osan-controls.css';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  size?: 'compact' | 'stage';
  tone?: 'neutral' | 'primary' | 'soft' | 'danger';
};

/** Visual control only: callers retain permission, busy and mutation decisions. */
export const OsanButton = forwardRef<HTMLButtonElement, Props>(function OsanButton(
  { size = 'compact', tone = 'neutral', type = 'button', className = '', ...props }, ref
) {
  return <button {...props} ref={ref} type={type} className={`osan-button ${className}`.trim()} data-size={size} data-tone={tone} />;
});
