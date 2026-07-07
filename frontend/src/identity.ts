export interface CurrentUser {
  userId: string;
  developmentUserKey: string;
  displayName: string;
  email: string | null;
  authProvider: 'Dev' | 'EntraId';
  isActive: boolean;
  approvalPending: boolean;
  department: string | null;
  roles: string[];
  permissions: string[];
  projectAccess: ProjectAccess[];
  isTestUserSwitch: boolean;
  testUserKey: string | null;
  canUseAdminTestUserSwitch: boolean;
  actualUser: CurrentUserPrincipal;
  effectiveUser: CurrentUserPrincipal;
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
  roles: string[];
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
}
