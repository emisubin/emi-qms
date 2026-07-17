import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { useAdaptiveLayout } from './adaptive-layout';
import {
  ApiError,
  cancelMaterialReceipt,
  closeMaterialArrivals,
  confirmMaterialReceipt,
  getMaterialIqcQueue,
  getMaterialReceipts,
  recordMaterialIqcResult,
  registerMaterialArrival,
  requestMaterialIqc,
  requestMaterialReinspection
} from './api';
import { MobileSheet } from './MobileSheet';
import type {
  MaterialIqcQueueItem,
  MaterialReceipt,
  MaterialReceiptListResponse,
  MaterialReceiptStatus,
  MaterialReceivingItem
} from './materials';

type LoadState<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; data: T }
  | { kind: 'error'; message: string };

type MaterialAction =
  | { kind: 'arrival'; item: MaterialReceivingItem }
  | { kind: 'receipt'; item: MaterialReceivingItem; receipt: MaterialReceipt }
  | { kind: 'close'; item: MaterialReceivingItem };

export function MaterialReceivingPage({
  developmentUserKey,
  canUpdate,
  onBack,
  onOpenIqc,
  onOpenPending
}: {
  developmentUserKey: string;
  canUpdate: boolean;
  onBack: () => void;
  onOpenIqc: () => void;
  onOpenPending: (pendingId: string) => void;
}) {
  const layout = useAdaptiveLayout();
  const [state, setState] = useState<LoadState<MaterialReceiptListResponse>>({ kind: 'loading' });
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [includeCompleted, setIncludeCompleted] = useState(false);
  const [supplyFilter, setSupplyFilter] = useState<'All' | 'Purchased' | 'CustomerSupplied'>('All');
  const [activeFilter, setActiveFilter] = useState<'all' | 'iqc' | 'blocked' | 'confirm'>('all');
  const [action, setAction] = useState<MaterialAction | null>(null);
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    try {
      const data = await getMaterialReceipts(developmentUserKey, appliedSearch, includeCompleted, '', '', supplyFilter);
      setState({ kind: 'ready', data });
    } catch (error) {
      setState({ kind: 'error', message: errorMessage(error, '자재 입고 현황을 불러오지 못했습니다.') });
    }
  }, [appliedSearch, developmentUserKey, includeCompleted, supplyFilter]);

  useEffect(() => {
    queueMicrotask(() => void load());
  }, [load]);

  useEffect(() => {
    if (state.kind !== 'ready') {
      return;
    }
    const receiptId = new URLSearchParams(window.location.search).get('receipt');
    if (!receiptId) {
      return;
    }
    for (const item of state.data.items) {
      const receipt = item.receipts.find((candidate) => candidate.receiptId === receiptId);
      if (receipt) {
        queueMicrotask(() => setAction({ kind: 'receipt', item, receipt }));
        break;
      }
    }
  }, [state]);

  const visibleItems = useMemo(() => {
    if (state.kind !== 'ready' || activeFilter === 'all') {
      return state.kind === 'ready' ? state.data.items : [];
    }
    const statusByFilter: Record<Exclude<typeof activeFilter, 'all'>, MaterialReceiptStatus> = {
      iqc: 'IqcRequested',
      blocked: 'FailedBlocked',
      confirm: 'Passed'
    };
    return state.data.items.filter((item) => item.receipts.some((receipt) => receipt.status === statusByFilter[activeFilter]));
  }, [activeFilter, state]);

  async function runAction(operation: () => Promise<unknown>, success: string) {
    setMessage('');
    try {
      await operation();
      setMessage(success);
      setAction(null);
      await load();
    } catch (error) {
      setMessage(errorMessage(error, '작업을 완료하지 못했습니다.'));
    }
  }

  const actionPanel = action ? (
    <MaterialActionPanel
      action={action}
      canUpdate={canUpdate}
      message={message}
      onClose={() => { setAction(null); setMessage(''); }}
      onOpenIqc={onOpenIqc}
      onOpenPending={onOpenPending}
      onRun={runAction}
      developmentUserKey={developmentUserKey}
    />
  ) : null;

  return (
    <section className="page-surface material-workspace" data-testid="material-receiving-page">
      <header className="material-hero">
        <div>
          <p className="eyebrow">MATERIAL FLOW</p>
          <h2>자재 입고 관리</h2>
          <p>도착부터 IQC, 입고 확정까지 한 흐름으로 관리합니다.</p>
        </div>
        <div className="material-hero-actions">
          <button type="button" onClick={onBack}>프로젝트</button>
          <button type="button" className="primary-button" onClick={onOpenIqc}>IQC 검사함</button>
        </div>
      </header>

      {state.kind === 'ready' ? (
        <div className="material-summary-strip" aria-label="자재 입고 요약">
          <SummaryButton label="도착 등록 대기" value={state.data.summary.pendingArrivalCount} active={activeFilter === 'all'} onClick={() => setActiveFilter('all')} />
          <SummaryButton label="IQC 대기" value={state.data.summary.waitingIqcCount} active={activeFilter === 'iqc'} onClick={() => setActiveFilter('iqc')} />
          <SummaryButton label="부적합 차단" value={state.data.summary.failedBlockedCount} active={activeFilter === 'blocked'} tone="danger" onClick={() => setActiveFilter('blocked')} />
          <SummaryButton label="확정 대기" value={state.data.summary.readyToConfirmCount} active={activeFilter === 'confirm'} onClick={() => setActiveFilter('confirm')} />
        </div>
      ) : null}

      <form className="material-search-panel" onSubmit={(event) => {
        event.preventDefault();
        const nextSearch = search.trim();
        if (nextSearch === appliedSearch) {
          void load();
        } else {
          setAppliedSearch(nextSearch);
        }
      }}>
        <label>
          <span>프로젝트·품목 검색</span>
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="PJT 코드, 발주품목, 업체" />
        </label>
        <label className="material-completed-toggle">
          <input type="checkbox" checked={includeCompleted} onChange={(event) => setIncludeCompleted(event.target.checked)} />
          <span>완료 포함</span>
        </label>
        <button type="submit">검색</button>
      </form>

      <div className="material-supply-filter" role="group" aria-label="공급 방식 필터">
        {([
          ['All', '전체'],
          ['Purchased', '일반 구매'],
          ['CustomerSupplied', '사급']
        ] as const).map(([value, label]) => (
          <button type="button" key={value} data-active={supplyFilter === value} onClick={() => setSupplyFilter(value)}>{label}</button>
        ))}
        {state.kind === 'ready' ? <span>사급 {state.data.summary.customerSuppliedItemCount} · 제공 지연 {state.data.summary.customerSupplyOverdueCount}</span> : null}
      </div>

      {state.kind === 'loading' ? <MaterialLoading /> : null}
      {state.kind === 'error' ? <p className="error-text" role="alert">{state.message}</p> : null}
      {state.kind === 'ready' && visibleItems.length === 0 ? (
        <div className="material-empty-state"><strong>표시할 입고 항목이 없습니다.</strong><span>필터를 바꾸거나 도착 등록 대상을 확인해 주세요.</span></div>
      ) : null}

      {state.kind === 'ready' ? (
        <div className={layout.isMobile ? 'material-item-list material-item-list--mobile' : 'material-item-list material-item-list--desktop'}>
          {visibleItems.map((item) => (
            <MaterialItemCard
              key={item.itemId}
              item={item}
              canUpdate={canUpdate}
              mobile={layout.isMobile}
              onAction={setAction}
            />
          ))}
        </div>
      ) : null}

      {layout.isMobile ? (
        <MobileSheet
          open={action !== null}
          title={actionTitle(action)}
          eyebrow="MATERIAL ACTION"
          description="현재 단계에서 허용된 작업만 표시합니다."
          onClose={() => { setAction(null); setMessage(''); }}
        >
          {actionPanel}
        </MobileSheet>
      ) : actionPanel ? <aside className="material-action-drawer">{actionPanel}</aside> : null}
    </section>
  );
}

