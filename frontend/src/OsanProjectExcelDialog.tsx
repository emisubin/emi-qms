import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { DsDialog } from './design-system';
import { ApiError } from './api';
import { applyOsanProjectExcel, downloadOsanProjectTemplate, previewOsanProjectExcel, type OsanProjectExcelPreview, type OsanProjectExcelRow } from './osanProjectExcel';
import './osan-project-excel.css';

const fields = [
  ['title', '장비명', 200], ['projectCode', '프로젝트 코드', 80], ['productName', 'part 분류', 100],
  ['quantity', '수량', 0], ['customerName', '고객사', 200], ['poNumber', 'PO No', 100],
  ['workOrderNumber', 'W/O No', 100], ['deliveryDate', '납기일', 10]
] as const;
type Field = typeof fields[number][0];
type Attempt = { rows: OsanProjectExcelRow[]; hash: string; operationId: string; confirmed: number[] };

function inputErrors(row: OsanProjectExcelRow) {
  const errors: string[] = [];
  for (const [key, label, max] of fields) {
    const value = String(row[key] ?? '').trim();
    if (key !== 'poNumber' && key !== 'workOrderNumber' && !value) errors.push(`${label}을(를) 입력해 주세요.`);
    if (max && value.length > max) errors.push(`${label}: ${max}자 이하로 입력해 주세요.`);
  }
  if (row.quantity !== null && (!Number.isInteger(row.quantity) || row.quantity < 1 || row.quantity > 500)) errors.push('수량은 1~500의 정수로 입력해 주세요.');
  if (row.deliveryDate && (!/^\d{4}-\d{2}-\d{2}$/.test(row.deliveryDate) || !Number.isFinite(Date.parse(`${row.deliveryDate}T00:00:00Z`)) || new Date(`${row.deliveryDate}T00:00:00Z`).toISOString().slice(0, 10) !== row.deliveryDate)) errors.push('납기일은 올바른 YYYY-MM-DD 날짜로 입력해 주세요.');
  return errors;
}

