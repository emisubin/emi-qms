import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { ApiError, createNotice, deleteNotice, getNotice, listNotices } from './api';
import type { NoticeDetail, NoticeListResponse } from './notices';

type LoadState<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; data: T }
  | { kind: 'error'; message: string };

type NoticeBoardPageProps = {
  developmentUserKey: string | undefined;
  selectedNoticeId?: string;
  compose?: boolean;
  mutationEnabled: boolean;
  onOpenHome: () => void;
  onOpenList: () => void;
  onOpenNotice: (noticeId: string) => void;
  onOpenCompose: () => void;
};

export function NoticeBoardPage({
  developmentUserKey,
  selectedNoticeId,
  compose = false,
  mutationEnabled,
  onOpenHome,
  onOpenList,
  onOpenNotice,
  onOpenCompose
}: NoticeBoardPageProps) {
  const [page, setPage] = useState(1);
  const [listState, setListState] = useState<LoadState<NoticeListResponse>>({ kind: 'loading' });
  const [detailState, setDetailState] = useState<LoadState<NoticeDetail> | null>(null);
  const listGeneration = useRef(0);
  const detailGeneration = useRef(0);

  const loadList = useCallback(async (targetPage: number) => {
    const generation = ++listGeneration.current;
    setListState({ kind: 'loading' });
    try {
      const data = await listNotices(developmentUserKey, targetPage, 20);
      if (generation !== listGeneration.current) return;
      setPage(targetPage);
      setListState({ kind: 'ready', data });
    } catch (error) {
      if (generation !== listGeneration.current) return;
      setListState({ kind: 'error', message: errorMessage(error, '공지 목록을 불러올 수 없습니다.') });
    }
  }, [developmentUserKey]);

  const loadDetail = useCallback(async (noticeId: string) => {
    const generation = ++detailGeneration.current;
    setDetailState({ kind: 'loading' });
    try {
      const data = await getNotice(developmentUserKey, noticeId);
      if (generation === detailGeneration.current) setDetailState({ kind: 'ready', data });
    } catch (error) {
      if (generation === detailGeneration.current) {
        setDetailState({ kind: 'error', message: errorMessage(error, '공지를 불러올 수 없습니다.') });
      }
    }
  }, [developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    queueMicrotask(() => { if (!cancelled) void loadList(1); });
    return () => {
      cancelled = true;
      listGeneration.current += 1;
    };
  }, [loadList]);

  useEffect(() => {
    let cancelled = false;
    if (!selectedNoticeId) {
      queueMicrotask(() => { if (!cancelled) setDetailState(null); });
    } else {
      queueMicrotask(() => { if (!cancelled) void loadDetail(selectedNoticeId); });
    }
    return () => {
      cancelled = true;
      detailGeneration.current += 1;
    };
  }, [loadDetail, selectedNoticeId]);

  const handleDeleted = async (noticeId: string) => {
    await deleteNotice(developmentUserKey, noticeId);
    onOpenList();
    await loadList(1);
  };

  return (
    <section className="page-surface notice-board-page" aria-labelledby="notice-board-title">
      <header className="notice-board-hero">
        <div>
          <p className="eyebrow">TEAM NOTICE</p>
          <h2 id="notice-board-title">공지사항</h2>
          <p>부서와 역할에 관계없이 업무에 필요한 소식을 함께 등록하고 확인합니다.</p>
        </div>
        <div className="notice-board-hero-actions">
          <button type="button" onClick={onOpenHome}>홈</button>
          <button type="button" className="primary-button" onClick={onOpenCompose} disabled={!mutationEnabled}>공지 작성</button>
        </div>
      </header>

      {!mutationEnabled ? <p className="notice-board-readonly" role="note">현재 검수 전용 모드에서는 공지를 작성하거나 삭제할 수 없습니다.</p> : null}

      {compose ? (
        <NoticeComposer
          developmentUserKey={developmentUserKey}
          onCancel={onOpenList}
          onCreated={async (notice) => {
            await loadList(1);
            onOpenNotice(notice.noticeId);
          }}
        />
      ) : selectedNoticeId ? (
        <NoticeDetailPanel
          state={detailState ?? { kind: 'loading' }}
          mutationEnabled={mutationEnabled}
          onBack={onOpenList}
          onRetry={() => void loadDetail(selectedNoticeId)}
          onDelete={handleDeleted}
        />
      ) : (
        <NoticeListPanel
          state={listState}
          page={page}
          onOpen={onOpenNotice}
          onCreate={onOpenCompose}
          onPage={(nextPage) => void loadList(nextPage)}
          onRetry={() => void loadList(page)}
        />
      )}
    </section>
  );
}

