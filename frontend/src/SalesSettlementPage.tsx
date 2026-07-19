import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { ApiError, completeSalesSettlement, getSalesSettlement, saveSalesSettlementDraft } from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { workflowShapeRole } from './design-system';
import type { SalesSettlementDetail } from './salesSettlement';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: SalesSettlementDetail }
  | { kind: 'error'; message: string };

type Feedback = { tone: 'success' | 'error' | 'info'; message: string };

export function SalesSettlementPage({
  developmentUserKey,
  projectId,
  onBack,
  onOpenPending
}: {
  developmentUserKey: string;
  projectId: string;
  onBack: () => void;
  onOpenPending: () => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [invoiceIssuedDate, setInvoiceIssuedDate] = useState('');
  const [invoiceNumber, setInvoiceNumber] = useState('');
  const [note, setNote] = useState('');
  const [busyAction, setBusyAction] = useState<'draft' | 'complete' | null>(null);
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const feedbackRef = useRef<HTMLDivElement>(null);

  const applyDetail = useCallback((detail: SalesSettlementDetail) => {
    setState({ kind: 'ready', data: detail });
    setInvoiceIssuedDate(detail.invoiceIssuedDate ?? '');
    setInvoiceNumber(detail.invoiceNumber ?? '');
    setNote(detail.note ?? '');
  }, []);

  const load = useCallback(async (quiet = false) => {
    if (!quiet) setState({ kind: 'loading' });
    try {
      applyDetail(await getSalesSettlement(developmentUserKey, projectId));
    } catch (error) {
      setState({ kind: 'error', message: messageOf(error, '정산 정보를 불러오지 못했습니다.') });
    }
  }, [applyDetail, developmentUserKey, projectId]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => { if (feedback?.tone === 'error') feedbackRef.current?.focus(); }, [feedback]);

  function projection() {
    return {
      expectedVersion: state.kind === 'ready' ? state.data.version : 0,
      invoiceIssuedDate: invoiceIssuedDate || null,
      invoiceNumber: invoiceNumber.trim() || null,
      note: note.trim() || null
    };
  }

  async function saveDraft(event?: FormEvent) {
    event?.preventDefault();
    if (state.kind !== 'ready' || !state.data.canMutate || busyAction) return;
    setBusyAction('draft');
    setFeedback(null);
    try {
      await saveSalesSettlementDraft(developmentUserKey, projectId, projection());
      await load(true);
      setFeedback({ tone: 'success', message: '세금계산서 정보를 임시 저장했습니다.' });
    } catch (error) {
      setFeedback({ tone: 'error', message: messageOf(error, '임시 저장하지 못했습니다.') });
      if (error instanceof ApiError && error.status === 409) await load(true);
    } finally {
      setBusyAction(null);
    }
  }

  async function complete() {
    if (state.kind !== 'ready' || !state.data.canMutate || busyAction) return;
    if (!invoiceIssuedDate) {
      setFeedback({ tone: 'error', message: '세금계산서 발행일을 입력해 주세요.' });
      setConfirmOpen(false);
      return;
    }
    setBusyAction('complete');
    setFeedback(null);
    try {
      await completeSalesSettlement(developmentUserKey, projectId, {
        ...projection(),
        operationId: crypto.randomUUID()
      });
      await load(true);
      setConfirmOpen(false);
      setFeedback({ tone: 'success', message: '정산과 프로젝트 최종 완료가 한 번에 처리되었습니다.' });
    } catch (error) {
      setFeedback({ tone: 'error', message: messageOf(error, '최종 완료하지 못했습니다.') });
      if (error instanceof ApiError && error.status === 409) await load(true);
    } finally {
      setBusyAction(null);
    }
  }

  if (state.kind === 'loading') {
    return <section className="page-surface sales-settlement-page"><div className="settlement-loading" aria-live="polite"><span />정산 조건을 확인하고 있습니다.</div></section>;
  }

  if (state.kind === 'error') {
    return (
      <section className="page-surface sales-settlement-page">
        <button type="button" className="settlement-back" onClick={onBack}>← 프로젝트</button>
        <div className="settlement-feedback" data-tone="error" role="alert"><strong>정산 화면을 열 수 없습니다.</strong><span>{state.message}</span><button type="button" onClick={() => void load()}>다시 시도</button></div>
      </section>
    );
  }

  const detail = state.data;
  const completed = detail.settlementStatus === 'Completed' || detail.projectStatus === 'Completed';
  const conditionsReady = detail.allPanelsDelivered && detail.noOpenPending;
  const displayedInvoiceReady = Boolean(invoiceIssuedDate);
  const canSubmitComplete = detail.canMutate && conditionsReady && displayedInvoiceReady && !busyAction;

  return (
    <section className={`page-surface sales-settlement-page ${isMobile ? 'mobile-first-page' : ''}`}>
      <header className="settlement-hero">
        <div className="settlement-hero-copy">
          <button type="button" className="settlement-back" onClick={onBack}>← 프로젝트</button>
          <p className="eyebrow">SALES · FINAL STEP</p>
          <h2>{completed ? '프로젝트 완료 내역' : '정산하고 완료하기'}</h2>
          <p><strong>{detail.projectCode}</strong><span aria-hidden="true">·</span>{detail.projectTitle}</p>
        </div>
        <div className="settlement-orbit" data-complete={completed}><span>{completed ? '완료' : '18'}</span><small>{completed ? '기록 잠금' : '마지막 단계'}</small></div>
      </header>

      {feedback ? <div ref={feedbackRef} tabIndex={-1} className="settlement-feedback" data-tone={feedback.tone} role={feedback.tone === 'error' ? 'alert' : 'status'} aria-live="polite"><span>{feedback.message}</span></div> : null}

      <section className="settlement-conditions" aria-labelledby="settlement-conditions-title">
        <div className="settlement-section-heading">
          <div><span className="settlement-kicker">01</span><h3 id="settlement-conditions-title">완료 조건</h3></div>
          <span className="settlement-condition-total" data-ready={conditionsReady}>{conditionsReady ? '조건 충족' : '확인 필요'}</span>
        </div>
        <div className="settlement-condition-grid">
          <ConditionCard ready={detail.allPanelsDelivered} label="납품" value={`${detail.deliveredPanelCount}/${detail.activePanelCount}`} supporting={detail.activePanelCount === 0 ? 'active panel이 없습니다' : 'active panel'} />
          <ConditionCard ready={detail.noOpenPending} blocked={!detail.noOpenPending} label="Pending" value={`${detail.openPendingCount}건`} supporting={detail.noOpenPending ? '미결 사항 없음' : '먼저 조치가 필요합니다'} action={detail.openPendingCount > 0 ? <button type="button" onClick={onOpenPending}>목록 열기</button> : undefined} />
          <ConditionCard ready={displayedInvoiceReady} label="세금계산서" value={displayedInvoiceReady ? '발행 기록' : '미입력'} supporting={displayedInvoiceReady ? invoiceIssuedDate : '발행일이 필요합니다'} />
        </div>
      </section>

      <div className="settlement-workspace">
        <section className="settlement-invoice-panel" aria-labelledby="settlement-invoice-title">
          <div className="settlement-section-heading">
            <div><span className="settlement-kicker">02</span><h3 id="settlement-invoice-title">세금계산서</h3></div>
            <span className="settlement-state-chip">{completed ? '읽기 전용' : detail.settlementStatus === 'Draft' ? `임시 저장 v${detail.version}` : '새 기록'}</span>
          </div>
          <form onSubmit={saveDraft} className="settlement-form">
            <label className="form-field settlement-date-field">
              <span>발행일 <em>필수</em></span>
              <input type="date" required value={invoiceIssuedDate} onChange={(event) => setInvoiceIssuedDate(event.target.value)} disabled={completed || !detail.canMutate || Boolean(busyAction)} />
            </label>
            <label className="form-field">
              <span>세금계산서 번호 <small>선택 · 64자</small></span>
              <input maxLength={64} value={invoiceNumber} onChange={(event) => setInvoiceNumber(event.target.value)} placeholder="발행 시스템의 확인 번호" disabled={completed || !detail.canMutate || Boolean(busyAction)} />
            </label>
            <label className="form-field settlement-note-field">
              <span>정산 메모 <small>{note.length}/500</small></span>
              <textarea maxLength={500} rows={isMobile ? 3 : 4} value={note} onChange={(event) => setNote(event.target.value)} placeholder="정산에 필요한 내부 메모만 입력하세요" disabled={completed || !detail.canMutate || Boolean(busyAction)} />
            </label>
            {!completed && detail.canMutate ? <button type="submit" className="settlement-draft-button" disabled={Boolean(busyAction)}>{busyAction === 'draft' ? '저장 중…' : '임시 저장'}</button> : null}
          </form>
        </section>

        <section className="settlement-final-panel" data-complete={completed} aria-labelledby="settlement-final-title">
          <div className="settlement-section-heading">
            <div><span className="settlement-kicker">03</span><h3 id="settlement-final-title">{completed ? '완료 기록' : '최종 확인'}</h3></div>
          </div>
          {completed ? (
            <div className="settlement-completed-record">
              <span className="settlement-seal" aria-hidden="true">✓</span>
              <strong>프로젝트가 최종 완료되었습니다.</strong>
              <dl><div><dt>완료 담당</dt><dd>{detail.completedByName ?? '-'}</dd></div><div><dt>완료 시각</dt><dd>{formatDateTime(detail.completedAtUtc)}</dd></div></dl>
              <p>완료 기록과 세금계산서 정보는 변경할 수 없습니다.</p>
            </div>
          ) : (
            <>
              <div className="settlement-lock-note"><span aria-hidden="true">!</span><p><strong>완료 후에는 되돌릴 수 없습니다.</strong>정산, 내 업무, workflow와 프로젝트 상태가 동시에 완료됩니다.</p></div>
              <ul className="settlement-final-checks">
                <li data-ready={detail.allPanelsDelivered}>모든 active panel 납품 완료</li>
                <li data-ready={detail.noOpenPending}>프로젝트 전체 open Pending 0건</li>
                <li data-ready={displayedInvoiceReady}>세금계산서 발행일 기록</li>
              </ul>
              {!confirmOpen ? (
                <button type="button" className="primary-button settlement-complete-button" disabled={!canSubmitComplete} onClick={() => setConfirmOpen(true)}>최종 완료 확인</button>
              ) : (
                <div className="settlement-confirm-box" role="group" aria-label="프로젝트 최종 완료 확인">
                  <strong>정말 최종 완료할까요?</strong>
                  <div><button type="button" onClick={() => setConfirmOpen(false)} disabled={Boolean(busyAction)}>돌아가기</button><button type="button" className="primary-button" onClick={() => void complete()} disabled={Boolean(busyAction)}>{busyAction === 'complete' ? '완료 처리 중…' : '정산·프로젝트 완료'}</button></div>
                </div>
              )}
              {!detail.canMutate ? <p className="settlement-permission-note">조회할 수 있지만 이 프로젝트의 정산 담당자는 아닙니다.</p> : null}
            </>
          )}
        </section>
      </div>
    </section>
  );
}

function ConditionCard({ ready, blocked = false, label, value, supporting, action }: { ready: boolean; blocked?: boolean; label: string; value: string; supporting: string; action?: ReactNode }) {
  const shapeRole = workflowShapeRole({ blocked, completed: ready });
  return <article className="settlement-condition-card" data-shape-role="surface" data-ready={ready}><div className="settlement-condition-icon" data-shape-role={shapeRole} aria-hidden="true">{ready ? '✓' : '·'}</div><div><span>{label}</span><strong>{value}</strong><small>{supporting}</small></div>{action}</article>;
}

function messageOf(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    const validation = error.errors ? Object.values(error.errors).flat()[0] : null;
    return validation ?? error.message;
  }
  return error instanceof Error ? error.message : fallback;
}

function formatDateTime(value: string | null) {
  if (!value) return '-';
  return new Intl.DateTimeFormat('ko-KR', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Seoul' }).format(new Date(value));
}
