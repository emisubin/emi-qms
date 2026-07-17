import { useEffect, useMemo, useState } from 'react';
import {
  ApiError,
  deleteIqcPhoto,
  downloadIqcPdf,
  finalizeIqcReport,
  getIqcPhotoBlob,
  getIqcReport,
  initializeIqcReport,
  retryIqcPdf,
  saveIqcResponses,
  uploadIqcPhoto
} from './api';
import type { IqcCheckResult, IqcItemResponse, IqcReport, IqcTemplateItem, SaveIqcItemResponse } from './iqc-report';

type Step = 'items' | 'photo' | 'review';

export function IqcReportWorkspace({
  attemptId,
  developmentUserKey,
  canInspect,
  onClose,
  onChanged
}: {
  attemptId: string;
  developmentUserKey: string;
  canInspect: boolean;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [report, setReport] = useState<IqcReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');
  const [step, setStep] = useState<Step>('items');
  const [draft, setDraft] = useState<Record<string, SaveIqcItemResponse>>({});
  const [reason, setReason] = useState('');

  useEffect(() => {
    let active = true;
    setLoading(true);
    void getIqcReport(developmentUserKey, attemptId)
      .then((value) => {
        if (!active) return;
        setReport(value);
        setDraft(toDraft(value.responses));
        setReason(value.reason ?? '');
      })
      .catch((error) => active && setMessage(errorMessage(error, '성적서를 불러오지 못했습니다.')))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [attemptId, developmentUserKey]);

  const requiredMissing = useMemo(() => {
    if (!report) return 0;
    return report.items.filter((item) => item.isRequired && !isAnswered(item, draft[item.itemId])).length;
  }, [draft, report]);
  const photoMissing = useMemo(() => {
    if (!report) return 0;
    return report.items.filter((item) => item.requiresPhoto && !report.photos.some((photo) => photo.templateItemId === item.itemId)).length;
  }, [report]);

  async function run(action: () => Promise<IqcReport>, success: string) {
    setSaving(true);
    setMessage('');
    try {
      const next = await action();
      setReport(next);
      setDraft(toDraft(next.responses));
      setMessage(success);
      return next;
    } catch (error) {
      setMessage(errorMessage(error, '요청을 처리하지 못했습니다.'));
      return null;
    } finally {
      setSaving(false);
    }
  }

  async function start() {
    await run(() => initializeIqcReport(developmentUserKey, attemptId), '검사성적서 작성을 시작했습니다.');
  }

  async function saveAndNext() {
    if (!report?.reportId || !report.reportVersion) return;
    const next = await run(
      () => saveIqcResponses(developmentUserKey, report.reportId!, report.reportVersion!, Object.values(draft)),
      '검사항목을 저장했습니다.'
    );
    if (next) setStep('photo');
  }

  async function addPhoto(itemId: string, file: File | null, altText: string) {
    if (!report?.reportId || !report.reportVersion || !file) {
      setMessage('등록할 사진을 선택해 주세요.');
      return;
    }
    await run(
      () => uploadIqcPhoto(developmentUserKey, report.reportId!, itemId, report.reportVersion!, altText, file),
      '외함 사진을 등록했습니다.'
    );
  }

  async function removePhoto(photoId: string) {
    if (!report?.reportId || !report.reportVersion) return;
    await run(
      () => deleteIqcPhoto(developmentUserKey, report.reportId!, photoId, report.reportVersion!),
      '사진을 삭제했습니다.'
    );
  }

  async function finalize(result: 'Passed' | 'Failed') {
    if (!report?.reportId || !report.reportVersion) return;
    if (requiredMissing > 0 || photoMissing > 0 || reason.trim().length < 3) {
      setMessage('필수 검사항목, 외함 사진과 판정 사유를 모두 완료해 주세요.');
      return;
    }
    const next = await run(
      () => finalizeIqcReport(
        developmentUserKey,
        report.reportId!,
        report.reportVersion!,
        report.receiptVersion,
        result,
        reason.trim()
      ),
      result === 'Passed' ? 'IQC 합격 성적서를 확정했습니다.' : '부적합 성적서를 확정하고 입고를 차단했습니다.'
    );
    if (next) onChanged();
  }

  if (loading) {
    return <div className="iqc-report-loading"><i /><span>검사 양식을 준비하고 있습니다.</span></div>;
  }
  if (!report) {
    return <div className="iqc-report-empty"><strong>성적서를 열 수 없습니다.</strong><p role="alert">{message}</p><button type="button" onClick={onClose}>닫기</button></div>;
  }
  if (report.decisionMode === 'Legacy') {
    return <LegacyReport report={report} onClose={onClose} />;
  }
  if (report.reportStatus === 'Finalized') {
    return <FinalizedReport report={report} developmentUserKey={developmentUserKey} saving={saving} message={message} onRetry={() => void run(() => retryIqcPdf(developmentUserKey, report.reportId!), 'PDF 생성을 다시 요청했습니다.')} onClose={onClose} />;
  }
  if (!report.reportId) {
    return (
      <div className="iqc-report-start">
        <span className="iqc-shape-mark">01</span>
        <p className="eyebrow">DIGITAL INSPECTION</p>
        <h3>{report.orderItem ?? '발주품목 미입력'}</h3>
        <p>{report.projectCode} · {report.attemptNumber}차 검사</p>
        <div className="iqc-start-points"><span>6개 기본 항목</span><span>외함 사진 필수</span><span>확정 후 수정 불가</span></div>
        {message ? <p role="alert" className="error-text">{message}</p> : null}
        <button type="button" className="primary-button" disabled={!canInspect || saving} onClick={() => void start()}>검사 시작</button>
        <button type="button" onClick={onClose}>닫기</button>
      </div>
    );
  }

  return (
    <div className="iqc-report-editor" data-step={step}>
      <ReportContext report={report} />
      <nav className="iqc-report-steps" aria-label="성적서 작성 단계">
        {(['items', 'photo', 'review'] as Step[]).map((value, index) => (
          <button type="button" key={value} data-active={step === value} onClick={() => setStep(value)}>
            <i>{index + 1}</i><span>{value === 'items' ? '검사항목' : value === 'photo' ? '사진' : '최종확인'}</span>
          </button>
        ))}
      </nav>
      <div className="iqc-progress-strip"><strong>{requiredMissing === 0 ? '필수 항목 완료' : `필수 ${requiredMissing}개 남음`}</strong><span>{photoMissing === 0 ? '사진 준비됨' : '외함 사진 필요'}</span></div>

      {step === 'items' ? (
        <section className="iqc-report-panel" aria-labelledby="iqc-items-title">
          <header><span>STEP 1</span><div><h4 id="iqc-items-title">검사항목</h4><p>현장에서 확인한 결과만 선택합니다.</p></div></header>
          <div className="iqc-item-stack">
            {report.items.map((item) => <IqcItemEditor key={item.itemId} item={item} value={draft[item.itemId]} disabled={!canInspect || saving} onChange={(value) => setDraft((current) => ({ ...current, [item.itemId]: value }))} />)}
          </div>
          <button type="button" className="primary-button iqc-next-button" disabled={!canInspect || saving} onClick={() => void saveAndNext()}>저장하고 사진 등록</button>
        </section>
      ) : null}

      {step === 'photo' ? (
        <PhotoStep report={report} developmentUserKey={developmentUserKey} disabled={!canInspect || saving} onUpload={addPhoto} onDelete={removePhoto} onNext={() => setStep('review')} />
      ) : null}

      {step === 'review' ? (
        <section className="iqc-report-panel iqc-review-panel" aria-labelledby="iqc-review-title">
          <header><span>STEP 3</span><div><h4 id="iqc-review-title">최종확인</h4><p>확정 후에는 성적서와 사진을 수정할 수 없습니다.</p></div></header>
          <div className="iqc-review-score"><div data-complete={requiredMissing === 0}><strong>{report.items.length - requiredMissing}/{report.items.length}</strong><span>검사항목</span></div><div data-complete={photoMissing === 0}><strong>{report.photos.length}</strong><span>증빙 사진</span></div><div><strong>v{report.templateVersion}</strong><span>검사 양식</span></div></div>
          <label className="iqc-reason-field"><span>종합 판정 사유</span><textarea value={reason} onChange={(event) => setReason(event.target.value)} placeholder="확인 결과를 3자 이상 기록" disabled={!canInspect || saving} /></label>
          {message ? <p role="alert" className={message.includes('했습니다') ? 'success-text' : 'error-text'}>{message}</p> : null}
          <div className="iqc-finalize-actions"><button type="button" className="iqc-fail-button" disabled={!canInspect || saving} onClick={() => void finalize('Failed')}>부적합 · 입고 차단</button><button type="button" className="primary-button" disabled={!canInspect || saving} onClick={() => void finalize('Passed')}>합격 · 성적서 확정</button></div>
        </section>
      ) : null}
      {message && step !== 'review' ? <p role="status" className={message.includes('했습니다') ? 'success-text' : 'error-text'}>{message}</p> : null}
      <button type="button" className="iqc-close-button" onClick={onClose}>검사함으로 돌아가기</button>
    </div>
  );
}

function IqcItemEditor({ item, value, disabled, onChange }: { item: IqcTemplateItem; value?: SaveIqcItemResponse; disabled: boolean; onChange: (value: SaveIqcItemResponse) => void }) {
  const current = value ?? { templateItemId: item.itemId, checkResult: null, textValue: null, note: null };
  return (
    <article className="iqc-item-card" data-complete={isAnswered(item, current)} data-photo={item.requiresPhoto}>
      <header><i>{String(item.displayOrder).padStart(2, '0')}</i><div><strong>{item.label}</strong><small>{item.guidance}</small></div>{item.requiresPhoto ? <span>PHOTO</span> : null}</header>
      {item.responseType === 'Check' ? (
        <div className="iqc-check-options">
          {([['Pass', '적합'], ['Fail', '부적합'], ['NotApplicable', '해당없음']] as Array<[IqcCheckResult, string]>).map(([result, label]) => <button type="button" key={result} data-active={current.checkResult === result} data-result={result} disabled={disabled} onClick={() => onChange({ ...current, checkResult: result })}>{label}</button>)}
        </div>
      ) : <textarea aria-label={item.label} value={current.textValue ?? ''} maxLength={item.maxTextLength ?? undefined} onChange={(event) => onChange({ ...current, textValue: event.target.value || null })} placeholder="측정값 또는 특이사항" disabled={disabled} />}
      {item.responseType === 'Check' && current.checkResult && current.checkResult !== 'Pass' ? <input aria-label={`${item.label} 비고`} value={current.note ?? ''} onChange={(event) => onChange({ ...current, note: event.target.value || null })} placeholder={current.checkResult === 'NotApplicable' ? '해당없음 사유 필수' : '부적합 근거 또는 비고'} disabled={disabled} /> : null}
    </article>
  );
}

function PhotoStep({ report, developmentUserKey, disabled, onUpload, onDelete, onNext }: { report: IqcReport; developmentUserKey: string; disabled: boolean; onUpload: (itemId: string, file: File | null, alt: string) => Promise<void>; onDelete: (photoId: string) => Promise<void>; onNext: () => void }) {
  const item = report.items.find((candidate) => candidate.requiresPhoto) ?? report.items[0];
  const [file, setFile] = useState<File | null>(null);
  const [alt, setAlt] = useState('외함 전체 상태');
  return (
    <section className="iqc-report-panel iqc-photo-panel" aria-labelledby="iqc-photo-title">
      <header><span>STEP 2</span><div><h4 id="iqc-photo-title">외함 사진</h4><p>손상과 오염 여부를 다시 확인할 수 있는 사진을 남깁니다.</p></div></header>
      <label className="iqc-photo-drop"><input type="file" accept="image/jpeg,image/png" capture="environment" onChange={(event) => setFile(event.target.files?.[0] ?? null)} disabled={disabled} /><i>＋</i><strong>{file ? file.name : '카메라 또는 사진 선택'}</strong><span>JPEG · PNG / 장당 5MB 이하</span></label>
      <label className="iqc-photo-alt"><span>사진 설명</span><input value={alt} maxLength={200} onChange={(event) => setAlt(event.target.value)} disabled={disabled} /></label>
      <button type="button" className="primary-button" disabled={disabled || !file || alt.trim().length === 0} onClick={() => void onUpload(item.itemId, file, alt.trim()).then(() => setFile(null))}>사진 등록</button>
      <div className="iqc-photo-grid">{report.photos.map((photo) => <PhotoEvidence key={photo.photoId} reportId={report.reportId!} photo={photo} developmentUserKey={developmentUserKey} editable={!disabled} onDelete={() => void onDelete(photo.photoId)} />)}</div>
      <button type="button" className="iqc-next-button" disabled={report.photos.length === 0} onClick={onNext}>최종확인으로</button>
    </section>
  );
}

function PhotoEvidence({ reportId, photo, developmentUserKey, editable, onDelete }: { reportId: string; photo: IqcReport['photos'][number]; developmentUserKey: string | undefined; editable: boolean; onDelete?: () => void }) {
  const [source, setSource] = useState<string | null>(null);
  useEffect(() => {
    let active = true;
    let objectUrl: string | null = null;
    void getIqcPhotoBlob(developmentUserKey, reportId, photo.photoId).then((blob) => {
      if (!active) return;
      objectUrl = URL.createObjectURL(blob);
      setSource(objectUrl);
    }).catch(() => undefined);
    return () => { active = false; if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [developmentUserKey, photo.photoId, reportId]);
  return <figure className="iqc-photo-evidence">{source ? <img src={source} alt={photo.altText} /> : <div aria-label="사진 불러오는 중" />}<figcaption><strong>{photo.altText}</strong><span>{(photo.byteSize / 1024).toFixed(0)}KB</span></figcaption>{editable && onDelete ? <button type="button" onClick={onDelete}>삭제</button> : null}</figure>;
}

function FinalizedReport({ report, developmentUserKey, saving, message, onRetry, onClose }: { report: IqcReport; developmentUserKey: string; saving: boolean; message: string; onRetry: () => void; onClose: () => void }) {
  async function download() {
    try {
      const file = await downloadIqcPdf(developmentUserKey, report.reportId!);
      const url = URL.createObjectURL(file.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = file.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      // The status panel remains the authoritative feedback for download readiness.
    }
  }
  return (
    <div className="iqc-final-report">
      <ReportContext report={report} />
      <div className="iqc-final-seal" data-result={report.result}><i>✓</i><div><span>FINALIZED</span><strong>{report.result === 'Passed' ? 'IQC 합격' : '부적합 · 입고 차단'}</strong><small>{formatDateTime(report.finalizedAtUtc)} · {report.finalizedBy}</small></div></div>
      <blockquote>{report.reason}</blockquote>
      <div className="iqc-final-items">{report.items.map((item) => { const response = report.responses.find((value) => value.templateItemId === item.itemId); return <div key={item.itemId}><i>{item.displayOrder}</i><span>{item.label}</span><strong>{responseLabel(response)}</strong></div>; })}</div>
      <div className="iqc-photo-grid">{report.photos.map((photo) => <PhotoEvidence key={photo.photoId} reportId={report.reportId!} photo={photo} developmentUserKey={developmentUserKey} editable={false} />)}</div>
      <div className="iqc-pdf-card" data-status={report.pdfStatus}><div><span>PDF SNAPSHOT</span><strong>{report.pdfStatus === 'Ready' ? '출력본 준비 완료' : report.pdfStatus === 'Failed' ? '생성 재시도 필요' : '출력본 생성 중'}</strong></div>{report.pdfStatus === 'Ready' ? <button type="button" className="primary-button" onClick={() => void download()}>PDF 저장</button> : null}{report.pdfStatus === 'Failed' ? <button type="button" disabled={saving} onClick={onRetry}>PDF 다시 만들기</button> : null}</div>
      {message ? <p role="status" className="success-text">{message}</p> : null}
      <button type="button" onClick={onClose}>검사함으로 돌아가기</button>
    </div>
  );
}

function LegacyReport({ report, onClose }: { report: IqcReport; onClose: () => void }) {
  return <div className="iqc-legacy-report"><span>LEGACY</span><h3>기존 간편 판정</h3><p>상세 성적서 기능 도입 전에 처리된 검사입니다. 확인하지 않은 항목은 소급 생성하지 않습니다.</p><dl><div><dt>판정</dt><dd>{report.attemptStatus === 'Passed' ? '합격' : report.attemptStatus === 'Failed' ? '부적합' : '검사 대기'}</dd></div><div><dt>기존 사유</dt><dd>{report.reason ?? '-'}</dd></div></dl><button type="button" onClick={onClose}>닫기</button></div>;
}

function ReportContext({ report }: { report: IqcReport }) {
  return <header className="iqc-report-context"><div><span>{report.projectCode}</span><strong>{report.orderItem ?? '발주품목 미입력'}</strong><small>{report.projectTitle}</small></div><div><b>{report.attemptNumber}차</b><span>{formatQuantity(report.quantity, report.unit)}</span></div></header>;
}

function toDraft(values: IqcItemResponse[]): Record<string, SaveIqcItemResponse> {
  return Object.fromEntries(values.map((value) => [value.templateItemId, { ...value }]));
}

function isAnswered(item: IqcTemplateItem, value?: SaveIqcItemResponse) {
  if (!value) return false;
  return item.responseType === 'Check' ? value.checkResult !== null : Boolean(value.textValue?.trim());
}

function responseLabel(response?: IqcItemResponse) {
  if (response?.checkResult === 'Pass') return '적합';
  if (response?.checkResult === 'Fail') return '부적합';
  if (response?.checkResult === 'NotApplicable') return '해당없음';
  return response?.textValue ?? '-';
}

function formatQuantity(quantity: number | null, unit: string | null) {
  return quantity == null ? '-' : `${quantity.toLocaleString('ko-KR', { maximumFractionDigits: 3 })} ${unit ?? ''}`.trim();
}

function formatDateTime(value: string | null) {
  return value ? new Intl.DateTimeFormat('ko-KR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-';
}

function errorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError) return error.errors ? Object.values(error.errors).flat()[0] ?? error.message : error.message;
  return fallback;
}
