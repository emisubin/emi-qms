import { useEffect, useMemo, useState } from 'react';
import {
  ApiError,
  deleteIqcScanAttachment,
  deleteIqcPhoto,
  downloadIqcPdf,
  finalizeIqcReport,
  finalizeIqcScanReport,
  getIqcPhotoBlob,
  getIqcReport,
  getIqcScanAttachmentBlob,
  getMaterialIqcQueue,
  getPendingIssue,
  initializeIqcReport,
  retryIqcPdf,
  saveIqcResponses,
  uploadIqcPhoto,
  uploadIqcScanAttachment
} from './api';
import type { IqcCheckResult, IqcItemResponse, IqcReinspectionSource, IqcReport, IqcTemplateItem, SaveIqcItemResponse } from './iqc-report';
import { EvidencePhoto } from './EvidencePhoto';
import type { ActionFeedbackTone } from './useActionFeedback';

type Step = 'items' | 'review';

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
  const [messageTone, setMessageTone] = useState<ActionFeedbackTone>('neutral');
  const [step, setStep] = useState<Step>('items');
  const [draft, setDraft] = useState<Record<string, SaveIqcItemResponse>>({});
  const [reason, setReason] = useState('');
  const [serverScopedReinspection, setServerScopedReinspection] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    void getIqcReport(developmentUserKey, attemptId)
      .then(async (value) => {
        if (!active) return;
        const serverScoped = Boolean(value.reinspectionSource);
        const next = value.decisionMode === 'Detailed' && value.attemptNumber > 1 && !value.reinspectionSource
          ? await discoverReinspectionSource(developmentUserKey, value)
          : value;
        if (!active) return;
        setServerScopedReinspection(serverScoped);
        setReport(next);
        setDraft(toDraft(next.responses));
        setReason(next.reason ?? '');
      })
      .catch((error) => {
        if (!active) return;
        setMessageTone('error');
        setMessage(errorMessage(error, '성적서를 불러오지 못했습니다.'));
      })
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [attemptId, developmentUserKey]);

  const scopedItems = useMemo(() => {
    if (!report?.reinspectionSource?.failures.length) return report?.items ?? [];
    const failedCodes = new Set(report.reinspectionSource.failures.map((failure) => failure.itemCode));
    return report.items.filter((item) => failedCodes.has(item.itemCode));
  }, [report]);
  const requiredMissing = useMemo(() => {
    if (!report) return 0;
    return scopedItems.filter((item) => item.isRequired && !isAnswered(item, draft[item.itemId])).length;
  }, [draft, report, scopedItems]);
  const photoMissing = useMemo(() => {
    if (!report) return 0;
    return scopedItems.filter((item) => item.requiresPhoto && !report.photos.some((photo) => photo.templateItemId === item.itemId)).length;
  }, [report, scopedItems]);
  const reasonMissing = reason.trim().length < 3;
  const isReadyToFinalize = requiredMissing === 0 && photoMissing === 0 && !reasonMissing;
  const hasFailedResponse = scopedItems.some((item) => draft[item.itemId]?.checkResult === 'Fail');
  const finalResult = hasFailedResponse ? 'Failed' : 'Passed';
  const reportSteps = useMemo<Step[]>(() => ['items', 'review'], []);

  async function run(action: () => Promise<IqcReport>, success: string, syncDraft = true) {
    setSaving(true);
    setMessageTone('loading');
    setMessage('요청을 처리하는 중입니다.');
    try {
      const next = await action();
      setReport(next);
      if (syncDraft) setDraft(toDraft(next.responses));
      setMessageTone('success');
      setMessage(success);
      return next;
    } catch (error) {
      setMessageTone('error');
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
      () => saveIqcResponses(
        developmentUserKey,
        report.reportId!,
        report.reportVersion!,
        serverScopedReinspection
          ? scopedItems.map((item) => draft[item.itemId]).filter((value): value is SaveIqcItemResponse => Boolean(value))
          : Object.values(draft)
      ),
      '검사항목을 저장했습니다.'
    );
    if (next) setStep('review');
  }

  async function addPhoto(itemId: string, file: File | null, altText: string) {
    if (!report?.reportId || !report.reportVersion || !file) {
      setMessageTone('error');
      setMessage('등록할 사진을 선택해 주세요.');
      return;
    }
    await run(
      () => uploadIqcPhoto(developmentUserKey, report.reportId!, itemId, report.reportVersion!, altText, file),
      '검사항목 사진을 등록했습니다.',
      false
    );
  }

  async function removePhoto(photoId: string) {
    if (!report?.reportId || !report.reportVersion) return;
    await run(
      () => deleteIqcPhoto(developmentUserKey, report.reportId!, photoId, report.reportVersion!),
      '사진을 삭제했습니다.',
      false
    );
  }

  async function finalize(result: 'Passed' | 'Failed') {
    if (!report?.reportId || !report.reportVersion) return;
    if (requiredMissing > 0 || photoMissing > 0 || reason.trim().length < 3) {
      setMessageTone('error');
      setMessage('필수 검사항목, 외함 사진과 판정 사유를 모두 완료해 주세요.');
      queueMicrotask(() => document.querySelector<HTMLTextAreaElement>('[data-field="iqc-report-reason"]')?.focus());
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

  async function addScanAttachment(file: File | null) {
    if (!report?.reportId || !report.reportVersion || !file) {
      setMessageTone('error');
      setMessage('등록할 PDF 또는 사진 파일을 선택해 주세요.');
      return;
    }
    await run(
      () => uploadIqcScanAttachment(developmentUserKey, report.reportId!, report.reportVersion!, file),
      '서명 스캔본을 등록했습니다.',
      false
    );
  }

  async function removeScanAttachment(attachmentId: string) {
    if (!report?.reportId || !report.reportVersion) return;
    await run(
      () => deleteIqcScanAttachment(developmentUserKey, report.reportId!, attachmentId, report.reportVersion!),
      '스캔본을 삭제했습니다.',
      false
    );
  }

  async function finalizeScan(result: 'Passed' | 'Failed') {
    if (!report?.reportId || !report.reportVersion) return;
    if ((report.scanAttachments?.length ?? 0) === 0 || reason.trim().length < 3) {
      setMessageTone('error');
      setMessage('서명 스캔본 1개 이상과 판정 사유를 입력해 주세요.');
      return;
    }
    const next = await run(
      () => finalizeIqcScanReport(
        developmentUserKey,
        report.reportId!,
        report.reportVersion!,
        report.receiptVersion,
        result,
        reason.trim()
      ),
      result === 'Passed' ? '외함 IQC 합격을 확정했습니다.' : '외함 IQC 부적합을 확정하고 Pending을 생성했습니다.',
      false
    );
    if (next) onChanged();
  }

  if (loading) {
    return <div className="iqc-report-loading"><i /><span>검사 양식을 준비하고 있습니다.</span></div>;
  }
  if (!report) {
    return <div className="iqc-report-empty"><strong>성적서를 열 수 없습니다.</strong><ActionFeedback message={message} tone="error" /><button type="button" onClick={onClose}>닫기</button></div>;
  }
  if (report.decisionMode === 'Legacy') {
    return <LegacyReport report={report} onClose={onClose} />;
  }
  if (report.decisionMode === 'ScanBased') {
    return (
      <ScanIqcReport
        report={report}
        developmentUserKey={developmentUserKey}
        canInspect={canInspect}
        saving={saving}
        reason={reason}
        message={message}
        messageTone={messageTone}
        onReason={setReason}
        onStart={() => void start()}
        onUpload={(file) => void addScanAttachment(file)}
        onDelete={(attachmentId) => void removeScanAttachment(attachmentId)}
        onFinalize={(result) => void finalizeScan(result)}
        onClose={onClose}
      />
    );
  }
  if (report.reportStatus === 'Finalized') {
    return <FinalizedReport report={report} developmentUserKey={developmentUserKey} saving={saving} message={message} messageTone={messageTone} onRetry={() => void run(() => retryIqcPdf(developmentUserKey, report.reportId!), 'PDF 생성을 다시 요청했습니다.')} onClose={onClose} />;
  }
  if (!report.reportId) {
    return (
      <div className="iqc-report-start">
        <span className="iqc-shape-mark">01</span>
        <p className="eyebrow">DIGITAL INSPECTION</p>
        <h3>{report.orderItem ?? '발주품목 미입력'}</h3>
        <p>{report.projectCode} · {report.attemptNumber}차 검사</p>
        <div className="iqc-start-points"><span>6개 기본 항목</span><span>외함 사진 필수</span><span>확정 후 수정 불가</span></div>
        <ActionFeedback message={message} tone={messageTone} />
        <button type="button" className="primary-button" disabled={!canInspect || saving} onClick={() => void start()}>검사 시작</button>
        <button type="button" onClick={onClose}>닫기</button>
      </div>
    );
  }

  return (
    <div className="iqc-report-editor" data-step={step}>
      <ReportContext report={report} />
      {report.reinspectionSource ? <ReinspectionComparison source={report.reinspectionSource} developmentUserKey={developmentUserKey} /> : null}
      <nav className="iqc-report-steps" aria-label="성적서 작성 단계">
        {reportSteps.map((value, index) => (
          <button type="button" key={value} data-active={step === value} onClick={() => setStep(value)}>
            <i>{index + 1}</i><span>{value === 'items' ? '검사항목·사진' : '최종확인'}</span>
          </button>
        ))}
      </nav>
      <div className="iqc-progress-strip"><strong>{requiredMissing === 0 ? '필수 항목 완료' : `필수 ${requiredMissing}개 남음`}</strong><span>{photoMissing === 0 ? '사진 준비됨' : '외함 사진 필요'}</span></div>

      {step === 'items' ? (
        <section className="iqc-report-panel" aria-labelledby="iqc-items-title">
          <header><span>STEP 1</span><div><h4 id="iqc-items-title">검사항목</h4><p>현장에서 확인한 결과만 선택합니다.</p></div></header>
          <div className="iqc-item-stack">
            {scopedItems.map((item) => (
              <IqcItemEditor
                key={item.itemId}
                item={item}
                value={draft[item.itemId]}
                photos={report.photos.filter((photo) => photo.templateItemId === item.itemId)}
                reportId={report.reportId!}
                developmentUserKey={developmentUserKey}
                disabled={!canInspect || saving}
                reinspection={Boolean(report.reinspectionSource)}
                onChange={(value) => setDraft((current) => ({ ...current, [item.itemId]: value }))}
                onUpload={(file, alt) => addPhoto(item.itemId, file, alt)}
                onDelete={removePhoto}
              />
            ))}
          </div>
          <button type="button" className="primary-button iqc-next-button" disabled={!canInspect || saving} onClick={() => void saveAndNext()}>검사항목·사진 저장 후 최종확인</button>
        </section>
      ) : null}

      {step === 'review' ? (
        <section className="iqc-report-panel iqc-review-panel" aria-labelledby="iqc-review-title">
          <header><span>STEP 2</span><div><h4 id="iqc-review-title">최종확인</h4><p>확정 후에는 성적서와 사진을 수정할 수 없습니다.</p></div></header>
          <div className="iqc-review-score"><div data-complete={requiredMissing === 0}><strong>{scopedItems.length - requiredMissing}/{scopedItems.length}</strong><span>{report.attemptNumber > 1 ? '재검사 항목' : '검사항목'}</span></div><div data-complete={photoMissing === 0}><strong>{report.photos.length}</strong><span>증빙 사진</span></div><div><strong>v{report.templateVersion}</strong><span>검사 양식</span></div></div>
          <label className="iqc-reason-field"><span>{report.attemptNumber > 1 ? '재검사 코멘트' : '종합 판정 사유'}</span><textarea data-field="iqc-report-reason" aria-invalid={messageTone === 'error' && reason.trim().length < 3 || undefined} value={reason} onChange={(event) => setReason(event.target.value)} placeholder={report.attemptNumber > 1 ? '조치 내용을 확인한 결과와 재검사 판정 근거를 기록' : '확인 결과를 3자 이상 기록'} disabled={!canInspect || saving} /></label>
          <div className="iqc-finalize-readiness" id="iqc-finalize-readiness" data-ready={isReadyToFinalize} role="status">
            <strong>{isReadyToFinalize ? '판정 준비 완료' : '판정 전에 확인해 주세요'}</strong>
            <ul>
              <li data-complete={requiredMissing === 0}>{requiredMissing === 0 ? '필수 검사항목 완료' : `필수 검사항목 ${requiredMissing}개 남음`}</li>
              <li data-complete={photoMissing === 0}>{photoMissing === 0 ? '필수 사진 등록 완료' : `필수 사진 ${photoMissing}장 필요`}</li>
              <li data-complete={!reasonMissing}>{reasonMissing ? `${report.attemptNumber > 1 ? '재검사 코멘트' : '판정 사유'} 3자 이상 필요` : '판정 근거 입력 완료'}</li>
            </ul>
          </div>
          <ActionFeedback message={message} tone={messageTone} />
          <div className="iqc-finalize-actions" aria-describedby="iqc-finalize-readiness">
            <button
              type="button"
              className={finalResult === 'Failed' ? 'iqc-fail-button' : 'primary-button'}
              disabled={!canInspect || saving || !isReadyToFinalize}
              onClick={() => void finalize(finalResult)}
            >
              {finalResult === 'Failed'
                ? report.attemptNumber > 1 ? '불합격 · 재조치 요청' : '부적합 · 입고 차단'
                : report.attemptNumber > 1 ? '합격 · Pending 해제' : '합격 · 성적서 확정'}
            </button>
          </div>
        </section>
      ) : null}
      {step !== 'review' ? <ActionFeedback message={message} tone={messageTone} /> : null}
      <button type="button" className="iqc-close-button" onClick={onClose}>검사함으로 돌아가기</button>
    </div>
  );
}

function IqcItemEditor({
  item,
  value,
  photos,
  reportId,
  developmentUserKey,
  disabled,
  reinspection,
  onChange,
  onUpload,
  onDelete
}: {
  item: IqcTemplateItem;
  value?: SaveIqcItemResponse;
  photos: IqcReport['photos'];
  reportId: string;
  developmentUserKey: string;
  disabled: boolean;
  reinspection: boolean;
  onChange: (value: SaveIqcItemResponse) => void;
  onUpload: (file: File | null, alt: string) => Promise<void>;
  onDelete: (photoId: string) => Promise<void>;
}) {
  const current = value ?? { templateItemId: item.itemId, checkResult: null, textValue: null, note: null };
  return (
    <article className="iqc-item-card" data-complete={isAnswered(item, current)} data-photo={item.requiresPhoto} data-reinspection={reinspection || undefined}>
      <header><i>{String(item.displayOrder).padStart(2, '0')}</i><div><strong>{item.label}</strong><small>{item.guidance}</small></div>{item.requiresPhoto ? <span>PHOTO</span> : null}</header>
      {item.responseType === 'Check' ? (
        <div className="iqc-check-options">
          {([['Pass', '적합'], ['Fail', '부적합'], ...(reinspection ? [] : [['NotApplicable', '해당없음']])] as Array<[IqcCheckResult, string]>).map(([result, label]) => <button type="button" key={result} data-active={current.checkResult === result} data-result={result} disabled={disabled} onClick={() => onChange({ ...current, checkResult: result })}>{label}</button>)}
        </div>
      ) : <textarea aria-label={item.label} value={current.textValue ?? ''} maxLength={item.maxTextLength ?? undefined} onChange={(event) => onChange({ ...current, textValue: event.target.value || null })} placeholder="측정값 또는 특이사항" disabled={disabled} />}
      {item.responseType === 'Check' && current.checkResult && current.checkResult !== 'Pass' ? <input aria-label={`${item.label} 비고`} value={current.note ?? ''} onChange={(event) => onChange({ ...current, note: event.target.value || null })} placeholder={current.checkResult === 'NotApplicable' ? '해당없음 사유 필수' : '부적합 근거 또는 비고'} disabled={disabled} /> : null}
      {item.requiresPhoto ? (
        <IqcInlinePhotoEditor
          item={item}
          photos={photos}
          reportId={reportId}
          developmentUserKey={developmentUserKey}
          disabled={disabled}
          onUpload={onUpload}
          onDelete={onDelete}
        />
      ) : null}
    </article>
  );
}

function IqcInlinePhotoEditor({
  item,
  photos,
  reportId,
  developmentUserKey,
  disabled,
  onUpload,
  onDelete
}: {
  item: IqcTemplateItem;
  photos: IqcReport['photos'];
  reportId: string;
  developmentUserKey: string;
  disabled: boolean;
  onUpload: (file: File | null, alt: string) => Promise<void>;
  onDelete: (photoId: string) => Promise<void>;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [alt, setAlt] = useState(`${item.label} 사진`);
  return (
    <section className="iqc-inline-photo" aria-label={`${item.label} 사진 첨부`}>
      <div className="iqc-inline-photo-heading">
        <strong>사진 첨부 <b>필수</b></strong>
        <span>{photos.length > 0 ? `${photos.length}장 등록됨` : '판정과 함께 바로 등록해 주세요.'}</span>
      </div>
      <label className="iqc-photo-drop"><input type="file" accept="image/jpeg,image/png" capture="environment" onChange={(event) => setFile(event.target.files?.[0] ?? null)} disabled={disabled} /><i>＋</i><strong>{file ? file.name : '카메라 또는 사진 선택'}</strong><span>JPEG · PNG / 장당 5MB 이하</span></label>
      <label className="iqc-photo-alt"><span>사진 설명</span><input value={alt} maxLength={200} onChange={(event) => setAlt(event.target.value)} disabled={disabled} /></label>
      <button type="button" className="primary-button" disabled={disabled || !file || alt.trim().length === 0} onClick={() => void onUpload(file, alt.trim()).then(() => setFile(null))}>이 항목에 사진 등록</button>
      <div className="iqc-photo-grid">{photos.map((photo) => <PhotoEvidence key={photo.photoId} reportId={reportId} photo={photo} developmentUserKey={developmentUserKey} editable={!disabled} onDelete={() => void onDelete(photo.photoId)} />)}</div>
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

function FinalizedReport({ report, developmentUserKey, saving, message, messageTone, onRetry, onClose }: { report: IqcReport; developmentUserKey: string; saving: boolean; message: string; messageTone: ActionFeedbackTone; onRetry: () => void; onClose: () => void }) {
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
      <ActionFeedback message={message} tone={messageTone} />
      <button type="button" onClick={onClose}>검사함으로 돌아가기</button>
    </div>
  );
}

function LegacyReport({ report, onClose }: { report: IqcReport; onClose: () => void }) {
  return <div className="iqc-legacy-report"><span>LEGACY</span><h3>기존 간편 판정</h3><p>상세 성적서 기능 도입 전에 처리된 검사입니다. 확인하지 않은 항목은 소급 생성하지 않습니다.</p><dl><div><dt>판정</dt><dd>{report.attemptStatus === 'Passed' ? '합격' : report.attemptStatus === 'Failed' ? '부적합' : '검사 대기'}</dd></div><div><dt>기존 사유</dt><dd>{report.reason ?? '-'}</dd></div></dl><button type="button" onClick={onClose}>닫기</button></div>;
}

function ScanIqcReport({
  report,
  developmentUserKey,
  canInspect,
  saving,
  reason,
  message,
  messageTone,
  onReason,
  onStart,
  onUpload,
  onDelete,
  onFinalize,
  onClose
}: {
  report: IqcReport;
  developmentUserKey: string;
  canInspect: boolean;
  saving: boolean;
  reason: string;
  message: string;
  messageTone: ActionFeedbackTone;
  onReason: (value: string) => void;
  onStart: () => void;
  onUpload: (file: File | null) => void;
  onDelete: (attachmentId: string) => void;
  onFinalize: (result: 'Passed' | 'Failed') => void;
  onClose: () => void;
}) {
  async function download(reportId: string, attachmentId: string, fileName: string) {
    try {
      const blob = await getIqcScanAttachmentBlob(developmentUserKey, reportId, attachmentId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      // The page-level feedback remains focused on mutations; a browser retry is safe.
    }
  }

  if (!report.reportId) {
    return (
      <div className="iqc-report-start">
        <span className="iqc-shape-mark">01</span>
        <p className="eyebrow">PAPER-FIRST IQC</p>
        <h3>{report.orderItem ?? '외함'}</h3>
        <p>협력사 검사서에 현장 판정과 서명을 완료한 뒤 PDF 또는 사진으로 등록합니다.</p>
        <div className="iqc-start-points"><span>적합·부적합 1회 판정</span><span>서명 스캔본 필수</span><span>확정 후 수정 불가</span></div>
        <ActionFeedback message={message} tone={messageTone} />
        <button type="button" className="primary-button" disabled={!canInspect || saving} onClick={onStart}>검사 시작</button>
        <button type="button" onClick={onClose}>닫기</button>
      </div>
    );
  }

  const finalized = report.reportStatus === 'Finalized';
  return (
    <div className="iqc-report-editor iqc-scan-editor">
      <ReportContext report={report} />
      {(report.scanHistory?.length ?? 0) > 0 ? (
        <section className="iqc-report-panel">
          <header><span>HISTORY</span><div><h4>이전 검사·조치 이력</h4><p>이전 부적합 근거와 조치 내용을 먼저 확인합니다.</p></div></header>
          <div className="iqc-item-stack">
            {report.scanHistory?.map((history) => (
              <article className="iqc-item-editor" key={history.attemptNumber}>
                <strong>{history.attemptNumber}차 · {history.result === 'Passed' ? '적합' : '부적합'}</strong>
                <p>부적합 근거: {history.reason}</p>
                {history.actionReason ? <p>조치 내용: {history.actionReason}</p> : null}
                <div className="iqc-scan-files">{history.attachments.map((file) => <button type="button" className="link-button" key={file.attachmentId} onClick={() => void download(history.reportId, file.attachmentId, file.originalFileName)}>{file.originalFileName}</button>)}</div>
              </article>
            ))}
          </div>
        </section>
      ) : null}
      <section className="iqc-report-panel">
        <header><span>STEP 1</span><div><h4>서명 스캔본</h4><p>PDF, JPEG, PNG를 최대 10개까지 등록할 수 있습니다.</p></div></header>
        <div className="iqc-scan-files">
          {report.scanAttachments?.map((file) => (
            <article key={file.attachmentId}>
              <button type="button" className="link-button" onClick={() => void download(report.reportId!, file.attachmentId, file.originalFileName)}>{file.originalFileName}</button>
              <small>{formatBytes(file.byteSize)}</small>
              {!finalized && canInspect ? <button type="button" disabled={saving} onClick={() => onDelete(file.attachmentId)}>삭제</button> : null}
            </article>
          ))}
          {(report.scanAttachments?.length ?? 0) === 0 ? <p>등록된 스캔본이 없습니다.</p> : null}
        </div>
        {!finalized ? <label className="iqc-scan-upload"><span>검사서 파일 추가</span><input type="file" accept=".pdf,image/jpeg,image/png" disabled={!canInspect || saving || (report.scanAttachments?.length ?? 0) >= 10} onChange={(event) => { onUpload(event.target.files?.[0] ?? null); event.currentTarget.value = ''; }} /></label> : null}
      </section>
      <section className="iqc-report-panel iqc-review-panel">
        <header><span>STEP 2</span><div><h4>판정 확정</h4><p>현장에서 서명한 검사서와 같은 판정을 선택합니다.</p></div></header>
        <label className="iqc-reason-field"><span>판정 사유</span><textarea value={reason} onChange={(event) => onReason(event.target.value)} disabled={finalized || !canInspect || saving} placeholder="검사 결과와 특이사항을 3자 이상 입력" /></label>
        <ActionFeedback message={message} tone={messageTone} />
        {finalized ? <div className="iqc-final-seal" data-result={report.result}><i>✓</i><div><span>FINALIZED</span><strong>{report.result === 'Passed' ? 'IQC 합격' : '부적합 · 입고 차단'}</strong><small>{formatDateTime(report.finalizedAtUtc)} · {report.finalizedBy}</small></div></div> : (
          <div className="iqc-final-actions">
            <button type="button" disabled={!canInspect || saving || (report.scanAttachments?.length ?? 0) === 0 || reason.trim().length < 3} onClick={() => onFinalize('Failed')}>부적합 확정</button>
            <button type="button" className="primary-button" disabled={!canInspect || saving || (report.scanAttachments?.length ?? 0) === 0 || reason.trim().length < 3} onClick={() => onFinalize('Passed')}>합격 확정</button>
          </div>
        )}
        <button type="button" onClick={onClose}>닫기</button>
      </section>
    </div>
  );
}

function formatBytes(value: number) {
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

function ReportContext({ report }: { report: IqcReport }) {
  return <header className="iqc-report-context"><div><span>{report.projectCode}</span><strong>{report.orderItem ?? '발주품목 미입력'}</strong><small>{report.projectTitle}</small></div><div><b>{report.attemptNumber}차</b><span>{formatQuantity(report.quantity, report.unit)}</span></div></header>;
}

function ReinspectionComparison({ source, developmentUserKey }: { source: IqcReinspectionSource; developmentUserKey: string | undefined }) {
  const originalPhotos = source.evidence?.originalFailurePhotos ?? [];
  const actionRound = source.evidence?.latestActionRound ?? null;
  return (
    <section className="iqc-reinspection-comparison" aria-labelledby="iqc-reinspection-title">
      <header>
        <div><span>REINSPECTION SCOPE</span><h4 id="iqc-reinspection-title">부적합 항목만 다시 확인합니다</h4></div>
        <strong>{source.previousAttemptNumber}차 검사 기준 · {source.failures.length}개</strong>
      </header>
      <div className="iqc-reinspection-evidence">
        <article>
          <span>1. 최초 부적합 근거</span>
          <strong>{source.failureReason}</strong>
          {originalPhotos.length > 0 ? <div className="reinspection-evidence-photo-grid">{originalPhotos.map((photo) => <EvidencePhoto key={photo.photoId} developmentUserKey={developmentUserKey} photo={photo} />)}</div> : <small>등록된 부적합 사진 없음</small>}
        </article>
        <article>
          <span>2. 조치 내용과 사진</span>
          <strong>{actionRound?.actionReasonSnapshot ?? (source.actionReason?.trim() || '아래 Pending 조치 이력에서 확인')}</strong>
          {actionRound ? <small>{actionRound.actionRound}차 조치 · {actionRound.confirmedByDisplayName}</small> : null}
          {actionRound?.photos.length ? <div className="reinspection-evidence-photo-grid">{actionRound.photos.map((photo) => <EvidencePhoto key={photo.photoId} developmentUserKey={developmentUserKey} photo={photo} />)}</div> : <small>등록된 조치 사진 없음</small>}
        </article>
      </div>
      <ul>
        {source.failures.map((failure) => (
          <li key={failure.itemCode}>
            <span>{source.previousAttemptNumber}차 부적합</span>
            <div><strong>{failure.label}</strong>{failure.note ? <p>{failure.note}</p> : null}</div>
          </li>
        ))}
      </ul>
      <p><b>3. 재검사 판정</b> · 아래 항목은 <b>적합</b> 또는 <b>부적합</b>으로만 다시 판정할 수 있습니다.</p>
    </section>
  );
}

function ActionFeedback({ message, tone }: { message: string; tone: ActionFeedbackTone }) {
  if (!message) return null;
  return (
    <p
      className="action-feedback iqc-action-feedback"
      data-tone={tone}
      role={tone === 'error' ? 'alert' : 'status'}
      aria-live={tone === 'error' ? 'assertive' : 'polite'}
    >
      {message}
    </p>
  );
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

async function discoverReinspectionSource(developmentUserKey: string, current: IqcReport): Promise<IqcReport> {
  try {
    const queue = await getMaterialIqcQueue(developmentUserKey, true);
    const previous = queue.items
      .filter((item) => item.receiptId === current.receiptId && item.attemptNumber < current.attemptNumber && item.status === 'Failed')
      .sort((left, right) => right.attemptNumber - left.attemptNumber)[0];
    if (!previous) return current;

    const previousReport = await getIqcReport(developmentUserKey, previous.attemptId);
    const responseByItem = new Map(previousReport.responses.map((response) => [response.templateItemId, response]));
    const failures = previousReport.items.flatMap((item) => {
      const response = responseByItem.get(item.itemId);
      return response?.checkResult === 'Fail'
        ? [{ itemCode: item.itemCode, label: item.label, note: response.note }]
        : [];
    });
    if (failures.length === 0) return current;

    let actionReason: string | null = null;
    if (previous.pendingIssueId) {
      try {
        const pending = await getPendingIssue(developmentUserKey, previous.pendingIssueId);
        actionReason = [...pending.history]
          .reverse()
          .find((event) => event.toStatus === 'ReinspectionRequested' && event.reason?.trim())
          ?.reason?.trim() ?? null;
      } catch {
        actionReason = null;
      }
    }

    return {
      ...current,
      reinspectionSource: {
        previousAttemptNumber: previous.attemptNumber,
        failureReason: previousReport.reason?.trim()
          || failures.map((failure) => failure.note?.trim()).filter(Boolean).join(' · ')
          || '이전 검사 부적합',
        actionReason,
        failures
      }
    };
  } catch {
    return current;
  }
}

function errorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError) return error.errors ? Object.values(error.errors).flat()[0] ?? error.message : error.message;
  return fallback;
}
