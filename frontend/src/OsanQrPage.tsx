import { useEffect, useRef, useState } from 'react';
import { ApiError, getOsanProject } from './api';
import type { OsanProjectDetail } from './projects';
import { getOsanProgress, osanStageNames, type OsanProgressDetail } from './osanProgress';
import { SavedPhoto } from './OsanPhotoGallery';
import { OsanStageGuidance } from './OsanStageGuidance';
import { formatOsanDday, useKoreaDate } from './osanDday';
import logo from './assets/emi-qr-logo.png';
import './osan-qr.css';

export function OsanQrPage({ projectId, targetId, userKey }: { projectId: string; targetId?: string; userKey?: string }) {
  const [data, setData] = useState<{ project: OsanProjectDetail; progress: OsanProgressDetail }>();
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [selected, setSelected] = useState<{ targetId: string; sequence: number }>();
  const dialog = useRef<HTMLDialogElement>(null);
  const today = useKoreaDate();
  useEffect(() => {
    let alive = true;
    const controller = new AbortController();
    setData(undefined); setError(''); setSelected(undefined);
    Promise.all([getOsanProject(userKey ?? '', projectId, { signal: controller.signal }), getOsanProgress(projectId, userKey, controller.signal)])
      .then(([project, progress]) => { if (alive) { if (targetId && !progress.targets.some(target => target.targetId === targetId)) setError('삭제되었거나 찾을 수 없는 패널입니다.'); else setData({ project, progress }); } })
      .catch((e: unknown) => { if (alive) setError(e instanceof ApiError && e.status === 403 ? '이 프로젝트를 볼 권한이 없습니다.' : e instanceof ApiError && e.status === 404 ? '삭제되었거나 찾을 수 없는 프로젝트입니다.' : '프로젝트를 불러오지 못했습니다. 다시 시도해 주세요.'); });
    return () => { alive = false; controller.abort(); };
  }, [projectId, targetId, userKey, revision]);
  useEffect(() => { if (selected) dialog.current?.showModal(); else dialog.current?.close(); }, [selected]);
  const target = data?.progress.targets.find(item => item.targetId === selected?.targetId);
  const step = target?.steps.find(item => item.sequenceNumber === selected?.sequence);
  const percent = data && data.progress.totalStepCount ? Math.round(data.progress.completedStepCount / data.progress.totalStepCount * 100) : 0;
  const visibleTargets = data?.progress.targets.filter(item => !targetId || item.targetId === targetId) ?? [];
  const completedTargets = data?.progress.targets.filter(item => item.steps.length === 7 && item.steps.every(s => s.status === 'Completed')).length ?? 0;
  return <main className="osan-qr-page">
    <header className="osan-qr-appbar"><img src={logo} alt="EMI" /><span>오산 · 프로젝트 조회</span></header>
    {!data ? <section className="osan-qr-content">{error ? <><p role="alert">{error}</p><button onClick={() => setRevision(v => v + 1)}>다시 시도</button></> : <p role="status">프로젝트를 불러오는 중…</p>}</section> : <article className="osan-qr-content">
      <p className="osan-qr-eyebrow">PROJECT OVERVIEW</p>
      <div className="osan-qr-title"><div><h1>{data.project.title}</h1><p>{data.project.projectCode}</p></div><strong>{formatOsanDday(data.project.deliveryDate, today)}</strong></div>
      <span className="osan-qr-status">{data.project.status === 'Completed' ? '완료' : data.project.status === 'InProgress' ? '진행 중' : '시작 전'}</span>
      <dl className="osan-qr-facts"><div><dt>고객사</dt><dd>{data.project.customerName}</dd></div><div><dt>part 분류</dt><dd>{data.project.productName}</dd></div><div><dt>수량</dt><dd>{data.project.quantity}대</dd></div><div><dt>납기일</dt><dd>{data.project.deliveryDate}</dd></div></dl>
      <details className="osan-qr-documents"><summary>PO · W/O 정보 보기</summary><p>PO No. {data.project.poNumber || '없음'}<br />W/O No. {data.project.workOrderNumber || '없음'}</p></details>
      <section className="osan-qr-summary" aria-label="전체 진행 요약"><div><strong>전체 진행률</strong><b>{percent}<small>%</small></b></div><progress value={percent} max="100" aria-label="전체 진행률" /><footer><span>완료 단계 <b>{data.progress.completedStepCount} / {data.progress.totalStepCount}</b></span><span>완료 대상 <b>{completedTargets} / {data.progress.targets.length}대</b></span></footer></section>
      <h2>{targetId ? `${visibleTargets[0]?.displayName ?? "패널"} 진행 현황` : "대상별 진행 현황"}</h2><p className="osan-qr-hint">단계를 누르면 작업 설명과 완료 기록을 볼 수 있습니다.</p>
      <div className="osan-qr-panels">{visibleTargets.map(item => {
        const count = item.steps.filter(s => s.status === 'Completed').length;
        return <section className="osan-qr-panel" key={item.targetId}><header><strong>{item.displayName}</strong><span>{count === 7 ? '완료' : count > 0 ? '진행 중' : '시작 전'} · {count}/7단계</span></header><div className="osan-qr-stages">{osanStageNames.map((name, i) => {
          const done = item.steps.some(s => s.sequenceNumber === i + 1 && s.status === 'Completed');
          return <button key={name} className={done ? 'done' : ''} aria-haspopup="dialog" aria-label={`${item.displayName} ${name} ${done ? '완료' : '미완료'} 기록`} onClick={() => setSelected({ targetId: item.targetId, sequence: i + 1 })}><span>{done ? '✓' : i + 1}</span><b>{name}</b><small>{done ? '완료' : '미완료'}</small></button>;
        })}</div></section>;
      })}</div>
      {data.progress.targets.length === 0 && <p>진행 대상이 없습니다.</p>}
      <p className="osan-qr-legend"><span>✓ 완료</span> ○ 미완료</p>
      <footer className="osan-qr-footer"><button onClick={() => setRevision(v => v + 1)}>새로고침</button><p>로그인한 계정의 프로젝트 조회 권한으로 제공됩니다.</p></footer>
    </article>}
    <dialog className="osan-qr-dialog" ref={dialog} aria-labelledby="osan-qr-stage-title" onCancel={() => setSelected(undefined)}>
      {selected && <><header className="osan-qr-dialog-header"><div><h2 id="osan-qr-stage-title">{osanStageNames[selected.sequence - 1]}</h2><p>{data?.project.title} · {target?.displayName}</p></div><button onClick={() => setSelected(undefined)}>닫기</button></header><div className="osan-qr-dialog-body"><h3>작업 설명</h3><OsanStageGuidance stage={selected.sequence} /><h3>완료 기록</h3>{step?.status === 'Completed' ? <><div>{step.photos.map(photo => <SavedPhoto key={photo.photoId} projectId={projectId} photo={photo} userKey={userKey} />)}{step.photos.length === 0 && <p className="osan-qr-empty">등록된 완료 사진이 없습니다.</p>}</div><dl className="osan-qr-record"><div><dt>작업자</dt><dd>{step.completedByDisplayName || '기록 없음'}</dd></div><div><dt>완료 일시</dt><dd>{step.completedAtUtc ? new Date(step.completedAtUtc).toLocaleString('ko-KR', { timeZone: 'Asia/Seoul' }) : '기록 없음'}</dd></div></dl></> : <p className="osan-qr-empty">아직 완료 기록이 없습니다.</p>}</div></>}
    </dialog>
  </main>;
}
