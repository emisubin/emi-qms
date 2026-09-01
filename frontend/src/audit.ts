export type AuditSummary = {
  totalEvents: number;
  loginEvents: number;
  successfulChanges: number;
  failedChanges: number;
  authorizationDenials: number;
  siteAccessEvents: number;
};

export type AuditCoverage = {
  coverageStartedAtUtc: string;
  completenessNotice: string;
  siteAccessCoverageStartedAtUtc: string;
  siteAccessCompletenessNotice: string;
  lastActivityNotice: string;
};

export type AuditListItem = {
  eventId: string;
  source: 'Global' | 'Authorization' | 'SiteAccess';
  occurredAtUtc: string;
  eventType: 'Login' | 'Logout' | 'MutationSucceeded' | 'MutationFailed' | 'AuthorizationDenied' | 'SiteAccess';
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
  lastActivityAtUtc: string | null;
  endedAtUtc: string | null;
  siteAccessStatus: 'RecentSignal' | 'ExplicitLogout' | 'TimedOut' | null;
  menuCodes: string[];
  menuLabels: string[];
  siteAccessCoverageStartedAtUtc: string;
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
