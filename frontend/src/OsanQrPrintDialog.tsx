import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from 'react';
import { fetchBlob, getOsanProject } from './api';
import type { OsanProjectDetail } from './projects';
import './osan-qr.css';

type Label = { project: OsanProjectDetail; imageUrl: string };
import { qrLabelCss, populateQrPrintDocument } from './osanQrPrint';

function QrLabel({ item, size }: { item: Label; size: 30 | 50 }) {
  const caption = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    caption.current?.querySelectorAll('p').forEach(line => {
      line.style.fontSize = '';
      const available = line.clientWidth;
      if (line.scrollWidth > available && available > 0) line.style.fontSize = `${parseFloat(getComputedStyle(line).fontSize) * available / line.scrollWidth * .98}px`;
    });
  }, [size, item]);
  return <div className="osan-qr-label" style={{ '--label-size': `${size}mm`, '--qr-size': size === 30 ? '21mm' : '36mm', '--caption-line': size === 30 ? '2.1mm' : '3.1mm', '--caption-size': size === 30 ? '1.8mm' : '2.8mm' } as CSSProperties}>
    <img src={item.imageUrl} alt={`${item.project.title} QR 코드`} />
    <div className="osan-qr-label-caption" ref={caption}><p>{item.project.title}</p><p>{item.project.productName}</p><p>{item.project.workOrderNumber || 'W/O 없음'}</p></div>
  </div>;
}
export function OsanQrPrintDialog({ projectIds, userKey, onClose }: { projectIds: string[]; userKey?: string; onClose: () => void }) {
  const [size, setSize] = useState<30 | 50>(30);
  const [items, setItems] = useState<Label[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [printing, setPrinting] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const dialog = useRef<HTMLDialogElement>(null);
  const preview = useRef<HTMLDivElement>(null);
  const printFrame = useRef<HTMLIFrameElement | null>(null);
  const idsKey = JSON.stringify([...new Set(projectIds)]);
  useEffect(() => { dialog.current?.showModal(); return () => printFrame.current?.remove(); }, []);
  useEffect(() => {
    let alive = true;
    const controller = new AbortController();
    const urls: string[] = [];
    setItems([]); setError(''); setLoading(true);
    async function load() {
      const results: Label[] = [];
      for (const id of JSON.parse(idsKey) as string[]) {
        const [project, blob] = await Promise.all([getOsanProject(userKey ?? '', id, { signal: controller.signal }), fetchBlob(`/api/osan/projects/${encodeURIComponent(id)}/qr?format=svg`, userKey, controller.signal)]);
        if (!alive) return;
        const imageUrl = URL.createObjectURL(blob); urls.push(imageUrl); results.push({ project, imageUrl });
      }
      if (alive) { setItems(results); setLoading(false); }
    }
    load().catch(() => { if (alive) { setError('선택한 프로젝트의 QR을 모두 준비하지 못했습니다. 삭제 여부와 조회 권한을 확인하고 다시 시도해 주세요.'); setLoading(false); } });
    return () => { alive = false; controller.abort(); urls.forEach(url => URL.revokeObjectURL(url)); };
  }, [idsKey, userKey, attempt]);
  async function print() {
    if (printing || loading || !items.length || error) return;
    setPrinting(true);
    try {
      printFrame.current?.remove();
      const frame = document.createElement('iframe');
      frame.title = '프로젝트 QR 인쇄'; frame.style.cssText = 'position:fixed;width:1px;height:1px;left:-10000px;border:0';
      document.body.append(frame); printFrame.current = frame;
      const doc = frame.contentDocument!;
      doc.title = '오산 프로젝트 QR';
      populateQrPrintDocument(doc, [...(preview.current?.querySelectorAll('.osan-qr-label') ?? [])], size);
      await Promise.all([...doc.images].map(img => new Promise<void>((resolve, reject) => {
        if (img.complete) { if (img.naturalWidth) resolve(); else reject(new Error('image')); return; }
        img.onload = () => resolve(); img.onerror = () => reject(new Error('image'));
      })));
      frame.contentWindow!.focus(); frame.contentWindow!.print();
    } catch { setError('인쇄 화면을 준비하지 못했습니다. 다시 시도해 주세요.'); }
    finally { setPrinting(false); }
  }
  return <dialog ref={dialog} className="osan-qr-print-dialog" aria-labelledby="osan-qr-print-title" onCancel={e => { if (printing) e.preventDefault(); else onClose(); }}>
    <style>{qrLabelCss}</style><header className="osan-qr-dialog-header"><div><h2 id="osan-qr-print-title">프로젝트 QR 코드</h2><p>선택 프로젝트 {JSON.parse(idsKey).length}개</p></div><button disabled={printing} onClick={onClose}>닫기</button></header>
    <div className="osan-qr-dialog-body"><div className="osan-qr-print-controls">{([30, 50] as const).map(value => <label key={value}><input type="radio" name="osan-qr-size" checked={size === value} disabled={printing} onChange={() => setSize(value)} /> {value} × {value}mm</label>)}</div>
      {loading && <p role="status">QR 코드를 준비하는 중…</p>}{error && <div role="alert"><p>{error}</p><button onClick={() => setAttempt(v => v + 1)}>다시 시도</button></div>}
      <div className="osan-qr-print-preview" ref={preview}>{items.map(item => <QrLabel key={item.project.projectId} item={item} size={size} />)}</div>
      <p className="osan-qr-print-note">선택한 정사각형 안에 QR·여백·장비명·part 분류·W/O 3줄이 모두 포함됩니다. 긴 이름은 한 줄에 맞춰 작게 표시됩니다.<br />인쇄 설정은 배율 100%·실제 크기, 머리글·바닥글 끄기를 사용해 주세요. QR 조회에는 로그인과 프로젝트 조회 권한이 필요합니다.</p>
      <button className="primary" disabled={loading || printing || !!error || !items.length} onClick={() => void print()}>{printing ? '인쇄 준비 중…' : `${items.length}개 QR 인쇄`}</button>
    </div>
  </dialog>;
}
