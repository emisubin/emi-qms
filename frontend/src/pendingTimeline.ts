import type { PendingDetail } from './pending';

export type PendingTimelineEvent = {
  key: string;
  title: string;
  summary: string;
  detail: string;
  note: string;
  actor: string;
  createdAtUtc: string;
  rank: number;
};

export function buildPendingTimeline(detail: PendingDetail): PendingTimelineEvent[] {
  return [
    ...detail.history.map((event) => ({
      key: `history:${event.historyId}`,
      title: event.eventLabel,
      summary: event.fromStatusLabel && event.toStatusLabel
        ? `${event.fromStatusLabel} → ${event.toStatusLabel}`
        : event.toStatusLabel ?? '',
      detail: event.fromAssigneeDisplayName !== event.toAssigneeDisplayName && event.toAssigneeDisplayName
        ? `담당 ${event.toAssigneeDisplayName}`
        : event.reason ?? '',
      note: event.reason && event.fromAssigneeDisplayName !== event.toAssigneeDisplayName ? event.reason : '',
      actor: event.changedByDisplayName,
      createdAtUtc: event.createdAtUtc,
      rank: 0
    })),
    ...detail.comments.map((item) => ({
      key: `comment:${item.commentId}`,
      title: '코멘트',
      summary: item.body,
      detail: '',
      note: '',
      actor: item.createdByDisplayName,
      createdAtUtc: item.createdAtUtc,
      rank: 1
    }))
  ].sort((left, right) => left.createdAtUtc.localeCompare(right.createdAtUtc) || left.rank - right.rank || left.key.localeCompare(right.key));
}
