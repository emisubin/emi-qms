import { useEffect, useRef } from 'react';
import { exportSelectedRowsExcel, type ExcelExportDownload, type SelectedExportScreen } from './api';
import { ExcelExportAction } from './ExcelExportAction';

export function SelectionCheckbox({
  checked,
  indeterminate = false,
  disabled = false,
  label,
  onChange
}: {
  checked: boolean;
  indeterminate?: boolean;
  disabled?: boolean;
  label: string;
  onChange: (checked: boolean) => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (inputRef.current) inputRef.current.indeterminate = indeterminate;
  }, [indeterminate]);

  return (
    <input
      ref={inputRef}
      type="checkbox"
      className="selected-export-checkbox"
      aria-label={label}
      checked={checked}
      disabled={disabled}
      onClick={(event) => event.stopPropagation()}
      onChange={(event) => {
        event.stopPropagation();
        onChange(event.target.checked);
      }}
    />
  );
}

export function SelectedExportTray({
  developmentUserKey,
  screen,
  visibleIds,
  selectedIds,
  allSelected,
  busy,
  filters,
  ariaLabel = '선택 Excel 내보내기',
  label = '선택 Excel 내보내기',
  exportFile,
  onBusyChange,
  onToggleAll,
  onClear
}: {
  developmentUserKey: string | undefined;
  screen: SelectedExportScreen;
  visibleIds: readonly string[];
  selectedIds: ReadonlySet<string>;
  allSelected: boolean;
  busy: boolean;
  filters?: Record<string, string | undefined>;
  ariaLabel?: string;
  label?: string;
  exportFile?: () => Promise<ExcelExportDownload>;
  onBusyChange: (busy: boolean) => void;
  onToggleAll: (selected: boolean) => void;
  onClear: () => void;
}) {
  return (
    <section className="selected-export-tray" aria-label={ariaLabel}>
      <label className="selected-export-all">
        <SelectionCheckbox
          checked={allSelected}
          indeterminate={selectedIds.size > 0 && !allSelected}
          disabled={busy || visibleIds.length === 0}
          label="현재 목록 전체 선택"
          onChange={onToggleAll}
        />
        <span>전체선택</span>
      </label>
      <div className="selected-export-summary" aria-live="polite">
        <strong>{selectedIds.size}개 선택</strong>
        <small>현재 목록 {visibleIds.length}건 중 선택</small>
      </div>
      <ExcelExportAction
        exportFile={exportFile ?? (() => exportSelectedRowsExcel(developmentUserKey, screen, [...selectedIds], filters))}
        scopeLabel={selectedIds.size === 0 ? '항목을 먼저 선택해 주세요' : `선택 ${selectedIds.size}건만 포함`}
        label={label}
        disabled={selectedIds.size === 0}
        disabledReason="항목을 한 건 이상 선택해 주세요."
        unprocessableEntityHint="목록을 새로고침한 뒤 다시 선택해 주세요."
        onBusyChange={onBusyChange}
      />
      <button type="button" className="selected-export-clear" disabled={busy || selectedIds.size === 0} onClick={onClear}>
        선택 해제
      </button>
    </section>
  );
}
