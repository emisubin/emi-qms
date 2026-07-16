import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { ApiError, getMyWorkSummary, getNotificationSummary, listPendingIssues, listProjects } from './api';
import type { PendingListResponse } from './pending';
import type { MyWorkSummary, NotificationSummary, ProjectListResponse } from './projects';

type WidgetState<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; data: T }
  | { kind: 'empty'; data: T }
  | { kind: 'hidden' }
  | { kind: 'error'; message: string };

type HomePageProps = {
  developmentUserKey: string | undefined;
  requestContextKey: string;
  canReadPending: boolean;
  onOpenMyWork: () => void;
  onOpenProjects: () => void;
  onOpenProject: (projectId: string) => void;
  onOpenPending: () => void;
  onOpenNotifications: () => void;
};

export function HomePage({
  developmentUserKey,
  requestContextKey,
  canReadPending,
  onOpenMyWork,
  onOpenProjects,
  onOpenProject,
  onOpenPending,
  onOpenNotifications
}: HomePageProps) {
  const [myWorkState, setMyWorkState] = useState<WidgetState<MyWorkSummary>>({ kind: 'loading' });
  const [projectsState, setProjectsState] = useState<WidgetState<ProjectListResponse>>({ kind: 'loading' });
  const [pendingState, setPendingState] = useState<WidgetState<PendingListResponse>>(
    canReadPending ? { kind: 'loading' } : { kind: 'hidden' }
  );
  const [notificationsState, setNotificationsState] = useState<WidgetState<NotificationSummary>>({ kind: 'loading' });
  const myWorkGeneration = useRef(0);
  const projectsGeneration = useRef(0);
  const pendingGeneration = useRef(0);
  const notificationsGeneration = useRef(0);

  const loadMyWork = useCallback(async () => {
    const generation = ++myWorkGeneration.current;
    setMyWorkState({ kind: 'loading' });
    try {
      const data = await getMyWorkSummary(developmentUserKey);
      if (generation !== myWorkGeneration.current) return;
      const isEmpty = data.requestedCount + data.inProgressCount + data.blockingCount + data.assignedProjectCount === 0;
      setMyWorkState({ kind: isEmpty ? 'empty' : 'ready', data });
    } catch (error) {
      if (generation !== myWorkGeneration.current) return;
      setMyWorkState(widgetError(error, '내 업무 요약을 불러올 수 없습니다.'));
    }
  }, [developmentUserKey]);

  const loadProjects = useCallback(async () => {
    const generation = ++projectsGeneration.current;
    setProjectsState({ kind: 'loading' });
    try {
      const data = await listProjects(developmentUserKey, '', 'All', { pageSize: 5 });
      if (generation !== projectsGeneration.current) return;
      setProjectsState({ kind: data.items.length === 0 ? 'empty' : 'ready', data });
    } catch (error) {
      if (generation !== projectsGeneration.current) return;
      setProjectsState(widgetError(error, '프로젝트 병목을 불러올 수 없습니다.'));
    }
  }, [developmentUserKey]);

  const loadPending = useCallback(async () => {
    const generation = ++pendingGeneration.current;
    if (!canReadPending) {
      setPendingState({ kind: 'hidden' });
      return;
    }

    setPendingState({ kind: 'loading' });
    try {
      const data = await listPendingIssues(developmentUserKey);
      if (generation !== pendingGeneration.current) return;
      setPendingState({ kind: data.summary.openCount === 0 ? 'empty' : 'ready', data });
    } catch (error) {
      if (generation !== pendingGeneration.current) return;
      setPendingState(widgetError(error, 'Pending 요약을 불러올 수 없습니다.'));
    }
  }, [canReadPending, developmentUserKey]);

  const loadNotifications = useCallback(async () => {
    const generation = ++notificationsGeneration.current;
    setNotificationsState({ kind: 'loading' });
    try {
      const data = await getNotificationSummary(developmentUserKey);
      if (generation !== notificationsGeneration.current) return;
      setNotificationsState({ kind: data.unreadCount === 0 && data.blockingCount === 0 ? 'empty' : 'ready', data });
    } catch (error) {
      if (generation !== notificationsGeneration.current) return;
      setNotificationsState(widgetError(error, '알림 요약을 불러올 수 없습니다.'));
    }
  }, [developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    queueMicrotask(() => { if (!cancelled) void loadMyWork(); });
    return () => {
      cancelled = true;
      myWorkGeneration.current += 1;
    };
  }, [loadMyWork, requestContextKey]);

  useEffect(() => {
    let cancelled = false;
    queueMicrotask(() => { if (!cancelled) void loadProjects(); });
    return () => {
      cancelled = true;
      projectsGeneration.current += 1;
    };
  }, [loadProjects, requestContextKey]);

  useEffect(() => {
    let cancelled = false;
    queueMicrotask(() => { if (!cancelled) void loadPending(); });
    return () => {
      cancelled = true;
      pendingGeneration.current += 1;
    };
  }, [loadPending, requestContextKey]);

  useEffect(() => {
    let cancelled = false;
    queueMicrotask(() => { if (!cancelled) void loadNotifications(); });
    return () => {
      cancelled = true;
      notificationsGeneration.current += 1;
    };
  }, [loadNotifications, requestContextKey]);

  const visibleWidgetCount = [myWorkState, projectsState, pendingState, notificationsState]
    .filter((state) => state.kind !== 'hidden').length;

  return (
    <section className="page-surface home-page" aria-labelledby="home-page-title">
      <header className="home-hero">
        <div>
          <p className="eyebrow">TODAY AT A GLANCE</p>
          <h2 id="home-page-title">업무 홈</h2>
          <p>지금 확인할 업무와 프로젝트 흐름을 한눈에 보고 원본 화면으로 바로 이동하세요.</p>
        </div>
        <button type="button" className="primary-button" onClick={onOpenMyWork}>내 업무 시작하기</button>
      </header>

      {visibleWidgetCount === 0 ? (
        <section className="home-empty" role="status">
          <strong>표시할 수 있는 요약이 없습니다.</strong>
          <p>현재 허용된 메뉴에서 업무를 계속 확인할 수 있습니다.</p>
          <button type="button" onClick={onOpenProjects}>프로젝트로 이동</button>
        </section>
      ) : (
        <div className="home-widget-grid">
          <HomeWidget
            eyebrow="MY WORK"
            title="내 업무"
            state={myWorkState}
            onRetry={loadMyWork}
            onOpen={onOpenMyWork}
            openLabel="내 업무 전체 보기"
            emptyMessage="대기 중이거나 진행 중인 업무가 없습니다."
          >
            {(data) => (
              <div className="home-metric-grid">
                <HomeMetric label="시작 전" value={data.requestedCount} tone={data.requestedCount > 0 ? 'danger' : undefined} />
                <HomeMetric label="진행 중" value={data.inProgressCount} />
                <HomeMetric label="차단" value={data.blockingCount} tone={data.blockingCount > 0 ? 'danger' : undefined} />
                <HomeMetric label="담당 프로젝트" value={data.assignedProjectCount} />
              </div>
            )}
          </HomeWidget>

          <HomeWidget
            eyebrow="NEXT ATTENTION"
            title="프로젝트 병목"
            state={projectsState}
            onRetry={loadProjects}
            onOpen={onOpenProjects}
            openLabel="프로젝트 전체 보기"
            emptyMessage="진행 중인 프로젝트가 없습니다."
            wide
          >
            {(data) => (
              <div className="home-project-list">
                {data.items.map((project, index) => (
                  <button type="button" key={project.projectId} className="home-project-item" onClick={() => onOpenProject(project.projectId)}>
                    <span className="home-project-rank">{index + 1}</span>
                    <span>
                      <strong>{project.projectTitle}</strong>
                      <small>{project.projectCode} · {project.bottleneck?.label ?? '병목 정보 확인 중'}</small>
                    </span>
                    <span className="home-project-action">
                      {!canReadPending && project.bottleneck?.nextAction === 'Pending'
                        ? '프로젝트 열기'
                        : project.bottleneck?.nextActionLabel ?? '프로젝트 열기'} →
                    </span>
                  </button>
                ))}
              </div>
            )}
          </HomeWidget>

          {canReadPending ? (
            <HomeWidget
              eyebrow="ISSUE CONTROL"
              title="Pending"
              state={pendingState}
              onRetry={loadPending}
              onOpen={onOpenPending}
              openLabel="Pending 전체 보기"
              emptyMessage="열린 Pending이 없습니다."
            >
              {(data) => (
                <div className="home-metric-grid">
                  <HomeMetric label="Open" value={data.summary.openCount} tone={data.summary.openCount > 0 ? 'danger' : undefined} />
                  <HomeMetric label="긴급" value={data.summary.urgentCount} tone={data.summary.urgentCount > 0 ? 'danger' : undefined} />
                  <HomeMetric label="기한 초과" value={data.summary.overdueCount} />
                  <HomeMetric label="재검사" value={data.summary.reinspectionCount} />
                </div>
              )}
            </HomeWidget>
          ) : null}

          <HomeWidget
            eyebrow="NOTIFICATIONS"
            title="알림"
            state={notificationsState}
            onRetry={loadNotifications}
            onOpen={onOpenNotifications}
            openLabel="알림 전체 보기"
            emptyMessage="읽지 않은 알림이 없습니다."
          >
            {(data) => (
              <div className="home-metric-grid home-metric-grid--two">
                <HomeMetric label="읽지 않음" value={data.unreadCount} tone={data.unreadCount > 0 ? 'danger' : undefined} />
                <HomeMetric label="긴급·차단" value={data.blockingCount} tone={data.blockingCount > 0 ? 'danger' : undefined} />
              </div>
            )}
          </HomeWidget>
        </div>
      )}
    </section>
  );
}