export function MaterialIqcPage({
  developmentUserKey,
  canInspect,
  onBack,
  onOpenPending
}: {
  developmentUserKey: string;
  canInspect: boolean;
  onBack: () => void;
  onOpenPending: (pendingId: string) => void;
}) {
  const layout = useAdaptiveLayout();
  const [state, setState] = useState<LoadState<MaterialIqcQueueItem[]>>({ kind: 'loading' });
  const [includeDecided, setIncludeDecided] = useState(false);
  const [selected, setSelected] = useState<MaterialIqcQueueItem | null>(null);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    try {
      const response = await getMaterialIqcQueue(developmentUserKey, includeDecided);
      setState({ kind: 'ready', data: response.items });
      const requestedId = new URLSearchParams(window.location.search).get('request');
      setSelected((current) => response.items.find((item) => item.attemptId === requestedId)
        ?? response.items.find((item) => item.attemptId === current?.attemptId)
        ?? null);
    } catch (error) {
      setState({ kind: 'error', message: errorMessage(error, 'IQC 검사함을 불러오지 못했습니다.') });
    }
  }, [developmentUserKey, includeDecided]);

  useEffect(() => { queueMicrotask(() => void load()); }, [load]);

  async function submit(result: 'Passed' | 'Failed') {
    if (!selected || reason.trim().length < 3) {
      setMessage('판정 사유를 3자 이상 입력해 주세요.');
      return;
    }
    setSaving(true);
    setMessage('');
    try {
      const response = await recordMaterialIqcResult(
        developmentUserKey,
        selected.attemptId,
        selected.receiptVersion,
        result,
        reason.trim()
      );
      setMessage(result === 'Passed' ? 'IQC 합격으로 판정했습니다.' : '부적합 Pending을 등록하고 입고를 차단했습니다.');
      setReason('');
      setSelected(null);
      await load();
      if (result === 'Failed' && response.pendingIssueId) {
        window.setTimeout(() => onOpenPending(response.pendingIssueId!), 450);
      }
    } catch (error) {
      setMessage(errorMessage(error, 'IQC 판정을 저장하지 못했습니다.'));
    } finally {
      setSaving(false);
    }
  }

  const inspector = selected ? (
    <IqcInspector
      item={selected}
      reason={reason}
      message={message}
      disabled={!canInspect || saving || selected.status !== 'Requested'}
      onReason={setReason}
      onSubmit={submit}
      onClose={() => { setSelected(null); setMessage(''); setReason(''); }}
      onOpenPending={onOpenPending}
    />
  ) : null;

  return (
    <section className="page-surface material-workspace material-iqc-workspace" data-testid="material-iqc-page">
      <header className="material-hero material-hero--quality">
        <div>
          <p className="eyebrow">QUALITY GATE</p>
          <h2>IQC 검사함</h2>
          <p>요청된 도착분을 확인하고 합격 또는 부적합을 기록합니다.</p>
        </div>
        <button type="button" onClick={onBack}>자재 입고로</button>
      </header>

      <div className="iqc-toolbar">
        <div><strong>{state.kind === 'ready' ? state.data.filter((item) => item.status === 'Requested').length : '-'}건</strong><span>검사 대기</span></div>
        <label><input type="checkbox" checked={includeDecided} onChange={(event) => setIncludeDecided(event.target.checked)} /> 판정 완료 포함</label>
      </div>

      {state.kind === 'loading' ? <MaterialLoading /> : null}
      {state.kind === 'error' ? <p className="error-text" role="alert">{state.message}</p> : null}
      {state.kind === 'ready' && state.data.length === 0 ? <div className="material-empty-state"><strong>검사 대기 항목이 없습니다.</strong><span>새 IQC 요청이 들어오면 여기에 표시됩니다.</span></div> : null}
      {state.kind === 'ready' ? (
        <div className={layout.isMobile ? 'iqc-card-list iqc-card-list--mobile' : 'iqc-card-list'}>
          {state.data.map((item) => (
            <button type="button" className="iqc-request-card" key={item.attemptId} data-status={item.status} onClick={() => setSelected(item)}>
              <span className="iqc-request-top"><strong>{item.projectCode}</strong><span className="material-card-badges">{item.supplyType === 'CustomerSupplied' ? <SupplyBadge overdue={false} /> : null}<StatusBadge status={item.status === 'Requested' ? 'IqcRequested' : item.status === 'Passed' ? 'Passed' : 'FailedBlocked'} /></span></span>
              <b>{item.orderItem ?? '발주품목 미입력'}</b>
              <small>{item.projectTitle}</small>
              <span>{formatQuantity(item.quantity, item.unit)} · {item.attemptNumber}차 검사</span>
              <small>{formatDateTime(item.requestedAtUtc)} 요청</small>
            </button>
          ))}
        </div>
      ) : null}

      {layout.isMobile ? (
        <MobileSheet open={selected !== null} title="IQC 판정" eyebrow="QUALITY CHECK" description="도착분과 검사 차수를 확인한 뒤 판정합니다." onClose={() => setSelected(null)} fullScreen>
          {inspector}
        </MobileSheet>
      ) : inspector ? <aside className="material-action-drawer">{inspector}</aside> : null}
    </section>
  );
}

