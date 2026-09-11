import { describe, expect, it } from 'vitest';
import { matchesUserAccessFilters } from '../src/userAccessFilters';
import type { BusinessUnitAccessAdministrationUser } from '../src/identity';

const user = { profiles: [
  { businessUnitCode: 'CHEONGJU', membershipActive: true, departmentCode: 'quality' },
  { businessUnitCode: 'OSAN', membershipActive: true, departmentCode: 'sales' }
] } as BusinessUnitAccessAdministrationUser;

describe('user access filters', () => {
  it('matches both filters in the same membership, not across business units', () => {
    expect(matchesUserAccessFilters(user, 'OSAN', 'quality')).toBe(false);
    expect(matchesUserAccessFilters(user, 'OSAN', 'sales')).toBe(true);
    expect(matchesUserAccessFilters(user, '', 'quality')).toBe(true);
    expect(matchesUserAccessFilters(user, '', '')).toBe(true);
  });
  it('keeps unassigned pending accounts discoverable and excludes inactive memberships', () => {
    const pending = { profiles: [{ businessUnitCode: 'OSAN', membershipActive: false, departmentCode: 'quality' }] } as BusinessUnitAccessAdministrationUser;
    expect(matchesUserAccessFilters(pending, 'OSAN', 'quality')).toBe(false);
    expect(matchesUserAccessFilters(pending, 'unassigned', '')).toBe(true);
    expect(matchesUserAccessFilters(pending, '', 'unassigned')).toBe(true);
    const missingDepartment = { profiles: [{ businessUnitCode: 'OSAN', membershipActive: true, departmentCode: null }] } as BusinessUnitAccessAdministrationUser;
    expect(matchesUserAccessFilters(missingDepartment, 'OSAN', 'unassigned')).toBe(true);
  });
});
