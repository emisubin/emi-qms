import type { BusinessUnitAccessAdministrationUser } from './identity';

export function matchesUserAccessFilters(user: BusinessUnitAccessAdministrationUser, businessUnit: string, department: string): boolean {
  if (!businessUnit && !department) return true;
  const profiles = user.profiles.filter((profile) => profile.membershipActive);
  if (businessUnit === 'unassigned')
    return profiles.length === 0 && (!department || department === 'unassigned');
  const candidates = profiles.filter((profile) => !businessUnit || profile.businessUnitCode === businessUnit);
  if (!department) return candidates.length > 0;
  if (!businessUnit && profiles.length === 0) return department === 'unassigned';
  return candidates.some((profile) => department === 'unassigned' ? !profile.departmentCode : profile.departmentCode === department);
}
