import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { DsDialog } from './design-system';
import { ApiError } from './api';
import { applyOsanProjectExcel, downloadOsanProjectTemplate, previewOsanProjectExcel, type OsanProjectExcelPreview } from './osanProjectExcel';
import './osan-project-excel.css';

export function OsanProjectExcelDialog({ developmentUserKey, onClose, onApplied }: {
  developmentUserKey: string; onClose: () => void; onApplied: (count: number) => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<OsanProjectExcelPreview | null>(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState<'download' | 'preview' | 'apply' | null>(null);
  const [downloaded, setDownloaded] = useState(false);
  const operationId = useRef('');
  const activeRequest = useRef<AbortController | null>(null);
  const busyRef = useRef(false);
  const dialog = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previousFocus = document.activeElement;
    dialog.current?.focus();
    return () => {
      activeRequest.current?.abort();
      if (previousFocus instanceof HTMLElement && previousFocus.isConnected) previousFocus.focus();
    };
  }, []);

  function keyboard(event: KeyboardEvent) {
    if (event.key === 'Escape') { event.preventDefault(); if (!busyRef.current) onClose(); }
    if (event.key !== 'Tab') return;
    const elements = dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), [tabindex="0"]');
    if (!elements?.length) { event.preventDefault(); return; }
    const first = elements[0], last = elements[elements.length - 1];
    if (event.shiftKey && (document.activeElement === first || document.activeElement === dialog.current)) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && (document.activeElement === last || document.activeElement === dialog.current)) { event.preventDefault(); first.focus(); }
  }

  async function run(kind: 'download' | 'preview' | 'apply') {
    if (busyRef.current || (kind !== 'download' && !file)) return;
    if (kind === 'apply' && (!preview || preview.errorCount > 0 || preview.errors.length > 0 || preview.totalRowCount === 0)) return;
    busyRef.current = true;
    dialog.current?.focus();
    const controller = new AbortController();
    activeRequest.current = controller;
    setBusy(kind); setMessage('');
    if (kind === 'preview') setPreview(null);
    try {
      if (kind === 'download') {
        const blob = await downloadOsanProjectTemplate(developmentUserKey, controller.signal);
        if (controller.signal.aborted) return;
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url; anchor.download = 'EMI_오산_프로젝트_등록양식.xlsx';
        document.body.append(anchor); anchor.click(); anchor.remove(); URL.revokeObjectURL(url);
        setDownloaded(true);
      } else if (kind === 'preview') {
        const result = await previewOsanProjectExcel(developmentUserKey, file!, controller.signal);
        if (!controller.signal.aborted) setPreview(result);
      } else {
        const result = await applyOsanProjectExcel(developmentUserKey, file!, preview!.fileSha256, operationId.current, controller.signal);
        if (!controller.signal.aborted) onApplied(result.createdCount);
      }
    } catch (error) {
      if (!controller.signal.aborted) setMessage(error instanceof ApiError ? error.message : '요청을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.');
    } finally {
      busyRef.current = false;
      if (!controller.signal.aborted) setBusy(null);
    }
  }

  const canApply = preview && preview.totalRowCount > 0 && preview.errorCount === 0 && preview.errors.length === 0 && preview.rows.every(row => row.errors.length === 0);
  return <DsDialog label="오산 프로젝트 엑셀 업로드" onClose={onClose} closeDisabled={busy !== null}>
    <div className="dialog osan-excel-dialog" ref={dialog} tabIndex={-1} onKeyDown={keyboard}>
      <div className="osan-excel-heading"><h2>프로젝트 엑셀 업로드</h2><button type="button" disabled={busy !== null} onClick={onClose}>닫기</button></div>
      <p>양식을 내려받아 한 행에 프로젝트 하나씩 입력해 주세요. PO No·W/O No는 선택 항목입니다.</p>
      <p className="osan-excel-hint">.xlsx · 최대 5MiB · 프로젝트 100개 · 전체 수량 1,000개 이하</p>
      <button type="button" disabled={busy !== null} onClick={() => void run('download')}>{busy === 'download' ? '다운로드 중…' : '엑셀 양식 다운로드'}</button>
      {downloaded && <p role="status">양식을 다운로드했습니다.</p>}
      <label className="osan-excel-file">작성한 엑셀 파일
        <input type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" disabled={busy !== null} onChange={event => {
          if (busyRef.current) return;
          const selected = event.target.files?.[0] ?? null;
          setPreview(null); setMessage(''); setFile(null); operationId.current = '';
          if (selected && (!selected.name.toLowerCase().endsWith('.xlsx') || selected.size === 0 || selected.size > 5 * 1024 * 1024)) {
            setMessage('비어 있지 않은 5MiB 이하의 .xlsx 파일을 선택해 주세요.'); return;
          }
          setFile(selected); if (selected) operationId.current = crypto.randomUUID();
        }} />
      </label>
      <button type="button" disabled={!file || busy !== null} onClick={() => void run('preview')}>{busy === 'preview' ? '확인 중…' : '내용 미리보기'}</button>
      {preview && <section className="osan-excel-preview" aria-label="엑셀 등록 미리보기">
        <p role="status">프로젝트 {preview.totalRowCount}개 · 전체 수량 {preview.totalQuantity.toLocaleString()}개 · 오류 {preview.errorCount}건</p>
        {preview.errors.length > 0 && <ul role="alert">{preview.errors.map((error, index) => <li key={index}>{error}</li>)}</ul>}
        {preview.rows.some(row => row.errors.length > 0) && <ul className="osan-excel-errors" aria-label="입력 오류 목록">{preview.rows.filter(row => row.errors.length > 0).map(row => <li key={row.rowNumber}>{row.rowNumber}행: {row.errors.join(' / ')}</li>)}</ul>}
        {preview.rows.length > 0 && <div className="osan-excel-table-scroll" tabIndex={0} role="region" aria-label="프로젝트별 입력 내용">
          <table><thead><tr>{['행', '프로젝트 Title', '프로젝트 코드', '거래처', 'PO No', 'W/O No', '납기일', '제품명', '수량', '확인 결과'].map(label => <th key={label} scope="col">{label}</th>)}</tr></thead>
            <tbody>{preview.rows.map(row => <tr key={row.rowNumber} className={row.errors.length ? 'osan-excel-invalid' : undefined}>
              <th scope="row">{row.rowNumber}</th><td>{row.title}</td><td>{row.projectCode}</td><td>{row.customerName}</td><td>{row.poNumber || '—'}</td><td>{row.workOrderNumber || '—'}</td><td>{row.deliveryDate || '—'}</td><td>{row.productName}</td><td>{row.quantity ?? '—'}</td><td>{row.errors.length ? row.errors.join(' / ') : '등록 가능'}</td>
            </tr>)}</tbody></table>
        </div>}
        <p>{canApply ? '확인한 내용을 새 프로젝트로 일괄 등록합니다.' : '오류가 있는 행을 엑셀에서 수정하고 파일을 다시 선택해 주세요. 오류가 있으면 전체 등록이 진행되지 않습니다.'}</p>
        <button type="button" className="primary-button" disabled={!canApply || busy !== null} onClick={() => void run('apply')}>{busy === 'apply' ? '등록 중…' : `${preview.totalRowCount}개 프로젝트 등록`}</button>
      </section>}
      {message && <p role="alert" className="error-text">{message}</p>}
    </div>
  </DsDialog>;
}