function NoticeListPanel({
  state,
  page,
  onOpen,
  onCreate,
  onPage,
  onRetry
}: {
  state: LoadState<NoticeListResponse>;
  page: number;
  onOpen: (noticeId: string) => void;
  onCreate: () => void;
  onPage: (page: number) => void;
  onRetry: () => void;
}) {
  if (state.kind === 'loading') return <div className="notice-board-state" role="status">공지 목록을 불러오는 중입니다.</div>;
  if (state.kind === 'error') {
    return <div className="notice-board-state notice-board-state--error" role="alert"><p>{state.message}</p><button type="button" onClick={onRetry}>다시 시도</button></div>;
  }
  if (state.data.items.length === 0) {
    return <div className="notice-board-empty"><span aria-hidden="true">✦</span><h3>아직 등록된 공지가 없습니다.</h3><p>첫 공지를 등록해 팀에 필요한 소식을 공유해 보세요.</p><button type="button" className="primary-button" onClick={onCreate}>첫 공지 작성</button></div>;
  }

  const totalPages = Math.max(1, Math.ceil(state.data.totalCount / state.data.pageSize));
  return (
    <div className="notice-board-list-shell">
      <div className="notice-board-list-heading"><strong>전체 {state.data.totalCount}건</strong><span>최신 공지부터 표시합니다.</span></div>
      <div className="notice-board-list" aria-label="공지 목록">
        {state.data.items.map((notice) => (
          <button type="button" key={notice.noticeId} className="notice-board-list-item" onClick={() => onOpen(notice.noticeId)}>
            <span className="notice-board-list-mark" aria-hidden="true">N</span>
            <span className="notice-board-list-copy">
              <strong>{notice.title}</strong>
              <small>{notice.preview}</small>
            </span>
            <span className="notice-board-list-meta">
              <b>{notice.authorDisplayName}</b>
              <small>{notice.authorDepartmentName ?? '소속 없음'} · {formatNoticeDate(notice.createdAtUtc)}</small>
            </span>
            <span className="notice-board-list-arrow" aria-hidden="true">→</span>
          </button>
        ))}
      </div>
      {totalPages > 1 ? (
        <nav className="notice-board-pagination" aria-label="공지 페이지">
          <button type="button" onClick={() => onPage(page - 1)} disabled={page <= 1}>이전</button>
          <span>{page} / {totalPages}</span>
          <button type="button" onClick={() => onPage(page + 1)} disabled={page >= totalPages}>다음</button>
        </nav>
      ) : null}
    </div>
  );
}

