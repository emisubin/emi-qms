import { useEffect, useState } from 'react';
import { ApiError, resolvePanelQr } from './api';
import type { PanelQrResolve } from './panelQr';

type ScanState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: PanelQrResolve }
  | { kind: 'error'; message: string; status?: number };

export function QrScanLandingPage({
  developmentUserKey,
  token,
  onOpenPath
}: {
  developmentUserKey: string;
  token: string;
  onOpenPath: (path: string) => void;
}) {
  const [state, setState] = useState<ScanState>({ kind: 'loading' });

  useEffect(() => {
    let cancelled = false;
    void resolvePanelQr(developmentUserKey, token)
      .then((data) => { if (!cancelled) setState({ kind: 'ready', data }); })
      .catch((error: unknown) => {
        if (!cancelled) setState({
          kind: 'error',
          message: error instanceof ApiError ? error.message : 'QR을 확인할 수 없습니다.',
          status: error instanceof ApiError ? error.status : undefined
        });
      });
    return () => { cancelled = true; };
  }, [developmentUserKey, token]);

  return (
    <main className="qr-scan-page">
      <section className="qr-scan-card">
        <div className="qr-scan-mark" aria-hidden="true"><span /><span /><span /></div>
        {state.kind === 'loading' ? (
          <div className="qr-scan-state"><p className="eyebrow">PANEL QR</p><h2>패널을 확인하는 중</h2><p>로그인 계정의 담당 업무를 찾고 있습니다.</p></div>
        ) : null}
        {state.kind === 'error' ? (
          <div className="qr-scan-state" data-tone="error">
            <p className="eyebrow">SCAN ERROR</p>
            <h2>{state.status === 410 ? '폐기된 QR입니다' : 'QR을 열 수 없습니다'}</h2>
            <p>{state.message}</p>
            <button type="button" className="secondary-button" onClick={() => onOpenPath('/')}>홈으로</button>
          </div>
        ) : null}
        {state.kind === 'ready' ? (
          <>
            <header className="qr-scan-heading">
              <div>
                <p className="eyebrow">PANEL FOUND</p>
                <h2>{state.data.panelDisplayName}</h2>
                <span>{state.data.projectCode} · {state.data.projectTitle}</span>
              </div>
              <span className="qr-scan-live" data-tone={state.data.status === 'Ok' ? 'active' : 'readonly'}>
                {state.data.status === 'Ok' ? '진행 중' : '조회 전용'}
              </span>
            </header>
            <div className="qr-scan-stage">
              <span>현재 단계</span>
              <strong>{state.data.currentStageName ?? '종합 현황'}</strong>
              <small>{state.data.currentDepartmentName ?? '담당 부서 확인 중'}</small>
            </div>
            <p className="qr-scan-message">{state.data.message}</p>
            <dl className="qr-scan-facts">
              <div><dt>프로젝트</dt><dd>{state.data.projectCode}</dd></div>
              <div><dt>패널</dt><dd>{state.data.panelDisplayName}</dd></div>
              <div><dt>내 권한</dt><dd>{state.data.canEditCurrentStage ? '현재 단계 입력 가능' : '조회 가능'}</dd></div>
            </dl>
            {state.data.primaryActionPath ? (
              <button type="button" className="primary-button qr-scan-primary" onClick={() => onOpenPath(state.data.primaryActionPath!)}>
                {state.data.primaryActionLabel ?? '패널 열기'}
              </button>
            ) : null}
            {state.data.overviewPath && state.data.overviewPath !== state.data.primaryActionPath ? (
              <button type="button" className="qr-scan-overview" onClick={() => onOpenPath(state.data.overviewPath!)}>패널 종합현황 보기</button>
            ) : null}
          </>
        ) : null}
      </section>
      <p className="qr-scan-footnote">QR 스캔은 상태를 변경하지 않습니다.</p>
    </main>
  );
}
