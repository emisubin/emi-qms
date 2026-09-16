import type { OsanDashboardStage } from './osanDashboard';
export function OsanStepper({ stages }: { stages: OsanDashboardStage[]; individual?: boolean }) {
  return <span className="osan-status-track" role="group" aria-label="단계별 진행 상태">{stages.map(stage => {
    const issue = (stage.openIssueTargetCount ?? 0) > 0;
    const done = !issue && stage.totalTargetCount > 0 && stage.completedTargetCount === stage.totalTargetCount;
    const available = stage.availableTargetCount ?? 0;
    const next = available > 0 && stage.sequenceNumber !== 5;
    const hint = stage.sequenceNumber === 5 && !done ? '상시 가능' : next ? '진행 대기' : stage.sequenceNumber === 7 && !done ? '포장 대기' : '';
    const state = issue ? '미조치 이상' : done ? '정상 완료' : '미완료·일부 완료';
    return <span key={stage.sequenceNumber} className={`osan-status-step${next ? ' is-next' : ''}`} aria-label={`${stage.stepName} ${stage.completedTargetCount}/${stage.totalTargetCount} 완료 · ${state}${hint ? ` · ${hint}` : ''}`} title={`${stage.stepName} · ${state}${hint ? ` · ${hint}` : ''}`}>
      {hint && <b className={`osan-status-hint${next ? '' : ' is-muted'}`}>{hint}</b>}
      <i className={`osan-status-light ${issue ? 'is-issue' : done ? 'is-done' : ''}`} aria-hidden="true"/>
      <span>{stage.stepName}</span><small>{stage.completedTargetCount}/{stage.totalTargetCount}</small>
    </span>;
  })}</span>;
}
