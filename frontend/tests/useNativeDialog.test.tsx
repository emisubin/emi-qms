import { StrictMode, useState } from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { useNativeDialog } from '../src/useNativeDialog';
import { OsanStageGuidance } from '../src/OsanStageGuidance';
import { dismissOnBackdrop } from '../src/dialogBackdrop';

function Example() {
  const [open, setOpen] = useState(false);
  const dialog = useNativeDialog(open, setOpen);
  return <><button onClick={() => setOpen(true)}>Open</button><button>Other</button>
    <dialog ref={dialog} aria-label="Photo" onCancel={() => setOpen(false)} onClick={e => dismissOnBackdrop(e, () => setOpen(false))}>
      {open && <button onClick={() => setOpen(false)}>Close</button>}
    </dialog></>;
}
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function () {
    this.open = true;
    this.querySelector('button')?.focus();
  };
  HTMLDialogElement.prototype.close = function () {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});
afterEach(() => vi.restoreAllMocks());
function open() {
  screen.getByText('Open').focus();
  fireEvent.click(screen.getByText('Open'));
  return screen.getByRole('dialog') as HTMLDialogElement;
}
it('synchronizes close, cancel, backdrop and reopening while restoring the trigger', () => {
  render(<StrictMode><Example /></StrictMode>);
  let dialog = open();
  expect(dialog.open).toBe(true);
  fireEvent.click(screen.getByText('Close'));
  expect(dialog.open).toBe(false);
  expect(document.activeElement).toBe(screen.getByText('Open'));
  dialog = open();
  fireEvent(dialog, new Event('cancel'));
  expect(dialog.open).toBe(false);
  dialog = open();
  vi.spyOn(dialog, 'getBoundingClientRect').mockReturnValue({ left: 10, right: 100, top: 10, bottom: 100 } as DOMRect);
  fireEvent.click(dialog, { clientX: 50, clientY: 50 });
  expect(dialog.open).toBe(true);
  fireEvent.click(dialog, { clientX: 1, clientY: 1 });
  expect(dialog.open).toBe(false);
  dialog = open();
  act(() => dialog.close());
  expect(screen.queryByText('Close')).not.toBeInTheDocument();
  expect(open().open).toBe(true);
});
it('does not steal focus moved to another control or restore a detached trigger', () => {
  const view = render(<Example />);
  const dialog = open();
  screen.getByText('Other').focus();
  act(() => dialog.close());
  expect(document.activeElement).toBe(screen.getByText('Other'));
  open();
  const trigger = screen.getByText('Open');
  const focus = vi.spyOn(trigger, 'focus');
  view.unmount();
  expect(dialog.open).toBe(false);
  expect(focus).not.toHaveBeenCalled();
});

it('keeps the existing guidance enlargement markup and closes back to its image trigger', () => {
  render(<OsanStageGuidance stage={1} />);
  const trigger = screen.getByRole('button', { name: 'Rack 예시 크게 보기' });
  trigger.focus();
  fireEvent.click(trigger);
  const dialog = screen.getByRole('dialog', { name: 'Rack 예시 확대' });
  expect(dialog.className).toBe('osan-guidance-zoom');
  expect(screen.getByAltText('Rack 예시 원본')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '닫기' }));
  expect(dialog).not.toHaveAttribute('open');
  expect(document.activeElement).toBe(trigger);
});
