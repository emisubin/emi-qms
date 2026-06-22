export interface CurrentUser {
  developmentUserKey: string;
  displayName: string;
  department: string | null;
  roles: string[];
  permissions: string[];
  projectAccess: ProjectAccess[];
}

export interface ProjectAccess {
  projectKey: string;
  projectNumber: string;
  name: string;
}
