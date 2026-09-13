import { useEffect, useRef, useState } from 'react';
import { parseOsanPanelQr } from './osanQrScan';
import './osan-mobile-tools.css';

export function OsanQrScanner({ onClose, onScan }: { onClose: () => void; onScan: (projectId: string, targetId: string) => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const video = useRef<HTMLVideoElement>(null);
  const [attempt, setAttempt] = useState(0);
  const [message, setMessage] = useState('카메라를 준비하고 있습니다.');
  const [failed, setFailed] = useState(false);
  const callbacks = useRef({ onClose, onScan });
  useEffect(() => { callbacks.current = { onClose, onScan }; }, [onClose, onScan]);
  useEffect(() => { dialog.current?.showModal(); }, []);
  useEffect(() => {
    let disposed = false;
    let stream: MediaStream | undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const stop = () => { if (timer) clearTimeout(timer); stream?.getTracks().forEach(track => track.stop()); };
    const fail = (text: string) => { stop(); if (!disposed) { setMessage(text); setFailed(true); } };
    const visibility = () => { if (document.hidden) { disposed = true; stop(); setMessage('스캔이 일시 중지되었습니다. 다시 시작해주세요.'); setFailed(true); } };
    document.addEventListener('visibilitychange', visibility);
    async function start() {
      setFailed(false); setMessage('카메라를 준비하고 있습니다.');
      if (!navigator.mediaDevices?.getUserMedia) { fail('카메라를 사용할 수 없습니다. HTTPS로 접속했는지 확인해주세요.'); return; }
      try {
        stream = await navigator.mediaDevices.getUserMedia({ audio: false, video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } } });
        if (disposed) { stop(); return; }
        const element = video.current;
        if (!element) { stop(); return; }
        element.srcObject = stream;
        await element.play();
        const { default: decode } = await import('jsqr');
        if (disposed) { stop(); return; }
        const canvas = document.createElement('canvas');
        const context = canvas.getContext('2d', { willReadFrequently: true });
        if (!context) { fail('카메라 영상을 읽을 수 없습니다. 다시 시도해주세요.'); return; }
        setMessage('QR 코드를 찾고 있습니다.');
        const scan = () => {
          if (disposed) return;
          try {
            if (element.readyState >= 2 && element.videoWidth && element.videoHeight) {
              const scale = Math.min(1, 960 / element.videoWidth);
              canvas.width = Math.round(element.videoWidth * scale); canvas.height = Math.round(element.videoHeight * scale);
              context.drawImage(element, 0, 0, canvas.width, canvas.height);
              const pixels = context.getImageData(0, 0, canvas.width, canvas.height);
              const result = decode(pixels.data, pixels.width, pixels.height, { inversionAttempts: 'dontInvert' });
              if (result) {
                const panel = parseOsanPanelQr(result.data);
                if (!panel) { fail('PMS 패널 QR 코드가 아닙니다. 올바른 QR 코드를 다시 스캔해주세요.'); return; }
                disposed = true; stop(); setMessage('패널 정보를 확인합니다.');
                callbacks.current.onScan(panel.projectId, panel.targetId); return;
              }
            }
            timer = setTimeout(scan, 180);
          } catch { fail('QR 코드를 읽지 못했습니다. 다시 시도해주세요.'); }
        };
        scan();
      } catch (error) {
        const name = error && typeof error === 'object' && 'name' in error ? String(error.name) : '';
        fail(name === 'NotAllowedError' ? '카메라 사용 권한이 필요합니다. 카메라 접근을 허용한 뒤 다시 시도해주세요.' : name === 'NotFoundError' ? '연결된 카메라를 찾을 수 없습니다.' : '카메라를 시작하지 못했습니다. 다른 앱의 카메라 사용을 종료한 뒤 다시 시도해주세요.');
      }
    }
    void start();
    return () => { disposed = true; stop(); document.removeEventListener('visibilitychange', visibility); };
  }, [attempt]);
  return <dialog ref={dialog} className="osan-scan-dialog" aria-labelledby="osan-scan-title" onCancel={event => { event.preventDefault(); onClose(); }}>
    <header><h2 id="osan-scan-title">QR 스캔</h2><button type="button" onClick={onClose}>닫기</button></header>
    <div className="osan-scan-body"><p>패널에 부착된 QR 코드를 화면에 맞춰주세요.</p>
      <div className="osan-scan-camera"><video ref={video} muted playsInline aria-label="QR 스캔 카메라" /><div className="osan-scan-frame" aria-hidden="true"><i/><i/><i/><i/></div></div>
      <p className="osan-scan-status" role={failed ? 'alert' : 'status'}>{message}</p>
      {!failed && <p className="osan-scan-hint">인식하면 해당 패널 화면으로 자동 이동합니다.</p>}
      {failed && <button type="button" className="osan-scan-retry" onClick={() => setAttempt(value => value + 1)}>다시 시도</button>}
    </div>
  </dialog>;
}