function HomeWidget<T>({
  eyebrow,
  title,
  state,
  onRetry,
  onOpen,
  openLabel,
  emptyMessage,
  wide = false,
  children
}: {
  eyebrow: string;
  title: string;
  state: WidgetState<T>;
  onRetry: () => void | Promise<void>;
  onOpen: () => void;
  openLabel: string;
  emptyMessage: string;
  wide?: boolean;
  children: (data: T) => ReactNode;
}) {
  if (state.kind === 'hidden') {
    return null;
  }

  return (
    <section className={wide ? 'home-widget home-widget--wide' : 'home-widget'} aria-label={`${title} 요약`}>
      <header className="home-widget-header">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h3>{title}</h3>
        </div>
        <button type="button" className="home-widget-link" onClick={onOpen}>{openLabel}</button>
      </header>

      {state.kind === 'loading' ? <p className="home-widget-status" role="status">요약을 불러오는 중입니다.</p> : null}
      {state.kind === 'error' ? (
        <div className="home-widget-status home-widget-status--error" role="alert">
          <p>{state.message}</p>
          <button type="button" onClick={() => void onRetry()}>다시 시도</button>
        </div>
      ) : null}
      {state.kind === 'empty' ? (
        <div className="home-widget-status">
          <p>{emptyMessage}</p>
          <button type="button" onClick={onOpen}>원본 화면 열기</button>
        </div>
      ) : null}
      {state.kind === 'ready' ? children(state.data) : null}
    </section>
  );
}

function HomeMetric({ label, value, tone }: { label: string; value: number; tone?: 'danger' }) {
  return (
    <div className="home-metric" data-tone={tone}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function widgetError<T>(error: unknown, message: string): WidgetState<T> {
  return {
    kind: 'error',
    message: error instanceof ApiError && error.status === 403
      ? '현재 권한으로 이 요약을 확인할 수 없습니다. 다시 로그인하거나 관리자에게 문의해 주세요.'
      : message
  };
}
