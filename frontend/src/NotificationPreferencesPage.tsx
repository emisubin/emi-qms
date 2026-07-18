import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  getNotificationPreferences,
  resetNotificationPreferences,
  saveNotificationPreferences
} from './api';
import type { NotificationPreferenceResponse } from './notificationPreferences';
import { useActionFeedback } from './useActionFeedback';

type Props = {
  developmentUserKey: string;
  targetUserId?: string;
  onBack: () => void;
};

type PageState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: NotificationPreferenceResponse }
  | { kind: 'error'; message: string };

export function NotificationPreferencesPage({ developmentUserKey, targetUserId, onBack }: Props) {
  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [draft, setDraft] = useState<Record<string, boolean>>({});
  const [stale, setStale] = useState(false);
  const actions = useActionFeedback();
  const errorRef = useRef<HTMLDivElement>(null);
  const isAdminSupport = Boolean(targetUserId);

  const setData = useCallback((data: NotificationPreferenceResponse, preserveDraft = false) => {
    setState({ kind: 'ready', data });
    if (!preserveDraft) {
      setDraft(Object.fromEntries(data.items.map((item) => [preferenceKey(item.deliveryType, item.channel), item.enabled])));
    }
  }, []);

  const load = useCallback(async (preserveDraft = false): Promise<boolean> => {
    try {
      const data = await getNotificationPreferences(developmentUserKey, targetUserId);
      setData(data, preserveDraft);
      setStale(false);
      return true;
    } catch (error) {
      const message = error instanceof ApiError ? error.message : '알림 설정을 불러올 수 없습니다.';
      setState({ kind: 'error', message });
      queueMicrotask(() => errorRef.current?.focus());
      return false;
    }
  }, [developmentUserKey, setData, targetUserId]);

  useEffect(() => {
    let active = true;
    getNotificationPreferences(developmentUserKey, targetUserId)
      .then((data) => {
        if (active) setData(data);
      })
      .catch((error: unknown) => {
        if (!active) return;
        setState({
          kind: 'error',
          message: error instanceof ApiError ? error.message : '알림 설정을 불러올 수 없습니다.'
        });
        queueMicrotask(() => errorRef.current?.focus());
      });
    return () => {
      active = false;
    };
  }, [developmentUserKey, setData, targetUserId]);

  const save = async () => {
    if (state.kind !== 'ready') return;
    setStale(false);
    await actions.run('notification-preferences:save', async () => {
      try {
        const data = await saveNotificationPreferences(developmentUserKey, targetUserId, {
          expectedVersion: state.data.version,
          items: state.data.items
            .filter((item) => item.canChange)
            .map((item) => ({
              deliveryType: item.deliveryType,
              channel: item.channel,
              enabled: draft[preferenceKey(item.deliveryType, item.channel)] ?? item.enabled
            }))
        });
        setData(data);
      } catch (error) {
        if (error instanceof ApiError && error.status === 409) setStale(true);
        throw error;
      }
    }, {
      loadingMessage: '알림 설정을 저장하는 중입니다.',
      successMessage: '알림 설정을 저장했습니다.',
      errorFallback: '알림 설정을 저장할 수 없습니다.',
      conflicts: { scopes: ['notification-preferences:reset'] }
    });
  };

  const reset = async () => {
    if (state.kind !== 'ready') return;
    setStale(false);
    await actions.run('notification-preferences:reset', async () => {
      try {
        const data = await resetNotificationPreferences(
          developmentUserKey,
          targetUserId,
          state.data.version
        );
        setData(data);
      } catch (error) {
        if (error instanceof ApiError && error.status === 409) setStale(true);
        throw error;
      }
    }, {
      loadingMessage: '기본 알림 설정으로 복원하는 중입니다.',
      successMessage: state.data.isDefault ? '이미 기본 알림 설정입니다.' : '기본 알림 설정으로 복원했습니다.',
      errorFallback: '기본 알림 설정으로 복원할 수 없습니다.',
      conflicts: { scopes: ['notification-preferences:save'] }
    });
  };

  const saveFeedback = actions.feedbackFor('notification-preferences:save');
  const resetFeedback = actions.feedbackFor('notification-preferences:reset');
  const busy = actions.hasBusyPrefix('notification-preferences:');

  return (
    <section className="page-surface notification-preferences-page">
      <div className="page-header notification-preferences-header">
        <div>
          <p className="eyebrow">{isAdminSupport ? 'ADMIN SUPPORT' : 'MY NOTIFICATIONS'}</p>
          <h2>{isAdminSupport ? '사용자 알림 설정 지원' : '내 알림 설정'}</h2>
          {state.kind === 'ready' ? <p>{state.data.userDisplayName} · 설정 버전 {state.data.version}</p> : null}
        </div>
        <button type="button" className="secondary-button" onClick={onBack}>돌아가기</button>
      </div>

      <div className="notification-preference-notice">
        <span aria-hidden="true">i</span>
        <div>
          <strong>놓치면 안 되는 알림은 그대로 유지됩니다.</strong>
          <p>인앱 알림은 항상 저장되고 통합 채널 공지는 조직 공지로 유지됩니다.</p>
          <p>관리자가 직접 보낸 알림·업무 배정·테스트 발송은 이 설정과 무관하게 발송됩니다.</p>
        </div>
      </div>

      {state.kind === 'loading' ? <div className="notification-preference-state">알림 설정을 불러오는 중입니다.</div> : null}
      {state.kind === 'error' ? (
        <div ref={errorRef} tabIndex={-1} className="notification-preference-state notification-preference-state--error" role="alert">
          <strong>{state.message}</strong>
          <button type="button" onClick={() => { setState({ kind: 'loading' }); void load(); }}>다시 시도</button>
        </div>
      ) : null}

      {state.kind === 'ready' ? (
        <>
          <div className="notification-preference-summary" data-default={state.data.isDefault}>
            <span>{state.data.isDefault ? '모두 기본값' : '내가 변경한 설정 있음'}</span>
            <small>{state.data.isDefault ? '모든 선택 알림을 받고 있습니다.' : '끄기로 선택한 외부 알림이 있습니다.'}</small>
          </div>

          {stale ? (
            <div className="notification-preference-conflict" role="alert">
              <div>
                <strong>다른 곳에서 설정이 변경되었습니다.</strong>
                <span>현재 선택은 유지했습니다. 서버 값을 다시 불러온 뒤 비교해 주세요.</span>
              </div>
              <button type="button" onClick={() => void load(true)}>다시 불러오기</button>
            </div>
          ) : null}

          <div className="notification-preference-grid">
            {state.data.items.map((item, index) => {
              const key = preferenceKey(item.deliveryType, item.channel);
              const checked = draft[key] ?? item.enabled;
              const descriptionId = `notification-preference-description-${index}`;
              return (
                <article
                  key={key}
                  className={`notification-preference-card${item.canChange ? '' : ' notification-preference-card--locked'}`}
                >
                  <header>
                    <span className="notification-preference-shape" aria-hidden="true">{index + 1}</span>
                    <div>
                      <strong>{item.eventLabel}</strong>
                      <span>{item.channelLabel}</span>
                    </div>
                    {item.canChange ? (
                      <label className="notification-toggle">
                        <input
                          type="checkbox"
                          role="switch"
                          checked={checked}
                          aria-label={`${item.eventLabel} ${checked ? '받기' : '끄기'}`}
                          aria-describedby={descriptionId}
                          disabled={busy}
                          onChange={(event) => setDraft((current) => ({ ...current, [key]: event.target.checked }))}
                        />
                        <span aria-hidden="true" />
                        <b>{checked ? '받기' : '끄기'}</b>
                      </label>
                    ) : <span className="notification-lock-badge">필수</span>}
                  </header>
                  <p id={descriptionId}>{item.description}</p>
                  <footer>
                    <span>{item.canChange ? (item.isOverridden ? '내가 변경함' : '기본값') : '항상 받기'}</span>
                    {item.lockReason ? <small>🔒 {item.lockReason}</small> : null}
                  </footer>
                </article>
              );
            })}
          </div>

          <div className="notification-preference-actions">
            <div>
              <button type="button" disabled={busy} onClick={() => void save()}>
                {actions.isBusy('notification-preferences:save') ? '저장 중' : '설정 저장'}
              </button>
              {saveFeedback ? <ActionMessage tone={saveFeedback.tone} message={saveFeedback.message} /> : null}
            </div>
            <div>
              <button type="button" className="secondary-button" disabled={busy} onClick={() => void reset()}>
                {actions.isBusy('notification-preferences:reset') ? '복원 중' : '기본값 복원'}
              </button>
              {resetFeedback ? <ActionMessage tone={resetFeedback.tone} message={resetFeedback.message} /> : null}
            </div>
          </div>
        </>
      ) : null}
    </section>
  );
}

function ActionMessage({ tone, message }: { tone: string; message: string }) {
  return <p className="notification-preference-feedback" data-tone={tone} aria-live="polite">{message}</p>;
}

function preferenceKey(deliveryType: string, channel: string) {
  return `${deliveryType}:${channel}`;
}
