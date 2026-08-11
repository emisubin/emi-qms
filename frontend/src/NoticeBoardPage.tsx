import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ChangeEvent,
  type FormEvent,
  type ReactNode,
  type RefObject
} from 'react';
import {
  ApiError,
  createNotice,
  deleteNotice,
  deleteNoticeAttachment,
  downloadNoticeAttachment,
  getNotice,
  listNotices,
  updateNotice,
  uploadNoticeAttachment
} from './api';
import type { NoticeBodyFormat, NoticeDetail, NoticeListResponse } from './notices';

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

type NoticeFormValue = {
  title: string;
  body: string;
  bodyFormat: NoticeBodyFormat;
};

const allowedFileExtensions = '.pdf,.jpg,.jpeg,.png,.docx,.xlsx,.pptx';
const maximumAttachmentBytes = 10 * 1024 * 1024;
const maximumAttachments = 5;

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
  const [detailMessage, setDetailMessage] = useState<string | null>(null);
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
    setDetailMessage(null);
    onOpenList();
    await loadList(1);
  };

  return (
    <section className="page-surface notice-board-page" aria-labelledby="notice-board-title">
      <header className="notice-board-hero">
        <div>
          <p className="eyebrow">TEAM NOTICE</p>
          <h2 id="notice-board-title">공지사항</h2>
          <p>부서와 역할에 관계없이 업무에 필요한 소식과 파일을 함께 등록하고 확인합니다.</p>
        </div>
        <div className="notice-board-hero-actions">
          <button type="button" onClick={onOpenHome}>홈</button>
          <button type="button" className="primary-button" onClick={onOpenCompose} disabled={!mutationEnabled}>공지 작성</button>
        </div>
      </header>

      {!mutationEnabled ? <p className="notice-board-readonly" role="note">현재 검수 전용 모드에서는 공지를 작성·수정·삭제하거나 첨부를 변경할 수 없습니다.</p> : null}

      {compose ? (
        <NoticeComposer
          developmentUserKey={developmentUserKey}
          onCancel={onOpenList}
          onCreated={async (notice, message) => {
            setDetailState({ kind: 'ready', data: notice });
            setDetailMessage(message);
            await loadList(1);
            onOpenNotice(notice.noticeId);
          }}
        />
      ) : selectedNoticeId ? (
        <NoticeDetailPanel
          state={detailState ?? { kind: 'loading' }}
          developmentUserKey={developmentUserKey}
          mutationEnabled={mutationEnabled}
          transitionMessage={detailMessage}
          onBack={onOpenList}
          onRetry={() => void loadDetail(selectedNoticeId)}
          onRefresh={() => loadDetail(selectedNoticeId)}
          onUpdated={async (notice) => {
            setDetailState({ kind: 'ready', data: notice });
            await loadList(1);
          }}
          onDelete={handleDeleted}
        />
      ) : (
        <NoticeListPanel
          state={listState}
          page={page}
          onOpen={(noticeId) => {
            setDetailMessage(null);
            onOpenNotice(noticeId);
          }}
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
              <small>{notice.authorDepartmentName ?? '소속 없음'} · {formatNoticeDate(notice.createdAtUtc)}{notice.updatedAtUtc ? ' · 수정됨' : ''}</small>
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

function NoticeDetailPanel(props: {
  state: LoadState<NoticeDetail>;
  developmentUserKey: string | undefined;
  mutationEnabled: boolean;
  transitionMessage: string | null;
  onBack: () => void;
  onRetry: () => void;
  onRefresh: () => Promise<void>;
  onUpdated: (notice: NoticeDetail) => Promise<void>;
  onDelete: (noticeId: string) => Promise<void>;
}) {
  if (props.state.kind === 'loading') return <div className="notice-board-state" role="status">공지를 불러오는 중입니다.</div>;
  if (props.state.kind === 'error') return <div className="notice-board-state notice-board-state--error" role="alert"><p>{props.state.message}</p><button type="button" onClick={props.onRetry}>다시 시도</button><button type="button" onClick={props.onBack}>목록</button></div>;
  return <NoticeReadyDetail {...props} notice={props.state.data} />;
}

function NoticeReadyDetail({
  notice,
  developmentUserKey,
  mutationEnabled,
  transitionMessage,
  onBack,
  onRefresh,
  onUpdated,
  onDelete
}: {
  notice: NoticeDetail;
  developmentUserKey: string | undefined;
  mutationEnabled: boolean;
  transitionMessage: string | null;
  onBack: () => void;
  onRefresh: () => Promise<void>;
  onUpdated: (notice: NoticeDetail) => Promise<void>;
  onDelete: (noticeId: string) => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(transitionMessage);

  useEffect(() => {
    setFeedback(transitionMessage);
  }, [transitionMessage]);

  if (editing) {
    return (
      <NoticeEditForm
        notice={notice}
        developmentUserKey={developmentUserKey}
        attachmentEditor={(
          <NoticeAttachments
          notice={notice}
          developmentUserKey={developmentUserKey}
          mutationEnabled={mutationEnabled}
          manageEnabled
          onRefresh={onRefresh}
          />
        )}
        onCancel={() => setEditing(false)}
        onSaved={async (updated) => {
          await onUpdated(updated);
          setFeedback('공지 수정 내용을 저장했습니다.');
          setEditing(false);
        }}
      />
    );
  }

  return (
    <article className="notice-detail">
      <header>
        <button type="button" className="notice-detail-back" onClick={onBack}>← 목록</button>
        <div className="notice-detail-title"><span>공지</span><h3>{notice.title}</h3></div>
        <p>
          {notice.authorDisplayName} · {notice.authorDepartmentName ?? '소속 없음'} · 작성 {formatNoticeDate(notice.createdAtUtc)}
          {notice.updatedAtUtc ? ` · 수정 ${formatNoticeDate(notice.updatedAtUtc)}` : ''}
        </p>
      </header>
      <div className="notice-detail-body"><NoticeBody body={notice.body} bodyFormat={notice.bodyFormat} /></div>
      <NoticeAttachments
        notice={notice}
        developmentUserKey={developmentUserKey}
        mutationEnabled={mutationEnabled}
        manageEnabled={false}
        onRefresh={onRefresh}
      />
      <footer>
        {feedback ? <p role="status">{feedback}</p> : null}
        {notice.canEdit ? (
          <button type="button" disabled={!mutationEnabled || busy} onClick={() => setEditing(true)}>공지 수정</button>
        ) : null}
        {notice.canDelete ? (
          <button
            type="button"
            className="danger-button"
            disabled={!mutationEnabled || busy}
            onClick={async () => {
              if (!window.confirm('이 공지를 삭제할까요? 삭제한 공지는 복구할 수 없습니다.')) return;
              setBusy(true);
              setFeedback(null);
              try { await onDelete(notice.noticeId); }
              catch (deleteError) { setFeedback(errorMessage(deleteError, '공지를 삭제할 수 없습니다.')); }
              finally { setBusy(false); }
            }}
          >{busy ? '삭제 중…' : '내 공지 삭제'}</button>
        ) : null}
      </footer>
    </article>
  );
}

function NoticeAttachments({ notice, developmentUserKey, mutationEnabled, manageEnabled, onRefresh }: {
  notice: NoticeDetail;
  developmentUserKey: string | undefined;
  mutationEnabled: boolean;
  manageEnabled: boolean;
  onRefresh: () => Promise<void>;
}) {
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const chooseFiles = (event: ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(event.target.files ?? []);
    const validation = validateSelectedFiles(selected, notice.attachments.length);
    setMessage(validation);
    setFiles(validation ? [] : selected);
  };

  const upload = async () => {
    if (files.length === 0 || busy) return;
    setBusy(true);
    setMessage(null);
    const failures: string[] = [];
    for (const file of files) {
      try {
        await uploadNoticeAttachment(developmentUserKey, notice.noticeId, file);
      } catch {
        failures.push(file.name);
      }
    }
    await onRefresh();
    setFiles([]);
    if (inputRef.current) inputRef.current.value = '';
    setMessage(failures.length > 0
      ? `일부 파일을 추가하지 못했습니다: ${failures.join(', ')}`
      : '첨부파일을 추가했습니다.');
    setBusy(false);
  };

  const download = async (attachmentId: string, fileName: string) => {
    setMessage(null);
    try {
      const file = await downloadNoticeAttachment(developmentUserKey, notice.noticeId, attachmentId, fileName);
      const url = URL.createObjectURL(file.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = file.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage(errorMessage(error, '첨부파일을 내려받을 수 없습니다.'));
    }
  };

  return (
    <section className="notice-attachments" aria-labelledby="notice-attachments-title">
      <div className="notice-attachments-heading">
        <div><h4 id="notice-attachments-title">첨부파일</h4><p>{notice.attachments.length}/{maximumAttachments}개 · 파일당 10MB 이하</p></div>
      </div>
      {notice.attachments.length > 0 ? (
        <ul className="notice-attachment-list">
          {notice.attachments.map((attachment) => (
            <li key={attachment.attachmentId}>
              <button type="button" className="notice-attachment-download" onClick={() => void download(attachment.attachmentId, attachment.fileName)}>
                <span aria-hidden="true">↓</span>
                <span><strong>{attachment.fileName}</strong><small>{formatFileSize(attachment.byteSize)}</small></span>
              </button>
              {manageEnabled && attachment.canDelete ? (
                <button
                  type="button"
                  className="notice-attachment-remove"
                  disabled={!mutationEnabled || busy}
                  onClick={async () => {
                    if (!window.confirm(`${attachment.fileName} 파일을 공지에서 제거할까요?`)) return;
                    setBusy(true);
                    setMessage(null);
                    try {
                      await deleteNoticeAttachment(developmentUserKey, notice.noticeId, attachment.attachmentId);
                      await onRefresh();
                      setMessage('첨부파일을 제거했습니다.');
                    } catch (error) {
                      setMessage(errorMessage(error, '첨부파일을 제거할 수 없습니다.'));
                    } finally {
                      setBusy(false);
                    }
                  }}
                >제거</button>
              ) : null}
            </li>
          ))}
        </ul>
      ) : <p className="notice-attachments-empty">등록된 첨부파일이 없습니다.</p>}
      {manageEnabled && notice.canEdit && notice.attachments.length < maximumAttachments ? (
        <div className="notice-attachment-uploader">
          <label>
            <span>파일 선택</span>
            <input ref={inputRef} type="file" multiple accept={allowedFileExtensions} disabled={!mutationEnabled || busy} onChange={chooseFiles} />
          </label>
          <button type="button" disabled={!mutationEnabled || busy || files.length === 0} onClick={() => void upload()}>
            {busy ? '처리 중…' : files.length > 0 ? `선택 파일 ${files.length}개 추가` : '선택 파일 추가'}
          </button>
          <small>PDF, 이미지, Word, Excel, PowerPoint 파일을 최대 5개까지 등록할 수 있습니다.</small>
        </div>
      ) : null}
      {message ? <p className="notice-attachment-feedback" role="status">{message}</p> : null}
    </section>
  );
}

function NoticeComposer({ developmentUserKey, onCancel, onCreated }: {
  developmentUserKey: string | undefined;
  onCancel: () => void;
  onCreated: (notice: NoticeDetail, message: string | null) => Promise<void>;
}) {
  const [requestId] = useState(createRequestId);
  const [value, setValue] = useState<NoticeFormValue>({ title: '', body: '', bodyFormat: 'BoldMarkupV1' });
  const [files, setFiles] = useState<File[]>([]);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const titleRef = useRef<HTMLInputElement>(null);
  const bodyRef = useRef<HTMLTextAreaElement>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (busyRef.current) return;
    const nextErrors = validateNoticeFields(value);
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
      const created = await createNotice(developmentUserKey, {
        requestId,
        title: value.title.trim(),
        body: value.body.trim(),
        bodyFormat: value.bodyFormat
      });
      const failures: string[] = [];
      for (const file of files) {
        try {
          await uploadNoticeAttachment(developmentUserKey, created.noticeId, file);
        } catch {
          failures.push(file.name);
        }
      }
      let refreshed = created;
      if (files.length > 0) {
        try { refreshed = await getNotice(developmentUserKey, created.noticeId); } catch { /* created response remains usable */ }
      }
      const uploadMessage = failures.length > 0
        ? `공지는 등록했지만 일부 첨부를 추가하지 못했습니다: ${failures.join(', ')}`
        : files.length > 0 ? '공지와 첨부파일을 등록했습니다.' : '공지를 등록했습니다.';
      await onCreated(refreshed, uploadMessage);
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
      <NoticeFormFields idPrefix="notice-create" value={value} onChange={setValue} errors={errors} titleRef={titleRef} bodyRef={bodyRef} />
      <label className="notice-file-picker">
        <span>첨부파일 <small>{files.length}/{maximumAttachments}</small></span>
        <input
          type="file"
          multiple
          accept={allowedFileExtensions}
          disabled={busy}
          onChange={(event) => {
            const selected = Array.from(event.target.files ?? []);
            const validation = validateSelectedFiles(selected, 0);
            setMessage(validation);
            setFiles(validation ? [] : selected);
          }}
        />
        <small>선택 사항 · PDF, 이미지, Word, Excel, PowerPoint · 파일당 10MB 이하</small>
      </label>
      {files.length > 0 ? <ul className="notice-selected-files">{files.map((file) => <li key={`${file.name}-${file.size}`}>{file.name}<small>{formatFileSize(file.size)}</small></li>)}</ul> : null}
      {message ? <p className="notice-composer-error" role="alert">{message}</p> : null}
      <footer><button type="button" onClick={onCancel} disabled={busy}>취소</button><button type="submit" className="primary-button" disabled={busy}>{busy ? '등록 중…' : '공지 등록'}</button></footer>
    </form>
  );
}

function NoticeEditForm({ notice, developmentUserKey, attachmentEditor, onCancel, onSaved }: {
  notice: NoticeDetail;
  developmentUserKey: string | undefined;
  attachmentEditor: ReactNode;
  onCancel: () => void;
  onSaved: (notice: NoticeDetail) => Promise<void>;
}) {
  const [value, setValue] = useState<NoticeFormValue>({ title: notice.title, body: notice.body, bodyFormat: notice.bodyFormat });
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const titleRef = useRef<HTMLInputElement>(null);
  const bodyRef = useRef<HTMLTextAreaElement>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (busy) return;
    const nextErrors = validateNoticeFields(value);
    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      (nextErrors.title ? titleRef : bodyRef).current?.focus();
      return;
    }
    setBusy(true);
    setErrors({});
    setMessage(null);
    try {
      const updated = await updateNotice(developmentUserKey, notice.noticeId, {
        expectedVersion: notice.version,
        title: value.title.trim(),
        body: value.body.trim(),
        bodyFormat: value.bodyFormat
      });
      await onSaved(updated);
    } catch (error) {
      if (error instanceof ApiError && error.errors) setErrors(error.errors);
      setMessage(errorMessage(error, '공지를 수정할 수 없습니다. 입력 내용은 유지됩니다.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="notice-composer" onSubmit={submit} noValidate>
      <header><div><p className="eyebrow">EDIT NOTICE</p><h3>공지 수정</h3></div><span>수정 전 내용은 변경 이력으로 보존됩니다.</span></header>
      <NoticeFormFields idPrefix="notice-edit" value={value} onChange={setValue} errors={errors} titleRef={titleRef} bodyRef={bodyRef} />
      {attachmentEditor}
      {message ? <p className="notice-composer-error" role="alert">{message}</p> : null}
      <footer><button type="button" onClick={onCancel} disabled={busy}>취소</button><button type="submit" className="primary-button" disabled={busy}>{busy ? '저장 중…' : '수정 저장'}</button></footer>
    </form>
  );
}

function NoticeFormFields({ idPrefix, value, onChange, errors, titleRef, bodyRef }: {
  idPrefix: string;
  value: NoticeFormValue;
  onChange: (value: NoticeFormValue) => void;
  errors: Record<string, string[]>;
  titleRef: RefObject<HTMLInputElement | null>;
  bodyRef: RefObject<HTMLTextAreaElement | null>;
}) {
  const [formatMessage, setFormatMessage] = useState<string | null>(null);

  const toggleBold = () => {
    const textarea = bodyRef.current;
    if (!textarea) return;
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    if (start === end) {
      setFormatMessage('굵게 표시할 문장을 먼저 선택해 주세요.');
      textarea.focus();
      return;
    }

    let nextBody: string;
    let nextStart: number;
    let nextEnd: number;
    const selected = value.body.slice(start, end);
    if (start >= 2 && value.body.slice(start - 2, start) === '**' && value.body.slice(end, end + 2) === '**') {
      nextBody = `${value.body.slice(0, start - 2)}${selected}${value.body.slice(end + 2)}`;
      nextStart = start - 2;
      nextEnd = end - 2;
    } else if (selected.startsWith('**') && selected.endsWith('**') && selected.length > 4) {
      nextBody = `${value.body.slice(0, start)}${selected.slice(2, -2)}${value.body.slice(end)}`;
      nextStart = start;
      nextEnd = end - 4;
    } else {
      nextBody = `${value.body.slice(0, start)}**${selected}**${value.body.slice(end)}`;
      nextStart = start + 2;
      nextEnd = end + 2;
    }
    onChange({ ...value, body: nextBody, bodyFormat: 'BoldMarkupV1' });
    setFormatMessage('선택한 문장의 굵게 표시를 변경했습니다.');
    queueMicrotask(() => {
      textarea.focus();
      textarea.setSelectionRange(nextStart, nextEnd);
    });
  };

  return (
    <>
      <div className="notice-field">
        <label htmlFor={`${idPrefix}-title`}>제목 <small>{value.title.length}/100</small></label>
        <input id={`${idPrefix}-title`} ref={titleRef} value={value.title} maxLength={100} onChange={(event) => onChange({ ...value, title: event.target.value })} aria-invalid={Boolean(errors.title)} />
        {errors.title?.[0] ? <small className="field-error">{errors.title[0]}</small> : null}
      </div>
      <div className="notice-field">
        <label htmlFor={`${idPrefix}-body`}>내용 <small>{value.body.length}/2,000</small></label>
        <span className="notice-editor-toolbar" role="toolbar" aria-label="본문 서식">
          <button type="button" className="notice-bold-button" onClick={toggleBold} aria-label="선택한 글씨 굵게"><strong>B</strong> 굵게</button>
          <small>문장을 선택한 뒤 눌러 주세요.</small>
        </span>
        <textarea id={`${idPrefix}-body`} ref={bodyRef} value={value.body} maxLength={2000} rows={10} onChange={(event) => onChange({ ...value, body: event.target.value })} aria-invalid={Boolean(errors.body)} />
        {formatMessage ? <small className="notice-format-feedback" role="status">{formatMessage}</small> : null}
        {errors.body?.[0] ? <small className="field-error">{errors.body[0]}</small> : null}
      </div>
    </>
  );
}

function NoticeBody({ body, bodyFormat }: { body: string; bodyFormat: NoticeBodyFormat }) {
  if (bodyFormat !== 'BoldMarkupV1') return <>{body}</>;
  const nodes: ReactNode[] = [];
  const expression = /\*\*([\s\S]+?)\*\*/g;
  let cursor = 0;
  let match: RegExpExecArray | null;
  while ((match = expression.exec(body)) !== null) {
    if (match.index > cursor) nodes.push(body.slice(cursor, match.index));
    nodes.push(<strong key={`${match.index}-${expression.lastIndex}`}>{match[1]}</strong>);
    cursor = expression.lastIndex;
  }
  if (cursor < body.length) nodes.push(body.slice(cursor));
  return <>{nodes}</>;
}

function validateNoticeFields(value: NoticeFormValue) {
  const errors: Record<string, string[]> = {};
  if (!value.title.trim()) errors.title = ['제목을 입력해 주세요.'];
  else if (value.title.trim().length > 100) errors.title = ['제목은 100자 이하로 입력해 주세요.'];
  if (!value.body.trim()) errors.body = ['내용을 입력해 주세요.'];
  else if (value.body.trim().length > 2000) errors.body = ['내용은 2,000자 이하로 입력해 주세요.'];
  return errors;
}

function validateSelectedFiles(files: File[], existingCount: number) {
  if (existingCount + files.length > maximumAttachments) return `공지당 첨부파일은 최대 ${maximumAttachments}개까지 등록할 수 있습니다.`;
  const oversized = files.find((file) => file.size > maximumAttachmentBytes);
  if (oversized) return `${oversized.name}: 파일은 개별 10MB 이하여야 합니다.`;
  return null;
}

function formatNoticeDate(value: string) {
  return new Intl.DateTimeFormat('ko-KR', {
    month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Seoul'
  }).format(new Date(value));
}

function formatFileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.ceil(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
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
