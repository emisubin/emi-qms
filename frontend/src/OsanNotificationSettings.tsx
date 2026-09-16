import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { ApiError, BusinessUnitRequestInvalidatedError } from './api';
import { DsDialog } from './design-system';
import {
  getOsanNotificationPreferences,
  saveOsanNotificationPreferences,
  type OsanNotificationPreferenceResponse
} from './osanNotificationPreferences';
import './OsanNotificationSettings.css';

type Props = {
  developmentUserKey?: string;
  contextKey: string;
  mutationAllowed: boolean;
  onClose: () => void;
};

type State =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; saved: OsanNotificationPreferenceResponse; draft: OsanNotificationPreferenceResponse; saving: boolean; error: string | null };

const clone = (value: OsanNotificationPreferenceResponse) => structuredClone(value);

export function OsanNotificationSettings({ developmentUserKey, contextKey, mutationAllowed, onClose }: Props) {
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [detail, setDetail] = useState(false);
  const [reloadNonce, setReloadNonce] = useState(0);
  const closeRef = useRef<HTMLButtonElement>(null);
  const detailRef = useRef<HTMLButtonElement>(null);
  const saveControllerRef = useRef<AbortController | null>(null);
  const saveLockRef = useRef(false);

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    getOsanNotificationPreferences(developmentUserKey, controller.signal)
      .then((response) => {
        if (!controller.signal.aborted) setState({ kind: 'ready', saved: response, draft: clone(response), saving: false, error: null });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted || error instanceof BusinessUnitRequestInvalidatedError) return;
        setState({ kind: 'error', message: error instanceof ApiError ? error.message : '알림 설정을 불러오지 못했습니다.' });
      });
    return () => {
      controller.abort();
      saveControllerRef.current?.abort();
      saveLockRef.current = false;
    };
  }, [contextKey, developmentUserKey, reloadNonce]);

  useEffect(() => {
    closeRef.current?.focus();
  }, []);

  const ready = state.kind === 'ready' ? state : null;
  const dirty = ready ? JSON.stringify(ready.saved) !== JSON.stringify(ready.draft) : false;
  const master = ready?.draft.items.find((item) => item.kind === 'StepCompleted');

  function updateMain(index: number, channel: 'mailEnabled' | 'pushEnabled') {
    setState((current) => current.kind !== 'ready' ? current : {
      ...current,
      error: null,
      draft: { ...current.draft, items: current.draft.items.map((item, itemIndex) => itemIndex === index ? { ...item, [channel]: !item[channel] } : item) }
    });
  }

  function updateStage(index: number, channel: 'mailEnabled' | 'pushEnabled') {
    setState((current) => current.kind !== 'ready' ? current : {
      ...current,
      error: null,
      draft: { ...current.draft, stepCompletedStages: current.draft.stepCompletedStages.map((item, itemIndex) => itemIndex === index ? { ...item, [channel]: !item[channel] } : item) }
    });
  }

  async function save() {
    if (!ready || ready.saving || saveLockRef.current || !mutationAllowed || !dirty) return;
    const controller = new AbortController();
    saveControllerRef.current = controller;
    saveLockRef.current = true;
    setState({ ...ready, saving: true, error: null });
    try {
      const response = await saveOsanNotificationPreferences(developmentUserKey, {
        ...ready.draft,
        expectedVersion: ready.saved.version
      }, controller.signal);
      if (controller.signal.aborted) return;
      setState({ kind: 'ready', saved: response, draft: clone(response), saving: false, error: null });
      onClose();
    } catch (error: unknown) {
      if (controller.signal.aborted || error instanceof BusinessUnitRequestInvalidatedError) return;
      setState({ ...ready, saving: false, error: error instanceof ApiError ? error.message : '알림 설정을 저장하지 못했습니다.' });
    } finally {
      if (saveControllerRef.current === controller) saveControllerRef.current = null;
      saveLockRef.current = false;
    }
  }

  function cancel() {
    if (ready?.saving) return;
    onClose();
  }

  const body = state.kind === 'loading'
    ? <p className="osan-notification-state" role="status">알림 설정을 불러오는 중입니다.</p>
    : state.kind === 'error'
      ? <div className="osan-notification-state osan-notification-state--error"><p role="alert">{state.message}</p><button type="button" className="osan-notification-button" onClick={() => setReloadNonce((value) => value + 1)}>다시 불러오기</button></div>
      : <>
          {detail && (!master?.mailEnabled || !master?.pushEnabled) ? (
            <p className="osan-notification-off-note">
              <strong>{[!master?.mailEnabled ? '메일' : null, !master?.pushEnabled ? '푸시' : null].filter(Boolean).join('·')} 전체 수신 꺼짐</strong><br />
              알림 종류에서 다시 켜면 아래 설정이 그대로 적용됩니다.
            </p>
          ) : null}
          <div className="osan-notification-columns"><span>{detail ? '진행단계' : '알림 종류'}</span><span>메일</span><span>푸시</span></div>
          {(detail ? state.draft.stepCompletedStages : state.draft.items).map((item, index) => (
            <div className="osan-notification-row" key={'kind' in item ? item.kind : item.sequence}>
              <div className="osan-notification-name">
                {item.label}
                {!detail && 'kind' in item && item.kind === 'StepCompleted' ? (
                  <button ref={detailRef} type="button" className="osan-notification-detail" onClick={() => setDetail(true)}>단계별 상세 설정 ›</button>
                ) : null}
              </div>
              <PreferenceSwitch
                label={`${item.label} 메일`}
                checked={item.mailEnabled}
                disabled={state.saving || (detail && !master?.mailEnabled)}
                onChange={() => detail ? updateStage(index, 'mailEnabled') : updateMain(index, 'mailEnabled')}
              />
              <PreferenceSwitch
                label={`${item.label} 푸시`}
                checked={item.pushEnabled}
                disabled={state.saving || (detail && !master?.pushEnabled)}
                onChange={() => detail ? updateStage(index, 'pushEnabled') : updateMain(index, 'pushEnabled')}
              />
            </div>
          ))}
          <p className="osan-notification-note">
            {detail ? '상위 ‘Gate 완료’가 켜져 있어야 선택한 단계의 알림을 받습니다.' : <>메일·푸시를 꺼도 PMS 알림 메뉴의 기록은 남습니다.<br />청주 알림 설정에는 영향을 주지 않습니다.</>}
          </p>
          {!detail ? <p className="osan-notification-note osan-notification-push-note">푸시는 PMS 앱·브라우저의 알림 권한이 허용된 기기에서 받을 수 있습니다.</p> : null}
          {state.error ? <div className="osan-notification-save-error"><p role="alert">{state.error}</p><button type="button" className="osan-notification-button" onClick={() => setReloadNonce((value) => value + 1)}>다시 불러오기</button></div> : null}
        </>;

  return createPortal(
    <DsDialog labelledBy="osan-notification-settings-title" onClose={cancel} closeDisabled={ready?.saving} className="osan-notification-backdrop">
      <section className="osan-notification-dialog" onKeyDown={(event) => {
        if (event.key === 'Escape') { event.preventDefault(); cancel(); }
      }}>
        <header>
          {detail ? <button type="button" className="osan-notification-back" onClick={() => { setDetail(false); setTimeout(() => detailRef.current?.focus(), 0); }}>‹ 알림 종류로 돌아가기</button> : null}
          <div className="osan-notification-heading">
            <h2 id="osan-notification-settings-title">{detail ? 'Gate 완료 알림' : '오산 알림 설정'}</h2>
            <button ref={closeRef} type="button" className="osan-notification-close" aria-label="알림 설정 닫기" onClick={cancel} disabled={ready?.saving}>×</button>
          </div>
          <p>{detail ? '완료 알림을 받을 공정을 선택하세요.' : '오산 전체 사용자에게 적용되는 설정입니다.'}</p>
        </header>
        <div className="osan-notification-content">{body}</div>
        <footer>
          <span>{dirty ? '저장하면 적용됩니다' : '오산 전체 사용자에게 적용'}</span>
          <div>
            <button type="button" className="osan-notification-button" onClick={cancel} disabled={ready?.saving}>취소</button>
            <button type="button" className="osan-notification-button osan-notification-button--primary" onClick={() => void save()} disabled={!ready || ready.saving || !mutationAllowed || !dirty}>{ready?.saving ? '저장 중…' : '저장'}</button>
          </div>
        </footer>
      </section>
    </DsDialog>,
    document.body
  );
}

function PreferenceSwitch({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: () => void }) {
  return (
    <button type="button" className="osan-notification-toggle" role="switch" aria-checked={checked} aria-label={label} disabled={disabled} onClick={onChange}>
      <span className="osan-notification-rail" aria-hidden="true"><i /></span>
      <small>{disabled ? '전체 꺼짐' : checked ? '켜짐' : '꺼짐'}</small>
    </button>
  );
}
