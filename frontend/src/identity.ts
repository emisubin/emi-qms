export interface CurrentUser {
  userId: string;
  developmentUserKey: string;
  displayName: string;
  email: string | null;
  authProvider: 'Dev' | 'EntraId';
  isActive: boolean;
  approvalPending: boolean;
  department: string | null;
  departmentName: string | null;
  profilePhotoVersion: string | null;
  roles: string[];
  permissions: string[];
  projectAccess: ProjectAccess[];
  isTestUserSwitch: boolean;
  testUserKey: string | null;
  canUseAdminTestUserSwitch: boolean;
  actualUser: CurrentUserPrincipal;
  effectiveUser: CurrentUserPrincipal;
  businessUnitAccess?: BusinessUnitAccess;
}

export type BusinessUnitCode = 'CHEONGJU' | 'OSAN';

export type BusinessUnitAccessStatus =
  | 'selected'
  | 'no_membership'
  | 'selection_required'
  | 'selection_denied'
  | 'local_profile_pending';

export interface BusinessUnitAccess {
  status: BusinessUnitAccessStatus;
  selectedBusinessUnit: BusinessUnitCode | null;
  allowedBusinessUnits: BusinessUnitCode[];
  isOverallAdministrator: boolean;
  errorCode: string | null;
}

export interface BusinessUnitAccessAdministrationResponse {
  users: BusinessUnitAccessAdministrationUser[];
  availableBusinessUnits: BusinessUnitCode[];
  businessUnits: BusinessUnitAccessAdministrationUnit[];
}

export interface BusinessUnitAccessAdministrationUser {
  userId: string;
  authProvider: 'Dev' | 'EntraId';
  displayName: string;
  email: string | null;
  memberships: BusinessUnitCode[];
  isOverallAdministrator: boolean;
  accessVersion: number;
  pendingOperationId: string | null;
  pendingOperationStatus: 'Preparing' | 'RetryRequired' | null;
  pendingFailureCode: string | null;
  pendingProfiles: UpdateBusinessUnitUserAccessProfile[];
  profiles: BusinessUnitAccessAdministrationProfile[];
}

export interface BusinessUnitAccessAdministrationProfile {
  businessUnitCode: BusinessUnitCode;
  membershipActive: boolean;
  localProfileExists: boolean;
  isActive: boolean;
  departmentId: string | null;
  departmentCode: string | null;
  departmentName: string | null;
  roles: string[];
  isDepartmentHead: boolean;
  canManage: boolean;
}

export interface BusinessUnitAccessAdministrationUnit {
  code: BusinessUnitCode;
  canManage: boolean;
  departments: AdminDepartment[];
  roles: AdminRole[];
}

export interface UpdateBusinessUnitUserAccessProfile {
  businessUnitCode: BusinessUnitCode;
  departmentId: string | null;
  roleCodes: string[];
  isActive: boolean;
  isDepartmentHead: boolean;
}

export interface BusinessUnitUserAccessUpdateResponse {
  changed: boolean;
  accessVersion: number;
  snapshot: BusinessUnitAccessAdministrationResponse;
}

export interface CurrentUserPrincipal {
  userId: string;
  developmentUserKey: string;
  displayName: string;
  email: string | null;
  authProvider: 'Dev' | 'EntraId';
  isActive: boolean;
  approvalPending: boolean;
  department: string | null;
  departmentName: string | null;
  profilePhotoVersion: string | null;
  roles: string[];
}

export interface ProfilePhotoMetadata {
  profilePhotoVersion: string;
  normalizedMime: 'image/jpeg' | 'image/png';
  byteSize: number;
}

export interface ProjectAccess {
  projectKey: string;
  projectNumber: string;
  name: string;
}

export interface AdminUsersResponse {
  users: AdminUser[];
  departments: AdminDepartment[];
  roles: AdminRole[];
}

export interface AdminUser {
  userId: string;
  developmentUserKey: string;
  displayName: string;
  email: string | null;
  authProvider: 'Dev' | 'EntraId';
  isActive: boolean;
  approvalPending: boolean;
  departmentId: string | null;
  departmentCode: string | null;
  departmentName: string | null;
  roles: string[];
  isReadOnly: boolean;
  isDepartmentHead: boolean;
  deletionRequestedAtUtc: string | null;
  scheduledHardDeleteAtUtc: string | null;
  purgeBlockedAtUtc: string | null;
  purgeBlockedReason: string | null;
  preDeleteIsActive: boolean | null;
  lifecycleStatus: string;
  lifecycleStatusLabel: string;
  scheduledHardDeleteLabel: string | null;
}

export interface AdminDepartment {
  departmentId: string;
  code: string;
  name: string;
  defaultRoleCode: string | null;
}

export interface AdminRole {
  roleId: string;
  code: string;
  name: string;
}

export interface UpdateAdminUserRequest {
  departmentId: string | null;
  roleCodes: string[];
  isActive: boolean;
  isDepartmentHead: boolean;
}