function MaterialItemCard({ item, canUpdate, mobile, onAction }: {
  item: MaterialReceivingItem;
  canUpdate: boolean;
  mobile: boolean;
  onAction: (action: MaterialAction) => void;
}) {
  return (
    <article className="material-item-card" data-completed={item.receiptCompleted}>
      <header>
        <div><span>{item.projectCode}</span><strong>{item.orderItem ?? '발주품목 미입력'}</strong><small>{item.projectTitle}</small></div>
        <div className="material-card-badges">{item.supplyType === 'CustomerSupplied' ? <SupplyBadge overdue={item.customerSupplyOverdue} /> : null}<StatusBadge status={item.receiptCompleted ? 'Confirmed' : item.arrivalsClosed ? 'Passed' : item.receipts.at(0)?.status ?? 'Arrived'} /></div>
      </header>
      {mobile ? (
        <dl className="material-item-meta material-item-meta--priority">
          <div><dt>입고예정</dt><dd>{item.expectedReceiptDate ?? '-'}</dd></div>
          <div><dt>{item.supplyType === 'CustomerSupplied' ? '미도착' : '누적 도착'}</dt><dd>{formatQuantity(item.supplyType === 'CustomerSupplied' ? item.remainingQuantity : item.arrivedQuantity, item.orderUnit)}</dd></div>
          <div><dt>{item.supplyType === 'CustomerSupplied' ? '처리 대기' : '발주 수량'}</dt><dd>{formatQuantity(item.supplyType === 'CustomerSupplied' ? item.processingQuantity : item.orderQuantity, item.orderUnit)}</dd></div>
        </dl>
      ) : (
        <dl className="material-item-meta">
          <div><dt>{item.supplyType === 'CustomerSupplied' ? '공급 책임' : '업체'}</dt><dd>{item.supplyType === 'CustomerSupplied' ? '고객 제공' : item.supplierName ?? '-'}</dd></div>
          <div><dt>입고예정</dt><dd>{item.expectedReceiptDate ?? '-'}</dd></div>
          <div><dt>{item.supplyType === 'CustomerSupplied' ? '제공 예정' : '발주'}</dt><dd>{formatQuantity(item.orderQuantity, item.orderUnit)}</dd></div>
          <div><dt>누적 도착</dt><dd>{formatQuantity(item.arrivedQuantity, item.orderUnit)}</dd></div>
          {item.supplyType === 'CustomerSupplied' ? <><div><dt>입고 확정</dt><dd>{formatQuantity(item.confirmedQuantity, item.orderUnit)}</dd></div><div><dt>미도착 잔량</dt><dd>{formatQuantity(item.remainingQuantity, item.orderUnit)}</dd></div><div><dt>처리 대기량</dt><dd>{formatQuantity(item.processingQuantity, item.orderUnit)}</dd></div><div><dt>업체 참고</dt><dd>{item.supplierName ?? '-'}</dd></div></> : null}
        </dl>
      )}
      {mobile ? (
        <details className="mobile-card-details material-card-details">
          <summary>입고 상세</summary>
          <dl className="material-item-meta">
            <div><dt>{item.supplyType === 'CustomerSupplied' ? '공급 책임' : '업체'}</dt><dd>{item.supplyType === 'CustomerSupplied' ? '고객 제공' : item.supplierName ?? '-'}</dd></div>
            <div><dt>{item.supplyType === 'CustomerSupplied' ? '제공 예정' : '발주'}</dt><dd>{formatQuantity(item.orderQuantity, item.orderUnit)}</dd></div>
            <div><dt>누적 도착</dt><dd>{formatQuantity(item.arrivedQuantity, item.orderUnit)}</dd></div>
            {item.supplyType === 'CustomerSupplied' ? <><div><dt>입고 확정</dt><dd>{formatQuantity(item.confirmedQuantity, item.orderUnit)}</dd></div><div><dt>업체 참고</dt><dd>{item.supplierName ?? '-'}</dd></div></> : null}
          </dl>
        </details>
      ) : null}
      {item.receipts.length === 0 ? <p className="material-no-receipts">아직 등록된 도착분이 없습니다.</p> : (
        <div className="material-receipt-stack">
          {item.receipts.map((receipt) => (
            <button type="button" key={receipt.receiptId} className="material-receipt-chip" onClick={() => onAction({ kind: 'receipt', item, receipt })}>
              <span><b>{formatQuantity(receipt.quantity, receipt.unit)}</b><small>{receipt.arrivalDate}</small></span>
              {!mobile ? <ReceiptSteps status={receipt.status} /> : <span className="mobile-step-label">{receiptStatusLabel(receipt.status)}</span>}
              <span aria-hidden="true">›</span>
            </button>
          ))}
        </div>
      )}
      <footer>
        <button type="button" className="primary-button" disabled={!canUpdate || item.arrivalsClosed} onClick={() => onAction({ kind: 'arrival', item })}>+ 도착 등록</button>
        <button type="button" disabled={!canUpdate || item.arrivalsClosed || !canCloseItem(item)} onClick={() => onAction({ kind: 'close', item })}>입고 마감</button>
      </footer>
    </article>
  );
}

