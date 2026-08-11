import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  deactivateAllWebPushSubscriptions,
  deactivateCurrentWebPushSubscription,
  getCurrentWebPushStatus,
  getWebPushConfiguration,
  saveCurrentWebPushSubscription
} from './api';
import {
  getCurrentBrowserSubscription,
  getOrCreateBrowserSubscription,
  getWebPushRegistration,
  isInstalledPwa,
  supportsWebPush,
  toWebPushRequest,
  webPushGuideDismissedStorageKey,
  type WebPushConfiguration
} from './webPush';

type State =
  | { kind: 'loading' }
  | { kind: 'ready'; configuration: WebPushConfiguration; currentActive: boolean; localAccessError: boolean }
  | { kind: 'error'; message: string };

export function WebPushSettings({ developmentUserKey }: { developmentUserKey: string }) {
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<{ tone: 'success' | 'error'; message: string } | null>(null);
  const browserSupported = supportsWebPush();
  const canManageCurrentDevice = state.kind === 'ready'
    && browserSupported
    && !state.localAccessError
    && isInstalledPwa()
    && (state.configuration.configured || state.currentActive);
  const canResetAllDevices = state.kind === 'ready' && state.configuration.activeDeviceCount > 0;

  const load = useCallback(async () => {
    try {
      const configuration = await getWebPushConfiguration(developmentUserKey);
      if (!supportsWebPush()) {
        setState({ kind: 'ready', configuration, currentActive: false, localAccessError: false });
        return;
      }

      try {
        const subscription = await getCurrentBrowserSubscription();
        const current = subscription
          ? await getCurrentWebPushStatus(developmentUserKey, subscription.endpoint)
          : { active: false };
        setState({ kind: 'ready', configuration, currentActive: current.active, localAccessError: false });
      } catch {
        setState({ kind: 'ready', configuration, currentActive: false, localAccessError: true });
      }
    } catch (error) {
      setState({ kind: 'error', message: errorMessage(error, '푸시 알림 설정을 불러올 수 없습니다.') });
    }
  }, [developmentUserKey]);

  useEffect(() => { void load(); }, [load]);

  const enable = async () => {
    if (state.kind !== 'ready' || !state.configuration.publicKey) return;
    setBusy(true);
    setFeedback(null);
    try {
      const permission = Notification.permission === 'granted'
        ? 'granted'
        : await Notification.requestPermission();
      if (permission !== 'granted') {
        throw new Error('브라우저 알림 권한이 허용되지 않았습니다. 기기 설정에서 EMI PMS 알림을 허용해 주세요.');
      }
      const registration = await getWebPushRegistration();
      if (!registration) throw new Error('이 브라우저는 PWA 푸시 알림을 지원하지 않습니다.');
      const subscription = await getOrCreateBrowserSubscription(registration, state.configuration.publicKey);
      const result = await saveCurrentWebPushSubscription(developmentUserKey, toWebPushRequest(subscription));
      setState({ kind: 'ready', configuration: { ...state.configuration, activeDeviceCount: result.activeDeviceCount }, currentActive: true, localAccessError: false });
      setFeedback({ tone: 'success', message: '이 기기의 푸시 알림을 켰습니다.' });
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, '이 기기의 푸시 알림을 켤 수 없습니다.') });
    } finally {
      setBusy(false);
    }
  };

  const disableCurrent = async () => {
    if (state.kind !== 'ready') return;
    setBusy(true);
    setFeedback(null);
    try {
      const subscription = await getCurrentBrowserSubscription();
      let activeDeviceCount = state.configuration.activeDeviceCount;
      if (subscription) {
        const result = await deactivateCurrentWebPushSubscription(developmentUserKey, subscription.endpoint);
        activeDeviceCount = result.activeDeviceCount;
        setState({ kind: 'ready', configuration: { ...state.configuration, activeDeviceCount }, currentActive: false, localAccessError: false });
        try {
          await subscription.unsubscribe();
          setFeedback({ tone: 'success', message: '이 기기의 푸시 알림을 껐습니다.' });
        } catch {
          setFeedback({ tone: 'success', message: '서버에서 이 기기의 푸시 연결을 해제했습니다. 브라우저의 로컬 알림 정보는 정리하지 못했지만 다시 알림이 발송되지는 않습니다.' });
        }
      } else {
        setState({ kind: 'ready', configuration: { ...state.configuration, activeDeviceCount }, currentActive: false, localAccessError: false });
        setFeedback({ tone: 'success', message: '이 기기의 푸시 알림을 껐습니다.' });
      }
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, '이 기기의 푸시 알림을 끌 수 없습니다.') });
    } finally {
      setBusy(false);
    }
  };

  const disableAll = async () => {
    if (state.kind !== 'ready' || !window.confirm('로그인한 모든 기기의 푸시 연결을 해제할까요?')) return;
    setBusy(true);
    setFeedback(null);
    try {
      await deactivateAllWebPushSubscriptions(developmentUserKey);
      setState({ kind: 'ready', configuration: { ...state.configuration, activeDeviceCount: 0 }, currentActive: false, localAccessError: false });
      try {
        const subscription = await getCurrentBrowserSubscription();
        await subscription?.unsubscribe();
        setFeedback({ tone: 'success', message: '모든 기기의 푸시 연결을 해제했습니다. 다른 기기의 Microsoft 365 로그인은 유지됩니다.' });
      } catch {
        setFeedback({ tone: 'success', message: '서버의 모든 기기 푸시 연결을 해제했습니다. 이 브라우저의 로컬 알림 정보는 브라우저 정책 때문에 정리하지 못했지만 다시 알림이 발송되지는 않습니다.' });
      }
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, '모든 기기의 푸시 연결을 해제할 수 없습니다.') });
    } finally {
      setBusy(false);
    }
  };

  const statusMessage = state.kind === 'loading'
    ? '푸시 알림 상태를 확인하는 중입니다.'
    : state.kind === 'error'
      ? null
      : !browserSupported
        ? '이 브라우저에서는 PWA 푸시 알림을 사용할 수 없습니다.'
        : state.localAccessError
          ? null
          : !state.configuration.configured
            ? '푸시 알림은 현재 준비 중입니다. 기존 인앱 알림은 그대로 이용할 수 있습니다.'
            : !isInstalledPwa()
              ? '먼저 EMI PMS를 홈 화면에 설치한 뒤 설치된 앱에서 이 설정을 열어 주세요.'
              : Notification.permission === 'denied'
                ? '브라우저에서 알림이 차단되었습니다. 기기 설정에서 EMI PMS 알림을 허용해 주세요.'
                : state.currentActive
                  ? '이 기기의 푸시 알림이 켜져 있습니다.'
                  : Notification.permission === 'default'
                    ? '이 기기에서 알림 권한을 아직 선택하지 않았습니다.'
                    : '이 기기의 알림 권한은 허용되어 있지만 푸시 연결은 꺼져 있습니다.';

  return (
    <section className="web-push-settings" aria-labelledby="web-push-settings-title">
      <header>
        <div>
          <p className="eyebrow">PWA PUSH</p>
          <h3 id="web-push-settings-title">기기 푸시 알림</h3>
        </div>
        {state.kind === 'ready' ? <span>{state.configuration.activeDeviceCount}개 기기 연결</span> : null}
      </header>
      <p>인앱 알림과 같은 내용을 이 기기의 알림으로 받습니다. 여러 휴대폰과 태블릿에서 동시에 켤 수 있습니다.</p>

      {statusMessage ? <p className="web-push-state" role="status">{statusMessage}</p> : null}
      {state.kind === 'ready' && state.localAccessError ? (
        <div className="web-push-state web-push-error-state" role="alert">
          <span>{state.configuration.activeDeviceCount > 0
            ? '이 브라우저에서 푸시 알림 상태를 확인할 수 없습니다. 다른 기기의 연결은 아래에서 모두 해제할 수 있습니다.'
            : '이 브라우저에서 푸시 알림 상태를 확인할 수 없습니다.'}</span>
          <button type="button" onClick={() => { setState({ kind: 'loading' }); void load(); }}>다시 시도</button>
        </div>
      ) : null}
      {state.kind === 'error' ? (
        <div className="web-push-state web-push-error-state" role="alert">
          <span>{state.message}</span>
          <button type="button" onClick={() => { setState({ kind: 'loading' }); void load(); }}>다시 시도</button>
        </div>
      ) : null}
      {state.kind === 'ready' && (canManageCurrentDevice || canResetAllDevices) ? (
        <div className="web-push-controls">
          <div>
            <strong>{canManageCurrentDevice ? '현재 기기' : '연결된 기기'}</strong>
            <span>{canManageCurrentDevice
              ? '이 기기의 푸시 연결을 변경할 수 있습니다.'
              : '현재 사용 중인 브라우저와 관계없이 모든 기기의 푸시를 끌 수 있습니다.'}</span>
            {state.configuration.lastChangedAtUtc ? (
              <small>마지막 변경 {new Date(state.configuration.lastChangedAtUtc).toLocaleString('ko-KR')}</small>
            ) : null}
          </div>
          {canManageCurrentDevice ? (
            <button type="button" disabled={busy} onClick={() => void (state.currentActive ? disableCurrent() : enable())}>
              {busy ? '처리 중' : state.currentActive ? '이 기기 끄기' : '이 기기 켜기'}
            </button>
          ) : null}
          {canResetAllDevices ? (
            <button type="button" className="secondary-button" disabled={busy} onClick={() => void disableAll()}>
              모든 기기 연결 해제
            </button>
          ) : null}
        </div>
      ) : null}
      {feedback ? <p className="web-push-feedback" data-tone={feedback.tone} role={feedback.tone === 'error' ? 'alert' : 'status'}>{feedback.message}</p> : null}
      <small>모든 기기 연결 해제는 푸시만 끕니다. 분실 기기의 Microsoft 365 로그인 해제는 회사 계정 보안 메뉴에서 별도로 진행해야 합니다.</small>
    </section>
  );
}

