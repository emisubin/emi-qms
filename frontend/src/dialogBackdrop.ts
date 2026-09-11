export function dismissOnBackdrop(event: React.MouseEvent<HTMLDialogElement>, dismiss: () => void) {
  if (event.target !== event.currentTarget) return;
  const r = event.currentTarget.getBoundingClientRect();
  if (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) dismiss();
}