export function OsanProjectExcelDialog({ developmentUserKey, onClose, onApplied }: {
  developmentUserKey: string; onClose: () => void; onApplied: (count: number) => void;
}) {
  const [editingCell, setEditingCell] = useState<{ rowNumber: number; key: Field } | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<OsanProjectExcelPreview | null>(null);
  const [saved, setSaved] = useState<number[]>([]);
  const [message, setMessage] = useState('');
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState<'download' | 'preview' | 'apply' | null>(null);
  const [downloaded, setDownloaded] = useState(false);
  const [confirmation, setConfirmation] = useState<Attempt | null>(null);
  const [retry, setRetry] = useState<Attempt | null>(null);
  const activeRequest = useRef<AbortController | null>(null);
  const busyRef = useRef(false);
  const dialog = useRef<HTMLDivElement>(null);
  const confirmationPanel = useRef<HTMLElement>(null);
  const locked = busy !== null || retry !== null;

  useEffect(() => { if (confirmation && !busy) confirmationPanel.current?.focus(); }, [confirmation, busy]);

  useEffect(() => {
    const previousFocus = document.activeElement;
    dialog.current?.focus();
    return () => {
      activeRequest.current?.abort();
      if (previousFocus instanceof HTMLElement && previousFocus.isConnected) previousFocus.focus();
    };
  }, []);

  function keyboard(event: KeyboardEvent) {
    if (event.key === 'Escape') { event.preventDefault(); if (!busyRef.current && !retry) onClose(); }
    if (event.key !== 'Tab') return;
    const elements = dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), [tabindex="0"]');
    if (!elements?.length) { event.preventDefault(); return; }
    const first = elements[0], last = elements[elements.length - 1];
    if (event.shiftKey && (document.activeElement === first || document.activeElement === dialog.current)) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && (document.activeElement === last || document.activeElement === dialog.current)) { event.preventDefault(); first.focus(); }
  }

  function edit(rowNumber: number, key: Field, value: string) {
    if (busyRef.current || retry) return;
    setConfirmation(null); setMessage('');
    setPreview(current => current && { ...current, rows: current.rows.map(row => {
      if (row.rowNumber !== rowNumber) return row;
      const updated = { ...row, [key]: key === 'quantity' ? (value === '' ? null : Number(value)) : value, duplicateKind: null };
      return { ...updated, errors: inputErrors(updated) };
    }) });
  }

  async function run(kind: 'download' | 'preview' | 'apply', prepared?: Attempt) {
    if (busyRef.current || (kind !== 'download' && !file) || (retry && !prepared)) return;
    busyRef.current = true; setEditingCell(null); dialog.current?.focus();
    const controller = new AbortController(); activeRequest.current = controller;
    setBusy(kind); setMessage('');
    let submitted: Attempt | undefined;
    try {
      if (kind === 'download') {
        const blob = await downloadOsanProjectTemplate(developmentUserKey, controller.signal);
        if (controller.signal.aborted) return;
        const url = URL.createObjectURL(blob), anchor = document.createElement('a');
        anchor.href = url; anchor.download = 'EMI_오산_프로젝트_등록양식.xlsx';
        document.body.append(anchor); anchor.click(); anchor.remove(); URL.revokeObjectURL(url); setDownloaded(true);
      } else if (kind === 'preview') {
        const remaining = preview?.rows.filter(row => !saved.includes(row.rowNumber));
        const result = await previewOsanProjectExcel(developmentUserKey, file!, controller.signal, remaining);
        if (controller.signal.aborted) return;
        setConfirmation(null);
        setPreview({ ...result, rows: [...(preview?.rows.filter(row => saved.includes(row.rowNumber)) ?? []), ...result.rows].sort((a, b) => a.rowNumber - b.rowNumber) });
      } else {
        let attempt = prepared;
        if (!attempt) {
          const remaining = preview?.rows.filter(row => !saved.includes(row.rowNumber)) ?? [];
          if (!remaining.length) return;
          const checked = await previewOsanProjectExcel(developmentUserKey, file!, controller.signal, remaining);
          if (controller.signal.aborted) return;
          setPreview({ ...checked, rows: [...(preview?.rows.filter(row => saved.includes(row.rowNumber)) ?? []), ...checked.rows].sort((a, b) => a.rowNumber - b.rowNumber) });
          if (!checked.supportsRowEditing) { setMessage('서버 갱신이 필요합니다. 잠시 후 화면을 새로고침해 주세요.'); return; }
          if (checked.errors.length) { setMessage('파일 오류를 확인해 주세요.'); return; }
          const valid = checked.rows.filter(row => row.errors.length === 0 && inputErrors(row).length === 0);
          if (!valid.length) { setMessage('등록할 수 있는 행이 없습니다. 누락값과 오류를 수정해 주세요.'); return; }
          attempt = { rows: valid, hash: checked.fileSha256, operationId: crypto.randomUUID(), confirmed: [] };
          if (valid.some(row => row.duplicateKind)) { setConfirmation(attempt); return; }
        }
        submitted = attempt;
        const result = await applyOsanProjectExcel(developmentUserKey, file!, attempt.hash, attempt.operationId, controller.signal, attempt.rows, attempt.confirmed);
        if (controller.signal.aborted) return;
        if (!Array.isArray(result.createdRowNumbers) || result.createdRowNumbers.length !== result.createdCount) throw new Error('Incomplete registration response');
        const createdRows = result.createdRowNumbers;
        setSaved(current => [...new Set([...current, ...createdRows])]); setRetry(null); setConfirmation(null);
        const hasRemaining = preview?.rows.some(row => !saved.includes(row.rowNumber) && !createdRows.includes(row.rowNumber));
        setSuccess(`${result.createdCount}개 프로젝트를 등록했습니다. ${hasRemaining ? '미등록 행은 수정 후 추가 등록할 수 있습니다.' : '모든 행의 등록이 완료됐습니다.'}`);
        onApplied(result.createdCount);
      }
    } catch (error) {
      if (!controller.signal.aborted) {
        const uncertain = submitted && (retry !== null || !(error instanceof ApiError) || error.status === 0 || error.status >= 500);
        if (uncertain) setRetry(submitted!);
        else { setRetry(null); setConfirmation(null); }
        setMessage(uncertain ? `${error instanceof ApiError && error.status === 403 ? '조회 권한이 없어 등록 결과를 확인할 수 없습니다. ' : ''}등록 결과를 확인하지 못했습니다. 중복 등록을 막기 위해 같은 요청으로 결과를 다시 확인해 주세요.` : error instanceof ApiError ? error.message : '요청을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.');
      }
    } finally {
      busyRef.current = false;
      if (!controller.signal.aborted) setBusy(null);
    }
  }

  const remaining = preview?.rows.filter(row => !saved.includes(row.rowNumber)) ?? [];
  const validCount = remaining.filter(row => !row.errors.length && !inputErrors(row).length).length;
  return <DsDialog label="오산 프로젝트 엑셀 업로드" onClose={onClose} closeDisabled={locked}>
    <div className="dialog osan-excel-dialog" ref={dialog} tabIndex={-1} onKeyDown={keyboard}>
      <div className="osan-excel-heading"><h2>프로젝트 엑셀 업로드</h2><button type="button" disabled={locked} onClick={onClose}>닫기</button></div>
      <p>한 행에 프로젝트 하나씩 입력해 주세요. 미리보기 셀을 눌러 수정할 수 있습니다. PO No·W/O No는 선택 항목입니다.</p>
      <p className="osan-excel-hint">.xlsx · 최대 5MiB · 프로젝트 100개 · 전체 수량 1,000개 이하</p>
      <button type="button" disabled={locked} onClick={() => void run('download')}>{busy === 'download' ? '다운로드 중…' : '엑셀 양식 다운로드'}</button>
      {downloaded && <p role="status">양식을 다운로드했습니다.</p>}
      <label className="osan-excel-file">작성한 엑셀 파일
        <input type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" disabled={locked} onChange={event => {
          if (busyRef.current || retry) return;
          const selected = event.target.files?.[0] ?? null;
          setEditingCell(null); setPreview(null); setMessage(''); setSuccess(''); setFile(null); setSaved([]); setConfirmation(null);
          if (selected && (!selected.name.toLowerCase().endsWith('.xlsx') || selected.size === 0 || selected.size > 5 * 1024 * 1024)) {
            setMessage('비어 있지 않은 5MiB 이하의 .xlsx 파일을 선택해 주세요.'); return;
          }
          setFile(selected);
        }} />
      </label>
      <button type="button" disabled={!file || locked || (!!preview && !remaining.length)} onClick={() => void run('preview')}>{busy === 'preview' ? '확인 중…' : '내용 미리보기'}</button>
      {success && <p role="status" className="osan-excel-success">{success}</p>}
      {preview && <section className="osan-excel-preview" aria-label="엑셀 등록 미리보기">
        <p role="status">등록 완료 {saved.length}개 · 등록 가능 {validCount}개 · 수정 필요 {remaining.length - validCount}개</p>
        {!preview.supportsRowEditing && <p role="alert">서버 갱신이 필요합니다. 잠시 후 화면을 새로고침해 주세요.</p>}
        {preview.errors.length > 0 && <ul role="alert">{preview.errors.map((error, index) => <li key={index}>{error}</li>)}</ul>}
        {remaining.some(row => row.errors.length > 0) && <ul className="osan-excel-errors" aria-label="입력 오류 목록">{remaining.filter(row => row.errors.length > 0).map(row => <li key={row.rowNumber}>{row.rowNumber}행: {row.errors.join(' / ')}</li>)}</ul>}
        {preview.rows.length > 0 && <div className="osan-excel-table-scroll" tabIndex={0} role="region" aria-label="프로젝트별 입력 내용">
          <table><thead><tr><th scope="col">행</th>{fields.map(([, label]) => <th key={label} scope="col">{label}</th>)}<th scope="col">확인 결과</th></tr></thead>
            <tbody>{preview.rows.map(row => {
              const complete = saved.includes(row.rowNumber);
              const errors = [...new Set([...row.errors, ...inputErrors(row)])];
              return <tr key={row.rowNumber} className={complete ? 'osan-excel-saved' : errors.length ? 'osan-excel-invalid' : undefined}>
                <th scope="row">{row.rowNumber}</th>{fields.map(([key, label, max]) => <td key={key}>{editingCell?.rowNumber === row.rowNumber && editingCell.key === key && !complete ? <input
                  ref={element => { element?.focus(); }}
                  onBlur={() => setEditingCell(null)}
                  onKeyDown={event => { if (event.key === 'Enter' || event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); dialog.current?.focus(); setEditingCell(null); } }}
                  aria-label={`${row.rowNumber}행 ${label}`} aria-invalid={!complete && key !== 'poNumber' && key !== 'workOrderNumber' && !String(row[key] ?? '').trim()}
                  type={key === 'quantity' ? 'number' : 'text'} inputMode={key === 'quantity' ? 'numeric' : undefined}
                  min={key === 'quantity' ? 1 : undefined} max={key === 'quantity' ? 500 : undefined} step={key === 'quantity' ? 1 : undefined}
                  maxLength={max || undefined} placeholder={key === 'deliveryDate' ? 'YYYY-MM-DD' : key === 'poNumber' || key === 'workOrderNumber' ? '선택' : '입력 필요'}
                  value={row[key] ?? ''} disabled={locked || complete} onChange={event => edit(row.rowNumber, key, event.target.value)}
                /> : <button type="button" className="osan-excel-cell-value" aria-label={`${row.rowNumber}행 ${label}`} disabled={locked || complete}
                  onClick={() => setEditingCell({ rowNumber: row.rowNumber, key })}>{String(row[key] ?? '').trim() || <span className="osan-excel-cell-empty">{key === 'poNumber' || key === 'workOrderNumber' ? '—' : '입력 필요'}</span>}</button>}</td>)}<td>{complete ? '등록 완료' : errors.length ? errors.join(' / ') : row.duplicateKind === 'identical' ? '동일 프로젝트 확인 필요' : row.duplicateKind === 'code' ? '같은 코드 확인 필요' : '등록 가능'}</td>
              </tr>;
            })}</tbody></table>
        </div>}
        <p>입력 조건을 충족한 프로젝트만 등록합니다. 누락값이 있는 행은 이 화면에 남습니다.</p>
        {confirmation && <section className="osan-excel-confirm" aria-label="중복 프로젝트 확인" ref={confirmationPanel} tabIndex={-1}>
          <h3>중복 프로젝트 확인</h3>
          <p>아래 프로젝트가 이미 등록되어 있거나 이번 파일에 중복되어 있습니다. 그래도 등록하시겠습니까?</p>
          <ul>{confirmation.rows.filter(row => row.duplicateKind).map(row => <li key={row.rowNumber}>{row.rowNumber}행 · {row.title} · {row.projectCode}: {row.duplicateKind === 'identical' ? '장비명·코드·고객사·part 분류·수량이 모두 같습니다.' : '같은 프로젝트 코드가 사용되고 있습니다.'}</li>)}</ul>
          <button type="button" disabled={locked} onClick={() => setConfirmation(null)}>돌아가기</button>
          <button type="button" className="primary-button" disabled={locked} onClick={() => void run('apply', { ...confirmation, confirmed: confirmation.rows.filter(row => row.duplicateKind).map(row => row.rowNumber) })}>중복 포함 {confirmation.rows.length}개 등록</button>
        </section>}
        {retry ? <button type="button" disabled={busy !== null} onClick={() => void run('apply', retry)}>같은 요청으로 결과 확인</button>
          : !confirmation && <button type="button" className="primary-button" disabled={!preview.supportsRowEditing || !validCount || busy !== null} onClick={() => void run('apply')}>{busy === 'apply' ? '등록 중…' : `${validCount}개 프로젝트 등록`}</button>}
      </section>}
      {message && <p role="alert" className="error-text">{message}</p>}
    </div>
  </DsDialog>;
}
