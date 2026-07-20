export type NotificationPreferenceAuditSummary = {
  totalChanges: number;
  userChanges: number;
  adminChanges: number;
  turnedOffChanges: number;
};
export type NotificationPreferenceAuditItem = {
  auditEventId: string;
  occurredAtUtc: string;
  targetDisplayName: string;
  targetDepartmentName: string | null;
  targetIsActive: boolean;
  actorDisplayName: string;
  actorDepartmentName: string | null;
  actorIsActive: boolean;
  action: string;
  actionLabel: string;
  deliveryType: string;
  deliveryTypeLabel: string;
  channel: string;
  channelLabel: string;
  oldValue: boolean;
  newValue: boolean;
  changeLabel: string;
  resultingVersion: number;
};

export type NotificationPreferenceAuditList = {
  items: NotificationPreferenceAuditItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  summary: NotificationPreferenceAuditSummary;
  identityNotice: string;
  fromDate: string;
  toDate: string;
};

export type NotificationPreferenceAuditFilters = {
  from: string;
  to: string;
  action: string;
  deliveryType: string;
  search: string;
  page: number;
  pageSize: number;
};
