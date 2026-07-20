import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ApiError,
  getPanelQrImage,
  issuePanelQr,
  listProjectPanelQrs,
  preparePanelQrPrintSheet,
  rotatePanelQr
} from './api';
import type { ProjectPanelQrItem, ProjectPanelQrList } from './panelQr';

type PreviewItem = {
  panelId: string;
  displayCode: string;
  displayName: string;
  imageUrl: string;
};

export function PanelQrManager({
  developmentUserKey,
  projectId,
  canIssue,
  isSystemAdministrator
}: {
  developmentUserKey: string;
  projectId: string;
  canIssue: boolean;
  isSystemAdministrator: boolean;
}) {
  const [data, setData] = useState<ProjectPanelQrList | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [preview, setPreview] = useState<PreviewItem | null>(null);
  const [printItems, setPrintItems] = useState<PreviewItem[]>([]);
  const [rotationReason, setRotationReason] = useState('');
  const [busy, setBusy] = useState<string | null>('load');
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    setBusy('load');
    setMessage('');
    try {
      const next = await listProjectPanelQrs(developmentUserKey, projectId);
      setData(next);
      setSelected((current) => new Set([...current].filter((id) => next.panels.some((panel) => panel.panelId === id && panel.hasActiveQr))));
    } catch (error) {
      setMessage(qrErrorMessage(error, 'QR 목록을 불러올 수 없습니다.'));
    } finally {
      setBusy(null);
    }
  }, [developmentUserKey, projectId]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => () => {
    if (preview) URL.revokeObjectURL(preview.imageUrl);
  }, [preview]);
  useEffect(() => () => {
    printItems.forEach((item) => URL.revokeObjectURL(item.imageUrl));
  }, [printItems]);

  const issuedPanels = useMemo(() => data?.panels.filter((panel) => panel.hasActiveQr) ?? [], [data]);
  const allIssuedSelected = issuedPanels.length > 0 && issuedPanels.every((panel) => selected.has(panel.panelId));

  async function issue(panel: ProjectPanelQrItem) {
    setBusy(panel.panelId);
    setMessage('');
    try {
      await issuePanelQr(developmentUserKey, projectId, panel.panelId);
      setMessage(`${panel.displayName} QR을 발급했습니다.`);
      await load();
    } catch (error) {
      setMessage(qrErrorMessage(error, 'QR을 발급할 수 없습니다.'));
      setBusy(null);
    }
  }

  async function openPreview(panel: ProjectPanelQrItem) {
    setBusy(`preview:${panel.panelId}`);
    setMessage('');
    try {
      const blob = await getPanelQrImage(developmentUserKey, projectId, panel.panelId, 'svg');
      setPreview({
        panelId: panel.panelId,
        displayCode: panel.displayCode,
        displayName: panel.displayName,
        imageUrl: URL.createObjectURL(blob)
      });
      setRotationReason('');
    } catch (error) {
      setMessage(qrErrorMessage(error, 'QR 이미지를 불러올 수 없습니다.'));
    } finally {
      setBusy(null);
    }
  }

  async function downloadPng(panel: ProjectPanelQrItem) {
    setBusy(`download:${panel.panelId}`);
    setMessage('');
    try {
      const blob = await getPanelQrImage(developmentUserKey, projectId, panel.panelId, 'png');
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `panel-qr-${panel.displayCode}.png`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage(qrErrorMessage(error, 'QR 이미지를 저장할 수 없습니다.'));
    } finally {
      setBusy(null);
    }
  }

  async function rotate() {
    if (!preview) return;
    setBusy(`rotate:${preview.panelId}`);
    setMessage('');
    try {
      await rotatePanelQr(developmentUserKey, projectId, preview.panelId, rotationReason);
      setPreview(null);
      setRotationReason('');
      setMessage(`${preview.displayName} QR을 재발급했습니다. 기존 QR은 즉시 폐기되었습니다.`);
      await load();
    } catch (error) {
      setMessage(qrErrorMessage(error, 'QR을 재발급할 수 없습니다.'));
      setBusy(null);
    }
  }

  async function buildPrintSheet() {
    const panelIds = [...selected];
    if (panelIds.length === 0) {
      setMessage('인쇄할 QR을 선택해 주세요.');
      return;
    }
    setBusy('print');
    setMessage('');
    try {
      const sheet = await preparePanelQrPrintSheet(developmentUserKey, projectId, panelIds);
      const items = await Promise.all(sheet.items.map(async (item) => ({
        panelId: item.panelId,
        displayCode: item.displayCode,
        displayName: item.displayName,
        imageUrl: URL.createObjectURL(await getPanelQrImage(developmentUserKey, projectId, item.panelId, 'svg'))
      })));
      setPrintItems(items);
      setMessage(`${items.length}개 QR 인쇄판을 만들었습니다.`);
    } catch (error) {
      setMessage(qrErrorMessage(error, 'QR 인쇄판을 만들 수 없습니다. 목록을 새로고침해 주세요.'));
    } finally {
      setBusy(null);
    }
  }

  function toggle(panelId: string) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(panelId)) next.delete(panelId);
      else if (next.size < 50) next.add(panelId);
      return next;
    });
  }

  return (
    <section className="panel-qr-manager" aria-labelledby="panel-qr-title">
      <header className="panel-qr-header">
        <div>
          <p className="eyebrow">PANEL TRACE</p>
          <h4 id="panel-qr-title">패널 QR</h4>
          <span>패널 현황과 현재 담당 업무를 모바일에서 바로 엽니다.</span>
        </div>
        <div className="panel-qr-counts" aria-label="QR 현황">
          <strong>{data?.issuedCount ?? 0}</strong>
          <span>발급 / 가능 {data?.eligibleCount ?? 0}</span>
        </div>
      </header>

      {message ? <p className="panel-qr-message" role="status">{message}</p> : null}
      {busy === 'load' ? <p className="muted-text">QR 현황을 불러오는 중입니다.</p> : null}
      {data ? (
        <>
          <div className="panel-qr-toolbar">
            <label>
              <input
                type="checkbox"
                checked={allIssuedSelected}
                disabled={issuedPanels.length === 0}
                onChange={() => setSelected(allIssuedSelected ? new Set() : new Set(issuedPanels.slice(0, 50).map((panel) => panel.panelId)))}
              />
              발급 QR 전체 선택
            </label>
            <button type="button" className="secondary-button" disabled={selected.size === 0 || busy !== null} onClick={() => void buildPrintSheet()}>
              선택 {selected.size}개 인쇄판
            </button>
          </div>
          <div className="panel-qr-list">
            {data.panels.map((panel) => (
              <article key={panel.panelId} data-issued={panel.hasActiveQr || undefined}>
                <label className="panel-qr-select">
                  <input
                    type="checkbox"
                    checked={selected.has(panel.panelId)}
                    disabled={!panel.hasActiveQr}
                    onChange={() => toggle(panel.panelId)}
                    aria-label={`${panel.displayName} 인쇄 선택`}
                  />
                  <span>{panel.sequenceNumber}</span>
                </label>
                <div className="panel-qr-name">
                  <strong>{panel.displayName}</strong>
                  <small>{panel.displayCode}</small>
                </div>
                <span className="panel-qr-status" data-tone={panel.hasActiveQr ? 'issued' : panel.qrEligible ? 'ready' : 'blocked'}>
                  {panel.hasActiveQr ? '발급됨' : panel.qrEligible ? '발급 가능' : '패널명 필요'}
                </span>
                <div className="panel-qr-actions">
                  {panel.hasActiveQr ? (
                    <>
                      <button type="button" onClick={() => void openPreview(panel)} disabled={busy !== null}>보기</button>
                      <button type="button" onClick={() => void downloadPng(panel)} disabled={busy !== null}>PNG</button>
                    </>
                  ) : canIssue && panel.qrEligible ? (
                    <button type="button" className="primary-button" onClick={() => void issue(panel)} disabled={busy !== null}>QR 발급</button>
                  ) : null}
                </div>
              </article>
            ))}
          </div>
        </>
      ) : null}

      {preview ? (
        <div className="panel-qr-preview">
          <figure>
            <img src={preview.imageUrl} alt={`${preview.displayName} QR 코드`} />
            <figcaption><strong>{preview.displayName}</strong><span>{preview.displayCode}</span></figcaption>
          </figure>
          <div>
            <h5>현장 라벨 미리보기</h5>
            <p>QR에는 업무 데이터가 아닌 임의 식별자만 들어갑니다.</p>
            {isSystemAdministrator ? (
              <label>
                재발급 사유
                <textarea value={rotationReason} onChange={(event) => setRotationReason(event.target.value)} placeholder="분실·훼손 등 사유를 입력" />
                <button type="button" className="danger-button" disabled={busy !== null || rotationReason.trim().length < 2} onClick={() => void rotate()}>
                  기존 QR 폐기 후 재발급
                </button>
              </label>
            ) : null}
            <button type="button" className="secondary-button" onClick={() => setPreview(null)}>닫기</button>
          </div>
        </div>
      ) : null}

      {printItems.length > 0 ? (
        <section className="panel-qr-print-shell">
          <header><div><strong>QR 인쇄판</strong><span>{printItems.length}개 · 같은 프로젝트</span></div><button type="button" className="primary-button" onClick={() => window.print()}>인쇄</button></header>
          <div className="panel-qr-print-grid">
            {printItems.map((item) => (
              <figure key={item.panelId}>
                <img src={item.imageUrl} alt={`${item.displayName} QR 코드`} />
                <figcaption><strong>{item.displayName}</strong><span>{item.displayCode}</span></figcaption>
              </figure>
            ))}
          </div>
        </section>
      ) : null}
    </section>
  );
}

function qrErrorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}