function MaterialActionPanel({ action, canUpdate, message, onClose, onOpenIqc, onOpenPending, onRun, developmentUserKey }: {
  action: MaterialAction;
  canUpdate: boolean;
  message: string;
  onClose: () => void;
  onOpenIqc: () => void;
  onOpenPending: (pendingId: string) => void;
  onRun: (operation: () => Promise<unknown>, success: string) => Promise<void>;
  developmentUserKey: string;
}) {
  const [quantity, setQuantity] = useState('');
  const [unit, setUnit] = useState(action.item.orderUnit ?? 'EA');
  const [orderQuantity, setOrderQuantity] = useState(action.item.orderQuantity?.toString() ?? '');
  const [arrivalDate, setArrivalDate] = useState(new Date().toISOString().slice(0, 10));
  const [note, setNote] = useState('');
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  async function guarded(operation: () => Promise<unknown>, success: string) {
    setSaving(true);
    try { await onRun(operation, success); } finally { setSaving(false); }
  }

  if (action.kind === 'arrival') {
    async function submit(event: FormEvent) {
      event.preventDefault();
      await guarded(() => registerMaterialArrival(developmentUserKey, action.item.itemId, {
        quantity: Number(quantity),
        unit,
        orderQuantity: action.item.orderQuantity === null ? Number(orderQuantity) : null,
        orderUnit: action.item.orderUnit === null ? unit : null,
        arrivalDate,
        note: note.trim() || null
      }), '도착분을 등록했습니다.');
    }
    return (
      <form className="material-action-form" onSubmit={submit}>
        <ActionContext item={action.item} />
        {action.item.orderQuantity === null && action.item.supplyType === 'Purchased' ? <label><span>발주 수량</span><input inputMode="decimal" value={orderQuantity} onChange={(event) => setOrderQuantity(event.target.value)} required /></label> : null}
        <div className="material-form-pair">
          <label><span>도착 수량</span><input data-autofocus inputMode="decimal" value={quantity} onChange={(event) => setQuantity(event.target.value)} required /></label>
          <label><span>단위</span><input value={unit} onChange={(event) => setUnit(event.target.value)} maxLength={20} required disabled={action.item.orderUnit !== null} /></label>
        </div>
        <label><span>도착일</span><input type="date" value={arrivalDate} onChange={(event) => setArrivalDate(event.target.value)} required /></label>
        <label><span>비고</span><textarea value={note} onChange={(event) => setNote(event.target.value)} placeholder="운송 상태나 확인 메모" /></label>
        {message ? <p role="alert" className="error-text">{message}</p> : null}
        <div className="material-action-buttons"><button type="button" onClick={onClose}>취소</button><button className="primary-button" disabled={!canUpdate || saving}>도착 등록</button></div>
      </form>
    );
  }

  if (action.kind === 'close') {
    return (
      <form className="material-action-form" onSubmit={(event) => { event.preventDefault(); void guarded(() => closeMaterialArrivals(developmentUserKey, action.item.itemId, action.item.rowVersion, reason), '입고를 마감하고 완료값을 계산했습니다.'); }}>
        <ActionContext item={action.item} />
        <div className="material-action-notice"><strong>되돌릴 수 없는 마감입니다.</strong><span>{action.item.supplyType === 'CustomerSupplied' ? `미도착 잔량 0, 모든 도착분 확정이 필요합니다. 현재 잔량 ${formatQuantity(action.item.remainingQuantity, action.item.orderUnit)}` : '모든 유효 도착분이 확정된 경우 발주품목 입고가 완료됩니다.'}</span></div>
        <label><span>마감 사유</span><textarea data-autofocus value={reason} onChange={(event) => setReason(event.target.value)} required minLength={3} /></label>
        {message ? <p role="alert" className="error-text">{message}</p> : null}
        <div className="material-action-buttons"><button type="button" onClick={onClose}>취소</button><button className="primary-button" disabled={!canUpdate || saving}>입고 마감</button></div>
      </form>
    );
  }

  const receipt = action.receipt;
  const pendingId = receipt.iqcAttempts.findLast((attempt) => attempt.pendingIssueId)?.pendingIssueId;
  return (
    <div className="material-action-form">
      <ActionContext item={action.item} receipt={receipt} />
      <ReceiptSteps status={receipt.status} />
      {receipt.note ? <div className="material-action-notice"><strong>도착 메모</strong><span>{receipt.note}</span></div> : null}
      {message ? <p role="alert" className="error-text">{message}</p> : null}
      <div className="material-action-buttons material-action-buttons--stack">
        {receipt.status === 'Arrived' ? <button type="button" className="primary-button" disabled={!canUpdate || saving} onClick={() => void guarded(() => requestMaterialIqc(developmentUserKey, receipt.receiptId, receipt.version), 'IQC 검사를 요청했습니다.')}>IQC 요청</button> : null}
        {receipt.status === 'Arrived' ? <><textarea aria-label="취소 사유" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="취소 사유 3자 이상" /><button type="button" disabled={!canUpdate || saving || reason.trim().length < 3} onClick={() => void guarded(() => cancelMaterialReceipt(developmentUserKey, receipt.receiptId, receipt.version, reason), '도착 등록을 취소했습니다.')}>도착 취소</button></> : null}
        {receipt.status === 'IqcRequested' ? <button type="button" className="primary-button" onClick={onOpenIqc}>IQC 검사함 열기</button> : null}
        {receipt.status === 'FailedBlocked' && pendingId ? <button type="button" onClick={() => onOpenPending(pendingId)}>연결된 Pending 열기</button> : null}
        {receipt.status === 'FailedBlocked' ? <button type="button" className="primary-button" disabled={!canUpdate || saving} onClick={() => void guarded(() => requestMaterialReinspection(developmentUserKey, receipt.receiptId, receipt.version), '재검사를 요청했습니다.')}>Pending 조치 후 재검사 요청</button> : null}
        {receipt.status === 'Passed' ? <button type="button" className="primary-button" disabled={!canUpdate || saving} onClick={() => void guarded(() => confirmMaterialReceipt(developmentUserKey, receipt.receiptId, receipt.version), '입고를 확정했습니다.')}>입고 확정</button> : null}
        {receipt.status === 'Confirmed' ? <div className="material-success-panel"><strong>입고 확정 완료</strong><span>{formatDateTime(receipt.confirmedAtUtc)}</span></div> : null}
        {receipt.status === 'Cancelled' ? <div className="material-action-notice"><strong>취소된 도착분</strong><span>{receipt.cancellationReason}</span></div> : null}
      </div>
    </div>
  );
}

