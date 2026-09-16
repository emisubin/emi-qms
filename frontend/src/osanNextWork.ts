import type { OsanProgressTarget } from './osanProgress';

export function nextOsanWork(targets: OsanProgressTarget[], targetId?: string) {
  const candidates = targetId ? targets.filter(target => target.targetId === targetId) : targets;
  // Prefer an ordinary runnable stage; an open issue remains reachable for remediation.
  for (let sequence = 1; sequence <= 7; sequence++) {
    const target = candidates.find(item => item.steps.some(step => step.sequenceNumber === sequence && step.status !== 'Completed' && !step.openIssue && step.canCompleteIndividual));
    if (target) return { targetId: target.targetId, stage: sequence };
  }
  for (let sequence = 1; sequence <= 7; sequence++) {
    const target = candidates.find(item => item.steps.some(step => step.sequenceNumber === sequence && step.openIssue));
    if (target) return { targetId: target.targetId, stage: sequence };
  }
  for (let sequence = 1; sequence <= 7; sequence++) {
    const target = candidates.find(item => item.steps.some(step => step.sequenceNumber === sequence && step.status !== 'Completed'));
    if (target) return { targetId: target.targetId, stage: sequence };
  }
  return candidates[0] ? { targetId: candidates[0].targetId, stage: 7 } : undefined;
}
