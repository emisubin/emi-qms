import type { SelectedExportScreen } from './api';

export type SelectedExportPage = {
  route: string;
  screen: SelectedExportScreen;
  selectionKey: string;
  area: 'business' | 'admin';
};

export const selectedExportPageRegistry: readonly SelectedExportPage[] = [
  { route: '/projects', screen: 'projects', selectionKey: 'projectId', area: 'business' },
  { route: '/my-work', screen: 'my-work', selectionKey: 'workItemId', area: 'business' },
  { route: '/production-planning', screen: 'production-planning', selectionKey: 'projectId', area: 'business' },
  { route: '/procurement', screen: 'procurement', selectionKey: 'projectId', area: 'business' },
  { route: '/materials/receipts', screen: 'material-receipts', selectionKey: 'itemId', area: 'business' },
  { route: '/materials/kitting', screen: 'material-kitting', selectionKey: 'panelId', area: 'business' },
  { route: '/manufacturing/work', screen: 'manufacturing', selectionKey: 'panelId', area: 'business' },
  { route: '/quality/iqc', screen: 'material-iqc', selectionKey: 'attemptId', area: 'business' },
  { route: '/quality/inspections', screen: 'quality-inspections', selectionKey: 'panelId', area: 'business' },
  { route: '/logistics', screen: 'logistics', selectionKey: 'targetId', area: 'business' },
  { route: '/pending', screen: 'pending', selectionKey: 'pendingId', area: 'business' },
  { route: '/notifications', screen: 'notifications', selectionKey: 'notificationId', area: 'business' },
  { route: '/admin/users', screen: 'admin-users', selectionKey: 'userId', area: 'admin' },
  { route: '/admin/departments', screen: 'admin-departments', selectionKey: 'departmentId', area: 'admin' },
  { route: '/admin/calendar/holidays', screen: 'admin-calendar-holidays', selectionKey: 'holidayId', area: 'admin' },
  { route: '/admin/permissions', screen: 'admin-permissions', selectionKey: 'permissionId', area: 'admin' },
  { route: '/admin/history/master-data', screen: 'admin-master-history', selectionKey: 'changeLogId', area: 'admin' },
  { route: '/admin/history/work-items', screen: 'admin-work-history', selectionKey: 'workItemId', area: 'admin' },
  { route: '/admin/system/notification-deliveries', screen: 'admin-notification-deliveries', selectionKey: 'deliveryId', area: 'admin' },
  { route: '/admin/system/notification-preference-audit', screen: 'admin-notification-preference-audit', selectionKey: 'auditEventId', area: 'admin' },
  { route: '/admin/system/work-item-escalations', screen: 'admin-work-item-escalations', selectionKey: 'escalationId', area: 'admin' }
] as const;