function IqcInspector({ item, reason, message, disabled, onReason, onSubmit, onClose, onOpenPending }: {
  item: MaterialIqcQueueItem;
  reason: string;
  message: string;
  disabled: boolean;
  onReason: (value: string) => void;
  onSubmit: (result: 'Passed' | 'Failed') => Promise<void>;
  onClose: () => void;
  onOpenPending: (pendingId: string) => void;
}) {
  return (
    <div className="material-action-form iqc-inspector">
      <div className="iqc-inspector-context"><span>{item.projectCode}</span><h3>{item.orderItem ?? '발주품목 미입력'}</h3>{item.supplyType === 'CustomerSupplied' ? <SupplyBadge overdue={false} /> : null}<p>{item.projectTitle}</p></div>
      <dl className="material-item-meta"><div><dt>검사 차수</dt><dd>{item.attemptNumber}차</dd></div><div><dt>도착 수량</dt><dd>{formatQuantity(item.quantity, item.unit)}</dd></div></dl>
      <div className="iqc-check-guide"><strong>기본 확인</strong><span>품명·수량·외관·식별 정보를 확인한 뒤 판정하세요.</span></div>
      {item.pendingIssueId ? <button type="button" onClick={() => onOpenPending(item.pendingIssueId!)}>연결된 Pending 보기</button> : null}
      <label><span>판정 사유</span><textarea data-autofocus value={reason} onChange={(event) => onReason(event.target.value)} placeholder="확인 결과를 3자 이상 기록" disabled={disabled} /></label>
      {message ? <p role="alert" className={message.includes('했습니다') ? 'success-text' : 'error-text'}>{message}</p> : null}
      <div className="iqc-decision-grid"><button type="button" className="iqc-fail-button" disabled={disabled} onClick={() => void onSubmit('Failed')}>부적합 · 입고 차단</button><button type="button" className="primary-button" disabled={disabled} onClick={() => void onSubmit('Passed')}>합격</button></div>
      <button type="button" onClick={onClose}>닫기</button>
    </div>
  );
}

