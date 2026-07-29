import { describe, expect, it } from 'vitest';
import { workflowShapeRole } from '../src/design-system';

describe('DESIGN-000 semantic shape roles', () => {
  it('uses workflow meaning instead of list position', () => {
    expect(workflowShapeRole({})).toBe('surface');
    expect(workflowShapeRole({ inProgress: true })).toBe('status');
    expect(workflowShapeRole({ active: true })).toBe('active');
    expect(workflowShapeRole({ completed: true })).toBe('success');
    expect(workflowShapeRole({ blocked: true })).toBe('warning');
  });

  it('keeps blocking and completed meaning ahead of selection styling', () => {
    expect(workflowShapeRole({ blocked: true, active: true })).toBe('warning');
    expect(workflowShapeRole({ completed: true, active: true })).toBe('success');
  });
});
