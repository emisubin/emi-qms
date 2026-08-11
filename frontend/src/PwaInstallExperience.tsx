import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { PwaInstallExperienceContext, type PwaInstallExperienceValue } from './pwa-install';

type InstallChoice = {
  outcome: 'accepted' | 'dismissed';
  platform: string;
};

type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<InstallChoice>;
};

type InstallPlatform = 'ios-safari' | 'ios-browser' | 'android' | 'browser';

const dismissedStorageKey = 'emi-pms:pwa-install-guide-dismissed-session-v2';

function mediaMatches(query: string) {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia(query).matches;
}

function isStandaloneDisplay() {
  if (typeof window === 'undefined') return false;
  const standaloneNavigator = navigator as Navigator & { standalone?: boolean };
  return mediaMatches('(display-mode: standalone)') || standaloneNavigator.standalone === true;
}

function isEmbeddedContext() {
  if (typeof window === 'undefined') return false;
  try {
    if (window.self !== window.top) return true;
  } catch {
    return true;
  }
  return /teams\.microsoft\.com|teams\.live\.com/iu.test(document.referrer);
}

function isIosDevice() {
  if (typeof navigator === 'undefined') return false;
  return /iPad|iPhone|iPod/iu.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
}

function isAndroidDevice() {
  return typeof navigator !== 'undefined' && /Android/iu.test(navigator.userAgent);
}

function isSafariBrowserOnIos() {
  if (typeof navigator === 'undefined') return false;
  const userAgent = navigator.userAgent;
  return /Safari/iu.test(userAgent)
    && /Version\//iu.test(userAgent)
    && !/CriOS|EdgiOS|FxiOS|OPiOS|DuckDuckGo|GSA/iu.test(userAgent);
}

function isMobileDevice() {
  if (typeof navigator === 'undefined') return false;
  return mediaMatches('(max-width: 767px)')
    || /Android|iPad|iPhone|iPod|Mobile/iu.test(navigator.userAgent);
}

function wasGuideDismissed() {
  if (typeof window === 'undefined') return false;
  try {
    return window.sessionStorage.getItem(dismissedStorageKey) === 'true';
  } catch {
    return false;
  }
}

function rememberGuideDismissal() {
  try {
    window.sessionStorage.setItem(dismissedStorageKey, 'true');
  } catch {
    // Storage can be unavailable in private or embedded browser modes.
  }
}