function SummaryButton({ label, value, active, tone, onClick }: { label: string; value: number; active: boolean; tone?: string; onClick: () => void }) {
  return <button type="button" data-active={active} data-tone={tone} onClick={onClick}><span>{label}</span><strong>{value}</strong></button>;
}

function ReceiptSteps({ status }: { status: MaterialReceiptStatus }) {
  const stages: Array<{ value: MaterialReceiptStatus; label: string }> = [
    { value: 'Arrived', label: '도착' },
    { value: 'IqcRequested', label: 'IQC' },
    { value: 'Passed', label: '합격' },
    { value: 'Confirmed', label: '확정' }
  ];
  const current = status === 'FailedBlocked' ? 1 : status === 'Cancelled' ? 0 : stages.findIndex((stage) => stage.value === status);
  return <ol className="receipt-stepper" data-status={status}>{stages.map((stage, index) => <li key={stage.value} data-complete={index <= current}><i /><span>{stage.label}</span></li>)}</ol>;
}

function StatusBadge({ status }: { status: MaterialReceiptStatus }) {
  return <span className="material-status-badge" data-status={status}>{receiptStatusLabel(status)}</span>;
}

function SupplyBadge({ overdue }: { overdue: boolean }) {
  return <span className="customer-supply-badge" data-overdue={overdue}>{overdue ? '사급 · 제공 지연' : '사급 · 고객 제공'}</span>;
}

