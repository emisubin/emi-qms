export type AuditSummary = {
  totalEvents: number;
  loginEvents: number;
  successfulChanges: number;
  failedChanges: number;
  authorizationDenials: number;
};

export type AuditCoverage = {
  coverageStartedAtUtc: string;
  completenessNotice: string;
};

export type AuditListItem = {
  eventId: string;
  source: 'Global' | 'Authorization';
  occurredAtUtc: string;
  eventType: 'Login' | 'Logout' | 'MutationSucceeded' | 'MutationFailed' | 'AuthorizationDenied';
  actorUserId: string | null;
  actorDisplayName: string;
  actorDepartmentName: string | null;
  actualActorUserId: string | null;
  actualActorDisplayName: string | null;
  domain: string;
  action: string;
  targetType: string | null;
  targetKey: string | null;
  outcome: string;
  failureReason: string | null;
  reasonSummary: string | null;
  loginCorrelationId: string | null;
  changeCount: number;
  clientIp: string | null;
  browserFamily: string | null;
  osFamily: string | null;
  appAccessOutcome: string | null;
};

export type AuditList = {
  items: AuditListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  summary: AuditSummary;
  coverage: AuditCoverage;
  fromDate: string;
  toDate: string;
};

export type AuditChange = {
  changeId: number;
  rowAction: string;
  targetType: string;
  targetKey: string;
  fieldCode: string;
  projectionKind: 'ExactScalar' | 'MetadataOnly';
  beforeValue: string | null;
  afterValue: string | null;
  beforeLength: number | null;
  afterLength: number | null;
};

export type AuditDetail = {
  event: AuditListItem;
  changes: AuditChange[];
  loginContext: {
    occurredAtUtc: string;
    clientIp: string | null;
    browserFamily: string | null;
    osFamily: string | null;
    appAccessOutcome: string;
  } | null;
  valueNotice: string;
};

export type AuditFilters = {
  from: string;
  to: string;
  domain: string;
  action: string;
  eventType: string;
  failureReason: string;
  search: string;
  page: number;
  pageSize: number;
};
