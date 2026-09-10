import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from 'react';
import { fetchBlob, getOsanProject } from './api';
import type { OsanProjectDetail } from './projects';
import './osan-qr.css';

type Label = { project: OsanProjectDetail; targetId: string; targetName: string; imageUrl: string };
const labelKey = (item: Label) => item.project.projectId + ':' + item.targetId;
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
    <img src={item.imageUrl} alt={`${item.project.title} ${item.targetName} QR 코드`} />
    <div className="osan-qr-label-caption" ref={caption}><p>{item.project.title}</p><p>{item.project.productName}</p><p>{item.project.workOrderNumber || 'W/O 없음'}</p></div>
  </div>;
}
export function OsanQrPrintDialog({ projectIds, userKey, onClose }: { projectIds: string[]; userKey?: string; onClose: () => void }) {
  const [size, setSize] = useState<30 | 50>(30);
  const [items, setItems] = useState<Label[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
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
    setItems([]); setSelected(new Set()); setError(''); setLoading(true);
    async function load() {
      const results: Label[] = [];
      for (const id of JSON.parse(idsKey) as string[]) {
        const project = await getOsanProject(userKey ?? '', id, { signal: controller.signal });
        for (const target of project.targets) {
          const blob = await fetchBlob('/api/osan/projects/' + encodeURIComponent(id) + '/targets/' + encodeURIComponent(target.targetId) + '/qr?format=svg', userKey, controller.signal);
          if (!alive) return;
          const imageUrl = URL.createObjectURL(blob); urls.push(imageUrl);
          results.push({ project, targetId: target.targetId, targetName: target.displayName, imageUrl });
        }
      }
      if (alive) { setItems(results); setSelected(new Set(results.map(labelKey))); setLoading(false); }
    }
    load().catch(() => { if (alive) { setError('선택한 프로젝트의 QR을 모두 준비하지 못했습니다. 삭제 여부와 조회 권한을 확인하고 다시 시도해 주세요.'); setLoading(false); } });
    return () => { alive = false; controller.abort(); urls.forEach(url => URL.revokeObjectURL(url)); };
  }, [idsKey, userKey, attempt]);
  const selectedItems = items.filter(item => selected.has(labelKey(item)));
  async function print() {
    if (printing || loading || !selectedItems.length || error) return;
    setPrinting(true);
    try {
      printFrame.current?.remove();
      const frame = document.createElement('iframe');
      frame.title = '패널 QR 인쇄'; frame.style.cssText = 'position:fixed;width:1px;height:1px;left:-10000px;border:0';
      document.body.append(frame); printFrame.current = frame;
      const doc = frame.contentDocument!;
      doc.title = '오산 패널 QR';
      populateQrPrintDocument(doc, [...(preview.current?.querySelectorAll('[data-selected="true"] .osan-qr-label') ?? [])], size);
      await Promise.all([...doc.images].map(img => new Promise<void>((resolve, reject) => {
        if (img.complete) { if (img.naturalWidth) resolve(); else reject(new Error('image')); return; }
        img.onload = () => resolve(); img.onerror = () => reject(new Error('image'));
      })));
      frame.contentWindow!.focus(); frame.contentWindow!.print();
    } catch { setError('인쇄 화면을 준비하지 못했습니다. 다시 시도해 주세요.'); }
    finally { setPrinting(false); }
  }
  return <dialog ref={dialog} className="osan-qr-print-dialog" aria-labelledby="osan-qr-print-title" onCancel={e => { if (printing) e.preventDefault(); else onClose(); }}>
    <style>{qrLabelCss}</style><header className="osan-qr-dialog-header"><div><h2 id="osan-qr-print-title">패널 QR 코드</h2><p>프로젝트 {JSON.parse(idsKey).length}개 · 패널 {items.length}개 · 출력 {selectedItems.length}장</p></div><button disabled={printing} onClick={onClose}>닫기</button></header>
    <div className="osan-qr-dialog-body"><div className="osan-qr-print-controls">{([30, 50] as const).map(value => <label key={value}><input type="radio" name="osan-qr-size" checked={size === value} disabled={printing} onChange={() => setSize(value)} /> {value} × {value}mm</label>)}</div>
      {loading && <p role="status">QR 코드를 준비하는 중…</p>}{error && <div role="alert"><p>{error}</p><button onClick={() => setAttempt(v => v + 1)}>다시 시도</button></div>}
      <div className="osan-qr-target-selection"><label><input type="checkbox" checked={items.length > 0 && selectedItems.length === items.length} disabled={loading || printing || !items.length} onChange={event => setSelected(new Set(event.target.checked ? items.map(labelKey) : []))} />전체 패널 선택</label></div>
      {!loading && !error && !items.length && <p>출력할 진행 대상 패널이 없습니다.</p>}
      <div className="osan-qr-print-preview" ref={preview}>{items.map(item => <div className="osan-qr-print-item" key={labelKey(item)} data-selected={selected.has(labelKey(item))}><label><input type="checkbox" checked={selected.has(labelKey(item))} disabled={printing} onChange={event => setSelected(current => { const next = new Set(current); if (event.target.checked) next.add(labelKey(item)); else next.delete(labelKey(item)); return next; })} />{item.project.title} · {item.targetName}</label><QrLabel item={item} size={size} /></div>)}</div>
      <p className="osan-qr-print-note">QR 하나가 선택한 {size}×{size}mm 용지 한 장에 출력됩니다. 선택 패널 {selectedItems.length}개는 총 {selectedItems.length}장입니다. 용지 안에 QR·여백·장비명·part 분류·W/O 3줄이 모두 포함됩니다. 긴 이름은 한 줄에 맞춰 작게 표시됩니다.<br />프린터 용지도 같은 크기로 지정하고 배율 100%·실제 크기, 여백 없음, 머리글·바닥글 끄기를 사용해 주세요. QR 조회에는 로그인과 프로젝트 조회 권한이 필요합니다.</p>
      <button className="primary" disabled={loading || printing || !!error || !selectedItems.length} onClick={() => void print()}>{printing ? '인쇄 준비 중…' : `${selectedItems.length}장 QR 인쇄`}</button>
    </div>
  </dialog>;
}