function ActionContext({ item, receipt }: { item: MaterialReceivingItem; receipt?: MaterialReceipt }) {
  return <div className="material-action-context"><span>{item.projectCode}</span><h3>{item.orderItem ?? '발주품목 미입력'}</h3>{item.supplyType === 'CustomerSupplied' ? <SupplyBadge overdue={item.customerSupplyOverdue} /> : null}<p>{item.projectTitle} · {item.supplyType === 'CustomerSupplied' ? '고객 제공' : item.supplierName ?? '업체 미입력'}</p>{receipt ? <strong>{formatQuantity(receipt.quantity, receipt.unit)} · {receipt.arrivalDate}</strong> : null}</div>;
}

function MaterialLoading() {
  return <div className="material-loading" aria-label="불러오는 중"><i /><i /><i /></div>;
}

function actionTitle(action: MaterialAction | null) {
  if (!action) return '';
  if (action.kind === 'arrival') return '도착 등록';
  if (action.kind === 'close') return '입고 마감';
  return receiptStatusLabel(action.receipt.status);
}

function receiptStatusLabel(status: MaterialReceiptStatus) {
  return ({ Arrived: '도착 등록', IqcRequested: 'IQC 대기', Passed: 'IQC 합격', FailedBlocked: '부적합 차단', Confirmed: '입고 확정', Cancelled: '취소' } as const)[status];
}

function formatQuantity(quantity: number | null | undefined, unit: string | null | undefined) {
  return quantity == null ? '-' : `${quantity.toLocaleString('ko-KR', { maximumFractionDigits: 3 })} ${unit ?? ''}`.trim();
}

function formatDateTime(value: string | null) {
  if (!value) return '-';
  return new Intl.DateTimeFormat('ko-KR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function canCloseItem(item: MaterialReceivingItem) {
  return (item.supplyType !== 'CustomerSupplied' || item.remainingQuantity === 0)
    && item.receipts.some((receipt) => receipt.status !== 'Cancelled')
    && item.receipts.every((receipt) => receipt.status === 'Confirmed' || receipt.status === 'Cancelled');
}

function errorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    const fieldMessage = error.errors ? Object.values(error.errors).flat()[0] : null;
    return fieldMessage ?? error.message;
  }
  return fallback;
}
