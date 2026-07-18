import { useState } from 'react';

export function useSelectedRows(visibleIds: readonly string[]) {
  const visibleSet = new Set(visibleIds);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [busy, setBusy] = useState(false);
  const effectiveSelectedIds = new Set([...selectedIds].filter((id) => visibleSet.has(id)));
  const allSelected = visibleIds.length > 0 && visibleIds.every((id) => effectiveSelectedIds.has(id));

  function toggle(id: string, selected: boolean) {
    if (busy || !visibleSet.has(id)) return;
    setSelectedIds((current) => {
      const next = new Set(current);
      if (selected) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  function toggleAll(selected: boolean) {
    if (busy) return;
    setSelectedIds(selected ? new Set(visibleIds) : new Set());
  }

  function clear() {
    if (!busy) setSelectedIds(new Set());
  }

  return { selectedIds: effectiveSelectedIds, allSelected, busy, setBusy, toggle, toggleAll, clear };
}