export function PwaInstallProvider({ children }: { children: ReactNode }) {
  const [promptEvent, setPromptEvent] = useState<BeforeInstallPromptEvent | null>(null);
  const [standalone, setStandalone] = useState(isStandaloneDisplay);
  const [automaticGuideReady, setAutomaticGuideReady] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [automaticGuideDismissed, setAutomaticGuideDismissed] = useState(wasGuideDismissed);
  const [nativePromptDismissed, setNativePromptDismissed] = useState(false);
  const [copyFeedback, setCopyFeedback] = useState<'success' | 'error' | null>(null);
  const dialogRef = useRef<HTMLElement>(null);
  const embedded = isEmbeddedContext();
  const platform: InstallPlatform = isIosDevice()
    ? isSafariBrowserOnIos() ? 'ios-safari' : 'ios-browser'
    : isAndroidDevice() ? 'android' : 'browser';
  const iosPlatform = platform === 'ios-safari' || platform === 'ios-browser';
  const mobile = isMobileDevice();
  const available = !standalone && !embedded;

  const closeDialog = useCallback(() => {
    rememberGuideDismissal();
    setAutomaticGuideDismissed(true);
    setDialogOpen(false);
  }, []);

  useEffect(() => {
    const onBeforeInstallPrompt = (event: Event) => {
      event.preventDefault();
      setPromptEvent(event as BeforeInstallPromptEvent);
      setNativePromptDismissed(false);
    };
    const onInstalled = () => {
      setStandalone(true);
      setPromptEvent(null);
      setDialogOpen(false);
      rememberGuideDismissal();
    };

    window.addEventListener('beforeinstallprompt', onBeforeInstallPrompt);
    window.addEventListener('appinstalled', onInstalled);
    return () => {
      window.removeEventListener('beforeinstallprompt', onBeforeInstallPrompt);
      window.removeEventListener('appinstalled', onInstalled);
    };
  }, []);

  useEffect(() => {
    if (!automaticGuideReady || !available || !mobile || automaticGuideDismissed || dialogOpen) return;
    if (!iosPlatform && platform !== 'android') return;
    const timer = window.setTimeout(() => setDialogOpen(true), 0);
    return () => window.clearTimeout(timer);
  }, [automaticGuideDismissed, automaticGuideReady, available, dialogOpen, iosPlatform, mobile, platform]);

  useEffect(() => {
    if (!dialogOpen) return;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const firstButton = dialogRef.current?.querySelector<HTMLButtonElement>('button:not(:disabled)');
    firstButton?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.preventDefault();
      closeDialog();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previousFocus?.focus();
    };
  }, [closeDialog, dialogOpen]);

  const value = useMemo<PwaInstallExperienceValue>(() => ({
    available,
    entryLabel: promptEvent
      ? 'EMI PMS 설치'
      : iosPlatform
        ? 'iPhone 설치 안내'
        : platform === 'android'
          ? 'Android 설치 안내'
          : 'EMI PMS 설치 안내',
    openGuide: () => {
      setNativePromptDismissed(false);
      setCopyFeedback(null);
      setDialogOpen(true);
    },
    setAutomaticGuideReady
  }), [available, iosPlatform, platform, promptEvent]);

  const requestInstall = async () => {
    if (!promptEvent) return;
    await promptEvent.prompt();
    const choice = await promptEvent.userChoice;
    setPromptEvent(null);
    if (choice.outcome === 'accepted') {
      rememberGuideDismissal();
      setDialogOpen(false);
      return;
    }
    setNativePromptDismissed(true);
  };

  const copyInstallAddress = async () => {
    try {
      await navigator.clipboard.writeText(new URL('/', window.location.origin).toString());
      setCopyFeedback('success');
    } catch {
      setCopyFeedback('error');
    }
  };

  const guideTitle = iosPlatform
    ? 'iPhone 설치 안내'
    : platform === 'android'
      ? 'Android 설치 안내'
      : 'EMI PMS 설치 안내';

  return (
    <PwaInstallExperienceContext.Provider value={value}>
      {children}
      {available && dialogOpen ? (
        <div className="pwa-install-overlay" role="presentation">
          <section
            ref={dialogRef}
            className="pwa-install-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="pwa-install-title"
            aria-describedby="pwa-install-description"
          >
            <header className="pwa-install-header">
              <img className="pwa-install-brand" src="/icons/emi-qms-192.png" alt="" aria-hidden="true" />
              <div>
                <p>EMI PMS</p>
                <h2 id="pwa-install-title">{guideTitle}</h2>
              </div>
            </header>
            <p id="pwa-install-description">
              홈 화면에서 바로 열면 제조·품질 현장에서도 브라우저 주소를 다시 찾지 않고 업무를 시작할 수 있습니다.
              설치해도 기존 EMI PMS와 같은 계정·권한을 사용합니다.
            </p>

            <h3 className="pwa-install-section-title">설치 방법</h3>

            {platform === 'ios-safari' ? (
              <ol className="pwa-install-steps">
                <li><span>Safari 아래쪽의 <strong>공유</strong> 버튼을 누릅니다.</span></li>
                <li><span><strong>홈 화면에 추가</strong>를 선택합니다.</span></li>
                <li><span><strong>웹 앱으로 열기</strong>를 켭니다.</span></li>
                <li><span>오른쪽 위의 <strong>추가</strong>를 누릅니다.</span></li>
              </ol>
            ) : platform === 'ios-browser' ? (
              <>
                <p className="pwa-install-note">현재 브라우저의 공유 메뉴에서 먼저 <strong>홈 화면에 추가</strong>를 확인해 주세요.</p>
                <ol className="pwa-install-steps">
                  <li><span>현재 브라우저의 <strong>공유</strong> 메뉴를 엽니다.</span></li>
                  <li><span><strong>홈 화면에 추가</strong>가 있으면 선택해 바로 설치합니다.</span></li>
                  <li><span>메뉴가 없으면 아래 <strong>PMS 주소 복사</strong>를 누르고 Safari를 엽니다.</span></li>
                  <li><span>Safari 주소창에 붙여넣고 <strong>공유 → 홈 화면에 추가 → 웹 앱으로 열기 → 추가</strong> 순서로 진행합니다.</span></li>
                </ol>
              </>
            ) : promptEvent ? (
              <p className="pwa-install-note">설치를 누르면 브라우저의 설치 확인 창이 열립니다.</p>
            ) : (
              <p className="pwa-install-note">설치 버튼을 준비하고 있습니다. 잠시 후 버튼이 활성화되지 않으면 브라우저 메뉴에서 <strong>앱 설치</strong> 또는 <strong>홈 화면에 추가</strong>를 선택해 주세요.</p>
            )}

            <section className="pwa-install-usage" aria-labelledby="pwa-install-usage-title">
              <h3 id="pwa-install-usage-title">이용 안내</h3>
              <ul>
                <li>설치 후에도 온라인 연결과 Microsoft 365 로그인이 필요합니다.</li>
                <li>업무 안내는 인앱 알림, Teams Activity와 메일로 전달될 수 있습니다.</li>
                <li>향후 모바일 푸시 알림이 추가되면 기기로 알림이 갈 수 있습니다.</li>
                <li>현재 모바일 푸시는 준비 중이며, 설치나 이용에 알림 권한이 필요하지 않습니다.</li>
              </ul>
            </section>

            {nativePromptDismissed ? (
              <p className="pwa-install-feedback" role="status">설치를 취소했습니다. 계정 메뉴에서 언제든 다시 안내를 열 수 있습니다.</p>
            ) : null}

            {copyFeedback === 'success' ? (
              <p className="pwa-install-feedback" role="status">PMS 주소를 복사했습니다. Safari 주소창에 붙여넣어 주세요.</p>
            ) : copyFeedback === 'error' ? (
              <p className="pwa-install-feedback" role="alert">주소를 복사하지 못했습니다. 현재 주소창을 길게 눌러 주소를 복사해 주세요.</p>
            ) : null}

            <div className="pwa-install-actions">
              {platform === 'ios-browser' ? (
                <button type="button" onClick={() => void copyInstallAddress()}>PMS 주소 복사</button>
              ) : platform === 'android' ? (
                <button type="button" disabled={!promptEvent} onClick={() => void requestInstall()}>EMI PMS 설치</button>
              ) : promptEvent ? (
                <button type="button" onClick={() => void requestInstall()}>EMI PMS 설치</button>
              ) : null}
              <button type="button" className="secondary" onClick={closeDialog}>{promptEvent || platform === 'ios-browser' ? '나중에' : '확인'}</button>
            </div>
          </section>
        </div>
      ) : null}
    </PwaInstallExperienceContext.Provider>
  );
}
