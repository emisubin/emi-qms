import { useEffect, useRef, useState } from 'react';
import {
  exportSelectedRowsExcel,
  getSelectedExportColumns,
  type ExcelExportDownload,
  type SelectedExportColumn,
  type SelectedExportScreen
} from './api';
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
  const pickerEnabled = screen !== 'form-templates';
  const pickerTriggerRef = useRef<HTMLButtonElement>(null);
  const pickerPanelRef = useRef<HTMLDivElement>(null);
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [isLoadingColumns, setIsLoadingColumns] = useState(false);
  const [columns, setColumns] = useState<SelectedExportColumn[] | null>(null);
  const [selectedColumnKeys, setSelectedColumnKeys] = useState<Set<string> | null>(null);
  const [columnError, setColumnError] = useState('');
  const [columnNotice, setColumnNotice] = useState('');

  useEffect(() => {
    setIsPickerOpen(false);
    setColumns(null);
    setSelectedColumnKeys(null);
    setColumnError('');
    setColumnNotice('');
  }, [screen]);

  useEffect(() => {
    if (!isPickerOpen) return;

    function onPointerDown(event: MouseEvent) {
      const target = event.target as Node;
      if (!pickerPanelRef.current?.contains(target) && !pickerTriggerRef.current?.contains(target)) {
        setIsPickerOpen(false);
      }
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsPickerOpen(false);
        pickerTriggerRef.current?.focus();
      }
    }

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [isPickerOpen]);

  useEffect(() => {
    if (busy) setIsPickerOpen(false);
  }, [busy]);

  async function loadColumns() {
    if (!pickerEnabled || isLoadingColumns) return;
    setIsLoadingColumns(true);
    setColumnError('');
    try {
      const nextColumns = await getSelectedExportColumns(developmentUserKey, screen);
      setColumns(nextColumns);
      setSelectedColumnKeys(new Set(nextColumns.map((column) => column.key)));
      setColumnNotice('');
    } catch {
      setColumnError('컬럼 목록을 불러오지 못했습니다. 기본 컬럼 내보내기는 계속 사용할 수 있습니다.');
    } finally {
      setIsLoadingColumns(false);
    }
  }

  function openPicker() {
    setIsPickerOpen(true);
    if (!columns && !isLoadingColumns) void loadColumns();
  }

  function resetColumnsAfterValidationFailure() {
    if (!pickerEnabled || selectedColumnKeys === null) return;
    setColumns(null);
    setSelectedColumnKeys(null);
    setColumnError('');
    setColumnNotice('컬럼 선택이 초기화되었습니다. 기본 컬럼으로 다시 내보내거나 목록을 다시 확인해 주세요.');
  }

  const selectedColumns = columns && selectedColumnKeys
    ? columns.filter((column) => selectedColumnKeys.has(column.key))
    : null;
  const requestedColumns = selectedColumns?.map((column) => column.key);
  const columnSummary = columns && selectedColumnKeys
    ? selectedColumnKeys.size === columns.length
      ? `기본 컬럼 ${columns.length}개`
      : `컬럼 ${selectedColumnKeys.size}/${columns.length}`
    : '기본 컬럼';

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
      {pickerEnabled ? (
        <div className="selected-export-column-picker">
          <button
            ref={pickerTriggerRef}
            type="button"
            className="selected-export-column-trigger"
            aria-expanded={isPickerOpen}
            aria-haspopup="dialog"
            disabled={busy}
            onClick={() => isPickerOpen ? setIsPickerOpen(false) : openPicker()}
          >
            <span>컬럼 선택</span>
            <small aria-live="polite">{columnSummary}</small>
          </button>
          {isPickerOpen ? (
            <div ref={pickerPanelRef} className="selected-export-column-popover" role="dialog" aria-label="내보낼 컬럼 선택">
              <header>
                <div>
                  <strong>내보낼 컬럼</strong>
                  <small>필수 컬럼은 파일 식별을 위해 유지됩니다.</small>
                </div>
                <button type="button" aria-label="컬럼 선택 닫기" onClick={() => {
                  setIsPickerOpen(false);
                  pickerTriggerRef.current?.focus();
                }}>×</button>
              </header>
              {isLoadingColumns ? <p className="selected-export-column-state">컬럼 목록을 불러오는 중입니다.</p> : null}
              {columnError ? (
                <div className="selected-export-column-state" role="alert">
                  <p>{columnError}</p>
                  <button type="button" onClick={() => void loadColumns()}>다시 시도</button>
                </div>
              ) : null}
              {columns && selectedColumnKeys ? (
                <>
                  <div className="selected-export-column-list">
                    {columns.map((column) => (
                      <label key={column.key}>
                        <input
                          type="checkbox"
                          checked={selectedColumnKeys.has(column.key)}
                          disabled={busy || column.required}
                          onChange={(event) => {
                            const next = new Set(selectedColumnKeys);
                            if (event.target.checked) next.add(column.key);
                            else next.delete(column.key);
                            setSelectedColumnKeys(next);
                            setColumnNotice('');
                          }}
                        />
                        <span>{column.label}</span>
                        {column.required ? <small>필수</small> : null}
                      </label>
                    ))}
                  </div>
                  <footer>
                    <span aria-live="polite">{columnSummary}</span>
                    <button
                      type="button"
                      disabled={busy || selectedColumnKeys.size === columns.length}
                      onClick={() => {
                        setSelectedColumnKeys(new Set(columns.map((column) => column.key)));
                        setColumnNotice('기본 컬럼으로 복원했습니다.');
                      }}
                    >
                      전체 선택 · 기본값 복원
                    </button>
                  </footer>
                </>
              ) : null}
            </div>
          ) : null}
          {columnNotice ? <span className="selected-export-column-notice" role="status">{columnNotice}</span> : null}
        </div>
      ) : null}
      <ExcelExportAction
        exportFile={exportFile ?? (() => exportSelectedRowsExcel(
          developmentUserKey,
          screen,
          [...selectedIds],
          filters,
          requestedColumns
        ))}
        scopeLabel={selectedIds.size === 0
          ? '항목을 먼저 선택해 주세요'
          : `선택 ${selectedIds.size}건 · ${columnSummary}`}
        label={label}
        disabled={selectedIds.size === 0}
        disabledReason="항목을 한 건 이상 선택해 주세요."
        unprocessableEntityHint="목록과 컬럼 선택을 다시 확인해 주세요."
        onUnprocessableEntity={resetColumnsAfterValidationFailure}
        onBusyChange={(nextBusy) => {
          if (nextBusy) setIsPickerOpen(false);
          onBusyChange(nextBusy);
        }}
      />
      <button type="button" className="selected-export-clear" disabled={busy || selectedIds.size === 0} onClick={onClear}>
        선택 해제
      </button>
    </section>
  );
}