export function WebPushFirstRunPrompt({ developmentUserKey }: { developmentUserKey: string }) {
  const [configuration, setConfiguration] = useState<WebPushConfiguration | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const checkedRef = useRef(false);
  const dialogRef = useRef<HTMLElement>(null);

  useEffect(() => {
    if (checkedRef.current || !supportsWebPush() || !isInstalledPwa()) return;
    checkedRef.current = true;
    let cancelled = false;
    void getWebPushConfiguration(developmentUserKey).then(async (result) => {
      if (cancelled || !result.configured) return;
      const dismissed = window.localStorage.getItem(webPushGuideDismissedStorageKey) === 'true';
      const existing = await getCurrentBrowserSubscription();
      const current = existing
        ? await getCurrentWebPushStatus(developmentUserKey, existing.endpoint)
        : { active: false };
      if (!cancelled && !dismissed && !current.active) {
        setConfiguration(result);
        setOpen(true);
      }
    }).catch(() => undefined);
    return () => { cancelled = true; };
  }, [developmentUserKey]);

  useEffect(() => {
    if (!open) return;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialogRef.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      window.localStorage.setItem(webPushGuideDismissedStorageKey, 'true');
      setOpen(false);
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previousFocus?.focus();
    };
  }, [open]);

  if (!open || !configuration) return null;
  const close = () => {
    window.localStorage.setItem(webPushGuideDismissedStorageKey, 'true');
    setOpen(false);
  };
  const enable = async () => {
    setBusy(true);
    setFeedback(null);
    try {
      const permission = Notification.permission === 'granted'
        ? 'granted'
        : await Notification.requestPermission();
      if (permission !== 'granted') {
        setFeedback('알림 권한이 허용되지 않았습니다. 나중에 내 알림 설정에서 다시 켤 수 있습니다.');
        return;
      }
      const registration = await getWebPushRegistration();
      if (!registration || !configuration.publicKey) throw new Error('푸시 알림을 준비할 수 없습니다.');
      const subscription = await getOrCreateBrowserSubscription(registration, configuration.publicKey);
      await saveCurrentWebPushSubscription(developmentUserKey, toWebPushRequest(subscription));
      window.localStorage.setItem(webPushGuideDismissedStorageKey, 'true');
      setOpen(false);
    } catch (error) {
      setFeedback(errorMessage(error, '푸시 알림을 켤 수 없습니다.'));
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="web-push-guide-overlay" role="presentation">
      <section ref={dialogRef} className="web-push-guide" role="dialog" aria-modal="true" aria-labelledby="web-push-guide-title">
        <p className="eyebrow">PWA PUSH</p>
        <h2 id="web-push-guide-title">이 기기에서 업무 알림 받기</h2>
        <p>인앱 알림과 같은 업무 알림을 휴대폰이나 태블릿 알림으로 받을 수 있습니다.</p>
        <p><strong>푸시 알림 켜기</strong>를 누른 뒤 브라우저 알림 권한을 허용해 주세요.</p>
        {feedback ? <p className="web-push-feedback" data-tone="error" role="alert">{feedback}</p> : null}
        <div className="web-push-guide-actions">
          <button type="button" disabled={busy} onClick={() => void enable()}>{busy ? '처리 중' : '푸시 알림 켜기'}</button>
          <button type="button" className="secondary-button" disabled={busy} onClick={close}>나중에</button>
        </div>
      </section>
    </div>
  );
}

function errorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError || error instanceof Error ? error.message : fallback;
}
