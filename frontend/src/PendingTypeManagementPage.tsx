import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import {
  ApiError,
  createPendingType,
  getPendingTypeCatalog,
  reorderPendingTypes,
  setPendingTypeActive,
  updatePendingType
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import type { PendingTypeCatalog, PendingTypeCatalogItem } from './pendingTypes';

type PageState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: PendingTypeCatalog }
  | { kind: 'error'; message: string };

type EditDraft = {
  code: string;
  displayName: string;
  description: string;
  isManualEnabled: boolean;
};

export function PendingTypeManagementPage({ developmentUserKey, canManage }: { developmentUserKey: string | undefined; canManage: boolean }) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [orderDraft, setOrderDraft] = useState<PendingTypeCatalogItem[]>([]);
  const [editDraft, setEditDraft] = useState<EditDraft | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<{ tone: 'success' | 'error'; message: string } | null>(null);

  const load = useCallback(async () => {
    if (!canManage) return;
    setState({ kind: 'loading' });
    try {
      const data = await getPendingTypeCatalog(developmentUserKey);
      setState({ kind: 'ready', data });
      setOrderDraft(data.items);
    } catch (error) {
      setState({ kind: 'error', message: errorMessage(error) });
    }
  }, [canManage, developmentUserKey]);

  useEffect(() => { void load(); }, [load]);

  const orderChanged = useMemo(() => state.kind === 'ready'
    && orderDraft.some((item, index) => state.data.items[index]?.code !== item.code), [orderDraft, state]);

  function accept(data: PendingTypeCatalog, message: string) {
    setState({ kind: 'ready', data });
    setOrderDraft(data.items);
    setEditDraft(null);
    setFeedback({ tone: 'success', message });
  }

  async function run(operation: () => Promise<PendingTypeCatalog>, message: string) {
    setBusy(true);
    setFeedback(null);
    try {
      accept(await operation(), message);
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error) });
    } finally {
      setBusy(false);
    }
  }

  function move(code: string, offset: -1 | 1) {
    setOrderDraft((current) => {
      const index = current.findIndex((item) => item.code === code);
      const target = index + offset;
      if (index < 0 || target < 0 || target >= current.length) return current;
      const next = [...current];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  if (!canManage) {
    return <section className="page-surface pending-type-page"><div className="pending-type-state" role="alert"><strong>Pending 유형 관리 권한이 없습니다.</strong><p>이 화면은 시스템 관리자만 사용할 수 있습니다.</p></div></section>;
  }

  const items = state.kind === 'ready' ? orderDraft : [];
  const activeCount = items.filter((item) => item.isActive).length;
  const customCount = items.filter((item) => !item.isSystem).length;
  const usageCount = items.reduce((sum, item) => sum + item.usageCount, 0);

  return (
    <section className={`page-surface pending-type-page${isMobile ? ' mobile-first-page pending-type-page--mobile' : ''}`} aria-labelledby="pending-type-title">
      <header className={isMobile ? 'page-header mobile-page-header pending-type-header' : 'page-header pending-type-header'}>
        <div>
          <p className="eyebrow">MASTER DATA · PENDING</p>
          <h2 id="pending-type-title">Pending 유형 관리</h2>
          <p className="muted-text">업무 코드는 유지하고 화면 표시명·수동 등록 노출·정렬 순서를 관리합니다.</p>
        </div>
        {!isMobile && state.kind === 'ready' ? <button className="primary-button" type="button" disabled={busy} onClick={() => setShowCreate(true)}>+ 사용자 유형 추가</button> : null}
      </header>

      {state.kind === 'ready' ? (
        <div className="pending-type-kpi-grid" aria-label="Pending 유형 요약">
          <article><span>전체 유형</span><strong>{items.length}</strong><small>시스템 4개 포함</small></article>
          <article><span>사용 중</span><strong>{activeCount}</strong><small>수동·자동 업무 기준</small></article>
          <article><span>사용자 유형</span><strong>{customCount}</strong><small>관리자 추가 유형</small></article>
          <article><span>연결 Pending</span><strong>{usageCount}</strong><small>삭제 없이 이력 보존</small></article>
        </div>
      ) : null}

      {feedback ? <p className="action-feedback" data-tone={feedback.tone} role={feedback.tone === 'error' ? 'alert' : 'status'}>{feedback.message}</p> : null}
      {state.kind === 'loading' ? <div className="pending-type-state" role="status">Pending 유형을 불러오는 중입니다…</div> : null}
      {state.kind === 'error' ? <div className="pending-type-state" role="alert"><strong>유형 목록을 불러오지 못했습니다.</strong><p>{state.message}</p><button type="button" onClick={() => void load()}>다시 시도</button></div> : null}

      {state.kind === 'ready' && isMobile ? (
        <>
          <div className="pending-type-mobile-note"><strong>모바일은 조회 전용입니다.</strong><span>표시명·노출·순서 변경은 넓은 화면의 관리자 화면에서 진행하세요.</span></div>
          <div className="pending-type-mobile-list">
            {items.map((item, index) => (
              <article key={item.code} className="pending-type-mobile-card" data-active={item.isActive}>
                <div className="pending-type-mobile-rank">{String(index + 1).padStart(2, '0')}</div>
                <div><div className="pending-type-badges"><span data-tone={item.isSystem ? 'system' : 'custom'}>{item.isSystem ? '시스템' : '사용자'}</span><span data-tone={item.isActive ? 'active' : 'inactive'}>{item.isActive ? '사용 중' : '사용 중지'}</span></div><h3>{item.displayName}</h3><p>{item.description ?? '설명 없음'}</p><small>{item.isManualEnabled ? '수동 등록에 표시' : '자동 생성 전용'} · 연결 {item.usageCount}건</small></div>
              </article>
            ))}
          </div>
        </>
      ) : null}

      {state.kind === 'ready' && !isMobile ? (
        <div className="pending-type-table-shell">
          <div className="pending-type-toolbar">
            <div><strong>유형 목록</strong><span>{items.length}개 · 행의 화살표로 우선순위를 조정하세요.</span></div>
            <div><button type="button" disabled={!orderChanged || busy} onClick={() => { setOrderDraft(state.data.items); setFeedback(null); }}>순서 취소</button><button className="primary-button" type="button" disabled={!orderChanged || busy} onClick={() => void run(() => reorderPendingTypes(developmentUserKey, orderDraft.map((item, index) => ({ code: item.code, expectedRowVersion: item.rowVersion, newSortOrder: index + 1 }))), 'Pending 유형 순서가 저장되었습니다.')}>순서 저장</button></div>
          </div>
          <div className="pending-type-table-scroll">
            <table className="pending-type-table">
              <thead><tr><th>순서</th><th>구분</th><th>표시명 / 설명</th><th>수동 등록</th><th>상태</th><th>사용 건수</th><th>관리</th></tr></thead>
              <tbody>
                {items.map((item, index) => {
                  const editing = editDraft?.code === item.code;
                  return (
                    <tr key={item.code} data-active={item.isActive}>
                      <td><div className="pending-type-order"><strong>{String(index + 1).padStart(2, '0')}</strong><span><button aria-label={`${item.displayName} 위로`} disabled={busy || index === 0} type="button" onClick={() => move(item.code, -1)}>↑</button><button aria-label={`${item.displayName} 아래로`} disabled={busy || index === items.length - 1} type="button" onClick={() => move(item.code, 1)}>↓</button></span></div></td>
                      <td><span className="pending-type-kind" data-tone={item.isSystem ? 'system' : 'custom'}>{item.isSystem ? '시스템' : '사용자'}</span></td>
                      <td>{editing ? <div className="pending-type-edit-fields"><input aria-label="표시명" maxLength={80} value={editDraft.displayName} onChange={(event) => setEditDraft({ ...editDraft, displayName: event.target.value })} /><input aria-label="설명" maxLength={300} value={editDraft.description} onChange={(event) => setEditDraft({ ...editDraft, description: event.target.value })} /></div> : <div className="pending-type-name"><strong>{item.displayName}</strong><span>{item.description ?? '설명 없음'}</span><code>{item.code}</code></div>}</td>
                      <td>{editing ? <label className="pending-type-switch"><input type="checkbox" checked={editDraft.isManualEnabled} disabled={item.code === 'Other'} onChange={(event) => setEditDraft({ ...editDraft, isManualEnabled: event.target.checked })} /><span>{editDraft.isManualEnabled ? '표시' : '숨김'}</span></label> : <span>{item.isManualEnabled ? '표시' : '숨김'}</span>}</td>
                      <td><span className="pending-type-status" data-tone={item.isActive ? 'active' : 'inactive'}>{item.isActive ? '사용 중' : '사용 중지'}</span></td>
                      <td><strong>{item.usageCount}</strong>건</td>
                      <td>{editing ? <div className="pending-type-row-actions"><button type="button" disabled={busy} onClick={() => setEditDraft(null)}>취소</button><button className="primary-button" type="button" disabled={busy || editDraft.displayName.trim().length < 2} onClick={() => void run(() => updatePendingType(developmentUserKey, item.code, { expectedRowVersion: item.rowVersion, displayName: editDraft.displayName, description: editDraft.description.trim() || null, isManualEnabled: editDraft.isManualEnabled }), 'Pending 유형 정보가 저장되었습니다.')}>저장</button></div> : <div className="pending-type-row-actions"><button type="button" disabled={busy} onClick={() => setEditDraft({ code: item.code, displayName: item.displayName, description: item.description ?? '', isManualEnabled: item.isManualEnabled })}>편집</button>{!item.isSystem ? <button type="button" disabled={busy} onClick={() => void run(() => setPendingTypeActive(developmentUserKey, item.code, item.rowVersion, !item.isActive), item.isActive ? '사용자 유형을 사용 중지했습니다.' : '사용자 유형을 다시 활성화했습니다.')}>{item.isActive ? '사용 중지' : '활성화'}</button> : null}</div>}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {showCreate && !isMobile ? <CreatePendingTypeDialog busy={busy} onClose={() => setShowCreate(false)} onCreate={(displayName, description) => void run(async () => { const data = await createPendingType(developmentUserKey, { displayName, description }); setShowCreate(false); return data; }, '사용자 Pending 유형이 추가되었습니다.')} /> : null}
    </section>
  );
}

function CreatePendingTypeDialog({ busy, onClose, onCreate }: { busy: boolean; onClose: () => void; onCreate: (displayName: string, description: string | null) => void }) {
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  function submit(event: FormEvent) {
    event.preventDefault();
    if (displayName.trim().length >= 2) onCreate(displayName.trim(), description.trim() || null);
  }
  return <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}><section className="dialog pending-type-dialog" role="dialog" aria-modal="true" aria-labelledby="pending-type-create-title"><header className="page-header"><div><p className="eyebrow">NEW PENDING TYPE</p><h2 id="pending-type-create-title">사용자 유형 추가</h2><p className="muted-text">내부 코드는 서버가 자동으로 만들며 이후 변경되지 않습니다.</p></div><button type="button" onClick={onClose}>닫기</button></header><form onSubmit={submit}><label className="form-field"><span>표시명 *</span><input autoFocus maxLength={80} value={displayName} onChange={(event) => setDisplayName(event.target.value)} placeholder="예: 설계 변경" /></label><label className="form-field"><span>설명</span><textarea maxLength={300} value={description} onChange={(event) => setDescription(event.target.value)} placeholder="어떤 상황에 사용하는 유형인지 입력" /></label><div className="dialog-actions"><button type="button" onClick={onClose}>취소</button><button className="primary-button" type="submit" disabled={busy || displayName.trim().length < 2}>{busy ? '추가 중…' : '유형 추가'}</button></div></form></section></div>;
}

function errorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 403) return '시스템 관리자 권한이 필요합니다.';
    if (error.status === 409) return '다른 관리자가 먼저 변경했습니다. 목록을 새로고침한 뒤 다시 시도해 주세요.';
    return error.message;
  }
  return '알 수 없는 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.';
}
