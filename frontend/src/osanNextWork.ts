import type { OsanProgressTarget } from './osanProgress';

export function nextOsanWork(targets: OsanProgressTarget[], targetId?: string) {
  const candidates = targetId ? targets.filter(target => target.targetId === targetId) : targets;
  for (let sequence = 1; sequence <= 7; sequence++) {
    const target = candidates.find(item => item.steps.find(step => step.sequenceNumber === sequence)?.status !== 'Completed');
    if (target) return { targetId: target.targetId, stage: sequence };
  }
  return candidates[0] ? { targetId: candidates[0].targetId, stage: 7 } : undefined;
}