function NoticeDetailPanel({ state, mutationEnabled, onBack, onRetry, onDelete }: {
  state: LoadState<NoticeDetail>;
  mutationEnabled: boolean;
  onBack: () => void;
  onRetry: () => void;
  onDelete: (noticeId: string) => Promise<void>;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  if (state.kind === 'loading') return <div className="notice-board-state" role="status">공지를 불러오는 중입니다.</div>;
  if (state.kind === 'error') return <div className="notice-board-state notice-board-state--error" role="alert"><p>{state.message}</p><button type="button" onClick={onRetry}>다시 시도</button><button type="button" onClick={onBack}>목록</button></div>;

  const notice = state.data;
  return (
    <article className="notice-detail">
      <header>
        <button type="button" className="notice-detail-back" onClick={onBack}>← 목록</button>
        <div className="notice-detail-title"><span>공지</span><h3>{notice.title}</h3></div>
        <p>{notice.authorDisplayName} · {notice.authorDepartmentName ?? '소속 없음'} · {formatNoticeDate(notice.createdAtUtc)}</p>
      </header>
      <div className="notice-detail-body">{notice.body}</div>
      <footer>
        {error ? <p role="alert">{error}</p> : null}
        {notice.canDelete ? (
          <button
            type="button"
            className="danger-button"
            disabled={!mutationEnabled || busy}
            onClick={async () => {
              if (!window.confirm('이 공지를 삭제할까요? 삭제한 공지는 복구할 수 없습니다.')) return;
              setBusy(true);
              setError(null);
              try { await onDelete(notice.noticeId); }
              catch (deleteError) { setError(errorMessage(deleteError, '공지를 삭제할 수 없습니다.')); }
              finally { setBusy(false); }
            }}
          >{busy ? '삭제 중…' : '내 공지 삭제'}</button>
        ) : null}
      </footer>
    </article>
  );
}

function NoticeComposer({ developmentUserKey, onCancel, onCreated }: {
  developmentUserKey: string | undefined;
  onCancel: () => void;
  onCreated: (notice: NoticeDetail) => Promise<void>;
}) {
  const [requestId] = useState(createRequestId);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const titleRef = useRef<HTMLInputElement>(null);
  const bodyRef = useRef<HTMLTextAreaElement>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (busyRef.current) return;
    const nextErrors: Record<string, string[]> = {};
    if (!title.trim()) nextErrors.title = ['제목을 입력해 주세요.'];
    else if (title.trim().length > 100) nextErrors.title = ['제목은 100자 이하로 입력해 주세요.'];
    if (!body.trim()) nextErrors.body = ['내용을 입력해 주세요.'];
    else if (body.trim().length > 2000) nextErrors.body = ['내용은 2,000자 이하로 입력해 주세요.'];
    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      (nextErrors.title ? titleRef : bodyRef).current?.focus();
      return;
    }

    busyRef.current = true;
    setBusy(true);
    setErrors({});
    setMessage(null);
    try {
      const notice = await createNotice(developmentUserKey, { requestId, title: title.trim(), body: body.trim() });
      await onCreated(notice);
    } catch (error) {
      if (error instanceof ApiError && error.errors) setErrors(error.errors);
      setMessage(errorMessage(error, '공지를 등록할 수 없습니다. 입력 내용은 유지됩니다.'));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  return (
    <form className="notice-composer" onSubmit={submit} noValidate>
      <header><div><p className="eyebrow">NEW NOTICE</p><h3>공지 작성</h3></div><span>작성자는 로그인 정보로 자동 기록됩니다.</span></header>
      <label>
        <span>제목 <small>{title.length}/100</small></span>
        <input ref={titleRef} value={title} maxLength={100} onChange={(event) => setTitle(event.target.value)} aria-invalid={Boolean(errors.title)} />
        {errors.title?.[0] ? <small className="field-error">{errors.title[0]}</small> : null}
      </label>
      <label>
        <span>내용 <small>{body.length}/2,000</small></span>
        <textarea ref={bodyRef} value={body} maxLength={2000} rows={10} onChange={(event) => setBody(event.target.value)} aria-invalid={Boolean(errors.body)} />
        {errors.body?.[0] ? <small className="field-error">{errors.body[0]}</small> : null}
      </label>
      {message ? <p className="notice-composer-error" role="alert">{message}</p> : null}
      <footer><button type="button" onClick={onCancel} disabled={busy}>취소</button><button type="submit" className="primary-button" disabled={busy}>{busy ? '등록 중…' : '공지 등록'}</button></footer>
    </form>
  );
}

function formatNoticeDate(value: string) {
  return new Intl.DateTimeFormat('ko-KR', {
    month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Seoul'
  }).format(new Date(value));
}

function createRequestId() {
  if (typeof globalThis.crypto?.randomUUID === 'function') return globalThis.crypto.randomUUID();
  const bytes = new Uint8Array(16);
  globalThis.crypto?.getRandomValues?.(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const value = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
  return `${value.slice(0, 8)}-${value.slice(8, 12)}-${value.slice(12, 16)}-${value.slice(16, 20)}-${value.slice(20)}`;
}

function errorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError && error.message ? error.message : fallback;
}
