export type DsShapeRole = 'surface' | 'control' | 'active' | 'status' | 'count' | 'warning' | 'success';

export function workflowShapeRole({
  blocked = false,
  completed = false,
  active = false,
  inProgress = false
}: {
  blocked?: boolean;
  completed?: boolean;
  active?: boolean;
  inProgress?: boolean;
}): DsShapeRole {
  if (blocked) return 'warning';
  if (completed) return 'success';
  if (active) return 'active';
  if (inProgress) return 'status';
  return 'surface';
}
