import { Fragment, FormEvent, useCallback, useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useMsal } from '@azure/msal-react';
import { app as teamsApp } from '@microsoft/teams-js';
import { AdaptiveLayoutProvider, useAdaptiveLayout } from './adaptive-layout';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';
import { MobileSheet } from './MobileSheet';
import { MaterialIqcPage, MaterialReceivingPage } from './MaterialsWorkspace';
import { ManufacturingPage } from './ManufacturingPage';
import { LogisticsPage } from './LogisticsPage';
import { PanelKittingPage } from './PanelKittingPage';
import { QualityInspectionsPage } from './QualityInspectionsPage';
import { SalesSettlementPage } from './SalesSettlementPage';
import { SalesKpiPage } from './SalesKpiPage';
import { SalesBillingRequestPage } from './SalesBillingRequestPage';
import { FormTemplateManagementPage } from './FormTemplateManagementPage';
import { NotificationPreferencesPage } from './NotificationPreferencesPage';
import { NotificationPreferenceAuditPage } from './NotificationPreferenceAuditPage';
import { PanelQrManager } from './PanelQrManager';
import { QrScanLandingPage } from './QrScanLandingPage';
import { useActionFeedback, type ActionFeedbackState, type ActionFeedbackTone } from './useActionFeedback';
import type { QualityInspectionStage } from './qualityInspections';
import type { LogisticsStage } from './logistics';
import {
  ApiError,
  applyAdminCalendarHolidayExcel,
  applyPanelInformationExcel,
  applyProductionPlanningExcel,
  applyProjectExcel,
  applyProjectProductionPlanningExcel,
  applyProcurementExcel,
  bulkDeleteAdminCalendarHolidays,
  bulkDeleteAdminDepartments,
  bulkDeleteAdminUsers,
  bulkRestoreAdminCalendarHolidays,
  bulkRestoreAdminDepartments,
  bulkRestoreAdminUsers,
  changePanelCount,
  changeProjectStatus,
  acknowledgeAdminNotificationDeliveries,
  createAdminDepartment,
  createAdminCalendarHoliday,
  createProject,
  defaultDevelopmentUserKey,
  deleteProject,
  deactivateAdminCalendarHoliday,
  deactivateAdminDepartment,
  dismissAdminNotificationDeliveries,
  downloadAdminCalendarHolidayTemplate,
  downloadPanelInformationTemplate,
  downloadProductionPlanningBulkTemplate,
  downloadProductionPlanningTemplate,
  downloadProjectExcelTemplate,
  downloadProcurementDashboardTemplate,
  downloadProcurementTemplate,
  getAdminDashboard,
  getAdminCalendarHolidays,
  getAdminDepartments,
  getAdminMasterChangeLogs,
  getAdminNotificationDelivery,
  getAdminNotificationDeliveries,
  getAdminWorkItemEscalations,
  getAdminWorkItemHistory,
  getCurrentUser,
  getFormTemplateScope,
  getOwnProfilePhoto,
  getAdminUsers,
  getDeletedProject,
  getPanel,
  getPanelInformation,
  getPanelInformationHistory,
  getProject,
  getProjectWorkflow,
  getProjectProductionPlanning,
  getProjectProductionPlanningHistory,
  getProjectProcurement,
  getProjectProcurementHistory,
  getProjectSummary,
  getProductionPlanningSummary,
  getProcurementDashboard,
  getPanelKittingQueue,
  getManufacturingQueue,
  getManufacturingPanel,
  getQualityInspectionQueue,
  getQualityInspectionPanel,
  getLogisticsQueue,
  getLogisticsProjectHistory,
  getSalesSettlement,
  getBusinessCalendar,
  getMyTeamsActivityDelivery,
  getMyWorkSummary,
  getNotificationSummary,
  removeOwnProfilePhoto,
  reprocessFailedAdminNotificationDeliveries,
  getMaterialReceipts,
  getNotificationDetail,
  getReadyHealth,
  getRuntimeMode,
  getSalesOwners,
  getPermissionMatrix,
  listMyAssignedProjects,
  listProductionPlanningProjects,
  listProductionTemplateSettings,
  listProductionProductTypes,
  listProcurementRequiredItemSettings,
  listMyWorkItems,
  listNotifications,
  listDeletedProjects,
  listPanels,
  listProjects,
  previewAdminCalendarHolidayExcel,
  previewPanelInformationExcel,
  previewProductionPlanningExcel,
  previewProjectExcel,
  previewProjectProductionPlanningExcel,
  previewProcurementExcel,
  purgeAllDeletedProjects,
  purgeAdminCalendarHoliday,
  purgeAdminDepartment,
  purgeAdminUser,
  purgeDeletedProject,
  retryAdminNotificationDeliveries,
  completeMyWorkItem,
  markAllNotificationsRead,
  markProjectNotificationsRead,
  markNotificationRead,
  restoreDeletedProject,
  restoreAdminCalendarHoliday,
  restoreAdminDepartment,
  restoreAdminUser,
  scheduleAdminUserDeletion,
  sendAdminManualNotification,
  saveOwnProfilePhoto,
  startMyWorkItem,
  setAdminTestUserKey,
  setAccessTokenProvider,
  setRuntimeMutationAllowed,
  updateAdminCalendarHoliday,
  updateAdminDepartment,
  updateAdminUser,
  updateProjectProductionPlanning,
  updateProductionTemplateSettings,
  updateProcurementRequiredItemSettings,
  updateMaterialReceipts,
  updatePanelInformation,
  updateProjectProcurement,
  updateProject
} from './api';
import type { RuntimeMode } from './api';
import {
  acquireAccessToken,
  getRememberSessionPreference,
  hasMsalConfiguration,
  isEntraAuthMode,
  isInteractionRequiredAuthError,
  loginRequest,
  restoreActiveAccount
} from './auth';
import authEllipse66 from './assets/auth-ellipse-66.svg';
import authEllipse67 from './assets/auth-ellipse-67.svg';
import emiLogo from './assets/emi-logo.png';
import microsoftLogo from './assets/microsoft-logo.png';
import type { ReadyHealth } from './health';
import { HomePage } from './HomePage';
import { PendingPage } from './PendingPage';
import { PendingTypeManagementPage } from './PendingTypeManagementPage';
import type { AdminUser, AdminUsersResponse, CurrentUser } from './identity';
import { maxPanelsPerProject } from './projects';
import type {
  AdminBulkActionResponse,
  AdminCalendarHoliday,
  AdminCalendarHolidayListResponse,
  AdminDashboardEscalationLevel,
  AdminDashboardResponse,
  AdminDepartmentMaster,
  AdminDepartmentListResponse,
  AdminMasterChangeLogListResponse,
  AdminManualNotificationSendResponse,
  AdminNotificationDelivery,
  AdminNotificationDeliveryActionResponse,
  AdminNotificationDeliveryDetail,
  AdminNotificationDeliveryListResponse,
  AdminNotificationDeliveryReprocessResponse,
  AdminWorkItemEscalationListResponse,
  AdminWorkItemHistoryListResponse,
  AuditEvent,
  CalendarHolidayExcelPreviewResponse,
  DeletedProjectDetail,
  DeletedProjectListItem,
  PanelInformationExcelPreviewResponse,
  PanelInformationHistoryResponse,
  PanelInformationPanel,
  PanelInformationResponse,
  PanelInputUnit,
  PanelPlaceholder,
  PackagingMethod,
  PermissionMatrixResponse,
  ProcurementExcelPreviewResponse,
  ProcurementDashboardResponse,
  ProcurementHistoryResponse,
  ProcurementItem,
  ProcurementProjectSummary,
  ProcurementRequiredItemSettings,
  ProcurementRequiredItemSettingsRow,
  ProcurementResponse,
  ProcurementSupplyType,
  ProductionPlanningHistoryResponse,
  ProductionPlanningExcelPreviewResponse,
  ProductionPlanningProjectListResponse,
  ProductionPlanningResponse,
  ProductionPlanningSummary,
  ProductionTemplateSettings,
  ProductionTemplateSettingsStep,
  ProductionProductType,
  MyAssignedProjectsResponse,
  MyWorkItem,
  MyWorkListResponse,
  MyWorkSummary,
  NotificationItem,
  NotificationListResponse,
  NotificationSummary,
  ProjectAssignee,
  ProjectDetail,
  ProjectDashboardSummary,
  ProjectExcelPreviewResponse,
  ProjectListItem,
  ProjectListResponse,
  ProjectListTab,
  ProjectStatus,
  ProjectWorkflowResponse,
  ProjectWorkStatus,
  UpdateProcurementRequiredItemSettingsRequest,
  ProductWorkflowStage,
  ResponsibilityType,
  SalesOwner,
  BusinessCalendarDay,
  CreateAdminDepartmentRequest,
  HolidayType,
  UpdateAdminDepartmentRequest
} from './projects';

type View =
  | { kind: 'home' }
  | { kind: 'qr-scan'; token: string }
  | { kind: 'my-work' }
  | { kind: 'teams-activity' }
  | { kind: 'teams-activity-detail'; deliveryId: string }
  | { kind: 'teams-notification-detail'; notificationId: string }
  | { kind: 'list' }
  | { kind: 'create' }
  | { kind: 'detail'; projectId: string; section?: ProjectDetailSection }
  | { kind: 'sales-settlement'; projectId: string }
  | { kind: 'sales-kpi'; year?: number; currency?: string }
  | { kind: 'sales-billing' }
  | { kind: 'form-templates' }
  | { kind: 'deleted-detail'; projectId: string }
  | { kind: 'edit'; projectId: string }
  | { kind: 'panel-info-edit'; projectId: string }
  | { kind: 'production-planning-edit'; projectId: string }
  | { kind: 'production-planning-dashboard' }
  | { kind: 'production-planning-settings' }
  | { kind: 'procurement-edit'; projectId: string }
  | { kind: 'procurement-dashboard' }
  | { kind: 'procurement-settings' }
  | { kind: 'materials-receipts'; projectCode?: string }
  | { kind: 'materials-kitting'; projectId?: string; panelId?: string }
  | { kind: 'manufacturing-work'; projectId?: string; panelId?: string }
  | { kind: 'logistics'; stage?: LogisticsStage; projectId?: string; panelId?: string; unitId?: string; draftId?: string }
  | { kind: 'quality-iqc' }
  | { kind: 'quality-inspections'; stage?: QualityInspectionStage; projectId?: string; panelId?: string }
  | { kind: 'notifications' }
  | { kind: 'notification-preferences' }
  | { kind: 'pending'; projectId?: string }
  | { kind: 'pending-detail'; pendingId: string }
  | { kind: 'pending-types' }
  | { kind: 'admin-dashboard' }
  | { kind: 'admin-users' }
  | { kind: 'admin-user-notification-preferences'; userId: string }
  | { kind: 'admin-departments' }
  | { kind: 'admin-calendar-holidays' }
  | { kind: 'admin-permission-matrix' }
  | { kind: 'admin-master-change-logs' }
  | { kind: 'admin-work-history' }
  | { kind: 'admin-send-notification' }
  | { kind: 'admin-notification-deliveries'; status?: string | null; handlingStatus?: string | null; channel?: string | null; deliveryType?: string | null }
  | { kind: 'admin-notification-delivery-detail'; deliveryId: string }
  | { kind: 'admin-notification-preference-audit' }
  | { kind: 'admin-work-item-escalations'; status?: string | null; level?: string | null }
  | { kind: 'panel'; projectId: string; panelId: string };

type LoadState<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; data: T }
  | { kind: 'empty' }
  | { kind: 'forbidden'; message: string }
  | { kind: 'not-found'; message: string }
  | { kind: 'error'; message: string };

type ProjectDetailSection =
  | 'workflow'
  | 'sales'
  | 'production-planning'
  | 'panels'
  | 'procurement'
  | 'materials'
  | 'manufacturing'
  | 'quality'
  | 'logistics';

type ProjectDepartmentSection = Extract<ProjectDetailSection, 'sales' | 'materials' | 'manufacturing' | 'quality' | 'logistics'>;

type ProjectDepartmentData = {
  metrics: Array<{ label: string; value: string; tone?: StatusTone }>;
  records: ProjectDepartmentRecord[];
  canMutate: boolean;
};

type ProjectDepartmentRecord = {
  key: string;
  title: string;
  subtitle?: string;
  status: string;
  tone?: StatusTone;
  fields: Array<{ label: string; value: string }>;
  items?: Array<{ key: string; label: string; value: string; note?: string }>;
};

type ShellBadgeState = {
  requestedWorkCount: number;
  unreadNotificationCount: number;
};

type ProjectFormValues = {
  customerName: string;
  item: string;
  projectCode: string;
  projectTitle: string;
  panelCount: string;
  deliveryDate: string;
  salesOwnerUserId: string;
  packagingMethod: string;
  salesAmount: string;
  currencyCode: string;
  deliveryLocation: string;
  fatRequired: string;
  reason: string;
};

const developmentUsers = [
  'dev-sales',
  'dev-design',
  'dev-admin',
  'dev-production',
  'dev-manufacturing',
  'dev-quality',
  'dev-logistics',
  'dev-viewer',
  'dev-procurement',
  'dev-materials',
  'dev-disabled'
];
const developmentUserStorageKey = 'emi-qms-development-user-key';
const adminTestUserStorageKey = 'emi-admin-test-user-key';
const adminTestUsers = [
  'dev-admin',
  'dev-sales',
  'dev-production',
  'dev-procurement',
  'dev-materials',
  'dev-manufacturing',
  'dev-quality',
  'dev-logistics',
  'dev-viewer'
];

const emptyForm: ProjectFormValues = {
  customerName: '',
  item: '',
  projectCode: '',
  projectTitle: '',
  panelCount: '1',
  deliveryDate: '',
  salesOwnerUserId: '',
  packagingMethod: '',
  salesAmount: '',
  currencyCode: 'KRW',
  deliveryLocation: '',
  fatRequired: 'false',
  reason: ''
};

const packagingMethodOptions: Array<{ value: PackagingMethod; label: string }> = [
  { value: 'WoodenCrate', label: '목포장' },
  { value: 'StretchWrap', label: '청랩포장' },
  { value: 'HeavyDutyBox', label: '고강도박스포장' }
];

function initialViewFromLocation(): View {
  if (typeof window === 'undefined') {
    return { kind: 'home' };
  }

  const qrScanMatch = window.location.pathname.match(/^\/q\/([A-Za-z0-9_-]{43})$/);
  if (qrScanMatch?.[1]) {
    return { kind: 'qr-scan', token: qrScanMatch[1] };
  }

  if (window.location.pathname === '/' && isLikelyTeamsContext()) {
    return { kind: 'teams-activity' };
  }

  if (window.location.pathname === '/' || window.location.pathname === '/home') {
    return { kind: 'home' };
  }

  if (window.location.pathname === '/projects') {
    return { kind: 'list' };
  }

  if (window.location.pathname === '/my-work') {
    return { kind: 'my-work' };
  }

  if (window.location.pathname === '/sales/billing-requests') {
    return { kind: 'sales-billing' };
  }

  if (window.location.pathname === '/sales') {
    const params = new URLSearchParams(window.location.search);
    const year = Number(params.get('year'));
    return {
      kind: 'sales-kpi',
      year: Number.isInteger(year) && year >= 2000 && year <= 2100 ? year : undefined,
      currency: params.get('currency')?.toUpperCase() || undefined
    };
  }

  if (window.location.pathname === '/form-templates') {
    return { kind: 'form-templates' };
  }

  if (window.location.pathname.startsWith('/teams/activity/deliveries/')) {
    const deliveryId = window.location.pathname.split('/').filter(Boolean).at(-1);
    return deliveryId ? { kind: 'teams-activity-detail', deliveryId } : { kind: 'teams-activity' };
  }

  if (window.location.pathname.startsWith('/teams/activity/notifications/')) {
    const notificationId = window.location.pathname.split('/').filter(Boolean).at(-1);
    return notificationId ? { kind: 'teams-notification-detail', notificationId } : { kind: 'teams-activity' };
  }

  if (window.location.pathname === '/teams/activity' || window.location.pathname === '/teams/notifications') {
    const notificationId = extractNotificationIdFromTeamsActivityLocation();
    if (notificationId) {
      return { kind: 'teams-notification-detail', notificationId };
    }

    return { kind: 'teams-activity' };
  }

  if (window.location.pathname === '/notifications') {
    return { kind: 'notifications' };
  }

  if (window.location.pathname === '/notification-settings') {
    return { kind: 'notification-preferences' };
  }

  if (window.location.pathname === '/pending') {
    return { kind: 'pending', projectId: new URLSearchParams(window.location.search).get('projectId') ?? undefined };
  }

  if (window.location.pathname === '/admin/pending-types') {
    return { kind: 'pending-types' };
  }

  const pendingMatch = window.location.pathname.match(/^\/pending\/([^/]+)$/);
  if (pendingMatch?.[1]) {
    return { kind: 'pending-detail', pendingId: pendingMatch[1] };
  }

  if (window.location.pathname === '/admin') {
    return { kind: 'admin-dashboard' };
  }

  if (window.location.pathname === '/admin/users') {
    return { kind: 'admin-users' };
  }

  const adminNotificationPreferencesMatch = window.location.pathname.match(/^\/admin\/users\/([^/]+)\/notification-settings$/);
  if (adminNotificationPreferencesMatch?.[1]) {
    return { kind: 'admin-user-notification-preferences', userId: adminNotificationPreferencesMatch[1] };
  }

  if (window.location.pathname === '/admin/departments' || window.location.pathname === '/admin/master-data/departments') {
    return { kind: 'admin-departments' };
  }

  if (window.location.pathname === '/admin/calendar/holidays') {
    return { kind: 'admin-calendar-holidays' };
  }

  if (window.location.pathname === '/admin/permissions') {
    return { kind: 'admin-permission-matrix' };
  }

  if (window.location.pathname === '/admin/history/master-data') {
    return { kind: 'admin-master-change-logs' };
  }

  if (window.location.pathname === '/admin/history/work-items') {
    return { kind: 'admin-work-history' };
  }

  if (window.location.pathname === '/admin/system/send-notification') {
    return { kind: 'admin-send-notification' };
  }

  if (window.location.pathname.startsWith('/admin/system/notification-deliveries/')) {
    const deliveryId = window.location.pathname.split('/').filter(Boolean).at(-1);
    return deliveryId
      ? { kind: 'admin-notification-delivery-detail', deliveryId }
      : { kind: 'admin-notification-deliveries' };
  }

  if (window.location.pathname === '/admin/system/notification-deliveries') {
    const params = new URLSearchParams(window.location.search);
    return {
      kind: 'admin-notification-deliveries',
      status: params.get('status'),
      handlingStatus: params.get('handlingStatus'),
      channel: params.get('channel'),
      deliveryType: params.get('deliveryType')
    };
  }

  if (window.location.pathname === '/admin/system/notification-preference-audit') {
    return { kind: 'admin-notification-preference-audit' };
  }

  if (window.location.pathname === '/admin/system/work-item-escalations') {
    const params = new URLSearchParams(window.location.search);
    return { kind: 'admin-work-item-escalations', status: params.get('status'), level: params.get('level') };
  }

  const panelInformationEditMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/panel-information\/edit$/);
  if (panelInformationEditMatch?.[1]) {
    return { kind: 'panel-info-edit', projectId: panelInformationEditMatch[1] };
  }

  const procurementEditMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/procurement\/edit$/);
  if (procurementEditMatch?.[1]) {
    return { kind: 'procurement-edit', projectId: procurementEditMatch[1] };
  }

  const productionPlanningEditMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/production-planning\/edit$/);
  if (productionPlanningEditMatch?.[1]) {
    return { kind: 'production-planning-edit', projectId: productionPlanningEditMatch[1] };
  }

  if (window.location.pathname === '/materials/receipts') {
    return { kind: 'materials-receipts', projectCode: new URLSearchParams(window.location.search).get('project') ?? undefined };
  }

  if (window.location.pathname === '/materials/kitting') {
    const params = new URLSearchParams(window.location.search);
    return {
      kind: 'materials-kitting',
      projectId: params.get('project') ?? undefined,
      panelId: params.get('panel') ?? undefined
    };
  }

  if (window.location.pathname === '/manufacturing/work') {
    const params = new URLSearchParams(window.location.search);
    return {
      kind: 'manufacturing-work',
      projectId: params.get('project') ?? undefined,
      panelId: params.get('panel') ?? undefined
    };
  }

  if (window.location.pathname === '/logistics') {
    const params = new URLSearchParams(window.location.search);
    return {
      kind: 'logistics',
      stage: logisticsStageFromQuery(params.get('stage')),
      projectId: params.get('project') ?? undefined,
      panelId: params.get('panel') ?? undefined,
      unitId: params.get('unit') ?? undefined,
      draftId: params.get('draft') ?? undefined
    };
  }

  if (window.location.pathname === '/quality/iqc') {
    return { kind: 'quality-iqc' };
  }

  if (window.location.pathname === '/quality/inspections') {
    const params = new URLSearchParams(window.location.search);
    return {
      kind: 'quality-inspections',
      stage: qualityStageFromQuery(params.get('stage')),
      projectId: params.get('project') ?? undefined,
      panelId: params.get('panel') ?? undefined
    };
  }

  if (window.location.pathname === '/production-planning') {
    return { kind: 'production-planning-dashboard' };
  }

  if (window.location.pathname === '/production-planning/settings') {
    return { kind: 'production-planning-settings' };
  }

  if (window.location.pathname === '/procurement') {
    return { kind: 'procurement-dashboard' };
  }

  if (window.location.pathname === '/procurement/settings') {
    return { kind: 'procurement-settings' };
  }

  const panelMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/panels\/([^/]+)$/);
  if (panelMatch?.[1] && panelMatch?.[2]) {
    return { kind: 'panel', projectId: panelMatch[1], panelId: panelMatch[2] };
  }

  const settlementMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/settlement$/);
  if (settlementMatch?.[1]) {
    return { kind: 'sales-settlement', projectId: settlementMatch[1] };
  }

  const detailMatch = window.location.pathname.match(/^\/projects\/([^/]+)$/);
  if (detailMatch?.[1]) {
    const section = new URLSearchParams(window.location.search).get('section');
    return {
      kind: 'detail',
      projectId: detailMatch[1],
      section: parseProjectDetailSection(section)
    };
  }

  return { kind: 'home' };
}

function isLikelyTeamsContext() {
  if (typeof window === 'undefined') {
    return false;
  }

  try {
    if (window.self !== window.top) {
      return true;
    }
  } catch {
    return true;
  }

  return /teams\.microsoft\.com|teams\.live\.com/iu.test(document.referrer);
}

function extractNotificationIdFromTeamsActivityLocation() {
  if (typeof window === 'undefined') {
    return null;
  }

  const params = new URLSearchParams(window.location.search);
  const notificationId = normalizeNotificationId(params.get('notificationId'));
  if (notificationId) {
    return notificationId;
  }

  const subEntityId = normalizeNotificationSubEntityId(params.get('subEntityId') ?? params.get('subPageId'));
  if (subEntityId) {
    return subEntityId;
  }

  const context = params.get('context');
  if (!context) {
    return null;
  }

  try {
    return extractNotificationIdFromTeamsContext(JSON.parse(context));
  } catch {
    return null;
  }
}

async function resolveTeamsContextNotificationId() {
  if (!isLikelyTeamsContext()) {
    return null;
  }

  try {
    await teamsApp.initialize();
    return extractNotificationIdFromTeamsContext(await teamsApp.getContext());
  } catch {
    return null;
  }
}

function extractNotificationIdFromTeamsContext(context: unknown) {
  const candidates = [
    readNestedString(context, ['page', 'subPageId']),
    readNestedString(context, ['page', 'subEntityId']),
    readNestedString(context, ['subPageId']),
    readNestedString(context, ['subEntityId'])
  ];

  for (const candidate of candidates) {
    const notificationId = normalizeNotificationSubEntityId(candidate);
    if (notificationId) {
      return notificationId;
    }
  }

  return null;
}

function readNestedString(value: unknown, path: string[]) {
  let current = value;
  for (const segment of path) {
    if (!current || typeof current !== 'object' || !(segment in current)) {
      return null;
    }

    current = (current as Record<string, unknown>)[segment];
  }

  return typeof current === 'string' ? current : null;
}

function normalizeNotificationSubEntityId(value: string | null) {
  if (!value) {
    return null;
  }

  const prefix = 'notification:';
  const trimmed = value.trim();
  if (!trimmed.toLowerCase().startsWith(prefix)) {
    return normalizeNotificationId(trimmed);
  }

  return normalizeNotificationId(trimmed.slice(prefix.length));
}

function normalizeNotificationId(value: string | null) {
  if (!value) {
    return null;
  }

  const trimmed = value.trim();
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu.test(trimmed)
    ? trimmed
    : null;
}

function viewFromProjectLink(projectId: string, linkUrl?: string | null): View {
  if (!linkUrl) {
    return { kind: 'detail', projectId };
  }

  try {
    const url = new URL(linkUrl, window.location.origin);
    if (url.pathname === '/materials/receipts') {
      return { kind: 'detail', projectId, section: 'workflow' };
    }

    if (url.pathname === '/materials/kitting') {
      return {
        kind: 'materials-kitting',
        projectId: url.searchParams.get('project') ?? projectId,
        panelId: url.searchParams.get('panel') ?? undefined
      };
    }

    if (url.pathname === '/manufacturing/work') {
      return {
        kind: 'manufacturing-work',
        projectId: url.searchParams.get('project') ?? projectId,
        panelId: url.searchParams.get('panel') ?? undefined
      };
    }

    if (url.pathname === '/quality/inspections') {
      return {
        kind: 'quality-inspections',
        stage: qualityStageFromQuery(url.searchParams.get('stage')),
        projectId: url.searchParams.get('project') ?? projectId,
        panelId: url.searchParams.get('panel') ?? undefined
      };
    }

    if (url.pathname === '/logistics') {
      return {
        kind: 'logistics',
        stage: logisticsStageFromQuery(url.searchParams.get('stage')),
        projectId: url.searchParams.get('project') ?? projectId,
        panelId: url.searchParams.get('panel') ?? undefined,
        unitId: url.searchParams.get('unit') ?? undefined,
        draftId: url.searchParams.get('draft') ?? undefined
      };
    }

    const pendingMatch = url.pathname.match(/^\/pending\/([^/]+)$/);
    if (pendingMatch?.[1]) {
      return { kind: 'pending-detail', pendingId: pendingMatch[1] };
    }

    const productionPlanningEditMatch = url.pathname.match(/^\/projects\/([^/]+)\/production-planning\/edit$/);
    if (productionPlanningEditMatch?.[1]) {
      return { kind: 'production-planning-edit', projectId: productionPlanningEditMatch[1] };
    }

    const panelInformationEditMatch = url.pathname.match(/^\/projects\/([^/]+)\/panel-information\/edit$/);
    if (panelInformationEditMatch?.[1]) {
      return { kind: 'panel-info-edit', projectId: panelInformationEditMatch[1] };
    }

    const procurementEditMatch = url.pathname.match(/^\/projects\/([^/]+)\/procurement\/edit$/);
    if (procurementEditMatch?.[1]) {
      return { kind: 'procurement-edit', projectId: procurementEditMatch[1] };
    }

    const settlementMatch = url.pathname.match(/^\/projects\/([^/]+)\/settlement$/);
    if (settlementMatch?.[1]) {
      return { kind: 'sales-settlement', projectId: settlementMatch[1] };
    }

    const section = sectionFromQuery(url.searchParams.get('section'));
    return { kind: 'detail', projectId, section };
  } catch {
    return { kind: 'detail', projectId };
  }
}

function sectionFromQuery(value: string | null): ProjectDetailSection | undefined {
  if (value === 'workflow' || value === 'sales' || value === 'production-planning' || value === 'procurement'
    || value === 'panels' || value === 'materials' || value === 'manufacturing' || value === 'quality' || value === 'logistics') {
    return value;
  }

  return undefined;
}

function qualityStageFromQuery(value: string | null): QualityInspectionStage | undefined {
  return value === 'LQC' || value === 'OQC' || value === 'CustomerInspection' || value === 'FAT'
    ? value
    : undefined;
}

function logisticsStageFromQuery(value: string | null): LogisticsStage | undefined {
  return value === 'packing' || value === 'departure' || value === 'delivery' ? value : undefined;
}

function viewForHomeDestination(destinationKey: string): View {
  switch (destinationKey) {
    case 'my-work': return { kind: 'my-work' };
    case 'production-planning': return { kind: 'production-planning-dashboard' };
    case 'procurement': return { kind: 'procurement-dashboard' };
    case 'materials-receipts': return { kind: 'materials-receipts' };
    case 'materials-kitting': return { kind: 'materials-kitting' };
    case 'manufacturing': return { kind: 'manufacturing-work' };
    case 'quality': return { kind: 'quality-inspections', stage: 'LQC' };
    case 'pending': return { kind: 'pending' };
    case 'logistics-packing': return { kind: 'logistics', stage: 'packing' };
    case 'logistics-departure': return { kind: 'logistics', stage: 'departure' };
    case 'logistics-delivery': return { kind: 'logistics', stage: 'delivery' };
    case 'admin-users': return { kind: 'admin-users' };
    case 'admin-deliveries': return { kind: 'admin-notification-deliveries', status: 'Failed' };
    case 'admin-dashboard': return { kind: 'admin-dashboard' };
    default: return { kind: 'list' };
  }
}

function pathForView(view: View) {
  switch (view.kind) {
    case 'home':
      return '/';
    case 'qr-scan':
      return `/q/${view.token}`;
    case 'my-work':
      return '/my-work';
    case 'teams-activity':
      return '/teams/activity';
    case 'teams-activity-detail':
      return `/teams/activity/deliveries/${view.deliveryId}`;
    case 'teams-notification-detail':
      return `/teams/activity/notifications/${view.notificationId}`;
    case 'detail':
      return `/projects/${view.projectId}${view.section && view.section !== 'panels' ? `?section=${view.section}` : ''}`;
    case 'sales-settlement':
      return `/projects/${view.projectId}/settlement`;
    case 'sales-kpi': {
      const params = new URLSearchParams();
      if (view.year) params.set('year', String(view.year));
      if (view.currency) params.set('currency', view.currency);
      const query = params.toString();
      return `/sales${query ? `?${query}` : ''}`;
    }
    case 'sales-billing':
      return '/sales/billing-requests';
    case 'form-templates':
      return '/form-templates';
    case 'panel-info-edit':
      return `/projects/${view.projectId}/panel-information/edit`;
    case 'procurement-edit':
      return `/projects/${view.projectId}/procurement/edit`;
    case 'production-planning-edit':
      return `/projects/${view.projectId}/production-planning/edit`;
    case 'production-planning-dashboard':
      return '/production-planning';
    case 'production-planning-settings':
      return '/production-planning/settings';
    case 'materials-receipts':
      return `/materials/receipts${view.projectCode ? `?project=${encodeURIComponent(view.projectCode)}` : ''}`;
    case 'materials-kitting': {
      const params = new URLSearchParams();
      if (view.projectId) params.set('project', view.projectId);
      if (view.panelId) params.set('panel', view.panelId);
      const query = params.toString();
      return `/materials/kitting${query ? `?${query}` : ''}`;
    }
    case 'manufacturing-work': {
      const params = new URLSearchParams();
      if (view.projectId) params.set('project', view.projectId);
      if (view.panelId) params.set('panel', view.panelId);
      const query = params.toString();
      return `/manufacturing/work${query ? `?${query}` : ''}`;
    }
    case 'logistics': {
      const params = new URLSearchParams();
      if (view.stage) params.set('stage', view.stage);
      if (view.projectId) params.set('project', view.projectId);
      if (view.panelId) params.set('panel', view.panelId);
      if (view.unitId) params.set('unit', view.unitId);
      if (view.draftId) params.set('draft', view.draftId);
      const query = params.toString();
      return `/logistics${query ? `?${query}` : ''}`;
    }
    case 'quality-iqc':
      return '/quality/iqc';
    case 'quality-inspections': {
      const params = new URLSearchParams();
      if (view.stage) params.set('stage', view.stage);
      if (view.projectId) params.set('project', view.projectId);
      if (view.panelId) params.set('panel', view.panelId);
      const query = params.toString();
      return `/quality/inspections${query ? `?${query}` : ''}`;
    }
    case 'procurement-dashboard':
      return '/procurement';
    case 'procurement-settings':
      return '/procurement/settings';
    case 'notifications':
      return '/notifications';
    case 'notification-preferences':
      return '/notification-settings';
    case 'pending':
      return `/pending${view.projectId ? `?projectId=${encodeURIComponent(view.projectId)}` : ''}`;
    case 'pending-detail':
      return `/pending/${view.pendingId}`;
    case 'pending-types':
      return '/admin/pending-types';
    case 'admin-dashboard':
      return '/admin';
    case 'admin-users':
      return '/admin/users';
    case 'admin-user-notification-preferences':
      return `/admin/users/${view.userId}/notification-settings`;
    case 'admin-departments':
      return '/admin/departments';
    case 'admin-calendar-holidays':
      return '/admin/calendar/holidays';
    case 'admin-permission-matrix':
      return '/admin/permissions';
    case 'admin-master-change-logs':
      return '/admin/history/master-data';
    case 'admin-work-history':
      return '/admin/history/work-items';
    case 'admin-send-notification':
      return '/admin/system/send-notification';
    case 'admin-notification-deliveries':
      return `/admin/system/notification-deliveries${queryString({
        status: view.status ?? undefined,
        handlingStatus: view.handlingStatus ?? undefined,
        channel: view.channel ?? undefined,
        deliveryType: view.deliveryType ?? undefined
      })}`;
    case 'admin-notification-delivery-detail':
      return `/admin/system/notification-deliveries/${view.deliveryId}`;
    case 'admin-notification-preference-audit':
      return '/admin/system/notification-preference-audit';
    case 'admin-work-item-escalations':
      return `/admin/system/work-item-escalations${queryString({
        status: view.status ?? undefined,
        level: view.level ?? undefined
      })}`;
    case 'panel':
      return `/projects/${view.projectId}/panels/${view.panelId}`;
    case 'list':
      return '/projects';
    default:
      return '/';
  }
}

function queryString(values: Record<string, string | undefined>) {
  const params = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value) {
      params.set(key, value);
    }
  });
  const text = params.toString();
  return text ? `?${text}` : '';
}

function parseProjectDetailSection(value: string | null): ProjectDetailSection {
  return sectionFromQuery(value) ?? 'panels';
}

export function App({
  rememberSession = true,
  onRememberSessionChange
}: {
  rememberSession?: boolean;
  onRememberSessionChange?: (rememberSession: boolean) => void;
}) {
  return isEntraAuthMode
    ? (
      <EntraAuthenticatedApp
        rememberSession={rememberSession}
        onRememberSessionChange={onRememberSessionChange}
      />
    )
    : <QmsAppShell authMode="Dev" />;
}

function EntraAuthenticatedApp({
  rememberSession,
  onRememberSessionChange
}: {
  rememberSession: boolean;
  onRememberSessionChange?: (rememberSession: boolean) => void;
}) {
  const { instance, accounts, inProgress } = useMsal();
  const [authGate, setAuthGate] = useState<EntraAuthGateState>({ kind: 'loading' });
  const accountCacheKey = accounts.map((account) => account.homeAccountId).join('|');
  const accountSnapshotRef = useRef(accounts);

  useEffect(() => {
    accountSnapshotRef.current = accounts;
  }, [accountCacheKey, accounts]);

  const clearTestUserSwitch = () => {
    setAdminTestUserKey(null);
    if (typeof window !== 'undefined') {
      window.localStorage.removeItem(adminTestUserStorageKey);
    }
  };

  const login = () => {
    clearTestUserSwitch();
    void instance.loginRedirect({
      ...loginRequest,
      redirectStartPage: typeof window === 'undefined' ? undefined : window.location.href
    });
  };

  const logout = () => {
    clearTestUserSwitch();
    instance.setActiveAccount(null);
    setAccessTokenProvider(null);
    void instance.logoutRedirect();
  };

  useEffect(() => {
    if (!hasMsalConfiguration()) {
      setAccessTokenProvider(null);
      return;
    }

    if (inProgress !== 'none') {
      setAccessTokenProvider(null);
      return;
    }

    let cancelled = false;
    setAccessTokenProvider(null);

    void (async () => {
      setAuthGate({ kind: 'loading' });

      const restoredAccount = restoreActiveAccount(instance, accountSnapshotRef.current);
      if (restoredAccount.kind === 'none') {
        setAuthGate({ kind: 'login' });
        return;
      }

      if (restoredAccount.kind === 'multiple') {
        setAuthGate({
          kind: 'reauth-required',
          message: '로그인 계정을 선택해야 합니다. Microsoft 365로 다시 로그인해 주세요.'
        });
        return;
      }

      const account = restoredAccount.account;

      try {
        const accessToken = await acquireAccessToken(instance, account);
        if (cancelled) {
          return;
        }

        if (!accessToken) {
          setAccessTokenProvider(null);
          setAuthGate({
            kind: 'reauth-required',
            message: '로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.'
          });
          return;
        }

        setAccessTokenProvider(() => acquireAccessToken(instance, account));
        setAuthGate({ kind: 'ready' });
      } catch (error: unknown) {
        if (cancelled) {
          return;
        }

        setAccessTokenProvider(null);
        if (isInteractionRequiredAuthError(error)) {
          setAuthGate({
            kind: 'reauth-required',
            message: '로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.'
          });
          return;
        }

        setAuthGate({
          kind: 'error',
          message: 'Microsoft 365 인증 정보를 확인할 수 없습니다. 다시 로그인해 주세요.'
        });
      }
    })();

    return () => {
      cancelled = true;
      setAccessTokenProvider(null);
    };
  }, [accountCacheKey, inProgress, instance]);

  if (!hasMsalConfiguration()) {
    return (
      <AuthGateMessage
        state="configuration"
        title="Microsoft 로그인 설정이 필요합니다."
        message="운영 인증 모드에는 Tenant ID, Client ID, API Scope 설정이 필요합니다. 환경변수를 확인해 주세요."
      />
    );
  }

  if (inProgress !== 'none') {
    return (
      <AuthLoginScreen
        loading
        rememberSession={rememberSession}
      />
    );
  }

  if (authGate.kind === 'loading') {
    return (
      <AuthLoginScreen
        loading
        rememberSession={rememberSession}
      />
    );
  }

  if (authGate.kind === 'reauth-required' || authGate.kind === 'error') {
    return (
      <AuthGateMessage
        state={authGate.kind === 'reauth-required' ? 'reauth' : 'error'}
        title={authGate.kind === 'reauth-required' ? '다시 로그인이 필요합니다.' : '인증 정보를 확인할 수 없습니다.'}
        message={authGate.message}
        actionLabel={inProgress === 'none' ? 'Microsoft 365로 다시 로그인' : '로그인 진행 중'}
        actionDisabled={inProgress !== 'none'}
        onAction={login}
      />
    );
  }

  if (authGate.kind === 'login') {
    return (
      <AuthLoginScreen
        rememberSession={rememberSession}
        onRememberSessionChange={onRememberSessionChange}
        onLogin={login}
      />
    );
  }

  return (
    <QmsAppShell
      authMode="EntraId"
      onLogout={logout}
      onReauthenticate={login}
    />
  );
}

type EntraAuthGateState =
  | { kind: 'loading' }
  | { kind: 'login' }
  | { kind: 'ready' }
  | { kind: 'reauth-required'; message: string }
  | { kind: 'error'; message: string };

const reviewSafeActionPattern = /(저장|수정|삭제|복구|발송|재시도|확인 처리|제외 처리|작업 시작|업무 시작|작업 완료|업무 완료|취소|반영|적용|업로드|등록|추가|신규|승인|비활성|활성화|동기화|일괄|가져오기|읽음 처리|retry|acknowledge|dismiss|import|upload)/i;
const reviewSafeDisabledReason = '검수 전용 읽기 모드에서는 변경 작업을 수행할 수 없습니다.';

function ReviewSafeControlGuard({ mutationAllowed }: { mutationAllowed: boolean }) {
  useEffect(() => {
    const apply = () => {
      const controls = document.querySelectorAll<HTMLButtonElement | HTMLInputElement>('button, input[type="submit"], input[type="file"]');
      controls.forEach((control) => {
        const isNavigation = control.getAttribute('role') === 'tab'
          || control.hasAttribute('aria-current')
          || control.classList.contains('app-nav-button');
        const label = `${control.textContent ?? ''} ${control.getAttribute('aria-label') ?? ''} ${control.getAttribute('title') ?? ''}`.trim();
        const isMutation = control instanceof HTMLInputElement && control.type === 'file'
          || (!isNavigation && reviewSafeActionPattern.test(label));

        if (!isMutation) {
          return;
        }

        if (!mutationAllowed) {
          if (!control.hasAttribute('data-review-safe-original-disabled')) {
            control.setAttribute('data-review-safe-original-disabled', control.disabled ? 'true' : 'false');
          }
          control.disabled = true;
          control.setAttribute('aria-disabled', 'true');
          control.setAttribute('data-review-safe-disabled', 'true');
          control.title = reviewSafeDisabledReason;
          return;
        }

        const originalDisabled = control.getAttribute('data-review-safe-original-disabled');
        if (originalDisabled !== null) {
          control.disabled = originalDisabled === 'true';
          control.removeAttribute('data-review-safe-original-disabled');
          control.removeAttribute('data-review-safe-disabled');
          control.removeAttribute('aria-disabled');
          if (control.title === reviewSafeDisabledReason) {
            control.removeAttribute('title');
          }
        }
      });
    };

    apply();
    const observer = new MutationObserver(apply);
    observer.observe(document.body, { childList: true, subtree: true, characterData: true });
    return () => observer.disconnect();
  }, [mutationAllowed]);

  return null;
}

type QmsAppShellProps = {
  authMode: 'Dev' | 'EntraId';
  onLogout?: () => void;
  onReauthenticate?: () => void;
};

function QmsAppShell(props: QmsAppShellProps) {
  return (
    <AdaptiveLayoutProvider>
      <QmsAppShellContent {...props} />
    </AdaptiveLayoutProvider>
  );
}

function QmsAppShellContent({
  authMode,
  onLogout,
  onReauthenticate
}: QmsAppShellProps) {
  const layout = useAdaptiveLayout();
  const isDevMode = authMode === 'Dev';
  const [developmentUserKey, setDevelopmentUserKey] = useState(() => {
    if (!isDevMode) {
      return '';
    }

    if (typeof window === 'undefined') {
      return defaultDevelopmentUserKey ?? 'dev-sales';
    }

    const stored = window.localStorage.getItem(developmentUserStorageKey);
    return stored && developmentUsers.includes(stored)
      ? stored
      : defaultDevelopmentUserKey ?? 'dev-sales';
  });
  const [view, setViewState] = useState<View>(() => initialViewFromLocation());
  const [health, setHealth] = useState<LoadState<ReadyHealth>>({ kind: 'loading' });
  const [runtimeMode, setRuntimeMode] = useState<LoadState<RuntimeMode>>({ kind: 'loading' });
  const [currentUser, setCurrentUser] = useState<LoadState<CurrentUser>>({ kind: 'loading' });
  const [shellBadges, setShellBadges] = useState<ShellBadgeState>({ requestedWorkCount: 0, unreadNotificationCount: 0 });
  const [adminTestUserKey, setAdminTestUserKeyState] = useState('');
  const [mobileStatusOpen, setMobileStatusOpen] = useState(false);
  const [profilePhotoState, setProfilePhotoState] = useState<{ key: string; url: string | null } | null>(null);
  const [profilePhotoNonce, setProfilePhotoNonce] = useState(0);
  const [formTemplateScope, setFormTemplateScope] = useState<{ canManage: boolean; isSystemAdministrator: boolean; domains: string[] } | null>(null);
  const [projectActionFeedback, setProjectActionFeedback] = useState<{
    projectId: string;
    feedback: ActionFeedbackState;
  } | null>(null);
  const mobileStatusTriggerRef = useRef<HTMLButtonElement>(null);
  const profilePhotoGeneration = useRef(0);
  const restoredAdminTestUser = useRef(false);
  const user = currentUser.kind === 'ready' ? currentUser.data : null;
  const isAccessBlocked = isOperationalAccessBlocked(user);
  const canLoadBusinessData = isDevMode || (currentUser.kind === 'ready' && !isAccessBlocked);
  const displayedShellBadges = canLoadBusinessData
    ? shellBadges
    : { requestedWorkCount: 0, unreadNotificationCount: 0 };
  const canUseAdminTestUserSwitch = !isDevMode && user?.canUseAdminTestUserSwitch === true;
  const actualProfileUserId = user?.actualUser.userId ?? '';
  const actualProfilePhotoVersion = user?.actualUser.profilePhotoVersion ?? '';
  const profilePhotoKey = `${actualProfileUserId}:${actualProfilePhotoVersion}:${profilePhotoNonce}`;
  const profilePhotoUrl = profilePhotoState?.key === profilePhotoKey ? profilePhotoState.url : null;
  const formTemplateScopeUserId = currentUser.kind === 'ready' ? currentUser.data.effectiveUser.userId : '';
  const formTemplateScopeBlocked = currentUser.kind !== 'ready' || currentUser.data.approvalPending;

  useEffect(() => {
    let cancelled = false;
    queueMicrotask(() => {
      if (cancelled) return;
      if (formTemplateScopeBlocked) {
        setFormTemplateScope(null);
        return;
      }
      getFormTemplateScope(developmentUserKey)
        .then((scope) => { if (!cancelled) setFormTemplateScope(scope); })
        .catch(() => { if (!cancelled) setFormTemplateScope(null); });
    });
    return () => { cancelled = true; };
  }, [developmentUserKey, formTemplateScopeBlocked, formTemplateScopeUserId]);

  useEffect(() => {
    setAdminTestUserKey(isDevMode ? null : adminTestUserKey);
  }, [adminTestUserKey, isDevMode]);

  useEffect(() => {
    if (!layout.isMobile) {
      queueMicrotask(() => setMobileStatusOpen(false));
    }
  }, [layout.isMobile]);

  const setView = useCallback((nextView: View) => {
    setViewState(nextView);
    if (typeof window === 'undefined') {
      return;
    }

    const nextPath = pathForView(nextView);
    if (`${window.location.pathname}${window.location.search}` !== nextPath) {
      window.history.pushState(null, '', nextPath);
    }
  }, []);

  const returnToProjectWithFeedback = useCallback((
    projectId: string,
    section: ProjectDetailSection,
    feedback: ActionFeedbackState
  ) => {
    setProjectActionFeedback({ projectId, feedback });
    setView({ kind: 'detail', projectId, section });
  }, [setView]);

  const loadShell = useCallback(() => {
    setRuntimeMutationAllowed(false);
    getRuntimeMode()
      .then((data) => {
        setRuntimeMutationAllowed(data.mutationAllowed);
        setRuntimeMode({ kind: 'ready', data });
      })
      .catch((error: unknown) => {
        setRuntimeMutationAllowed(false);
        setRuntimeMode(toLoadError(error, '실행 모드를 확인할 수 없어 변경 작업을 차단했습니다.'));
      });

    getReadyHealth()
      .then((data) => setHealth({ kind: 'ready', data }))
      .catch((error: unknown) => setHealth(toLoadError(error, 'API 상태를 확인할 수 없습니다.')));

    getCurrentUser(developmentUserKey)
      .then((data) => {
        setCurrentUser({ kind: 'ready', data });
        if (!isDevMode && !adminTestUserKey && !restoredAdminTestUser.current && data.canUseAdminTestUserSwitch) {
          restoredAdminTestUser.current = true;
          const stored = window.localStorage.getItem(adminTestUserStorageKey);
          if (stored && adminTestUsers.includes(stored)) {
            setAdminTestUserKeyState(stored);
          }
        }
      })
      .catch((error: unknown) => setCurrentUser(toAuthenticationLoadError(error, isDevMode)));
  }, [adminTestUserKey, developmentUserKey, isDevMode]);

  const refreshShellBadges = useCallback(() => {
    Promise.all([
      getMyWorkSummary(developmentUserKey),
      getNotificationSummary(developmentUserKey)
    ])
      .then(([workSummary, notificationSummary]) => {
        setShellBadges({
          requestedWorkCount: workSummary.requestedCount,
          unreadNotificationCount: notificationSummary.unreadCount
        });
      })
      .catch(() => {
        setShellBadges({ requestedWorkCount: 0, unreadNotificationCount: 0 });
      });
  }, [developmentUserKey]);

  useEffect(() => {
    loadShell();
  }, [loadShell]);

  useEffect(() => {
    const generation = ++profilePhotoGeneration.current;
    let createdUrl: string | null = null;
    if (!actualProfileUserId || !actualProfilePhotoVersion) return undefined;

    void getOwnProfilePhoto(developmentUserKey)
      .then((blob) => {
        if (generation !== profilePhotoGeneration.current) return;
        createdUrl = blob ? URL.createObjectURL(blob) : null;
        setProfilePhotoState((current) => {
          if (current?.url) URL.revokeObjectURL(current.url);
          return { key: profilePhotoKey, url: createdUrl };
        });
      })
      .catch(() => undefined);

    return () => {
      profilePhotoGeneration.current += 1;
      if (createdUrl) URL.revokeObjectURL(createdUrl);
    };
  }, [
    actualProfileUserId,
    actualProfilePhotoVersion,
    developmentUserKey,
    profilePhotoKey
  ]);

  useEffect(() => {
    if (!canLoadBusinessData) {
      return;
    }

    refreshShellBadges();
  }, [adminTestUserKey, canLoadBusinessData, refreshShellBadges]);

  useEffect(() => {
    const handlePopState = () => {
      setViewState(initialViewFromLocation());
    };

    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  useEffect(() => {
    if (typeof window === 'undefined' || view.kind !== 'teams-notification-detail') {
      return;
    }

    if (window.location.pathname === '/teams/activity' || window.location.pathname === '/teams/notifications') {
      window.history.replaceState(null, '', pathForView(view));
    }
  }, [view]);

  useEffect(() => {
    if (view.kind !== 'teams-activity') {
      return undefined;
    }

    const notificationIdFromUrl = extractNotificationIdFromTeamsActivityLocation();
    let cancelled = false;
    if (notificationIdFromUrl) {
      queueMicrotask(() => {
        if (!cancelled) {
          setView({ kind: 'teams-notification-detail', notificationId: notificationIdFromUrl });
        }
      });

      return () => {
        cancelled = true;
      };
    }

    resolveTeamsContextNotificationId()
      .then((notificationId) => {
        if (!cancelled && notificationId) {
          setView({ kind: 'teams-notification-detail', notificationId });
        }
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, [setView, view.kind]);

  if (!isDevMode
    && (view.kind === 'teams-activity'
      || view.kind === 'teams-activity-detail'
      || view.kind === 'teams-notification-detail')
    && currentUser.kind !== 'ready') {
    return (
      <TeamsActivityAuthFallback
        state={currentUser}
        onRetry={loadShell}
        onLogin={onReauthenticate}
        onLogout={onLogout}
      />
    );
  }

  if (!isDevMode && currentUser.kind === 'loading') {
    return (
      <AuthLoginScreen
        loading
        rememberSession={getRememberSessionPreference()}
      />
    );
  }

  if (!isDevMode && currentUser.kind !== 'ready') {
    if (isAuthenticationExpiredState(currentUser)) {
      return (
        <AuthGateMessage
          state="reauth"
          title="다시 로그인이 필요합니다."
          message={loadStateMessage(currentUser) ?? '로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.'}
          actionLabel="Microsoft 365로 다시 로그인"
          onAction={onReauthenticate}
        />
      );
    }

    return (
      <AuthenticationRequiredPage
        message={loadStateMessage(currentUser)}
        onLogout={onLogout}
      />
    );
  }

  if (!isDevMode && isAccessBlocked) {
    return (
      <AuthenticationRequiredPage
        user={user}
        onLogout={onLogout}
      />
    );
  }

  const permissions = user?.permissions ?? [];
  const mutationEnabled = runtimeMode.kind === 'ready' && runtimeMode.data.mutationAllowed;
  const canCreate = permissions.includes('Project.Create');
  const canUpdate = permissions.includes('Project.Update');
  const canHold = permissions.includes('Project.Hold');
  const canCancel = permissions.includes('Project.Cancel');
  const canDelete = permissions.includes('Project.Delete');
  const canReadDeleted = permissions.includes('Project.Deleted.Read');
  const canReadSalesAmount = permissions.includes('Project.SalesAmount.Read');
  const canUpdatePanelInfo = permissions.includes('PanelInfo.Update');
  const canReadAuditAll = permissions.includes('Audit.Read.All');
  const canPurgeDeletedProjects = canReadAuditAll;
  const canUpdateProcurement = permissions.includes('ProcurementPlan.Update');
  const canUpdateMaterialReceipt = permissions.includes('MaterialReceipt.Update');
  const canUpdateManufacturing = permissions.includes('manufacturing.update');
  const canShipLogistics = permissions.includes('logistics.ship');
  const canInspectQuality = permissions.includes('quality.inspect');
  const canUpdateProductionPlanning = permissions.includes('ProductionPlan.Update');
  const canManageUsers = permissions.includes('users.manage');
  const canReadAdminHistory = permissions.includes('admin-history.read');
  const canReadPending = permissions.includes('Pending.Read');
  const canManagePending = permissions.includes('Pending.Manage');
  const canManagePendingTypes = permissions.includes('PendingType.Manage');
  const canSettleSales = permissions.includes('sales.settle');
  const canManageSalesTargets = permissions.includes('Sales.Target.Manage');
  const isSystemAdministrator = user?.roles.includes('system-administrator') ?? false;
  const canUseAdminPages = canManageUsers || canReadAdminHistory || isSystemAdministrator;
  const canBrowseOperationalPages = permissions.includes('projects.read');
  const canReadPendingWorkspace = canReadPending || canBrowseOperationalPages;
  const materialsHomeView: View = { kind: 'materials-receipts' };
  const switchDevelopmentUser = (nextUserKey: string) => {
    window.localStorage.setItem(developmentUserStorageKey, nextUserKey);
    setDevelopmentUserKey(nextUserKey);
    setView(view.kind === 'home' ? { kind: 'home' } : { kind: 'list' });
  };
  const switchAdminTestUser = (nextUserKey: string) => {
    if (nextUserKey) {
      window.localStorage.setItem(adminTestUserStorageKey, nextUserKey);
    } else {
      window.localStorage.removeItem(adminTestUserStorageKey);
    }
    setAdminTestUserKeyState(nextUserKey);
    setView(view.kind === 'home' ? { kind: 'home' } : { kind: 'list' });
  };
  const resetAdminTestUser = () => {
    window.localStorage.removeItem(adminTestUserStorageKey);
    setAdminTestUserKeyState('');
    setView({ kind: 'home' });
  };
  const navigationItems: NavigationItem[] = [
    { label: '홈', view: { kind: 'home' }, active: view.kind === 'home' },
    { label: '내 업무', view: { kind: 'my-work' }, active: view.kind === 'my-work', badge: displayedShellBadges.requestedWorkCount },
    { label: '프로젝트', view: { kind: 'list' }, active: isProjectWorkspace(view) },
    { label: 'Pending', view: { kind: 'pending' }, active: view.kind === 'pending' || view.kind === 'pending-detail' },
    { label: '생산관리', view: { kind: 'production-planning-dashboard' }, active: isProductionPlanningWorkspace(view) },
    { label: '구매', view: { kind: 'procurement-dashboard' }, active: isProcurementWorkspace(view) },
    { label: '자재', view: materialsHomeView, active: view.kind === 'materials-receipts' || view.kind === 'materials-kitting' },
    { label: '제조', view: { kind: 'manufacturing-work' }, active: view.kind === 'manufacturing-work' },
    { label: '품질', view: { kind: 'quality-inspections', stage: 'LQC' }, active: view.kind === 'quality-iqc' || view.kind === 'quality-inspections' },
    { label: '물류', view: { kind: 'logistics', stage: 'packing' }, active: view.kind === 'logistics' },
    ...(canReadSalesAmount ? [
      { label: '영업', view: { kind: 'sales-kpi' } as View, active: view.kind === 'sales-kpi' || view.kind === 'sales-billing' }
    ] : []),
    ...(formTemplateScope?.canManage ? [
      { label: '양식 관리', view: { kind: 'form-templates' } as View, active: view.kind === 'form-templates' }
    ] : []),
    ...(canManagePendingTypes ? [
      { label: 'Pending 유형', view: { kind: 'pending-types' } as View, active: view.kind === 'pending-types' }
    ] : []),
    {
      label: '알림',
      view: { kind: 'notifications' },
      active: view.kind === 'notifications' || view.kind === 'notification-preferences',
      badge: displayedShellBadges.unreadNotificationCount
    },
    ...(canUseAdminPages ? [
      { label: '관리자', view: { kind: 'admin-dashboard' } as View, active: isAdminWorkspace(view) }
    ] : [])
  ];

  const activeNavigationLabel = navigationItems.find((item) => item.active)?.label ?? '업무';
  const shellSwitchControls = (
    <ShellSwitchControls
      isDevMode={isDevMode}
      canUseAdminTestUserSwitch={canUseAdminTestUserSwitch}
      isTestUserSwitch={user?.isTestUserSwitch === true}
      developmentUserKey={developmentUserKey}
      adminTestUserKey={adminTestUserKey}
      onDevelopmentUserChange={switchDevelopmentUser}
      onAdminTestUserChange={switchAdminTestUser}
      onResetAdminTestUser={resetAdminTestUser}
    />
  );

  return (
    <main
      className="app-shell"
      data-layout-mode={layout.mode}
      data-touch-optimized={layout.touchOptimized}
    >
      <AppNavigation items={navigationItems} onNavigate={setView} footer={shellSwitchControls} />

      <div className="app-content">
        <ReviewSafeControlGuard mutationAllowed={mutationEnabled} />
        <header className="mobile-app-bar">
          <AppMobileNavigation items={navigationItems} onNavigate={setView} footer={shellSwitchControls} />
          <div className="mobile-app-brand">
            <img src={emiLogo} alt="" aria-hidden="true" />
            <span>
              <small>EMI PROJECT</small>
              <strong>{activeNavigationLabel}</strong>
            </span>
          </div>
          <button
            ref={mobileStatusTriggerRef}
            type="button"
            className="mobile-account-trigger"
            aria-label="내 계정 열기"
            aria-expanded={mobileStatusOpen}
            onClick={() => setMobileStatusOpen(true)}
          >
            <ProfileAvatar displayName={user?.actualUser.displayName ?? '사용자'} photoUrl={profilePhotoUrl} compact />
          </button>
        </header>

        <MobileSheet
          open={mobileStatusOpen}
          title="내 계정"
          eyebrow="ACCOUNT"
          description="로그인 정보와 프로필 사진을 확인하고 계정 작업을 실행합니다."
          onClose={() => setMobileStatusOpen(false)}
          triggerRef={mobileStatusTriggerRef}
        >
          {user ? (
            <AccountProfilePanel
              user={user}
              developmentUserKey={developmentUserKey}
              profilePhotoUrl={profilePhotoUrl}
              mutationAllowed={mutationEnabled}
              onPhotoChanged={() => setProfilePhotoNonce((value) => value + 1)}
              onLogout={onLogout}
              mobile
            />
          ) : null}
          <details className="mobile-system-details">
            <summary>연결 상태</summary>
            <div className="mobile-status-grid" aria-label="모바일 시스템 상태">
            <StatusChip label="API" value={health.kind === 'ready' ? health.data.status : health.kind} />
            <StatusChip label="Database" value={health.kind === 'ready' ? health.data.database.reason : '-'} />
            <StatusChip label="User" value={currentUser.kind === 'ready' ? currentUser.data.displayName : currentUser.kind} />
            </div>
          </details>
          {runtimeMode.kind === 'ready' && runtimeMode.data.reviewSafe ? (
            <div className="mobile-status-note" data-tone="warning">
              <strong>검수 전용 읽기 모드</strong>
              <span>조회·검색·필터만 가능하며 변경 action은 차단됩니다.</span>
            </div>
          ) : null}
          {currentUser.kind === 'ready' && !currentUser.data.approvalPending ? (
            <button
              type="button"
              className="mobile-status-action"
              onClick={() => {
                setView({ kind: 'notification-preferences' });
                setMobileStatusOpen(false);
              }}
            >
              <span>알림 설정</span>
              <small>Teams 개인 알림과 일일 요약 받기 선택</small>
            </button>
          ) : null}
        </MobileSheet>

        <header className="topbar">
          <div>
            <p className="eyebrow">PROJECT OPERATIONS</p>
            <h1>EMI 프로젝트 통합관리시스템</h1>
          </div>
          <div className="topbar-actions">
            {user ? (
              <DesktopAccountMenu
                user={user}
                developmentUserKey={developmentUserKey}
                profilePhotoUrl={profilePhotoUrl}
                mutationAllowed={mutationEnabled}
                onPhotoChanged={() => setProfilePhotoNonce((value) => value + 1)}
                onLogout={onLogout}
              />
            ) : null}
          </div>
        </header>

        {runtimeMode.kind === 'ready' && runtimeMode.data.reviewSafe ? (
          <div className="review-safe-banner" role="status">
            <strong>검수 전용 읽기 모드</strong>
            <span>조회·검색·필터만 가능하며 저장, 삭제, 발송 및 상태 변경은 차단되어 있습니다.</span>
            {runtimeMode.data.migrationLedgerStatus === 'CompatibleWithApprovedLegacy' ? (
              <span>
                Migration 이력은 현재 repository와 호환됩니다. Canonical {runtimeMode.data.expectedMigrationCount}개,
                Live {runtimeMode.data.actualMigrationCount ?? '-'}개이며 승인된 과거 marker {runtimeMode.data.approvedLegacyMigrations.length}건이 보존되어 있습니다.
              </span>
            ) : runtimeMode.data.migrationLedgerStatus === 'Exact' ? (
              <span>Migration 이력은 현재 repository와 정확히 일치합니다.</span>
            ) : (
              <span className="review-safe-ledger-warning">Migration 이력이 현재 repository와 일치하지 않아 검수 준비 상태가 아닙니다.</span>
            )}
          </div>
        ) : null}

        {runtimeMode.kind === 'loading' ? (
          <div className="review-safe-banner review-safe-banner--checking" role="status">
            실행 모드 확인 중 — 확인이 끝날 때까지 변경 작업을 차단합니다.
          </div>
        ) : null}

        {runtimeMode.kind === 'error' || runtimeMode.kind === 'forbidden' || runtimeMode.kind === 'not-found' ? (
          <div className="review-safe-banner review-safe-banner--error" role="alert">
            실행 모드를 확인할 수 없어 변경 작업을 차단했습니다. 조회는 가능하지만 저장·삭제·발송은 사용할 수 없습니다.
          </div>
        ) : null}

        {!isDevMode && user?.isTestUserSwitch ? (
          <div className="test-user-banner" role="status">
            검수 모드: {labelForDevelopmentUser(user.testUserKey ?? adminTestUserKey)} 권한으로 보는 중
            <button
              type="button"
              onClick={() => {
                window.localStorage.removeItem(adminTestUserStorageKey);
                setAdminTestUserKeyState('');
                setView({ kind: 'home' });
              }}
            >
              실제 계정으로 보기
            </button>
          </div>
        ) : null}

        <details className="system-status-disclosure">
          <summary>
            <span aria-hidden="true" />
            개발 연결 상태
          </summary>
          <section className="system-strip" aria-label="시스템 상태">
            <StatusChip label="API" value={health.kind === 'ready' ? health.data.status : health.kind} />
            <StatusChip
              label="Database"
              value={health.kind === 'ready' ? health.data.database.reason : '-'}
            />
            <StatusChip
              label="User"
              value={currentUser.kind === 'ready' ? currentUser.data.displayName : currentUser.kind}
            />
          </section>
        </details>

      {currentUser.kind === 'forbidden' || currentUser.kind === 'not-found' || currentUser.kind === 'error' ? (
        <StateMessage state={currentUser} />
      ) : null}

      {currentUser.kind === 'ready' && currentUser.data.approvalPending ? (
        <ApprovalPendingPage user={currentUser.data} onLogout={onLogout} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'home' ? (
        <HomePage
          developmentUserKey={developmentUserKey}
          requestContextKey={currentUser.data.effectiveUser?.userId ?? currentUser.data.userId}
          effectiveDisplayName={currentUser.data.effectiveUser.displayName}
          effectiveDepartmentCode={currentUser.data.effectiveUser.department}
          effectiveDepartmentName={currentUser.data.effectiveUser.departmentName}
          canReadPending={canReadPendingWorkspace}
          canReadSalesAmount={canReadSalesAmount}
          onOpenMyWork={() => setView({ kind: 'my-work' })}
          onOpenProjects={() => setView({ kind: 'list' })}
          onOpenProject={(projectId) => setView({ kind: 'detail', projectId })}
          onOpenPending={() => setView({ kind: 'pending' })}
          onOpenNotifications={() => setView({ kind: 'notifications' })}
          onOpenSalesKpi={(year, currency) => setView({ kind: 'sales-kpi', year, currency })}
          onOpenDepartmentMetric={(destinationKey) => setView(viewForHomeDestination(destinationKey))}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'qr-scan' ? (
        <QrScanLandingPage
          key={view.token}
          developmentUserKey={developmentUserKey}
          token={view.token}
          onOpenPath={(path) => {
            window.history.pushState(null, '', path);
            setViewState(initialViewFromLocation());
          }}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'sales-kpi' ? (
        <SalesKpiPage
          developmentUserKey={developmentUserKey}
          initialYear={view.year}
          initialCurrency={view.currency}
          canManageTargets={canManageSalesTargets}
          onOpenProject={(projectId) => setView({ kind: 'sales-settlement', projectId })}
          onOpenBilling={() => setView({ kind: 'sales-billing' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'sales-billing' ? (
        <SalesBillingRequestPage
          developmentUserKey={developmentUserKey}
          onOpenSalesKpi={() => setView({ kind: 'sales-kpi' })}
          onOpenProject={(projectId) => setView({ kind: 'sales-settlement', projectId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'form-templates' ? (
        <FormTemplateManagementPage developmentUserKey={developmentUserKey} isSystemAdministrator={isSystemAdministrator} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'pending-types' ? (
        <PendingTypeManagementPage developmentUserKey={developmentUserKey} canManage={canManagePendingTypes} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'my-work' ? (
        <MyWorkPage
          developmentUserKey={developmentUserKey}
          onOpenProject={(projectId, linkUrl) => setView(viewFromProjectLink(projectId, linkUrl))}
          onBadgeRefresh={refreshShellBadges}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'list' ? (
        <ProjectListPage
          developmentUserKey={developmentUserKey}
          canCreate={canCreate}
            canReadDeleted={canReadDeleted}
            canReadSalesAmount={canReadSalesAmount}
            canPurgeDeletedProjects={canPurgeDeletedProjects}
            onCreate={() => setView({ kind: 'create' })}
          onOpen={(projectId) => setView({ kind: 'detail', projectId })}
          onOpenPending={(projectId) => setView({ kind: 'pending', projectId })}
          onOpenDeleted={(projectId) => setView({ kind: 'deleted-detail', projectId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && (view.kind === 'pending' || view.kind === 'pending-detail') ? (
        <PendingPage
          developmentUserKey={developmentUserKey}
          pendingId={view.kind === 'pending-detail' ? view.pendingId : undefined}
          initialProjectId={view.kind === 'pending' ? view.projectId : undefined}
          canManage={canManagePending}
          onOpenPending={(pendingId) => setView({ kind: 'pending-detail', pendingId })}
          onBackToList={() => setView({ kind: 'pending' })}
          onOpenProject={(projectId) => setView({ kind: 'detail', projectId })}
          onBadgeRefresh={refreshShellBadges}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'create' ? (
        <ProjectCreatePage
          developmentUserKey={developmentUserKey}
          onCancel={() => setView({ kind: 'list' })}
          onCreated={(projectId) => setView({ kind: 'detail', projectId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'detail' ? (
        <>
          {projectActionFeedback?.projectId === view.projectId ? (
            <section className="page-action-feedback route-action-feedback" aria-label="최근 저장 결과">
              <ActionFeedback
                message={projectActionFeedback.feedback.message}
                tone={projectActionFeedback.feedback.tone}
                focusOnAttention={projectActionFeedback.feedback.tone === 'partial' || projectActionFeedback.feedback.tone === 'error'}
              />
              <button type="button" onClick={() => setProjectActionFeedback(null)}>확인</button>
            </section>
          ) : null}
          <ProjectDetailPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canUpdate={canUpdate}
          canHold={canHold}
          canCancel={canCancel}
          canDelete={canDelete}
          canReadSalesAmount={canReadSalesAmount}
          canUpdatePanelInfo={canUpdatePanelInfo}
          canReadAuditAll={canReadAuditAll}
          canUpdateProcurement={canUpdateProcurement}
          canUpdateProductionPlanning={canUpdateProductionPlanning}
          canUpdateMaterialReceipt={canUpdateMaterialReceipt}
          canUpdateManufacturing={canUpdateManufacturing}
          canInspectQuality={canInspectQuality}
          canShipLogistics={canShipLogistics}
          isSystemAdministrator={isSystemAdministrator}
          initialSection={view.section ?? 'panels'}
          onBack={() => setView({ kind: 'list' })}
          onEdit={() => setView({ kind: 'edit', projectId: view.projectId })}
          onEditPanelInformation={() => setView({ kind: 'panel-info-edit', projectId: view.projectId })}
          onEditProductionPlanning={() => setView({ kind: 'production-planning-edit', projectId: view.projectId })}
          onEditProcurement={() => setView({ kind: 'procurement-edit', projectId: view.projectId })}
          onOpenPanel={(panelId) => setView({ kind: 'panel', projectId: view.projectId, panelId })}
          onOpenPending={() => setView({ kind: 'pending', projectId: view.projectId })}
          onOpenSettlement={() => setView({ kind: 'sales-settlement', projectId: view.projectId })}
          onOpenDepartmentWorkspace={(section, projectCode) => {
            if (section === 'sales') setView({ kind: 'sales-settlement', projectId: view.projectId });
            if (section === 'materials') setView({ kind: 'materials-receipts', projectCode });
            if (section === 'manufacturing') setView({ kind: 'manufacturing-work', projectId: view.projectId });
            if (section === 'quality') setView({ kind: 'quality-inspections', stage: 'LQC', projectId: view.projectId });
            if (section === 'logistics') setView({ kind: 'logistics', projectId: view.projectId });
          }}
          onLoadOutcome={(loaded) => {
            if (loaded) return;
            setProjectActionFeedback((current) => current?.projectId === view.projectId && current.feedback.tone === 'success'
              ? {
                  ...current,
                  feedback: {
                    tone: 'partial',
                    message: `${current.feedback.message} 최신 화면을 불러오지 못했습니다. 새로고침해 주세요.`
                  }
                }
              : current);
          }}
          canSettleSales={canSettleSales}
          />
        </>
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'sales-settlement' ? (
        <SalesSettlementPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          onBack={() => setView({ kind: 'detail', projectId: view.projectId, section: 'workflow' })}
          onOpenPending={() => setView({ kind: 'pending', projectId: view.projectId })}
          onOpenBilling={() => setView({ kind: 'sales-billing' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'deleted-detail' ? (
        <DeletedProjectDetailPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canReadSalesAmount={canReadSalesAmount}
          canPurgeDeletedProjects={canPurgeDeletedProjects}
          onBack={() => setView({ kind: 'list' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'edit' ? (
        <ProjectEditPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          onCancel={() => setView({ kind: 'detail', projectId: view.projectId })}
          onSaved={() => setView({ kind: 'detail', projectId: view.projectId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'panel-info-edit' ? (
        <PanelInformationEditPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canUpdatePanelInfo={canUpdatePanelInfo}
          onBack={() => setView({ kind: 'detail', projectId: view.projectId })}
          onSaved={(feedback) => returnToProjectWithFeedback(view.projectId, 'panels', feedback)}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'procurement-edit' ? (
        <ProcurementEditPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canUpdateProcurement={canUpdateProcurement}
          onBack={() => setView({ kind: 'detail', projectId: view.projectId, section: 'procurement' })}
          onSaved={(feedback) => returnToProjectWithFeedback(view.projectId, 'procurement', feedback)}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'production-planning-edit' ? (
        <ProductionPlanningEditPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canUpdateProductionPlanning={canUpdateProductionPlanning}
          onBack={() => setView({ kind: 'detail', projectId: view.projectId, section: 'production-planning' })}
          onSaved={(feedback) => returnToProjectWithFeedback(view.projectId, 'production-planning', feedback)}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'production-planning-dashboard' ? (
        <ProductionPlanningDashboardPage
          developmentUserKey={developmentUserKey}
          canUpdateProductionPlanning={canUpdateProductionPlanning}
          onBack={() => setView({ kind: 'list' })}
          onOpenSettings={() => setView({ kind: 'production-planning-settings' })}
          onOpenProject={(projectId) => setView({ kind: 'detail', projectId, section: 'production-planning' })}
          onEditProject={(projectId) => setView({ kind: 'production-planning-edit', projectId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'production-planning-settings' ? (
        <ProductionPlanningSettingsPage
          developmentUserKey={developmentUserKey}
          canUpdateProductionPlanning={canUpdateProductionPlanning}
          onBack={() => setView({ kind: 'production-planning-dashboard' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'procurement-dashboard' ? (
        <ProcurementDashboardPage
          developmentUserKey={developmentUserKey}
          canUpdateProcurement={canUpdateProcurement}
          onBack={() => setView({ kind: 'list' })}
          onOpenSettings={() => setView({ kind: 'procurement-settings' })}
          onOpenProject={(projectId) => setView({ kind: 'detail', projectId, section: 'procurement' })}
          onEditProject={(projectId) => setView({ kind: 'procurement-edit', projectId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'procurement-settings' ? (
        <ProcurementRequiredItemSettingsPage
          developmentUserKey={developmentUserKey}
          canUpdateProcurement={canUpdateProcurement}
          onBack={() => setView({ kind: 'procurement-dashboard' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'materials-receipts' ? (
        <MaterialReceivingPage
          developmentUserKey={developmentUserKey}
          canUpdate={canUpdateMaterialReceipt}
          initialProjectCode={view.projectCode}
          onBack={() => setView({ kind: 'list' })}
          onOpenIqc={() => setView({ kind: 'quality-iqc' })}
          onOpenKitting={() => setView({ kind: 'materials-kitting' })}
          onOpenPending={(pendingId) => setView({ kind: 'pending-detail', pendingId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'materials-kitting' ? (
        <PanelKittingPage
          developmentUserKey={developmentUserKey}
          canComplete={canUpdateMaterialReceipt}
          initialProjectId={view.projectId}
          initialPanelId={view.panelId}
          onBack={() => setView({ kind: 'list' })}
          onOpenReceipts={() => setView({ kind: 'materials-receipts' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'manufacturing-work' ? (
        <ManufacturingPage
          developmentUserKey={developmentUserKey}
          canMutate={canUpdateManufacturing}
          initialProjectId={view.projectId}
          initialPanelId={view.panelId}
          onBack={() => setView({ kind: 'list' })}
          onOpenPending={(pendingId) => setView({ kind: 'pending-detail', pendingId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'quality-iqc' ? (
        <MaterialIqcPage
          developmentUserKey={developmentUserKey}
          canInspect={canInspectQuality}
          onBack={() => setView({ kind: 'materials-receipts' })}
          onOpenPending={(pendingId) => setView({ kind: 'pending-detail', pendingId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'quality-inspections' ? (
        <QualityInspectionsPage
          developmentUserKey={developmentUserKey}
          canInspect={canInspectQuality}
          initialStage={view.stage}
          initialProjectId={view.projectId}
          initialPanelId={view.panelId}
          onOpenIqc={() => setView({ kind: 'quality-iqc' })}
          onBack={() => setView({ kind: 'list' })}
          onOpenPending={(pendingId) => setView({ kind: 'pending-detail', pendingId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'logistics' ? (
        <LogisticsPage
          developmentUserKey={developmentUserKey}
          canMutate={canShipLogistics}
          initialStage={view.stage}
          initialProjectId={view.projectId}
          initialPanelId={view.panelId}
          initialUnitId={view.unitId}
          initialDraftId={view.draftId}
          onLocationChange={(stage, draftId) => setView({
            kind: 'logistics',
            stage,
            projectId: view.projectId,
            draftId
          })}
          onBack={() => setView({ kind: 'list' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'notifications' ? (
        <NotificationsPage
          developmentUserKey={developmentUserKey}
          onOpenPreferences={() => setView({ kind: 'notification-preferences' })}
          onOpenNotification={(notificationId) => setView({ kind: 'teams-notification-detail', notificationId })}
          onOpenProject={(projectId, linkUrl) => setView(viewFromProjectLink(projectId, linkUrl))}
          onBadgeRefresh={refreshShellBadges}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'notification-preferences' ? (
        <NotificationPreferencesPage
          developmentUserKey={developmentUserKey}
          onBack={() => setView({ kind: 'notifications' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'teams-activity' ? (
        <TeamsActivityPage
          developmentUserKey={developmentUserKey}
          onOpenProject={(projectId, linkUrl) => setView(viewFromProjectLink(projectId, linkUrl))}
          onOpenNotification={(notificationId) => setView({ kind: 'teams-notification-detail', notificationId })}
          onOpenMyWork={() => setView({ kind: 'my-work' })}
          onOpenHome={() => setView({ kind: 'home' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'teams-activity-detail' ? (
        <TeamsActivityDeliveryDetailPage
          developmentUserKey={developmentUserKey}
          deliveryId={view.deliveryId}
          onBack={() => setView({ kind: 'teams-activity' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'teams-notification-detail' ? (
        <TeamsActivityNotificationDetailPage
          developmentUserKey={developmentUserKey}
          notificationId={view.notificationId}
          onBack={() => setView({ kind: 'teams-activity' })}
          onOpenProject={(projectId, linkUrl) => setView(viewFromProjectLink(projectId, linkUrl))}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-dashboard' ? (
        <AdminDashboardPage
          developmentUserKey={developmentUserKey}
          canManageUsers={canManageUsers}
          canReadAdminHistory={canReadAdminHistory}
          onNavigate={setView}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-users' ? (
        <AdminUsersPage
          developmentUserKey={developmentUserKey}
          onOpenNotificationSettings={(userId) => setView({ kind: 'admin-user-notification-preferences', userId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-user-notification-preferences' ? (
        <NotificationPreferencesPage
          developmentUserKey={developmentUserKey}
          targetUserId={view.userId}
          onBack={() => setView({ kind: 'admin-users' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-departments' ? (
        <AdminDepartmentsPage developmentUserKey={developmentUserKey} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-calendar-holidays' ? (
        <AdminCalendarHolidaysPage developmentUserKey={developmentUserKey} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-permission-matrix' ? (
        <AdminPermissionMatrixPage developmentUserKey={developmentUserKey} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-master-change-logs' ? (
        <AdminMasterChangeLogsPage developmentUserKey={developmentUserKey} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-work-history' ? (
        <AdminWorkHistoryPage developmentUserKey={developmentUserKey} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-send-notification' ? (
        <AdminManualNotificationPage
          developmentUserKey={developmentUserKey}
          onOpenDeliveries={() => setView({ kind: 'admin-notification-deliveries', deliveryType: 'ManualTest' })}
          onBack={() => {
            if (typeof window !== 'undefined' && window.history.length > 1) {
              window.history.back();
              return;
            }
            setView({ kind: 'admin-dashboard' });
          }}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-notification-deliveries' ? (
        <AdminNotificationDeliveriesPage
          developmentUserKey={developmentUserKey}
          statusFilter={view.status ?? null}
          handlingStatusFilter={view.handlingStatus ?? null}
          channelFilter={view.channel ?? null}
          deliveryTypeFilter={view.deliveryType ?? null}
          onOpenDetail={(deliveryId) => setView({ kind: 'admin-notification-delivery-detail', deliveryId })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-notification-delivery-detail' ? (
        <AdminNotificationDeliveryDetailPage
          developmentUserKey={developmentUserKey}
          deliveryId={view.deliveryId}
          onBack={() => setView({ kind: 'admin-notification-deliveries' })}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-notification-preference-audit' ? (
        <NotificationPreferenceAuditPage developmentUserKey={developmentUserKey} />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'admin-work-item-escalations' ? (
        <AdminWorkItemEscalationsPage
          developmentUserKey={developmentUserKey}
          statusFilter={view.status ?? null}
          levelFilter={view.level ?? null}
        />
      ) : null}

      {currentUser.kind === 'ready' && !currentUser.data.approvalPending && view.kind === 'panel' ? (
        <PanelPlaceholderDetailPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          panelId={view.panelId}
          onBack={() => setView({ kind: 'detail', projectId: view.projectId })}
        />
      ) : null}
      </div>
    </main>
  );
}

type NavigationItem = {
  label: string;
  view: View;
  active: boolean;
  badge?: number;
};

const mobileNavigationHints: Record<string, string> = {
  '홈': '오늘의 우선 업무',
  '내 업무': '내 처리 항목',
  '프로젝트': '진행 현황과 병목',
  'Pending': '차단·조치 이슈',
  '생산관리': '생산계획과 일정',
  '구매': '발주와 입고예정',
  '자재': '입고와 IQC 요청',
  '제조': '패널 시작·체크·완료',
  '품질': '검사·재검사 현황',
  '물류': '포장·출발·납품',
  '알림': '업무 소식',
  '관리자': '시스템 운영'
};

function AppNavigation({
  items,
  onNavigate,
  footer
}: {
  items: NavigationItem[];
  onNavigate: (view: View) => void;
  footer?: ReactNode;
}) {
  return (
    <aside className="app-sidebar" role="navigation" aria-label="공통 메뉴">
      <div className="app-brand-lockup">
        <img src={emiLogo} alt="EMI Electric Modular Innovation" />
        <span>PROJECT OPERATIONS</span>
      </div>
      <div className="app-sidebar-heading">
        <p className="eyebrow">WORKSPACE</p>
        <strong>업무 메뉴</strong>
      </div>
      <div className="app-nav">
        {items.map((item) => (
          <button
            key={item.label}
            type="button"
            className={item.active ? 'app-nav-button active' : 'app-nav-button'}
            aria-current={item.active ? 'page' : undefined}
            onClick={() => onNavigate(item.view)}
          >
            <span className="app-nav-label"><NavigationIcon label={item.label} /><span>{item.label}</span></span>
            {item.badge && item.badge > 0 ? <span className="nav-badge" aria-hidden="true">{formatBadgeCount(item.badge)}</span> : null}
          </button>
        ))}
      </div>
      {footer ? <footer className="app-sidebar-footer">{footer}</footer> : null}
    </aside>
  );
}

function AppMobileNavigation({
  items,
  onNavigate,
  footer
}: {
  items: NavigationItem[];
  onNavigate: (view: View) => void;
  footer?: ReactNode;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuTriggerRef = useRef<HTMLButtonElement>(null);
  const drawerRef = useRef<HTMLElement>(null);
  const firstMenuItemRef = useRef<HTMLButtonElement>(null);

  const closeMenu = useCallback((restoreFocus = true) => {
    setMenuOpen(false);
    if (restoreFocus) {
      window.setTimeout(() => menuTriggerRef.current?.focus(), 0);
    }
  }, []);

  useEffect(() => {
    if (!isMobile) {
      queueMicrotask(() => setMenuOpen(false));
    }
  }, [isMobile]);

  useEffect(() => {
    if (!menuOpen) {
      return undefined;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const focusTimer = window.setTimeout(() => firstMenuItemRef.current?.focus(), 0);

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault();
        closeMenu();
        return;
      }

      if (event.key !== 'Tab' || !drawerRef.current) {
        return;
      }

      const focusable = Array.from(drawerRef.current.querySelectorAll<HTMLElement>(
        'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'
      ));
      if (focusable.length === 0) {
        event.preventDefault();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      window.clearTimeout(focusTimer);
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [closeMenu, menuOpen]);

  function navigate(item: NavigationItem) {
    onNavigate(item.view);
    closeMenu();
  }

  return (
    <>
      <button
        ref={menuTriggerRef}
        type="button"
        className="mobile-menu-trigger"
        aria-label={menuOpen ? '메뉴 닫기' : '메뉴 열기'}
        aria-expanded={menuOpen}
        aria-controls="app-mobile-menu-drawer"
        onClick={() => setMenuOpen((open) => !open)}
      >
        <span className="mobile-menu-trigger-lines" aria-hidden="true"><i /><i /><i /></span>
      </button>

      {menuOpen ? createPortal(
        <div
          className="mobile-menu-backdrop"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closeMenu();
            }
          }}
        >
          <aside
            ref={drawerRef}
            id="app-mobile-menu-drawer"
            className="mobile-menu-drawer"
            role="dialog"
            aria-modal="true"
            aria-labelledby="app-mobile-menu-title"
          >
            <header className="mobile-menu-header">
              <img className="mobile-menu-brand-logo" src={emiLogo} alt="" aria-hidden="true" />
              <div>
                <p className="eyebrow">EMI WORKSPACE</p>
                <h2 id="app-mobile-menu-title">전체 업무 메뉴</h2>
              </div>
              <button type="button" className="mobile-menu-close" aria-label="메뉴 닫기" onClick={() => closeMenu()}>
                ×
              </button>
            </header>
            <nav className="mobile-menu-list" aria-label="모바일 공통 메뉴">
              {items.map((item, index) => (
                <button
                  key={item.label}
                  ref={index === 0 ? firstMenuItemRef : undefined}
                  type="button"
                  className={item.active ? 'mobile-menu-item active' : 'mobile-menu-item'}
                  aria-label={item.badge && item.badge > 0 ? `${item.label} ${item.badge}건` : item.label}
                  aria-current={item.active ? 'page' : undefined}
                  onClick={() => navigate(item)}
                >
                  <span
                    className="mobile-menu-item-shape"
                    data-shape-role={item.active ? 'active' : 'control'}
                    aria-hidden="true"
                  ><NavigationIcon label={item.label} /></span>
                  <span className="mobile-menu-item-copy">
                    <strong>{item.label}</strong>
                    <small>{mobileNavigationHints[item.label] ?? '업무 화면'}</small>
                  </span>
                  {item.badge && item.badge > 0 ? (
                    <>
                      <span className="nav-badge" aria-hidden="true">{formatBadgeCount(item.badge)}</span>
                      <span className="sr-only">{item.badge}건</span>
                    </>
                  ) : <span className="mobile-menu-arrow" aria-hidden="true">→</span>}
                </button>
              ))}
            </nav>
            <footer className="mobile-menu-footer">
              {footer ?? <p>필요한 화면을 선택하면 메뉴가 자동으로 닫힙니다.</p>}
            </footer>
          </aside>
        </div>,
        document.body
      ) : null}
    </>
  );
}

function NavigationIcon({ label }: { label: string }) {
  const common = {
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.8,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true
  };

  switch (label) {
    case '홈':
      return <svg {...common}><path d="m3.5 10 8.5-7 8.5 7" /><path d="M5.5 9.5V21h13V9.5" /><path d="M9.5 21v-6h5v6" /></svg>;
    case '내 업무':
      return <svg {...common}><rect x="5" y="3" width="14" height="18" rx="2" /><path d="M9 3.5h6v3H9zM9 11h6M9 15h4" /></svg>;
    case '프로젝트':
      return <svg {...common}><path d="M3 7.5h7l2 2h9v10.5H3z" /><path d="M3 7.5V5h7l2 2h7" /></svg>;
    case 'Pending':
      return <svg {...common}><path d="M12 3 2.8 20h18.4z" /><path d="M12 9v5M12 17.5h.01" /></svg>;
    case '생산관리':
      return <svg {...common}><path d="M4 19V8l5 3V8l5 3V5h6v14z" /><path d="M7 15h2M12 15h2M17 15h1" /></svg>;
    case '구매':
      return <svg {...common}><path d="M3 5h2l2 11h10l2-8H6" /><circle cx="9" cy="20" r="1" /><circle cx="17" cy="20" r="1" /></svg>;
    case '자재':
      return <svg {...common}><path d="m4 7 8-4 8 4-8 4z" /><path d="m4 7 8 4 8-4v10l-8 4-8-4zM12 11v10" /></svg>;
    case '제조':
      return <svg {...common}><circle cx="12" cy="12" r="3" /><path d="M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9 7 7M17 17l2.1 2.1M19.1 4.9 17 7M7 17l-2.1 2.1" /></svg>;
    case '품질':
      return <svg {...common}><path d="m12 3 7 3v5c0 4.6-2.8 8-7 10-4.2-2-7-5.4-7-10V6z" /><path d="m8.5 12 2.2 2.2 4.8-5" /></svg>;
    case '물류':
      return <svg {...common}><path d="M3 6h11v10H3zM14 9h4l3 3v4h-7z" /><circle cx="7" cy="18" r="2" /><circle cx="18" cy="18" r="2" /></svg>;
    case '알림':
      return <svg {...common}><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" /><path d="M10 21h4" /></svg>;
    case '관리자':
      return <svg {...common}><circle cx="12" cy="8" r="4" /><path d="M4 21c.8-4.2 3.4-6.5 8-6.5s7.2 2.3 8 6.5" /><path d="m17.5 4.5 1 1 2-2" /></svg>;
    default:
      return <svg {...common}><circle cx="12" cy="12" r="8" /></svg>;
  }
}

function ShellSwitchControls({
  isDevMode,
  canUseAdminTestUserSwitch,
  isTestUserSwitch,
  developmentUserKey,
  adminTestUserKey,
  onDevelopmentUserChange,
  onAdminTestUserChange,
  onResetAdminTestUser
}: {
  isDevMode: boolean;
  canUseAdminTestUserSwitch: boolean;
  isTestUserSwitch: boolean;
  developmentUserKey: string;
  adminTestUserKey: string;
  onDevelopmentUserChange: (value: string) => void;
  onAdminTestUserChange: (value: string) => void;
  onResetAdminTestUser: () => void;
}) {
  if (!isDevMode && !canUseAdminTestUserSwitch && !isTestUserSwitch) return null;
  return (
    <div className="shell-switch-controls">
      <header><span aria-hidden="true">◇</span><strong>개발·검수 도구</strong></header>
      {isTestUserSwitch ? (
        <button type="button" className="shell-switch-reset" onClick={onResetAdminTestUser}>실제 계정으로 보기</button>
      ) : null}
      {canUseAdminTestUserSwitch ? (
        <label>
          <span>검수 사용자</span>
          <select value={adminTestUserKey} onChange={(event) => onAdminTestUserChange(event.target.value)}>
            <option value="">실제 계정</option>
            {adminTestUsers.map((userKey) => (
              <option key={userKey} value={userKey}>{labelForDevelopmentUser(userKey)}</option>
            ))}
          </select>
        </label>
      ) : null}
      {isDevMode ? (
        <label>
          <span>개발 사용자</span>
          <select value={developmentUserKey} onChange={(event) => onDevelopmentUserChange(event.target.value)}>
            {developmentUsers.map((userKey) => <option key={userKey} value={userKey}>{userKey}</option>)}
          </select>
        </label>
      ) : null}
    </div>
  );
}

function DesktopAccountMenu({
  user,
  developmentUserKey,
  profilePhotoUrl,
  mutationAllowed,
  onPhotoChanged,
  onLogout
}: {
  user: CurrentUser;
  developmentUserKey: string;
  profilePhotoUrl: string | null;
  mutationAllowed: boolean;
  onPhotoChanged: () => void;
  onLogout?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  const close = useCallback((restoreFocus = true) => {
    setOpen(false);
    if (restoreFocus) window.setTimeout(() => triggerRef.current?.focus(), 0);
  }, []);

  useEffect(() => {
    if (!open) return undefined;
    const onPointerDown = (event: MouseEvent) => {
      if (!wrapperRef.current?.contains(event.target as Node)) close(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        close();
      }
    };
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [close, open]);

  return (
    <div className="desktop-account-menu" ref={wrapperRef}>
      <button
        ref={triggerRef}
        type="button"
        className="account-identity-trigger"
        aria-expanded={open}
        aria-controls="desktop-account-popover"
        onClick={() => setOpen((value) => !value)}
      >
        <ProfileAvatar displayName={user.actualUser.displayName} photoUrl={profilePhotoUrl} compact />
        <span><small>{user.actualUser.departmentName ?? '부서 미지정'}</small><strong>{user.actualUser.displayName}</strong></span>
        <i aria-hidden="true">⌄</i>
      </button>
      {open ? (
        <div id="desktop-account-popover" className="account-popover" role="dialog" aria-label="내 계정">
          <AccountProfilePanel
            user={user}
            developmentUserKey={developmentUserKey}
            profilePhotoUrl={profilePhotoUrl}
            mutationAllowed={mutationAllowed}
            onPhotoChanged={onPhotoChanged}
            onLogout={onLogout}
          />
        </div>
      ) : null}
    </div>
  );
}

function AccountProfilePanel({
  user,
  developmentUserKey,
  profilePhotoUrl,
  mutationAllowed,
  onPhotoChanged,
  onLogout,
  mobile = false
}: {
  user: CurrentUser;
  developmentUserKey: string;
  profilePhotoUrl: string | null;
  mutationAllowed: boolean;
  onPhotoChanged: () => void;
  onLogout?: () => void;
  mobile?: boolean;
}) {
  const actions = useActionFeedback();
  const inputRef = useRef<HTMLInputElement>(null);
  const feedback = actions.feedbackFor('profile-photo');
  const busy = actions.isBusy('profile-photo');

  async function upload(file: File | null) {
    if (!file) return;
    const result = await actions.run('profile-photo', async () => {
      if (!['image/jpeg', 'image/png'].includes(file.type) || file.size < 1 || file.size > 5 * 1024 * 1024) {
        throw new ApiError(400, '5MB 이하 JPEG 또는 PNG 파일을 선택해 주세요.');
      }
      await saveOwnProfilePhoto(developmentUserKey, file);
    }, {
      loadingMessage: '프로필 사진을 올리는 중입니다.',
      successMessage: '프로필 사진을 변경했습니다.',
      errorFallback: '프로필 사진을 변경하지 못했습니다.'
    });
    if (inputRef.current) inputRef.current.value = '';
    if (result === 'success' || result === 'partial') onPhotoChanged();
  }

  async function remove() {
    const result = await actions.run('profile-photo', () => removeOwnProfilePhoto(developmentUserKey), {
      loadingMessage: '프로필 사진을 제거하는 중입니다.',
      successMessage: '기본 이니셜 사진으로 변경했습니다.',
      errorFallback: '프로필 사진을 제거하지 못했습니다.'
    });
    if (result === 'success' || result === 'partial') onPhotoChanged();
  }

  return (
    <section className={mobile ? 'account-profile-panel account-profile-panel--mobile' : 'account-profile-panel'}>
      <div className="account-photo-block">
        <label className="account-photo-editor" aria-label="프로필 사진 업로드">
          <input
            ref={inputRef}
            type="file"
            accept="image/jpeg,image/png"
            disabled={!mutationAllowed || busy}
            onChange={(event) => void upload(event.target.files?.[0] ?? null)}
          />
          <ProfileAvatar displayName={user.actualUser.displayName} photoUrl={profilePhotoUrl} />
          <span aria-hidden="true">＋</span>
        </label>
        <div>
          <strong>{user.actualUser.displayName}</strong>
          <span>{user.actualUser.departmentName ?? '부서 미지정'}</span>
          {user.actualUser.email ? <small>{user.actualUser.email}</small> : null}
        </div>
      </div>
      {user.isTestUserSwitch ? (
        <div className="account-effective-context">
          <span>현재 검수 화면</span>
          <strong>{user.effectiveUser.departmentName ?? '부서 미지정'} · {user.effectiveUser.displayName}</strong>
        </div>
      ) : null}
      <div className="account-photo-actions">
        <button type="button" onClick={() => inputRef.current?.click()} disabled={!mutationAllowed || busy}>사진 변경</button>
        <button type="button" onClick={() => void remove()} disabled={!mutationAllowed || busy || !profilePhotoUrl}>사진 제거</button>
      </div>
      {!mutationAllowed ? <p className="account-review-safe-note">검수 전용 읽기 모드에서는 사진을 변경할 수 없습니다.</p> : null}
      {feedback ? <p className="account-action-feedback" data-tone={feedback.tone} aria-live="polite">{feedback.message}</p> : null}
      <button type="button" className="account-logout-button" onClick={onLogout} disabled={!onLogout}>로그아웃</button>
    </section>
  );
}

function ProfileAvatar({
  displayName,
  photoUrl,
  compact = false
}: {
  displayName: string;
  photoUrl: string | null;
  compact?: boolean;
}) {
  const initial = displayName.trim().slice(0, 1).toUpperCase() || 'U';
  return (
    <span className={compact ? 'profile-avatar profile-avatar--compact' : 'profile-avatar'} role="img" aria-label={`${displayName} 프로필 사진`}>
      {photoUrl ? <img src={photoUrl} alt="" /> : <span aria-hidden="true">{initial}</span>}
    </span>
  );
}

type AuthGateVisualState = 'login' | 'loading' | 'reauth' | 'error' | 'configuration' | 'access';

const authLoginCanvasWidth = 1440;
const authLoginCanvasHeight = 810;

function authLoginCanvasScale() {
  if (typeof window === 'undefined') {
    return 1;
  }

  return Math.min(
    window.innerWidth / authLoginCanvasWidth,
    window.innerHeight / authLoginCanvasHeight
  );
}

function useAuthLoginCanvasScale(enabled: boolean) {
  const [scale, setScale] = useState(authLoginCanvasScale);

  useEffect(() => {
    if (!enabled) {
      return undefined;
    }

    const updateScale = () => setScale(authLoginCanvasScale());
    updateScale();
    window.addEventListener('resize', updateScale);

    return () => window.removeEventListener('resize', updateScale);
  }, [enabled]);

  return enabled ? scale : 1;
}

export function AuthInitializationScreen({ rememberSession = true }: { rememberSession?: boolean }) {
  return <AuthLoginScreen loading rememberSession={rememberSession} />;
}

function AuthLoginScreen({
  loading = false,
  rememberSession,
  onRememberSessionChange,
  onLogin
}: {
  loading?: boolean;
  rememberSession: boolean;
  onRememberSessionChange?: (rememberSession: boolean) => void;
  onLogin?: () => void;
}) {
  return (
    <AuthGateMessage
      state={loading ? 'loading' : 'login'}
      title="EMI 프로젝트 통합관리시스템"
      message={loading
        ? 'Microsoft 365 로그인 정보를 확인하고 있습니다.'
        : '회사 Microsoft 365 계정으로 로그인해 주세요.'}
      actionLabel={loading ? undefined : 'LOGIN'}
      onAction={onLogin}
    >
      {loading ? (
        <span className="auth-loading-indicator" role="status" aria-label="로그인 확인 중" />
      ) : (
        <label className="remember-session-option">
          <input
            type="checkbox"
            checked={rememberSession}
            onChange={(event) => onRememberSessionChange?.(event.target.checked)}
          />
          <span>로그인 상태 유지</span>
        </label>
      )}
    </AuthGateMessage>
  );
}

function AuthGateMessage({
  state,
  title,
  message,
  helperText,
  actionLabel,
  actionDisabled = false,
  onAction,
  secondaryActionLabel,
  onSecondaryAction,
  children
}: {
  state: AuthGateVisualState;
  title: string;
  message?: string;
  helperText?: string;
  actionLabel?: string;
  actionDisabled?: boolean;
  onAction?: () => void;
  secondaryActionLabel?: string;
  onSecondaryAction?: () => void;
  children?: ReactNode;
}) {
  const showsProductTitle = title === 'EMI 프로젝트 통합관리시스템';
  const usesLoginLayout = state === 'login' || state === 'loading';
  const loginCanvasScale = useAuthLoginCanvasScale(usesLoginLayout);
  const shell = (
    <>
      <section className="auth-brand-panel">
        <div className="auth-brand-canvas">
          <img className="auth-brand-effect auth-brand-effect-66" src={authEllipse66} alt="" aria-hidden="true" />
          <img className="auth-brand-effect auth-brand-effect-69" src={authEllipse66} alt="" aria-hidden="true" />
          <img className="auth-brand-effect auth-brand-effect-67" src={authEllipse67} alt="" aria-hidden="true" />
        </div>
        <span className="auth-brand-overlay" aria-hidden="true" />
        <div className="auth-brand-logo-canvas">
          <img className="auth-brand-logo" src={emiLogo} alt="EMI Electric Modular Innovation" />
        </div>
        <div className="auth-brand-pattern-canvas">
          <span className="auth-brand-dots" data-figma-node-id="1:181" aria-hidden="true" />
        </div>
      </section>
      <section
        className="auth-gate-panel"
        aria-labelledby="auth-gate-title"
        aria-busy={state === 'loading'}
      >
        <div className="auth-gate-canvas">
          <div className="auth-gate-content">
            {!showsProductTitle ? <p className="auth-product-name">EMI 프로젝트 통합관리시스템</p> : null}
            <h1 id="auth-gate-title">
              <span className="auth-gate-title-text">{title}</span>
            </h1>
            <div className="auth-microsoft-brand">
              <img src={microsoftLogo} alt="Microsoft" />
            </div>
            {message ? (
              <p className={usesLoginLayout ? 'auth-gate-message auth-login-guidance' : 'auth-gate-message'}>
                {message}
              </p>
            ) : null}
            {actionLabel ? (
              <button
                type="button"
                className="auth-primary-button"
                disabled={actionDisabled}
                onClick={onAction}
              >
                {actionLabel}
              </button>
            ) : null}
            {children ? <div className="auth-gate-extra">{children}</div> : null}
            {helperText ? <p className="auth-helper-text">{helperText}</p> : null}
            {secondaryActionLabel ? (
              <button
                type="button"
                className="auth-secondary-button"
                onClick={onSecondaryAction}
              >
                {secondaryActionLabel}
              </button>
            ) : null}
          </div>
        </div>
      </section>
    </>
  );

  return (
    <main className="auth-gate" data-auth-state={state} data-auth-layout={usesLoginLayout ? 'login' : 'default'}>
      {usesLoginLayout ? (
        <div
          className="auth-login-canvas"
          data-auth-canvas-scale={loginCanvasScale.toFixed(6)}
          style={{
            '--auth-login-content-scale': loginCanvasScale,
            '--auth-login-background-offset': `${-6 * loginCanvasScale}px`,
            '--auth-login-panel-radius': `${51 * loginCanvasScale}px`,
            '--auth-login-panel-overflow': `${0.5 * loginCanvasScale}px`,
            '--auth-login-shadow-x': `${-5.25 * loginCanvasScale}px`,
            '--auth-login-shadow-y': `${-1.5 * loginCanvasScale}px`,
            '--auth-login-shadow-blur': `${43.05 * loginCanvasScale}px`,
            '--auth-login-glass-blur': `${23.25 * loginCanvasScale}px`
          } as CSSProperties}
        >
          {shell}
        </div>
      ) : shell}
    </main>
  );
}

function AuthenticationRequiredPage({ user, message, onLogout }: { user?: CurrentUser | null; message?: string; onLogout?: () => void }) {
  return (
    <AuthGateMessage
      state="access"
      title="인증이 필요합니다."
      message={message ?? '시스템 관리자에게 문의하세요.'}
      helperText={`관리자가 부서와 역할을 지정하면 시스템을 사용할 수 있습니다.${user ? ` 현재 계정: ${user.displayName}${user.email ? ` (${user.email})` : ''}` : ''}`}
      actionLabel={onLogout ? '로그아웃' : undefined}
      onAction={onLogout}
    />
  );
}

function ApprovalPendingPage({ user, onLogout }: { user: CurrentUser; onLogout?: () => void }) {
  return (
    <section className="panel-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">승인 대기</p>
          <h2>사용자 승인이 필요합니다.</h2>
        </div>
        {onLogout ? <button type="button" onClick={onLogout}>로그아웃</button> : null}
      </div>
      <p className="muted-text">
        {user.displayName}{user.email ? ` (${user.email})` : ''} 계정은 아직 역할이 부여되지 않았습니다.
        System Administrator가 역할을 1개 이상 부여하면 업무 화면을 사용할 수 있습니다.
      </p>
    </section>
  );
}

function DeletionStatusDisplay({
  isActive,
  approvalPending,
  lifecycleStatus,
  lifecycleStatusLabel,
  deletionRequestedAtUtc,
  scheduledHardDeleteAtUtc,
  scheduledHardDeleteLabel,
  purgeBlockedAtUtc,
  purgeBlockedReason
}: {
  isActive: boolean;
  approvalPending?: boolean;
  lifecycleStatus?: string | null;
  lifecycleStatusLabel?: string | null;
  deletionRequestedAtUtc?: string | null;
  scheduledHardDeleteAtUtc?: string | null;
  scheduledHardDeleteLabel?: string | null;
  purgeBlockedAtUtc?: string | null;
  purgeBlockedReason?: string | null;
}) {
  const resolvedStatus = lifecycleStatus ?? resolveDeletionLifecycleStatus(
    isActive,
    deletionRequestedAtUtc,
    scheduledHardDeleteAtUtc,
    purgeBlockedAtUtc);
  const label = lifecycleStatusLabel ?? deletionLifecycleStatusLabel(resolvedStatus);
  const scheduledText = scheduledHardDeleteLabel
    ?? (scheduledHardDeleteAtUtc ? formatKoreanDateTime(scheduledHardDeleteAtUtc) : null);
  const badgeClassName = `status-badge lifecycle-badge lifecycle-${resolvedStatus.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()}`;

  if (resolvedStatus === 'PurgeBlocked') {
    return (
      <span className="lifecycle-status">
        <span className={badgeClassName} data-tone="warning">{label}</span>
        {scheduledText ? <small className="muted-text">완전 삭제 예정일 {scheduledText}</small> : null}
        <small className="warning-text">{purgeBlockedReason ?? '참조 데이터가 남아 있습니다.'}</small>
      </span>
    );
  }

  if (resolvedStatus === 'DeletionScheduled') {
    return (
      <span className="lifecycle-status">
        <span className={badgeClassName} data-tone="danger">{label}</span>
        {scheduledText ? <small className="muted-text">완전 삭제 예정일 {scheduledText}</small> : null}
      </span>
    );
  }

  if (approvalPending) {
    return <span className="status-badge warning">승인 대기</span>;
  }

  return <span className={badgeClassName} data-tone={resolvedStatus === 'Inactive' ? 'neutral' : 'success'}>{label}</span>;
}

function resolveDeletionLifecycleStatus(
  isActive: boolean,
  deletionRequestedAtUtc?: string | null,
  scheduledHardDeleteAtUtc?: string | null,
  purgeBlockedAtUtc?: string | null
) {
  if (purgeBlockedAtUtc) {
    return 'PurgeBlocked';
  }

  if (deletionRequestedAtUtc && scheduledHardDeleteAtUtc) {
    return 'DeletionScheduled';
  }

  return isActive ? 'Active' : 'Inactive';
}

function deletionLifecycleStatusLabel(status: string) {
  switch (status) {
    case 'PurgeBlocked':
      return '삭제 보류';
    case 'DeletionScheduled':
      return '삭제 예정';
    case 'Inactive':
      return '비활성';
    case 'Active':
      return '활성';
    default:
      return status;
  }
}

function isDeletionPending(item: {
  deletionRequestedAtUtc?: string | null;
  purgeBlockedAtUtc?: string | null;
  lifecycleStatus?: string | null;
}) {
  return Boolean(item.purgeBlockedAtUtc)
    || Boolean(item.deletionRequestedAtUtc)
    || item.lifecycleStatus === 'DeletionScheduled'
    || item.lifecycleStatus === 'PurgeBlocked';
}

function summarizeBulkAction(result: AdminBulkActionResponse, fallback: string) {
  const failures = result.items.filter((item) => item.status === 'Failed');
  const blocked = result.items.filter((item) => item.status === 'PurgeBlocked');
  const skipped = result.items.filter((item) => item.status === 'Skipped');
  const details = [...failures, ...blocked].slice(0, 3).map((item) => item.message).filter(Boolean);
  const prefix = `${fallback}: 성공 ${result.succeededCount}건, 실패 ${result.failedCount}건, 건너뜀 ${result.skippedCount}건`;
  const suffix = details.length > 0
    ? ` · ${details.join(' · ')}`
    : skipped.length > 0 ? ` · ${skipped[0].message}` : '';
  return `${prefix}${suffix}`;
}

function AdminUsersPage({
  developmentUserKey,
  onOpenNotificationSettings
}: {
  developmentUserKey: string;
  onOpenNotificationSettings: (userId: string) => void;
}) {
  const [state, setState] = useState<LoadState<AdminUsersResponse>>({ kind: 'loading' });
  const [editingUserId, setEditingUserId] = useState<string | null>(null);
  const [draftDepartmentId, setDraftDepartmentId] = useState<string>('');
  const [draftRoleCodes, setDraftRoleCodes] = useState<string[]>([]);
  const [draftIsActive, setDraftIsActive] = useState(true);
  const [message, setMessage] = useState('');
  const [selectedUserIds, setSelectedUserIds] = useState<string[]>([]);
  const [userExportBusy, setUserExportBusy] = useState(false);
  const isMobile = useIsMobileViewport();
  const [showAllMobileFields, setShowAllMobileFields] = useState(false);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminUsers(developmentUserKey)
      .then((data) => {
        setState({ kind: 'ready', data });
        setSelectedUserIds([]);
      })
      .catch((error: unknown) => setState(toLoadError(error, '사용자 목록을 불러올 수 없습니다.')));
  }, [developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    getAdminUsers(developmentUserKey)
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
          setSelectedUserIds([]);
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '사용자 목록을 불러올 수 없습니다.'));
        }
      });

    return () => {
      cancelled = true;
    };
  }, [developmentUserKey]);

  const startEdit = (user: AdminUser) => {
    setEditingUserId(user.userId);
    setDraftDepartmentId(user.departmentId ?? '');
    setDraftRoleCodes([...user.roles]);
    setDraftIsActive(user.isActive);
    setMessage('');
  };

  const toggleRole = (roleCode: string) => {
    setDraftRoleCodes((current) => (
      current.includes(roleCode)
        ? current.filter((code) => code !== roleCode)
        : [...current, roleCode].sort()
    ));
  };

  const save = async (user: AdminUser) => {
    setMessage('');
    try {
      const updated = await updateAdminUser(developmentUserKey, user.userId, {
        departmentId: draftDepartmentId || null,
        roleCodes: draftRoleCodes,
        isActive: draftIsActive
      });
      setState({ kind: 'ready', data: updated });
      setEditingUserId(null);
      setMessage('사용자 정보를 저장했습니다.');
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : '사용자 정보를 저장할 수 없습니다.');
    }
  };

  const deleteUser = async (user: AdminUser) => {
    const scheduled = isDeletionPending(user);
    const confirmed = window.confirm(scheduled
      ? '삭제 예정 데이터를 즉시 완전 삭제합니다. 참조 중인 데이터는 삭제 보류될 수 있습니다. 계속하시겠습니까?'
      : '삭제하면 즉시 비활성화되고 7일 후 완전 삭제 대상으로 예약됩니다. 계속하시겠습니까?');
    if (!confirmed) {
      return;
    }

    setMessage('');
    try {
      if (scheduled) {
        const result = await purgeAdminUser(developmentUserKey, user.userId);
        setMessage(summarizeBulkAction(result, '사용자 즉시 삭제 처리 완료'));
        load();
        return;
      }

      const updated = await scheduleAdminUserDeletion(developmentUserKey, user.userId);
      setState({ kind: 'ready', data: updated });
      setEditingUserId(null);
      setSelectedUserIds([]);
      setMessage('사용자를 삭제 예정으로 처리했습니다.');
    } catch (error) {
      const errorMessage = friendlyErrorMessage(error, '사용자를 삭제 예약할 수 없습니다.');
      setMessage(errorMessage === '대상을 찾을 수 없습니다.'
        ? '사용자를 삭제 예약할 수 없습니다. 목록을 새로고침한 뒤 다시 시도해 주세요.'
        : errorMessage);
    }
  };

  const restoreUser = async (user: AdminUser) => {
    if (!window.confirm('선택한 삭제 예정 데이터를 복구합니다.')) {
      return;
    }

    setMessage('');
    try {
      const updated = await restoreAdminUser(developmentUserKey, user.userId);
      setState({ kind: 'ready', data: updated });
      setSelectedUserIds([]);
      setMessage('사용자를 복구했습니다.');
    } catch (error) {
      setMessage(friendlyErrorMessage(error, '사용자를 복구할 수 없습니다.'));
    }
  };

  const bulkDeleteUsers = async () => {
    if (selectedUserIds.length === 0) {
      setMessage('삭제할 사용자를 선택해 주세요.');
      return;
    }

    if (!window.confirm('선택한 데이터의 상태에 맞게 삭제 예정 전환 또는 즉시 삭제를 수행합니다. 삭제 예정 데이터는 즉시 완전 삭제를 시도하며, 참조 중인 데이터는 삭제 보류될 수 있습니다. 계속하시겠습니까?')) {
      return;
    }

    setMessage('');
    try {
      const result = await bulkDeleteAdminUsers(developmentUserKey, { ids: selectedUserIds, reason: '선택 삭제' });
      setMessage(summarizeBulkAction(result, '선택 삭제 처리 완료'));
      load();
    } catch (error) {
      setMessage(friendlyErrorMessage(error, '선택 삭제를 처리할 수 없습니다.'));
    }
  };

  const bulkRestoreUsers = async () => {
    if (selectedUserIds.length === 0) {
      setMessage('복구할 사용자를 선택해 주세요.');
      return;
    }

    if (!window.confirm('선택한 삭제 예정 데이터를 복구합니다.')) {
      return;
    }

    setMessage('');
    try {
      const result = await bulkRestoreAdminUsers(developmentUserKey, { ids: selectedUserIds, reason: '선택 복구' });
      setMessage(summarizeBulkAction(result, '선택 복구 처리 완료'));
      load();
    } catch (error) {
      setMessage(friendlyErrorMessage(error, '선택 복구를 처리할 수 없습니다.'));
    }
  };

  const visibleUsers = state.kind === 'ready' ? state.data.users : [];
  const selectableUserIds = visibleUsers.filter((user) => !user.isReadOnly).map((user) => user.userId);
  const allUsersSelected = selectableUserIds.length > 0 && selectableUserIds.every((id) => selectedUserIds.includes(id));

  return (
    <section className={`panel-section admin-mobile-page${showAllMobileFields ? ' admin-mobile-page--all-fields' : ''}`}>
      <div className="page-header">
        <div>
          <p className="eyebrow">System Administrator</p>
          <h2>사용자 관리</h2>
        </div>
        <button type="button" onClick={load}>새로고침</button>
      </div>
      <p className="muted-text">EntraId 사용자의 부서, 역할, 활성 상태만 수정할 수 있습니다. Dev 사용자는 읽기 전용입니다.</p>
      {isMobile ? (
        <button
          type="button"
          className="mobile-admin-field-toggle"
          aria-pressed={showAllMobileFields}
          onClick={() => setShowAllMobileFields((current) => !current)}
        >
          <span>{showAllMobileFields ? '핵심 열로 보기' : '모든 관리 필드 보기'}</span>
          <small>{showAllMobileFields ? '모바일 우선 정보만 다시 표시' : '상태·부서·역할 열까지 가로로 확인'}</small>
        </button>
      ) : null}
      <ActionFeedback message={message} tone={message.includes('없습니다') || message.includes('수 없습니다') ? 'error' : message ? 'success' : 'neutral'} />
      {state.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="admin-users"
          visibleIds={selectableUserIds}
          selectedIds={new Set(selectedUserIds)}
          allSelected={allUsersSelected}
          busy={userExportBusy}
          onBusyChange={setUserExportBusy}
          onToggleAll={(checked) => setSelectedUserIds(checked ? selectableUserIds : [])}
          onClear={() => setSelectedUserIds([])}
        />
      ) : null}
      {state.kind === 'ready' ? (
        <div className="bulk-action-bar">
          <span>선택 {selectedUserIds.length}건</span>
          <button type="button" onClick={() => void bulkDeleteUsers()} disabled={selectedUserIds.length === 0}>선택 삭제</button>
          <button type="button" onClick={() => void bulkRestoreUsers()} disabled={selectedUserIds.length === 0}>선택 복구</button>
        </div>
      ) : null}
      {state.kind === 'loading' ? <p>사용자 목록을 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>
                  <input
                    type="checkbox"
                    aria-label="사용자 전체 선택"
                    checked={allUsersSelected}
                    disabled={selectableUserIds.length === 0}
                    onChange={(event) => setSelectedUserIds(event.target.checked ? selectableUserIds : [])}
                  />
                </th>
                <th>사용자</th>
                <th>구분</th>
                <th>상태</th>
                <th>부서</th>
                <th>역할</th>
                <th>작업</th>
              </tr>
            </thead>
            <tbody>
              {state.data.users.map((user) => {
                const editing = editingUserId === user.userId;
                return (
                  <tr key={user.userId}>
                    <td>
                      <input
                        type="checkbox"
                        aria-label={`${user.displayName} 선택`}
                        checked={selectedUserIds.includes(user.userId)}
                        disabled={user.isReadOnly}
                        onChange={(event) => setSelectedUserIds((current) => (
                          event.target.checked
                            ? [...current, user.userId]
                            : current.filter((id) => id !== user.userId)
                        ))}
                      />
                    </td>
                    <td>
                      <strong>{user.displayName}</strong>
                      <div className="muted-text">{user.email ?? user.developmentUserKey}</div>
                    </td>
                    <td>{user.authProvider}{user.isReadOnly ? ' · 읽기 전용' : ''}</td>
                    <td>
                      {editing ? (
                        <label className="inline-check">
                          <input
                            type="checkbox"
                            checked={draftIsActive}
                            onChange={(event) => setDraftIsActive(event.target.checked)}
                          />
                          활성
                        </label>
                      ) : (
                        <DeletionStatusDisplay
                          isActive={user.isActive}
                          approvalPending={user.approvalPending}
                          lifecycleStatus={user.lifecycleStatus}
                          lifecycleStatusLabel={user.lifecycleStatusLabel}
                          deletionRequestedAtUtc={user.deletionRequestedAtUtc}
                          scheduledHardDeleteAtUtc={user.scheduledHardDeleteAtUtc}
                          scheduledHardDeleteLabel={user.scheduledHardDeleteLabel}
                          purgeBlockedAtUtc={user.purgeBlockedAtUtc}
                          purgeBlockedReason={user.purgeBlockedReason}
                        />
                      )}
                    </td>
                    <td>
                      {editing ? (
                        <select value={draftDepartmentId} onChange={(event) => setDraftDepartmentId(event.target.value)}>
                          <option value="">부서 미지정</option>
                          {state.data.departments.map((department) => (
                            <option key={department.departmentId} value={department.departmentId}>{department.name}</option>
                          ))}
                        </select>
                      ) : (
                        user.departmentName ?? '부서 미지정'
                      )}
                    </td>
                    <td>
                      {editing ? (
                        <div className="role-checkboxes">
                          {state.data.roles.map((role) => (
                            <label key={role.code} className="inline-check">
                              <input
                                type="checkbox"
                                checked={draftRoleCodes.includes(role.code)}
                                onChange={() => toggleRole(role.code)}
                              />
                              {role.code}
                            </label>
                          ))}
                        </div>
                      ) : (
                        user.roles.length > 0 ? user.roles.join(', ') : '역할 없음'
                      )}
                    </td>
                    <td>
                      {user.isActive ? (
                        <button
                          type="button"
                          className="compact-link-button"
                          onClick={() => onOpenNotificationSettings(user.userId)}
                        >
                          알림 설정
                        </button>
                      ) : null}
                      {user.isReadOnly ? (
                        <span className="muted-text">개발 사용자는 삭제할 수 없습니다.</span>
                      ) : editing ? (
                        <div className="button-row">
                          <button type="button" onClick={() => void save(user)}>저장</button>
                          <button type="button" onClick={() => setEditingUserId(null)}>취소</button>
                        </div>
                      ) : (
                        <div className="button-row">
                          <button type="button" onClick={() => startEdit(user)}>수정</button>
                          {isDeletionPending(user) ? <button type="button" onClick={() => void restoreUser(user)}>복구</button> : null}
                          <button type="button" className="danger-button" onClick={() => void deleteUser(user)}>{isDeletionPending(user) ? '즉시 삭제' : '삭제'}</button>
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}

type CalendarHolidayDraft = {
  date: string;
  name: string;
  holidayType: HolidayType;
  isActive: boolean;
  note: string;
};

const holidayTypeOptions: Array<{ value: HolidayType; label: string }> = [
  { value: 'National', label: '국가공휴일' },
  { value: 'Substitute', label: '대체공휴일' },
  { value: 'Temporary', label: '임시공휴일' },
  { value: 'Company', label: '회사휴일' }
];

function AdminCalendarHolidaysPage({ developmentUserKey }: { developmentUserKey: string }) {
  const currentYear = new Date().getFullYear();
  const [year, setYear] = useState(currentYear);
  const [state, setState] = useState<LoadState<AdminCalendarHolidayListResponse>>({ kind: 'loading' });
  const [editingHolidayId, setEditingHolidayId] = useState<string | null>(null);
  const [draft, setDraft] = useState<CalendarHolidayDraft>(() => createHolidayDraft(currentYear));
  const [message, setMessage] = useState('');
  const [excelFile, setExcelFile] = useState<File | null>(null);
  const [excelPreview, setExcelPreview] = useState<CalendarHolidayExcelPreviewResponse | null>(null);
  const [isDownloading, setIsDownloading] = useState(false);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const [selectedHolidayIds, setSelectedHolidayIds] = useState<string[]>([]);
  const [holidayExportBusy, setHolidayExportBusy] = useState(false);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminCalendarHolidays(developmentUserKey, year)
      .then((data) => {
        setState({ kind: 'ready', data });
        setSelectedHolidayIds([]);
      })
      .catch((error: unknown) => setState(toLoadError(error, '휴일 목록을 불러올 수 없습니다.')));
  }, [developmentUserKey, year]);

  useEffect(() => {
    let cancelled = false;
    setState({ kind: 'loading' });
    getAdminCalendarHolidays(developmentUserKey, year)
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
          setSelectedHolidayIds([]);
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '휴일 목록을 불러올 수 없습니다.'));
        }
      });

    return () => {
      cancelled = true;
    };
  }, [developmentUserKey, year]);

  const startCreate = () => {
    setEditingHolidayId(null);
    setDraft(createHolidayDraft(year));
    setMessage('');
  };

  const startEdit = (holiday: AdminCalendarHoliday) => {
    setEditingHolidayId(holiday.holidayId);
    setDraft({
      date: holiday.date,
      name: holiday.name,
      holidayType: holiday.holidayType,
      isActive: holiday.isActive,
      note: holiday.note ?? ''
    });
    setMessage('');
  };

  const saveHoliday = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setMessage('');
    if (!draft.date || !draft.name.trim()) {
      setMessage('날짜와 휴일명을 입력해 주세요.');
      return;
    }

    try {
      const request = {
        date: draft.date,
        name: draft.name.trim(),
        holidayType: draft.holidayType,
        isActive: draft.isActive,
        note: draft.note.trim() || null
      };
      if (editingHolidayId) {
        await updateAdminCalendarHoliday(developmentUserKey, editingHolidayId, request);
        setMessage('휴일 정보를 저장했습니다.');
      } else {
        await createAdminCalendarHoliday(developmentUserKey, request);
        setMessage('휴일을 등록했습니다.');
      }
      setEditingHolidayId(null);
      setDraft(createHolidayDraft(year));
      load();
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : '휴일 정보를 저장할 수 없습니다.');
    }
  };

  const deactivateHoliday = async (holiday: AdminCalendarHoliday) => {
    const scheduled = isDeletionPending(holiday);
    const confirmed = window.confirm(scheduled
      ? '삭제 예정 데이터를 즉시 완전 삭제합니다. 참조 중인 데이터는 삭제 보류될 수 있습니다. 계속하시겠습니까?'
      : '삭제하면 즉시 비영업일 계산에서 제외되고 7일 후 완전 삭제 대상으로 예약됩니다. 계속하시겠습니까?');
    if (!confirmed) {
      return;
    }

    setMessage('');
    try {
      if (scheduled) {
        const result = await purgeAdminCalendarHoliday(developmentUserKey, holiday.holidayId);
        setMessage(summarizeBulkAction(result, '휴일 즉시 삭제 처리 완료'));
        load();
        return;
      }

      await deactivateAdminCalendarHoliday(developmentUserKey, holiday.holidayId);
      setMessage('휴일을 삭제 예정으로 처리했습니다.');
      load();
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : '휴일을 삭제 예약할 수 없습니다.');
    }
  };

  const restoreHoliday = async (holiday: AdminCalendarHoliday) => {
    if (!window.confirm('선택한 삭제 예정 데이터를 복구합니다.')) {
      return;
    }

    setMessage('');
    try {
      await restoreAdminCalendarHoliday(developmentUserKey, holiday.holidayId);
      setMessage('휴일을 복구했습니다.');
      load();
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : '휴일을 복구할 수 없습니다.');
    }
  };

  const bulkDeleteHolidays = async () => {
    if (selectedHolidayIds.length === 0) {
      setMessage('삭제할 휴일을 선택해 주세요.');
      return;
    }

    if (!window.confirm('선택한 데이터의 상태에 맞게 삭제 예정 전환 또는 즉시 삭제를 수행합니다. 삭제 예정 데이터는 즉시 완전 삭제를 시도하며, 참조 중인 데이터는 삭제 보류될 수 있습니다. 계속하시겠습니까?')) {
      return;
    }

    setMessage('');
    try {
      const result = await bulkDeleteAdminCalendarHolidays(developmentUserKey, { ids: selectedHolidayIds, reason: '선택 삭제' });
      setMessage(summarizeBulkAction(result, '선택 삭제 처리 완료'));
      load();
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : '선택 삭제를 처리할 수 없습니다.');
    }
  };

  const bulkRestoreHolidays = async () => {
    if (selectedHolidayIds.length === 0) {
      setMessage('복구할 휴일을 선택해 주세요.');
      return;
    }

    if (!window.confirm('선택한 삭제 예정 데이터를 복구합니다.')) {
      return;
    }

    setMessage('');
    try {
      const result = await bulkRestoreAdminCalendarHolidays(developmentUserKey, { ids: selectedHolidayIds, reason: '선택 복구' });
      setMessage(summarizeBulkAction(result, '선택 복구 처리 완료'));
      load();
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : '선택 복구를 처리할 수 없습니다.');
    }
  };

  const downloadTemplate = async () => {
    setIsDownloading(true);
    setMessage('');
    try {
      const template = await downloadAdminCalendarHolidayTemplate(developmentUserKey);
      const url = URL.createObjectURL(template.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = template.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : 'Excel 양식을 다운로드할 수 없습니다.');
    } finally {
      setIsDownloading(false);
    }
  };

  const previewExcel = async () => {
    if (!excelFile) {
      setMessage('업로드할 Excel 파일을 선택해 주세요.');
      return;
    }

    setIsPreviewing(true);
    setMessage('');
    try {
      setExcelPreview(await previewAdminCalendarHolidayExcel(developmentUserKey, excelFile));
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : 'Excel 파일을 미리 검토할 수 없습니다.');
    } finally {
      setIsPreviewing(false);
    }
  };

  const applyExcel = async () => {
    if (!excelFile || !excelPreview || excelPreview.saveableCount === 0) {
      setMessage('저장 가능한 Excel 미리보기 결과가 필요합니다.');
      return;
    }

    setIsApplying(true);
    setMessage('');
    try {
      const result = await applyAdminCalendarHolidayExcel(developmentUserKey, excelFile);
      setMessage(`Excel 반영 완료: 신규 ${result.insertedCount}건, 갱신 ${result.updatedCount}건, 제외 ${result.skippedCount}건`);
      setExcelPreview(null);
      setExcelFile(null);
      load();
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : 'Excel 휴일 정보를 반영할 수 없습니다.');
    } finally {
      setIsApplying(false);
    }
  };

  const visibleHolidayIds = state.kind === 'ready'
    ? state.data.holidays.map((holiday) => holiday.holidayId)
    : [];
  const allHolidaysSelected = visibleHolidayIds.length > 0 && visibleHolidayIds.every((id) => selectedHolidayIds.includes(id));

  return (
    <section className="panel-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">System Administrator</p>
          <h2>휴일 관리</h2>
        </div>
        <div className="button-row">
          <label className="compact-field">
            <span>연도</span>
            <input
              type="number"
              min="1900"
              max="2200"
              value={year}
              onChange={(event) => setYear(Number(event.target.value) || currentYear)}
            />
          </label>
          <button type="button" onClick={load}>새로고침</button>
        </div>
      </div>
      <p className="muted-text">토요일, 일요일과 활성 상태의 국가공휴일, 대체공휴일, 임시공휴일, 회사휴일은 비영업일로 계산됩니다.</p>
      <ActionFeedback message={message} tone={message.includes('없습니다') || message.includes('수 없습니다') ? 'error' : message ? 'success' : 'neutral'} />

      <form className="calendar-holiday-form" onSubmit={saveHoliday}>
        <label>
          날짜
          <input type="date" value={draft.date} onChange={(event) => setDraft({ ...draft, date: event.target.value })} required />
        </label>
        <label>
          휴일명
          <input value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} required />
        </label>
        <label>
          휴일유형
          <select value={draft.holidayType} onChange={(event) => setDraft({ ...draft, holidayType: event.target.value as HolidayType })}>
            {holidayTypeOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <label>
          비고
          <input value={draft.note} onChange={(event) => setDraft({ ...draft, note: event.target.value })} />
        </label>
        <label className="inline-check">
          <input type="checkbox" checked={draft.isActive} onChange={(event) => setDraft({ ...draft, isActive: event.target.checked })} />
          활성
        </label>
        <div className="button-row">
          <button type="submit">{editingHolidayId ? '저장' : '등록'}</button>
          <button type="button" onClick={startCreate}>신규 입력</button>
        </div>
      </form>

      <section className="excel-preview-card">
        <div className="page-header">
          <div>
            <h3>연간 휴일 Excel 등록</h3>
            <p className="muted-text">동일 날짜와 휴일유형은 갱신하고, 새 날짜/유형은 신규 등록합니다.</p>
          </div>
          <button type="button" onClick={downloadTemplate} disabled={isDownloading}>{isDownloading ? '다운로드 중' : 'Excel 양식 다운로드'}</button>
        </div>
        <div className="button-row">
          <input
            type="file"
            accept=".xlsx"
            onChange={(event) => {
              setExcelFile(event.target.files?.[0] ?? null);
              setExcelPreview(null);
            }}
          />
          <button type="button" onClick={previewExcel} disabled={!excelFile || isPreviewing}>{isPreviewing ? '검토 중' : '미리보기'}</button>
          <button
            type="button"
            onClick={() => void applyExcel()}
            disabled={!excelFile || !excelPreview || excelPreview.saveableCount === 0 || isApplying}
          >
            {isApplying ? '반영 중' : '저장 가능한 행 반영'}
          </button>
        </div>
        {excelPreview ? <AdminCalendarHolidayExcelPreview preview={excelPreview} /> : null}
      </section>

      {state.kind === 'loading' ? <p>휴일 목록을 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="table-scroll">
          <SelectedExportTray
            developmentUserKey={developmentUserKey}
            screen="admin-calendar-holidays"
            visibleIds={visibleHolidayIds}
            selectedIds={new Set(selectedHolidayIds)}
            allSelected={allHolidaysSelected}
            busy={holidayExportBusy}
            filters={{ year: String(year) }}
            onBusyChange={setHolidayExportBusy}
            onToggleAll={(checked) => setSelectedHolidayIds(checked ? visibleHolidayIds : [])}
            onClear={() => setSelectedHolidayIds([])}
          />
          <div className="bulk-action-bar">
            <span>선택 {selectedHolidayIds.length}건</span>
            <button type="button" onClick={() => void bulkDeleteHolidays()} disabled={selectedHolidayIds.length === 0}>선택 삭제</button>
            <button type="button" onClick={() => void bulkRestoreHolidays()} disabled={selectedHolidayIds.length === 0}>선택 복구</button>
          </div>
          <table>
            <thead>
              <tr>
                <th>
                  <input
                    type="checkbox"
                    aria-label="휴일 전체 선택"
                    checked={allHolidaysSelected}
                    onChange={(event) => setSelectedHolidayIds(event.target.checked ? visibleHolidayIds : [])}
                  />
                </th>
                <th>날짜</th>
                <th>휴일명</th>
                <th>유형</th>
                <th>상태</th>
                <th>비고</th>
                <th>작업</th>
              </tr>
            </thead>
            <tbody>
              {state.data.holidays.map((holiday) => (
                <tr key={holiday.holidayId}>
                  <td>
                    <input
                      type="checkbox"
                      aria-label={`${holiday.name} 선택`}
                      checked={selectedHolidayIds.includes(holiday.holidayId)}
                      onChange={(event) => setSelectedHolidayIds((current) => (
                        event.target.checked
                          ? [...current, holiday.holidayId]
                          : current.filter((id) => id !== holiday.holidayId)
                      ))}
                    />
                  </td>
                  <td>{holiday.date}</td>
                  <td><strong>{holiday.name}</strong></td>
                  <td><HolidayTypeBadge holidayType={holiday.holidayType} /></td>
                  <td>
                    <DeletionStatusDisplay
                      isActive={holiday.isActive}
                      lifecycleStatus={holiday.lifecycleStatus}
                      lifecycleStatusLabel={holiday.lifecycleStatusLabel}
                      deletionRequestedAtUtc={holiday.deletionRequestedAtUtc}
                      scheduledHardDeleteAtUtc={holiday.scheduledHardDeleteAtUtc}
                      scheduledHardDeleteLabel={holiday.scheduledHardDeleteLabel}
                      purgeBlockedAtUtc={holiday.purgeBlockedAtUtc}
                      purgeBlockedReason={holiday.purgeBlockedReason}
                    />
                  </td>
                  <td>{holiday.note || '-'}</td>
                  <td>
                    <div className="button-row">
                      {!holiday.deletionRequestedAtUtc ? <button type="button" onClick={() => startEdit(holiday)}>수정</button> : null}
                      {isDeletionPending(holiday) ? <button type="button" onClick={() => void restoreHoliday(holiday)}>복구</button> : null}
                      <button type="button" className="danger-button" onClick={() => void deactivateHoliday(holiday)}>{isDeletionPending(holiday) ? '즉시 삭제' : '삭제'}</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {state.data.holidays.length === 0 ? <p className="muted-text">등록된 휴일이 없습니다.</p> : null}
        </div>
      ) : null}
    </section>
  );
}

function AdminCalendarHolidayExcelPreview({ preview }: { preview: CalendarHolidayExcelPreviewResponse }) {
  return (
    <div className="excel-preview-section">
      <div className="excel-preview-counts">
        <span>전체 {preview.totalRows}행</span>
        <span>저장 가능 {preview.saveableCount}행</span>
        <span>신규 {preview.insertCount}행</span>
        <span>갱신 {preview.updateCount}행</span>
        <span>오류 {preview.errorCount}행</span>
      </div>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>행</th>
              <th>날짜</th>
              <th>휴일명</th>
              <th>유형</th>
              <th>결과</th>
              <th>오류</th>
            </tr>
          </thead>
          <tbody>
            {preview.rows.map((row) => (
              <tr key={`${row.excelRowNumber}-${row.date ?? 'empty'}-${row.holidayType ?? 'empty'}`}>
                <td>{row.excelRowNumber}</td>
                <td>{row.date ?? '-'}</td>
                <td>{row.name ?? '-'}</td>
                <td>{row.holidayType ? <HolidayTypeBadge holidayType={row.holidayType} /> : '-'}</td>
                <td>{calendarHolidayPreviewResultLabel(row.resultType)}</td>
                <td>{row.errorMessages.length > 0 ? row.errorMessages.join(', ') : '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function AdminSectionNav({ onNavigate }: { onNavigate: (view: View) => void }) {
  const groups: Array<{ title: string; items: Array<{ label: string; view: View }> }> = [
    {
      title: '운영',
      items: [
        { label: '사용자 관리', view: { kind: 'admin-users' } },
        { label: '알림 수동 발송', view: { kind: 'admin-send-notification' } },
        { label: '알림 발송 상태', view: { kind: 'admin-notification-deliveries' } },
        { label: '알림 설정 변경 이력', view: { kind: 'admin-notification-preference-audit' } },
        { label: '에스컬레이션 상태', view: { kind: 'admin-work-item-escalations' } }
      ]
    },
    {
      title: '시스템 관리',
      items: [
        { label: '부서', view: { kind: 'admin-departments' } },
        { label: '공휴일', view: { kind: 'admin-calendar-holidays' } }
      ]
    },
    {
      title: '조회',
      items: [
        { label: '권한 매트릭스', view: { kind: 'admin-permission-matrix' } },
        { label: '기준정보 변경 이력', view: { kind: 'admin-master-change-logs' } },
        { label: '업무 시작/완료 이력', view: { kind: 'admin-work-history' } }
      ]
    }
  ];

  return (
    <section className="subsection" aria-label="관리자 메뉴">
      {groups.map((group) => (
        <div key={group.title} className="subsection">
          <div className="subsection-header">
            <h3>{group.title}</h3>
          </div>
          <div className="button-row">
            {group.items.map((item) => (
              <button key={item.label} type="button" onClick={() => onNavigate(item.view)}>{item.label}</button>
            ))}
          </div>
        </div>
      ))}
    </section>
  );
}

function AdminDashboardPage({
  developmentUserKey,
  canManageUsers,
  canReadAdminHistory,
  onNavigate
}: {
  developmentUserKey: string;
  canManageUsers: boolean;
  canReadAdminHistory: boolean;
  onNavigate: (view: View) => void;
}) {
  const [state, setState] = useState<LoadState<AdminDashboardResponse>>({ kind: 'loading' });

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminDashboard(developmentUserKey)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '관리자 대시보드를 불러올 수 없습니다.')));
  }, [developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    getAdminDashboard(developmentUserKey)
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '관리자 대시보드를 불러올 수 없습니다.'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [developmentUserKey]);

  return (
    <section className="panel-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">System Administrator</p>
          <h2>관리자</h2>
        </div>
        <button type="button" onClick={load}>새로고침</button>
      </div>
      <p className="muted-text">관리자 기능은 서버 권한으로 강제됩니다. 기존 업무 입력 권한을 관리자 권한으로 우회하지 않습니다.</p>
      {state.kind === 'loading' ? <p>대시보드를 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="admin-dashboard-grid">
          <article className="admin-dashboard-card">
            <span>승인 대기 사용자</span>
            <strong>{state.data.pendingUserCount}건</strong>
            <p>역할이 부여되지 않은 Entra 사용자입니다.</p>
            <button type="button" onClick={() => onNavigate({ kind: 'admin-users' })}>사용자 관리</button>
          </article>
          <article className="admin-dashboard-card" data-tone="danger">
            <span>발송 실패</span>
            <strong>{state.data.failedDeliveryCount}건</strong>
            <p>외부 알림 발송이 실패한 건입니다. 상세에서 실패 채널, 수신자, 오류 사유를 확인하세요.</p>
            <button type="button" onClick={() => onNavigate({ kind: 'admin-notification-deliveries', status: 'Failed' })}>실패 알림 보기</button>
          </article>
          <article className="admin-dashboard-card" data-tone="warning">
            <span>발송 대기</span>
            <strong>{state.data.pendingDeliveryCount}건</strong>
            <p>아직 worker가 처리하지 않았거나 다음 재시도 시각을 기다리는 외부 알림입니다.</p>
            <button type="button" onClick={() => onNavigate({ kind: 'admin-notification-deliveries', status: 'Pending' })}>대기 알림 보기</button>
          </article>
          <article className="admin-dashboard-card" data-tone="warning">
            <span>발송 처리 중</span>
            <strong>{state.data.processingDeliveryCount}건</strong>
            <p>한 worker가 claim lease 안에서 처리 중인 외부 알림입니다.</p>
            <button type="button" onClick={() => onNavigate({ kind: 'admin-notification-deliveries', status: 'Processing' })}>처리 중 알림 보기</button>
          </article>
          <article className="admin-dashboard-card">
            <span>발송 완료</span>
            <strong>{state.data.sentDeliveryCount}건</strong>
            <p>외부 provider가 요청을 수락해 완료된 알림입니다.</p>
            <button type="button" onClick={() => onNavigate({ kind: 'admin-notification-deliveries', status: 'Sent' })}>완료 알림 보기</button>
          </article>
          <article className="admin-dashboard-card">
            <span>마지막 일일 요약</span>
            <strong>{formatNullableDateTime(state.data.lastDailyDigestSentAtUtc)}</strong>
            <p>Daily Digest가 마지막으로 발송 또는 dry-run 처리된 시각입니다.</p>
          </article>
          <article className="admin-dashboard-card admin-dashboard-card-wide" data-tone="warning">
            <span>진행 중 에스컬레이션</span>
            <strong>{state.data.activeEscalationCount}건</strong>
            <p>예정일 임박 또는 초과 상태로 아직 해소되지 않은 업무입니다. 완료/취소 시 해소됩니다.</p>
            <div className="escalation-level-breakdown" aria-label="에스컬레이션 단계별 건수">
              {dashboardEscalationLevels(state.data.activeEscalationLevels).map((item) => (
                <button
                  key={item.level}
                  type="button"
                  onClick={() => onNavigate({ kind: 'admin-work-item-escalations', status: 'Active', level: item.level })}
                >
                  <span>{item.label}</span>
                  <strong>{item.count}건</strong>
                </button>
              ))}
            </div>
            <button type="button" onClick={() => onNavigate({ kind: 'admin-work-item-escalations', status: 'Active' })}>진행 중 에스컬레이션 보기</button>
          </article>
          <article className="admin-dashboard-card">
            <span>최근 기준정보 변경</span>
            <strong>{state.data.recentMasterChangeCount}건</strong>
            <p>최근 7일 기준정보 변경 이력입니다.</p>
            <button type="button" onClick={() => onNavigate({ kind: 'admin-master-change-logs' })}>변경 이력 보기</button>
          </article>
        </div>
      ) : null}
      <AdminSectionNav onNavigate={onNavigate} />
      <section className="subsection">
        <h3>권한 상태</h3>
        <p className="muted-text">
          사용자/부서 관리 {canManageUsers ? '가능' : '불가'} · 관리자 이력 조회 {canReadAdminHistory ? '가능' : '불가'}
        </p>
      </section>
    </section>
  );
}

function AdminDepartmentsPage({ developmentUserKey }: { developmentUserKey: string }) {
  const [state, setState] = useState<LoadState<AdminDepartmentListResponse>>({ kind: 'loading' });
  const [drafts, setDrafts] = useState<Record<string, UpdateAdminDepartmentRequest>>({});
  const [createDraft, setCreateDraft] = useState<CreateAdminDepartmentRequest>({ code: '', name: '', isActive: true, sortOrder: 1000, reason: null });
  const [createFieldErrors, setCreateFieldErrors] = useState<Record<string, string>>({});
  const [departmentFieldErrors, setDepartmentFieldErrors] = useState<Record<string, Record<string, string>>>({});
  const [message, setMessage] = useState('');
  const [selectedDepartmentIds, setSelectedDepartmentIds] = useState<string[]>([]);
  const [departmentExportBusy, setDepartmentExportBusy] = useState(false);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminDepartments(developmentUserKey)
      .then((data) => {
        setState({ kind: 'ready', data });
        setDrafts(Object.fromEntries(data.departments.map((department) => [department.departmentId, departmentToDraft(department)])));
        setSelectedDepartmentIds([]);
      })
      .catch((error: unknown) => setState(toLoadError(error, '부서 기준정보를 불러올 수 없습니다.')));
  }, [developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    getAdminDepartments(developmentUserKey)
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
          setDrafts(Object.fromEntries(data.departments.map((department) => [department.departmentId, departmentToDraft(department)])));
          setSelectedDepartmentIds([]);
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '부서 기준정보를 불러올 수 없습니다.'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [developmentUserKey]);

  async function create() {
    setMessage('');
    setCreateFieldErrors({});
    try {
      await createAdminDepartment(developmentUserKey, createDraft);
      setCreateDraft({ code: '', name: '', isActive: true, sortOrder: 1000, reason: null });
      setCreateFieldErrors({});
      setMessage('부서를 추가했습니다.');
      load();
    } catch (error) {
      setCreateFieldErrors(fieldErrorsFromApiError(error));
      setMessage(friendlyErrorMessage(error, '부서를 추가할 수 없습니다.'));
    }
  }

  async function save(department: AdminDepartmentMaster) {
    setMessage('');
    setDepartmentFieldErrors((current) => ({ ...current, [department.departmentId]: {} }));
    try {
      await updateAdminDepartment(developmentUserKey, department.departmentId, drafts[department.departmentId] ?? departmentToDraft(department));
      setDepartmentFieldErrors((current) => ({ ...current, [department.departmentId]: {} }));
      setMessage('부서 기준정보를 저장했습니다.');
      load();
    } catch (error) {
      setDepartmentFieldErrors((current) => ({ ...current, [department.departmentId]: fieldErrorsFromApiError(error) }));
      setMessage(friendlyErrorMessage(error, '부서 기준정보를 저장할 수 없습니다.'));
    }
  }

  async function deleteDepartment(department: AdminDepartmentMaster) {
    const scheduled = isDeletionPending(department);
    const message = scheduled
      ? '삭제 예정 데이터를 즉시 완전 삭제합니다. 참조 중인 데이터는 삭제 보류될 수 있습니다. 계속하시겠습니까?'
      : department.userCount > 0
      ? '현재 사용자가 연결된 부서입니다. 삭제 후에도 기존 이력은 유지됩니다. 삭제하면 즉시 비활성화되고 7일 후 완전 삭제 대상으로 예약됩니다. 계속하시겠습니까?'
      : '삭제하면 즉시 비활성화되고 7일 후 완전 삭제 대상으로 예약됩니다. 계속하시겠습니까?';
    if (!window.confirm(message)) {
      return;
    }

    setMessage('');
    const draft = drafts[department.departmentId] ?? departmentToDraft(department);
    try {
      if (scheduled) {
        const result = await purgeAdminDepartment(developmentUserKey, department.departmentId);
        setMessage(summarizeBulkAction(result, '부서 즉시 삭제 처리 완료'));
        load();
        return;
      }

      await deactivateAdminDepartment(developmentUserKey, department.departmentId, {
        ...draft,
        isActive: false,
        reason: draft.reason || '삭제'
      });
      setMessage('부서를 삭제 예정으로 처리했습니다.');
      load();
    } catch (error) {
      setMessage(friendlyErrorMessage(error, '부서를 삭제 예약할 수 없습니다.'));
    }
  }

  async function restoreDepartment(department: AdminDepartmentMaster) {
    if (!window.confirm('선택한 삭제 예정 데이터를 복구합니다.')) {
      return;
    }

    setMessage('');
    const draft = drafts[department.departmentId] ?? departmentToDraft(department);
    try {
      await restoreAdminDepartment(developmentUserKey, department.departmentId, { ...draft, reason: draft.reason || '복구' });
      setMessage('부서를 복구했습니다.');
      load();
    } catch (error) {
      setMessage(friendlyErrorMessage(error, '부서를 복구할 수 없습니다.'));
    }
  }

  async function bulkDeleteDepartments() {
    if (selectedDepartmentIds.length === 0) {
      setMessage('삭제할 부서를 선택해 주세요.');
      return;
    }

    if (!window.confirm('선택한 데이터의 상태에 맞게 삭제 예정 전환 또는 즉시 삭제를 수행합니다. 삭제 예정 데이터는 즉시 완전 삭제를 시도하며, 참조 중인 데이터는 삭제 보류될 수 있습니다. 계속하시겠습니까?')) {
      return;
    }

    setMessage('');
    try {
      const result = await bulkDeleteAdminDepartments(developmentUserKey, { ids: selectedDepartmentIds, reason: '선택 삭제' });
      setMessage(summarizeBulkAction(result, '선택 삭제 처리 완료'));
      load();
    } catch (error) {
      setMessage(friendlyErrorMessage(error, '선택 삭제를 처리할 수 없습니다.'));
    }
  }

  async function bulkRestoreDepartments() {
    if (selectedDepartmentIds.length === 0) {
      setMessage('복구할 부서를 선택해 주세요.');
      return;
    }

    if (!window.confirm('선택한 삭제 예정 데이터를 복구합니다.')) {
      return;
    }

    setMessage('');
    try {
      const result = await bulkRestoreAdminDepartments(developmentUserKey, { ids: selectedDepartmentIds, reason: '선택 복구' });
      setMessage(summarizeBulkAction(result, '선택 복구 처리 완료'));
      load();
    } catch (error) {
      setMessage(friendlyErrorMessage(error, '선택 복구를 처리할 수 없습니다.'));
    }
  }

  const visibleDepartments = state.kind === 'ready' ? state.data.departments : [];
  const visibleDepartmentIds = visibleDepartments.map((department) => department.departmentId);
  const allDepartmentsSelected = visibleDepartmentIds.length > 0 && visibleDepartmentIds.every((id) => selectedDepartmentIds.includes(id));

  return (
    <AdminPageShell eyebrow="System Management" title="부서 관리" onRefresh={load} message="">
      <div className="subsection">
        <h3>부서 추가</h3>
        <div className="detail-grid">
          <label className={createFieldErrors.code ? 'form-field compact-field has-error' : 'form-field compact-field'}>
            <span>코드</span>
            <input value={createDraft.code} onChange={(event) => setCreateDraft((current) => ({ ...current, code: event.target.value.toUpperCase() }))} />
            <small className="muted-text">영문 대문자, 숫자, 하이픈(-), 언더스코어(_) 2~50자</small>
            {createFieldErrors.code ? <small role="alert" className="field-error-message">{createFieldErrors.code}</small> : null}
          </label>
          <label className={createFieldErrors.name ? 'form-field compact-field has-error' : 'form-field compact-field'}>
            <span>부서명</span>
            <input value={createDraft.name} onChange={(event) => setCreateDraft((current) => ({ ...current, name: event.target.value }))} />
            <small className="muted-text">한글, 영문, 숫자, 공백, 괄호, 하이픈만 사용</small>
            {createFieldErrors.name ? <small role="alert" className="field-error-message">{createFieldErrors.name}</small> : null}
          </label>
          <label className={createFieldErrors.sortOrder ? 'form-field compact-field has-error' : 'form-field compact-field'}>
            <span>정렬</span>
            <input type="number" min="0" max="9999" value={createDraft.sortOrder} onChange={(event) => setCreateDraft((current) => ({ ...current, sortOrder: Number(event.target.value) }))} />
            <small className="muted-text">0 이상 9999 이하 숫자</small>
            {createFieldErrors.sortOrder ? <small role="alert" className="field-error-message">{createFieldErrors.sortOrder}</small> : null}
          </label>
          <label className="form-field compact-field"><span>변경 사유</span><input value={createDraft.reason ?? ''} onChange={(event) => setCreateDraft((current) => ({ ...current, reason: event.target.value || null }))} placeholder="선택 입력" /></label>
        </div>
        <button type="button" onClick={() => void create()}>추가</button>
      </div>
      <ActionFeedback message={message} tone={message.includes('수 없습니다') || message.includes('선택해 주세요') ? 'error' : message ? 'success' : 'neutral'} />
      {state.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="admin-departments"
          visibleIds={visibleDepartmentIds}
          selectedIds={new Set(selectedDepartmentIds)}
          allSelected={allDepartmentsSelected}
          busy={departmentExportBusy}
          onBusyChange={setDepartmentExportBusy}
          onToggleAll={(checked) => setSelectedDepartmentIds(checked ? visibleDepartmentIds : [])}
          onClear={() => setSelectedDepartmentIds([])}
        />
      ) : null}
      {state.kind === 'ready' ? (
        <div className="bulk-action-bar">
          <span>선택 {selectedDepartmentIds.length}건</span>
          <button type="button" onClick={() => void bulkDeleteDepartments()} disabled={selectedDepartmentIds.length === 0}>선택 삭제</button>
          <button type="button" onClick={() => void bulkRestoreDepartments()} disabled={selectedDepartmentIds.length === 0}>선택 복구</button>
        </div>
      ) : null}
      {state.kind === 'loading' ? <p>부서 목록을 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="table-scroll">
          <table>
            <thead><tr><th><input type="checkbox" aria-label="부서 전체 선택" checked={allDepartmentsSelected} onChange={(event) => setSelectedDepartmentIds(event.target.checked ? visibleDepartmentIds : [])} /></th><th>코드</th><th>부서명</th><th>정렬</th><th>상태</th><th>사용자</th><th>변경 사유</th><th>작업</th></tr></thead>
            <tbody>
              {state.data.departments.map((department) => {
                const draft = drafts[department.departmentId] ?? departmentToDraft(department);
                const fieldErrors = departmentFieldErrors[department.departmentId] ?? {};
                return (
                  <tr key={department.departmentId}>
                    <td>
                      <input
                        type="checkbox"
                        aria-label={`${department.name} 선택`}
                        checked={selectedDepartmentIds.includes(department.departmentId)}
                        onChange={(event) => setSelectedDepartmentIds((current) => (
                          event.target.checked
                            ? [...current, department.departmentId]
                            : current.filter((id) => id !== department.departmentId)
                        ))}
                      />
                    </td>
                    <td><strong>{department.code}</strong></td>
                    <td>
                      <label className={fieldErrors.name ? 'form-field compact-field has-error' : 'form-field compact-field'}>
                        <input value={draft.name} onChange={(event) => setDrafts((current) => ({ ...current, [department.departmentId]: { ...draft, name: event.target.value } }))} />
                        {fieldErrors.name ? <small role="alert" className="field-error-message">{fieldErrors.name}</small> : null}
                      </label>
                    </td>
                    <td>
                      <label className={fieldErrors.sortOrder ? 'form-field compact-field has-error' : 'form-field compact-field'}>
                        <input type="number" min="0" max="9999" value={draft.sortOrder} onChange={(event) => setDrafts((current) => ({ ...current, [department.departmentId]: { ...draft, sortOrder: Number(event.target.value) } }))} />
                        {fieldErrors.sortOrder ? <small role="alert" className="field-error-message">{fieldErrors.sortOrder}</small> : null}
                      </label>
                    </td>
                    <td>
                      <DeletionStatusDisplay
                        isActive={department.isActive}
                        lifecycleStatus={department.lifecycleStatus}
                        lifecycleStatusLabel={department.lifecycleStatusLabel}
                        deletionRequestedAtUtc={department.deletionRequestedAtUtc}
                        scheduledHardDeleteAtUtc={department.scheduledHardDeleteAtUtc}
                        scheduledHardDeleteLabel={department.scheduledHardDeleteLabel}
                        purgeBlockedAtUtc={department.purgeBlockedAtUtc}
                        purgeBlockedReason={department.purgeBlockedReason}
                      />
                      <label className="inline-check">
                        <input
                          type="checkbox"
                          checked={draft.isActive}
                          disabled={Boolean(department.deletionRequestedAtUtc)}
                          onChange={(event) => setDrafts((current) => ({ ...current, [department.departmentId]: { ...draft, isActive: event.target.checked } }))}
                        />
                        활성
                      </label>
                    </td>
                    <td>{department.userCount}명{!draft.isActive && department.userCount > 0 ? <small className="warning-text"> · 기존 소속 유지</small> : null}</td>
                    <td><input value={draft.reason ?? ''} onChange={(event) => setDrafts((current) => ({ ...current, [department.departmentId]: { ...draft, reason: event.target.value || null } }))} placeholder="선택 입력" /></td>
                    <td>
                      <div className="button-row">
                        <button type="button" onClick={() => void save(department)}>저장</button>
                        {isDeletionPending(department) ? <button type="button" onClick={() => void restoreDepartment(department)}>복구</button> : null}
                        <button type="button" className="danger-button" onClick={() => void deleteDepartment(department)}>{isDeletionPending(department) ? '즉시 삭제' : '삭제'}</button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}
    </AdminPageShell>
  );
}

function AdminPermissionMatrixPage({ developmentUserKey }: { developmentUserKey: string }) {
  const [state, setState] = useState<LoadState<PermissionMatrixResponse>>({ kind: 'loading' });
  const permissionIds = state.kind === 'ready' ? state.data.permissions.map((permission) => permission.permissionId) : [];
  const permissionSelection = useSelectedRows(permissionIds);

  useEffect(() => {
    getPermissionMatrix(developmentUserKey)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '권한 매트릭스를 불러올 수 없습니다.')));
  }, [developmentUserKey]);

  return (
    <AdminPageShell eyebrow="Authorization" title="권한 매트릭스" message="">
      <p className="muted-text">권한 편집은 이번 TASK 범위가 아닙니다.</p>
      {state.kind === 'loading' ? <p>권한 정보를 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="table-scroll">
          <SelectedExportTray
            developmentUserKey={developmentUserKey}
            screen="admin-permissions"
            visibleIds={permissionIds}
            selectedIds={permissionSelection.selectedIds}
            allSelected={permissionSelection.allSelected}
            busy={permissionSelection.busy}
            onBusyChange={permissionSelection.setBusy}
            onToggleAll={permissionSelection.toggleAll}
            onClear={permissionSelection.clear}
          />
          <table className="permission-matrix-table">
            <thead>
              <tr>
                <th>선택</th>
                <th className="permission-matrix-label-cell">권한</th>
                {state.data.roles.map((role) => <th key={role.roleId} className="permission-matrix-value-cell">{role.name}<br /><small>{role.code}</small></th>)}
              </tr>
            </thead>
            <tbody>
              {state.data.permissions.map((permission) => (
                <tr key={permission.permissionId}>
                  <td><SelectionCheckbox checked={permissionSelection.selectedIds.has(permission.permissionId)} disabled={permissionSelection.busy} label={`${permission.name} 선택`} onChange={(checked) => permissionSelection.toggle(permission.permissionId, checked)} /></td>
                  <td className="permission-matrix-label-cell"><strong>{permission.name}</strong><br /><small>{permission.code}</small></td>
                  {state.data.roles.map((role) => (
                    <td key={role.roleId} className="permission-matrix-value-cell">{hasPermissionAssignment(state.data, role.roleId, permission.permissionId) ? '예' : '-'}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </AdminPageShell>
  );
}

function AdminMasterChangeLogsPage({ developmentUserKey }: { developmentUserKey: string }) {
  const [state, setState] = useState<LoadState<AdminMasterChangeLogListResponse>>({ kind: 'loading' });
  const masterHistoryIds = state.kind === 'ready' ? state.data.items.map((item) => item.changeLogId) : [];
  const masterHistorySelection = useSelectedRows(masterHistoryIds);
  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminMasterChangeLogs(developmentUserKey)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '기준정보 변경 이력을 불러올 수 없습니다.')));
  }, [developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    getAdminMasterChangeLogs(developmentUserKey)
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '기준정보 변경 이력을 불러올 수 없습니다.'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [developmentUserKey]);

  return (
    <AdminPageShell eyebrow="History" title="기준정보 변경 이력" onRefresh={load} message="">
      {state.kind === 'loading' ? <p>변경 이력을 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="table-scroll">
          <SelectedExportTray
            developmentUserKey={developmentUserKey}
            screen="admin-master-history"
            visibleIds={masterHistoryIds}
            selectedIds={masterHistorySelection.selectedIds}
            allSelected={masterHistorySelection.allSelected}
            busy={masterHistorySelection.busy}
            onBusyChange={masterHistorySelection.setBusy}
            onToggleAll={masterHistorySelection.toggleAll}
            onClear={masterHistorySelection.clear}
          />
          <table>
            <thead><tr><th>선택</th><th>일시</th><th>대상</th><th>작업</th><th>변경자</th><th>사유</th><th>변경 요약</th></tr></thead>
            <tbody>
              {state.data.items.map((item) => (
                <tr key={item.changeLogId}>
                  <td><SelectionCheckbox checked={masterHistorySelection.selectedIds.has(item.changeLogId)} disabled={masterHistorySelection.busy} label={`${item.entityType} 변경 이력 선택`} onChange={(checked) => masterHistorySelection.toggle(item.changeLogId, checked)} /></td>
                  <td>{formatDateTime(item.changedAtUtc)}</td>
                  <td>{item.entityType}<br /><small>{item.entityId ?? '-'}</small></td>
                  <td>{item.action}</td>
                  <td>{item.changedByDisplayName ?? '-'}</td>
                  <td>{item.reason ?? '-'}</td>
                  <td>{summarizeJsonChange(item.beforeJson, item.afterJson)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {state.data.items.length === 0 ? <p className="muted-text">기록된 변경 이력이 없습니다.</p> : null}
        </div>
      ) : null}
    </AdminPageShell>
  );
}

function AdminWorkHistoryPage({ developmentUserKey }: { developmentUserKey: string }) {
  const [state, setState] = useState<LoadState<AdminWorkItemHistoryListResponse>>({ kind: 'loading' });
  const workHistoryIds = state.kind === 'ready' ? state.data.items.map((item) => item.workItemId) : [];
  const workHistorySelection = useSelectedRows(workHistoryIds);
  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminWorkItemHistory(developmentUserKey)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '업무 이력을 불러올 수 없습니다.')));
  }, [developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    getAdminWorkItemHistory(developmentUserKey)
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '업무 이력을 불러올 수 없습니다.'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [developmentUserKey]);

  return (
    <AdminPageShell eyebrow="History" title="업무 시작/완료 이력" onRefresh={load} message="">
      <p className="muted-text">별도 이벤트 테이블이 아니라 work_items의 started/completed/cancelled timestamp를 조회합니다.</p>
      {state.kind === 'loading' ? <p>업무 이력을 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="table-scroll">
          <SelectedExportTray
            developmentUserKey={developmentUserKey}
            screen="admin-work-history"
            visibleIds={workHistoryIds}
            selectedIds={workHistorySelection.selectedIds}
            allSelected={workHistorySelection.allSelected}
            busy={workHistorySelection.busy}
            onBusyChange={workHistorySelection.setBusy}
            onToggleAll={workHistorySelection.toggleAll}
            onClear={workHistorySelection.clear}
          />
          <table>
            <thead><tr><th>선택</th><th>프로젝트</th><th>업무</th><th>담당자</th><th>상태</th><th>시작</th><th>완료</th><th>취소</th></tr></thead>
            <tbody>
              {state.data.items.map((item) => (
                <tr key={item.workItemId}>
                  <td><SelectionCheckbox checked={workHistorySelection.selectedIds.has(item.workItemId)} disabled={workHistorySelection.busy} label={`${item.title} 업무 이력 선택`} onChange={(checked) => workHistorySelection.toggle(item.workItemId, checked)} /></td>
                  <td><strong>{item.projectTitle}</strong><br /><small>{item.projectCode}</small></td>
                  <td>{item.title}<br /><small>{item.workflowStageName}</small></td>
                  <td>{item.assignedDisplayName ?? '-'}</td>
                  <td>{item.status}</td>
                  <td>{formatNullableDateTime(item.startedAtUtc)}</td>
                  <td>{formatNullableDateTime(item.completedAtUtc)}</td>
                  <td>{formatNullableDateTime(item.cancelledAtUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {state.data.items.length === 0 ? <p className="muted-text">조회 가능한 업무 이력이 없습니다.</p> : null}
        </div>
      ) : null}
    </AdminPageShell>
  );
}

type ManualNotificationDraft = {
  sendMode: 'Personal' | 'ChannelNotice' | 'WorkAssignment';
  notificationKind: string;
  title: string;
  projectSelectionType: 'Project' | 'Other';
  projectId: string;
  projectName: string;
  message: string;
  channels: string[];
  teamsActivityRecipientUserIds: string[];
  mailRecipientUserIds: string[];
  mailRecipientEmailsText: string;
  workAssigneeUserIds: string[];
  workflowStageCode: string;
  dueDate: string;
};

const manualSendModes = [
  { value: 'Personal', label: '개인 알림', description: 'Teams Activity 또는 Mail로 특정 사용자에게만 보냅니다.' },
  { value: 'ChannelNotice', label: '채널 공지', description: '설정된 Teams 채널에 게시하고 로그인된 active 사용자가 상세를 볼 수 있습니다.' },
  { value: 'WorkAssignment', label: '업무 배정', description: '실제 내 업무를 생성하고 담당자에게 알립니다.' }
] as const;

const manualNotificationKinds = [
  { value: 'ProjectCreated', label: '프로젝트 생성 알림' },
  { value: 'WorkItemAssigned', label: '업무 배정 알림' },
  { value: 'Urgent', label: '긴급 알림' },
  { value: 'Custom', label: '일반 알림' }
];

const manualNotificationChannels = [
  { value: 'TeamsActivity', label: 'Teams Activity' },
  { value: 'Mail', label: 'Mail' }
];

const manualWorkflowStageOptions = [
  { value: 'ProductionPlanning', label: '생산계획·담당자' },
  { value: 'DesignPanelInfo', label: '제품명·사이즈' },
  { value: 'ProcurementInfo', label: '구매정보' },
  { value: 'MaterialArrived', label: '자재 도착' },
  { value: 'IQC', label: '수입검사' },
  { value: 'ManufacturingWork', label: '제조 작업' },
  { value: 'LQC', label: 'LQC' },
  { value: 'OQC', label: '자체검수' },
  { value: 'PackingCompleted', label: '포장 완료' }
];

function createManualNotificationDraft(): ManualNotificationDraft {
  return {
    sendMode: 'Personal',
    notificationKind: 'ProjectCreated',
    title: '[테스트] 프로젝트 생성 알림',
    projectSelectionType: 'Other',
    projectId: '',
    projectName: 'TASK-NOTIFY-003 통합 알림 테스트',
    message: 'EMI 프로젝트 통합관리시스템 프로젝트 생성 알림 3채널 최종 검수입니다. 실제 업무 알림이 아닙니다.',
    channels: ['TeamsActivity', 'Mail'],
    teamsActivityRecipientUserIds: [],
    mailRecipientUserIds: [],
    mailRecipientEmailsText: '',
    workAssigneeUserIds: [],
    workflowStageCode: 'ProductionPlanning',
    dueDate: ''
  };
}

function AdminManualNotificationPage({
  developmentUserKey,
  onOpenDeliveries,
  onBack
}: {
  developmentUserKey: string;
  onOpenDeliveries: () => void;
  onBack: () => void;
}) {
  const [usersState, setUsersState] = useState<LoadState<AdminUsersResponse>>({ kind: 'loading' });
  const [projectsState, setProjectsState] = useState<LoadState<ProjectListResponse>>({ kind: 'loading' });
  const [draft, setDraft] = useState<ManualNotificationDraft>(() => createManualNotificationDraft());
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const [result, setResult] = useState<AdminManualNotificationSendResponse | null>(null);
  const [isSending, setIsSending] = useState(false);
  const redirectTimerRef = useRef<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    getAdminUsers(developmentUserKey)
      .then((data) => {
        if (cancelled) {
          return;
        }
        setUsersState({ kind: 'ready', data });
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setUsersState(toLoadError(error, '사용자 목록을 불러올 수 없습니다.'));
        }
      });
    listProjects(developmentUserKey, '', 'All')
      .then((data) => {
        if (!cancelled) {
          setProjectsState({ kind: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setProjectsState(toLoadError(error, '프로젝트 목록을 불러올 수 없습니다.'));
        }
      });
    return () => {
      cancelled = true;
      if (redirectTimerRef.current !== null) {
        window.clearTimeout(redirectTimerRef.current);
      }
    };
  }, [developmentUserKey]);

  const activeUsers = usersState.kind === 'ready' ? usersState.data.users.filter((user) => user.isActive) : [];
  const entraUsers = activeUsers.filter((user) => user.authProvider === 'EntraId');
  const mailUsers = activeUsers.filter((user) => Boolean(user.email));
  const projects = projectsState.kind === 'ready' ? projectsState.data.items : [];
  const selectedProject = projects.find((project) => project.projectId === draft.projectId);
  const selectedProjectName = draft.projectSelectionType === 'Project'
    ? selectedProject?.projectTitle ?? ''
    : draft.projectName.trim() || '기타';
  const effectiveNotificationKind = draft.sendMode === 'WorkAssignment' ? 'WorkItemAssigned' : draft.notificationKind;
  const notificationKindLabel = manualNotificationKindLabel(effectiveNotificationKind);
  const manualBodyPreview = buildManualNotificationBodyPreview(notificationKindLabel, selectedProjectName, draft.title, draft.message);
  const teamsActivityPreview = `${notificationKindLabel}, ${draft.title}\n${summarizeInline(draft.message, 150)}`;

  const setSendMode = (sendMode: ManualNotificationDraft['sendMode']) => {
    setDraft((current) => {
      if (sendMode === 'ChannelNotice') {
        return {
          ...current,
          sendMode,
          channels: ['TeamsChannel'],
          teamsActivityRecipientUserIds: [],
          mailRecipientUserIds: [],
          mailRecipientEmailsText: '',
          workAssigneeUserIds: []
        };
      }

      if (sendMode === 'WorkAssignment') {
        return {
          ...current,
          sendMode,
          notificationKind: 'WorkItemAssigned',
          channels: current.channels.filter((channel) => channel === 'TeamsActivity' || channel === 'Mail'),
          teamsActivityRecipientUserIds: [],
          mailRecipientUserIds: [],
          mailRecipientEmailsText: ''
        };
      }

      return {
        ...current,
        sendMode,
        channels: current.channels.filter((channel) => channel === 'TeamsActivity' || channel === 'Mail'),
        workAssigneeUserIds: []
      };
    });
  };

  const setChannel = (channel: string, checked: boolean) => {
    setDraft((current) => ({
      ...current,
      channels: checked
        ? [...current.channels.filter((item) => item !== channel), channel]
        : current.channels.filter((item) => item !== channel)
    }));
  };

  const validate = () => {
    const errors: Record<string, string> = {};
    if (!draft.title.trim()) {
      errors.title = '제목은 필수입니다.';
    }
    if (!draft.message.trim()) {
      errors.message = '본문은 필수입니다.';
    }
    if (draft.projectSelectionType === 'Project' && !draft.projectId) {
      errors.projectId = '프로젝트를 선택하거나 기타를 선택해 주세요.';
    }
    if (draft.sendMode === 'WorkAssignment' && draft.projectSelectionType !== 'Project') {
      errors.projectId = '업무 배정은 기존 프로젝트를 선택해야 합니다.';
    }
    if (draft.sendMode !== 'WorkAssignment' && draft.channels.length === 0) {
      errors.channels = '발송 채널을 하나 이상 선택해 주세요.';
    }
    if (draft.sendMode === 'Personal' && draft.channels.includes('TeamsActivity') && draft.teamsActivityRecipientUserIds.length === 0) {
      errors.teamsActivityRecipientUserIds = 'Teams Activity 수신자를 한 명 이상 선택해 주세요.';
    }
    const mailEmails = splitEmailList(draft.mailRecipientEmailsText);
    if (draft.sendMode === 'Personal' && draft.channels.includes('Mail') && draft.mailRecipientUserIds.length === 0 && mailEmails.length === 0) {
      errors.mailRecipients = '메일 수신자를 한 명 이상 선택하거나 이메일을 입력해 주세요.';
    }
    if (mailEmails.some((email) => !isEmailLike(email))) {
      errors.mailRecipientEmailsText = '메일 수신자 이메일 형식이 올바르지 않습니다.';
    }
    if (draft.sendMode === 'WorkAssignment' && draft.workAssigneeUserIds.length === 0) {
      errors.workAssigneeUserIds = '업무 담당자를 한 명 이상 선택해 주세요.';
    }
    if (draft.sendMode === 'WorkAssignment' && !draft.workflowStageCode) {
      errors.workflowStageCode = '업무 단계를 선택해 주세요.';
    }
    return errors;
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setMessage('');
    setResult(null);
    if (redirectTimerRef.current !== null) {
      window.clearTimeout(redirectTimerRef.current);
      redirectTimerRef.current = null;
    }
    const errors = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      setMessage('입력값을 확인해 주세요.');
      return;
    }

    setIsSending(true);
    setMessage('발송 요청을 저장하고 있습니다.');
    try {
      const response = await sendAdminManualNotification(developmentUserKey, {
        sendMode: draft.sendMode,
        notificationKind: effectiveNotificationKind,
        projectId: draft.projectSelectionType === 'Project' ? draft.projectId : null,
        projectSelectionType: draft.projectSelectionType,
        title: draft.title.trim(),
        projectName: selectedProjectName,
        message: draft.message.trim(),
        channels: draft.channels,
        teamsActivityRecipientUserIds: draft.teamsActivityRecipientUserIds,
        mailRecipientUserIds: draft.mailRecipientUserIds,
        mailRecipientEmails: splitEmailList(draft.mailRecipientEmailsText),
        workAssigneeUserIds: draft.workAssigneeUserIds,
        workflowStageCode: draft.workflowStageCode || null,
        dueDate: draft.dueDate || null
      });
      setResult(response);
      setMessage('발송 요청이 접수되었습니다. 알림발송상태에서 결과를 확인할 수 있습니다. 잠시 후 이동합니다.');
      redirectTimerRef.current = window.setTimeout(() => {
        redirectTimerRef.current = null;
        onOpenDeliveries();
      }, 700);
    } catch (error: unknown) {
      setMessage(friendlyErrorMessage(error, '발송 요청에 실패했습니다. 입력값과 채널 설정을 확인해주세요.'));
    } finally {
      setIsSending(false);
    }
  };

  const reset = () => {
    setDraft(createManualNotificationDraft());
    setFieldErrors({});
    setMessage('');
    setResult(null);
    if (redirectTimerRef.current !== null) {
      window.clearTimeout(redirectTimerRef.current);
      redirectTimerRef.current = null;
    }
  };

  return (
    <AdminPageShell eyebrow="System" title="알림 수동 발송" message="">
      <div className="admin-guidance">
        <p>관리자가 테스트/수동 알림을 작성해 Teams 채널, Teams Activity, Mail로 직접 발송합니다. 실제 프로젝트 row나 workflow event는 생성하지 않습니다.</p>
        <p>발송 버튼을 누르면 delivery가 대기열에 저장되고 worker가 처리합니다. 실제 발송 결과는 알림발송상태에서 확인합니다.</p>
      </div>
      {usersState.kind === 'loading' ? <p>사용자 목록을 불러오는 중입니다.</p> : null}
      {usersState.kind !== 'loading' && usersState.kind !== 'ready' ? <StateMessage state={usersState} /> : null}
      {projectsState.kind === 'loading' ? <p>프로젝트 목록을 불러오는 중입니다.</p> : null}
      {projectsState.kind !== 'loading' && projectsState.kind !== 'ready' ? <StateMessage state={projectsState} /> : null}
      {usersState.kind === 'ready' && projectsState.kind === 'ready' ? (
        <form className="subsection manual-notification-form" onSubmit={(event) => void submit(event)}>
          <fieldset className="manual-channel-fieldset">
            <legend>발송 유형</legend>
            <div className="segmented-control">
              {manualSendModes.map((mode) => (
                <button
                  key={mode.value}
                  type="button"
                  className={draft.sendMode === mode.value ? 'active-filter' : undefined}
                  onClick={() => setSendMode(mode.value)}
                >
                  {mode.label}
                </button>
              ))}
            </div>
            <p className="muted-text">{manualSendModes.find((mode) => mode.value === draft.sendMode)?.description}</p>
          </fieldset>
          <div className="detail-grid">
            <label className="form-field compact-field">
              <span>알림 유형</span>
              <select
                value={effectiveNotificationKind}
                disabled={draft.sendMode === 'WorkAssignment'}
                onChange={(event) => setDraft((current) => ({ ...current, notificationKind: event.target.value }))}
              >
                {manualNotificationKinds.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
              </select>
            </label>
            <label className={fieldErrors.title ? 'form-field compact-field has-error' : 'form-field compact-field'}>
              <span>제목</span>
              <input value={draft.title} onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))} />
              {fieldErrors.title ? <small role="alert" className="field-error-message">{fieldErrors.title}</small> : null}
            </label>
            <label className={fieldErrors.projectId ? 'form-field compact-field has-error' : 'form-field compact-field'}>
              <span>프로젝트</span>
              <select
                value={draft.projectSelectionType === 'Project' ? draft.projectId : '__other__'}
                onChange={(event) => {
                  const value = event.target.value;
                  setDraft((current) => value === '__other__'
                    ? { ...current, projectSelectionType: 'Other', projectId: '' }
                    : { ...current, projectSelectionType: 'Project', projectId: value });
                }}
              >
                <option value="__other__">기타</option>
                {projects.map((project) => (
                  <option key={project.projectId} value={project.projectId}>{project.projectTitle} ({project.projectCode})</option>
                ))}
              </select>
              {fieldErrors.projectId ? <small role="alert" className="field-error-message">{fieldErrors.projectId}</small> : null}
            </label>
            {draft.projectSelectionType === 'Other' ? (
              <label className="form-field compact-field">
                <span>프로젝트명/구분</span>
                <input value={draft.projectName} onChange={(event) => setDraft((current) => ({ ...current, projectName: event.target.value }))} placeholder="기타" />
              </label>
            ) : null}
          </div>
          <label className={fieldErrors.message ? 'form-field has-error' : 'form-field'}>
            <span>본문</span>
            <textarea rows={5} value={draft.message} onChange={(event) => setDraft((current) => ({ ...current, message: event.target.value }))} />
            {fieldErrors.message ? <small role="alert" className="field-error-message">{fieldErrors.message}</small> : null}
          </label>
          {draft.sendMode === 'WorkAssignment' ? (
            <div className="detail-grid">
              <label className={fieldErrors.workAssigneeUserIds ? 'form-field compact-field has-error' : 'form-field compact-field'}>
                <span>업무 담당자</span>
                <select
                  multiple
                  value={draft.workAssigneeUserIds}
                  onChange={(event) => {
                    const values = Array.from(event.currentTarget.selectedOptions, (option) => option.value);
                    setDraft((current) => ({ ...current, workAssigneeUserIds: values }));
                  }}
                >
                  {activeUsers.map((user) => (
                    <option key={user.userId} value={user.userId}>{user.displayName}{user.email ? ` · ${user.email}` : ''}</option>
                  ))}
                </select>
                <small className="muted-text">담당자별로 실제 내 업무가 생성됩니다.</small>
                {fieldErrors.workAssigneeUserIds ? <small role="alert" className="field-error-message">{fieldErrors.workAssigneeUserIds}</small> : null}
              </label>
              <label className={fieldErrors.workflowStageCode ? 'form-field compact-field has-error' : 'form-field compact-field'}>
                <span>업무 단계</span>
                <select value={draft.workflowStageCode} onChange={(event) => setDraft((current) => ({ ...current, workflowStageCode: event.target.value }))}>
                  {manualWorkflowStageOptions.map((stage) => <option key={stage.value} value={stage.value}>{stage.label}</option>)}
                </select>
                {fieldErrors.workflowStageCode ? <small role="alert" className="field-error-message">{fieldErrors.workflowStageCode}</small> : null}
              </label>
              <label className="form-field compact-field">
                <span>예정일</span>
                <input type="date" value={draft.dueDate} onChange={(event) => setDraft((current) => ({ ...current, dueDate: event.target.value }))} />
              </label>
            </div>
          ) : null}
          <section className="manual-preview-panel" aria-label="발송 미리보기">
            <h3>미리보기</h3>
            <div className="detail-grid">
              <div>
                <strong>Mail 제목</strong>
                <p>[{notificationKindLabel}] {draft.title || '제목'}</p>
              </div>
              <div>
                <strong>Teams Activity</strong>
                <p>{teamsActivityPreview}</p>
              </div>
            </div>
            <pre>{manualBodyPreview}</pre>
          </section>
          <fieldset className={fieldErrors.channels ? 'manual-channel-fieldset has-error' : 'manual-channel-fieldset'}>
            <legend>수신/게시 채널</legend>
            {draft.sendMode === 'ChannelNotice' ? (
              <p className="muted-text">설정된 Teams 채널에 게시합니다.</p>
            ) : (
              <>
                <div className="checkbox-row">
                  {manualNotificationChannels.map((channel) => (
                    <label className="inline-check" key={channel.value}>
                      <input
                        type="checkbox"
                        checked={draft.channels.includes(channel.value)}
                        onChange={(event) => setChannel(channel.value, event.target.checked)}
                      />
                      {channel.label}
                    </label>
                  ))}
                </div>
                {draft.sendMode === 'WorkAssignment' ? <small className="muted-text">외부 채널을 선택하지 않아도 인앱 알림과 내 업무는 생성됩니다.</small> : null}
              </>
            )}
            {fieldErrors.channels ? <small role="alert" className="field-error-message">{fieldErrors.channels}</small> : null}
          </fieldset>
          {draft.sendMode === 'Personal' ? (
            <div className="detail-grid">
              <label className={fieldErrors.teamsActivityRecipientUserIds ? 'form-field compact-field has-error' : 'form-field compact-field'}>
                <span>Teams Activity 수신자</span>
                <select
                  multiple
                  value={draft.teamsActivityRecipientUserIds}
                  onChange={(event) => {
                    const values = Array.from(event.currentTarget.selectedOptions, (option) => option.value);
                    setDraft((current) => ({
                      ...current,
                      teamsActivityRecipientUserIds: values
                    }));
                  }}
                >
                  {entraUsers.map((user) => (
                    <option key={user.userId} value={user.userId}>{user.displayName}{user.email ? ` · ${user.email}` : ''}</option>
                  ))}
                </select>
                <small className="muted-text">actual 발송은 EntraId active 사용자만 가능합니다.</small>
                {fieldErrors.teamsActivityRecipientUserIds ? <small role="alert" className="field-error-message">{fieldErrors.teamsActivityRecipientUserIds}</small> : null}
              </label>
              <label className={fieldErrors.mailRecipients ? 'form-field compact-field has-error' : 'form-field compact-field'}>
                <span>Mail 사용자</span>
                <select
                  multiple
                  value={draft.mailRecipientUserIds}
                  onChange={(event) => {
                    const values = Array.from(event.currentTarget.selectedOptions, (option) => option.value);
                    setDraft((current) => ({
                      ...current,
                      mailRecipientUserIds: values
                    }));
                  }}
                >
                  {mailUsers.map((user) => (
                    <option key={user.userId} value={user.userId}>{user.displayName}{user.email ? ` · ${user.email}` : ''}</option>
                  ))}
                </select>
                {fieldErrors.mailRecipients ? <small role="alert" className="field-error-message">{fieldErrors.mailRecipients}</small> : null}
              </label>
              <label className={fieldErrors.mailRecipientEmailsText ? 'form-field compact-field has-error' : 'form-field compact-field'}>
                <span>Mail 직접 입력</span>
                <textarea rows={4} value={draft.mailRecipientEmailsText} onChange={(event) => setDraft((current) => ({ ...current, mailRecipientEmailsText: event.target.value }))} placeholder="쉼표, 세미콜론, 줄바꿈으로 여러 이메일 입력" />
                {fieldErrors.mailRecipientEmailsText ? <small role="alert" className="field-error-message">{fieldErrors.mailRecipientEmailsText}</small> : null}
              </label>
            </div>
          ) : null}
          {draft.sendMode === 'ChannelNotice' ? (
            <div className="detail-grid">
              <label className="form-field compact-field">
                <span>Teams 채널 게시 대상</span>
                <input value="설정된 Teams 채널" readOnly />
              </label>
              <div className="admin-guidance">
                <p>채널 공지는 현재 Teams 멤버십을 시스템에서 알 수 없으므로 로그인된 active 사용자가 상세를 볼 수 있습니다.</p>
              </div>
            </div>
          ) : null}
          <div className="button-row">
            <button type="submit" disabled={isSending}>{isSending ? '요청 저장 중...' : '발송'}</button>
            <button type="button" onClick={reset} disabled={isSending}>초기화</button>
            <button type="button" className="secondary-button" onClick={onBack}>이전 페이지로 돌아가기</button>
          </div>
          <ActionFeedback
            message={message}
            tone={fieldErrors && Object.keys(fieldErrors).length > 0 ? 'error' : result ? 'success' : isSending ? 'loading' : message ? 'error' : 'neutral'}
          />
        </form>
      ) : null}
      {result ? (
        <section className="subsection">
          <h3>발송 요청 결과</h3>
          <p className="muted-text">요청 {result.requestedCount}건 중 {result.queuedCount}건이 대기열에 저장되었습니다. 실제 발송 결과는 알림발송상태에서 확인하세요.</p>
          <div className="table-scroll">
            <table className="admin-table">
              <thead>
                <tr>
                  <th className="admin-table__cell--text">채널</th>
	                  <th className="admin-table__cell--text">대상</th>
	                  <th className="admin-table__cell--status">상태</th>
	                  <th className="admin-table__cell--text">delivery id</th>
                  <th className="admin-table__cell--text">오류/메시지</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((item) => (
	                  <tr key={`${item.channel}-${item.deliveryId ?? item.errorCode ?? 'failed'}`}>
	                    <td className="admin-table__cell--text">{item.channelLabel}</td>
	                    <td className="admin-table__cell--text">{item.target}</td>
	                    <td className="admin-table__cell--status"><StatusBadge label={item.status === 'Queued' ? '요청 접수' : item.status} tone={item.status === 'Queued' ? 'info' : item.status === 'Failed' ? 'danger' : 'neutral'} /></td>
	                    <td className="admin-table__cell--text"><small>{item.deliveryId ?? '-'}</small></td>
                    <td className="admin-table__cell--text">{item.errorCode ?? item.message}{item.errorMessage ? <><br /><small>{item.errorMessage}</small></> : null}</td>
                  </tr>
                ))}
              </tbody>
	            </table>
	          </div>
          <div className="button-row">
            <button type="button" onClick={onOpenDeliveries}>알림발송상태로 이동</button>
            <button type="button" className="secondary-button" onClick={onBack}>이전 페이지로 돌아가기</button>
          </div>
        </section>
      ) : null}
    </AdminPageShell>
  );
}

function isEmailLike(value: string) {
  return /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value);
}

function splitEmailList(value: string) {
  return value
    .split(/[,;\n\r]+/)
    .map((item) => item.trim())
    .filter(Boolean)
    .filter((item, index, items) => items.findIndex((candidate) => candidate.toLowerCase() === item.toLowerCase()) === index);
}

function manualNotificationKindLabel(kind: string) {
  return manualNotificationKinds.find((item) => item.value === kind)?.label ?? '일반 알림';
}

function buildManualNotificationBodyPreview(kindLabel: string, projectName: string, title: string, message: string) {
  return `EMI 프로젝트 통합관리시스템 알림

알림 유형: ${kindLabel}
프로젝트명: ${projectName || '기타'}

제목: ${title || '제목'}
내용:
${message || '내용'}

발송시각: ${formatManualPreviewDate(new Date())}

끝.`;
}

function formatManualPreviewDate(value: Date) {
  const parts = new Intl.DateTimeFormat('sv-SE', {
    timeZone: 'Asia/Seoul',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false
  }).format(value);
  return parts;
}

function summarizeInline(value: string, maxLength: number) {
  const normalized = value.replace(/\s+/g, ' ').trim();
  return normalized.length <= maxLength ? normalized : `${normalized.slice(0, maxLength)}...`;
}

type DeliveryTabKey = 'all' | 'open-failed' | 'open-pending' | 'processing' | 'sent' | 'acknowledged' | 'dismissed' | 'other';

const deliveryTabs: Array<{ key: DeliveryTabKey; label: string }> = [
  { key: 'all', label: '전체' },
  { key: 'open-failed', label: '미처리 실패' },
  { key: 'open-pending', label: '미처리 대기' },
  { key: 'processing', label: '발송 처리 중' },
  { key: 'sent', label: '발송 완료' },
  { key: 'acknowledged', label: '확인됨' },
  { key: 'dismissed', label: '제외됨' },
  { key: 'other', label: 'Dry-run/비활성/제외' }
];

const deliveryTypeOptions = [
  'ManualTest',
  'WorkItemCreated',
  'UrgentBlocking',
  'DailyDigest',
  'DueSoonL0',
  'OverdueL1',
  'OverdueL2',
  'OverdueL3',
  'ProjectCompletion',
  'ReferenceDigest'
];

function deliveryTabFromFilters(status: string | null, handlingStatus: string | null): DeliveryTabKey {
  if (handlingStatus === 'Acknowledged') {
    return 'acknowledged';
  }
  if (handlingStatus === 'Dismissed') {
    return 'dismissed';
  }
  if (status === 'Failed') {
    return 'open-failed';
  }
  if (status === 'Pending') {
    return 'open-pending';
  }
  if (status === 'Processing') {
    return 'processing';
  }
  if (status === 'Sent') {
    return 'sent';
  }
  if (status === 'DryRunSent' || status === 'Disabled' || status === 'Suppressed') {
    return 'other';
  }
  return 'all';
}

function deliveryTabFilters(tab: DeliveryTabKey): { status?: string | null; handlingStatus?: string | null } {
  switch (tab) {
    case 'open-failed':
      return { status: 'Failed', handlingStatus: 'Open' };
    case 'open-pending':
      return { status: 'Pending', handlingStatus: 'Open' };
    case 'processing':
      return { status: 'Processing' };
    case 'sent':
      return { status: 'Sent' };
    case 'acknowledged':
      return { handlingStatus: 'Acknowledged' };
    case 'dismissed':
      return { handlingStatus: 'Dismissed' };
    case 'other':
      return {};
    default:
      return {};
  }
}

function deliveryTabLocalFilter(tab: DeliveryTabKey, item: AdminNotificationDelivery) {
  if (tab === 'other') {
    return item.status === 'DryRunSent' || item.status === 'Disabled' || item.status === 'Suppressed';
  }

  return true;
}

function deliveryTabLabel(tab: DeliveryTabKey) {
  return deliveryTabs.find((item) => item.key === tab)?.label ?? '전체';
}

function deliveryChannelLabel(channel: string) {
  switch (channel) {
    case 'TeamsChannel':
      return 'Teams 채널';
    case 'TeamsActivity':
      return 'Teams Activity';
    case 'Mail':
      return '메일';
    case 'TeamsDirectMessage':
      return 'Teams 개인 dry-run';
    default:
      return channel;
  }
}

function deliveryTypeLabel(deliveryType: string) {
  switch (deliveryType) {
    case 'ManualTest':
      return '수동 알림';
    case 'WorkItemCreated':
      return '업무 배정 알림';
    case 'UrgentBlocking':
      return '긴급 알림';
    case 'DailyDigest':
      return '일일 업무 요약';
    case 'DueSoonL0':
      return '예정일 임박 알림';
    case 'OverdueL1':
    case 'OverdueL2':
    case 'OverdueL3':
      return '예정일 초과 알림';
    case 'ProjectCompletion':
      return '프로젝트 완료 알림';
    case 'ReferenceDigest':
      return '참조 알림';
    default:
      return deliveryType;
  }
}

function deliveryAttemptOutcomeLabel(outcome: string) {
  switch (outcome) {
    case 'Processing':
      return '처리 중';
    case 'Sent':
      return '발송 완료';
    case 'DryRunSent':
      return 'Dry-run 완료';
    case 'Disabled':
      return '채널 비활성';
    case 'Suppressed':
      return '발송 제외';
    case 'RetryScheduled':
      return '재시도 예약';
    case 'FailedPermanent':
      return '영구 실패';
    case 'LeaseExpiredBeforeProviderCall':
      return 'Provider 호출 전 lease 만료';
    case 'LeaseExpiredAfterProviderCallStarted':
      return 'Provider 호출 후 결과 불확실';
    case 'OwnershipLost':
      return 'Claim 소유권 상실';
    default:
      return '알 수 없음';
  }
}

function deliveryActionResultMessage(result: AdminNotificationDeliveryActionResponse) {
  const failedDetails = result.items
    .filter((item) => item.status !== 'Succeeded')
    .map((item) => item.message)
    .slice(0, 2);
  const suffix = failedDetails.length > 0 ? ` (${failedDetails.join(', ')})` : '';
  return `처리 완료 ${result.succeededCount}건, 실패 ${result.failedCount}건, 건너뜀 ${result.skippedCount}건${suffix}`;
}

function shouldShowDeliveryHandlingStatus(status: string) {
  return status === 'Failed' || status === 'Pending';
}

type EscalationTabKey = 'all' | 'active' | 'l0' | 'l1' | 'l2' | 'l3' | 'resolved' | 'cancelled';

const escalationTabs: Array<{ key: EscalationTabKey; label: string }> = [
  { key: 'all', label: '전체' },
  { key: 'active', label: '진행 중' },
  { key: 'l0', label: 'L0 예정일 임박' },
  { key: 'l1', label: 'L1 예정일 초과' },
  { key: 'l2', label: 'L2 +2영업일' },
  { key: 'l3', label: 'L3 +3영업일' },
  { key: 'resolved', label: '해소됨' },
  { key: 'cancelled', label: '취소됨' }
];

function escalationTabFromFilters(status: string | null, level: string | null): EscalationTabKey {
  if (status === 'Resolved') {
    return 'resolved';
  }
  if (status === 'Cancelled') {
    return 'cancelled';
  }
  if (level === 'L0') {
    return 'l0';
  }
  if (level === 'L1') {
    return 'l1';
  }
  if (level === 'L2') {
    return 'l2';
  }
  if (level === 'L3') {
    return 'l3';
  }
  if (status === 'Active') {
    return 'active';
  }
  return 'all';
}

function escalationTabFilters(tab: EscalationTabKey): { status?: string | null; level?: string | null } {
  switch (tab) {
    case 'active':
      return { status: 'Active' };
    case 'l0':
      return { status: 'Active', level: 'L0' };
    case 'l1':
      return { status: 'Active', level: 'L1' };
    case 'l2':
      return { status: 'Active', level: 'L2' };
    case 'l3':
      return { status: 'Active', level: 'L3' };
    case 'resolved':
      return { status: 'Resolved' };
    case 'cancelled':
      return { status: 'Cancelled' };
    default:
      return {};
  }
}

function escalationTabLabel(tab: EscalationTabKey) {
  return escalationTabs.find((item) => item.key === tab)?.label ?? '전체';
}

function AdminNotificationDeliveriesPage({
  developmentUserKey,
  statusFilter,
  handlingStatusFilter,
  channelFilter,
  deliveryTypeFilter,
  onOpenDetail
}: {
  developmentUserKey: string;
  statusFilter: string | null;
  handlingStatusFilter: string | null;
  channelFilter: string | null;
  deliveryTypeFilter: string | null;
  onOpenDetail: (deliveryId: string) => void;
}) {
  const initialTab = deliveryTabFromFilters(statusFilter, handlingStatusFilter);
  const [activeTab, setActiveTab] = useState(initialTab);
  const [channel, setChannel] = useState(channelFilter ?? '');
  const [deliveryType, setDeliveryType] = useState(deliveryTypeFilter ?? '');
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [deliveryExportBusy, setDeliveryExportBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [reprocessOpen, setReprocessOpen] = useState(false);
  const [reprocessReason, setReprocessReason] = useState('');
  const [duplicateRiskAcknowledged, setDuplicateRiskAcknowledged] = useState(false);
  const [reprocessBusy, setReprocessBusy] = useState(false);
  const [state, setState] = useState<LoadState<AdminNotificationDeliveryListResponse>>({ kind: 'loading' });

  useEffect(() => {
    setActiveTab(deliveryTabFromFilters(statusFilter, handlingStatusFilter));
  }, [handlingStatusFilter, statusFilter]);

  useEffect(() => {
    setChannel(channelFilter ?? '');
    setDeliveryType(deliveryTypeFilter ?? '');
  }, [channelFilter, deliveryTypeFilter]);

  const currentFilters = deliveryTabFilters(activeTab);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminNotificationDeliveries(developmentUserKey, {
      status: currentFilters.status,
      handlingStatus: currentFilters.handlingStatus,
      channel: channel || null,
      deliveryType: deliveryType || null
    })
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '알림 발송 상태를 불러올 수 없습니다.')));
  }, [channel, currentFilters.handlingStatus, currentFilters.status, deliveryType, developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    getAdminNotificationDeliveries(developmentUserKey, {
      status: currentFilters.status,
      handlingStatus: currentFilters.handlingStatus,
      channel: channel || null,
      deliveryType: deliveryType || null
    })
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '알림 발송 상태를 불러올 수 없습니다.'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [channel, currentFilters.handlingStatus, currentFilters.status, deliveryType, developmentUserKey]);

  const displayedItems = state.kind === 'ready'
    ? state.data.items.filter((item) => deliveryTabLocalFilter(activeTab, item))
    : [];
  const visibleIds = displayedItems.filter((item) => item.status !== 'Processing').map((item) => item.deliveryId);
  const allSelected = visibleIds.length > 0 && visibleIds.every((id) => selectedIds.includes(id));
  const selectedItems = displayedItems.filter((item) => selectedIds.includes(item.deliveryId));

  const runDeliveryAction = async (action: 'acknowledge' | 'dismiss' | 'retry') => {
    if (selectedIds.length === 0) {
      setMessage('선택된 알림이 없습니다.');
      return;
    }

    const confirmText = action === 'retry'
      ? '선택한 대기 알림을 재발송 대기열에 등록합니다. worker가 다음 주기에서 처리합니다.'
      : action === 'dismiss'
        ? '선택한 알림을 목록 제외 처리합니다. 발송 상태는 변경되지 않습니다.'
        : '선택한 알림을 확인 처리합니다. 발송 상태는 변경되지 않습니다.';
    if (!window.confirm(confirmText)) {
      return;
    }

    const request = {
      ids: selectedIds,
      note: action === 'retry'
        ? '관리자 재발송 요청'
        : action === 'dismiss'
          ? '관리자 목록 제외 처리'
          : '관리자 확인 처리'
    };

    try {
      setMessage(action === 'retry' ? '재발송 요청을 저장하고 있습니다.' : '선택한 알림 처리 상태를 저장하고 있습니다.');
      const result = action === 'retry'
        ? await retryAdminNotificationDeliveries(developmentUserKey, request)
        : action === 'dismiss'
          ? await dismissAdminNotificationDeliveries(developmentUserKey, request)
          : await acknowledgeAdminNotificationDeliveries(developmentUserKey, request);
      setMessage(deliveryActionResultMessage(result));
      setSelectedIds([]);
      load();
    } catch (error: unknown) {
      setMessage(error instanceof Error ? error.message : '알림 처리 중 오류가 발생했습니다.');
    }
  };

  const runFailedReprocess = async () => {
    if (selectedItems.length === 0 || selectedItems.some((item) => item.status !== 'Failed')) {
      setMessage('최종 실패 상태의 알림만 재처리할 수 있습니다.');
      return;
    }
    if (reprocessReason.trim().length < 10 || reprocessReason.trim().length > 500) {
      setMessage('재처리 사유를 10자 이상 500자 이하로 입력해 주세요.');
      return;
    }
    if (!duplicateRiskAcknowledged) {
      setMessage('외부 provider 중복 가능성을 확인해 주세요.');
      return;
    }

    try {
      setReprocessBusy(true);
      setMessage('새 재처리 generation을 만들고 있습니다.');
      const result: AdminNotificationDeliveryReprocessResponse = await reprocessFailedAdminNotificationDeliveries(developmentUserKey, {
        items: selectedItems.map((item) => ({ deliveryId: item.deliveryId, expectedGeneration: item.currentGeneration ?? 1 })),
        reason: reprocessReason.trim(),
        duplicateRiskAcknowledged
      });
      setMessage(result.message);
      setSelectedIds([]);
      setReprocessOpen(false);
      setReprocessReason('');
      setDuplicateRiskAcknowledged(false);
      load();
    } catch (error: unknown) {
      setMessage(error instanceof Error ? error.message : '최종 실패 알림 재처리 중 오류가 발생했습니다.');
    } finally {
      setReprocessBusy(false);
    }
  };

  return (
    <AdminPageShell eyebrow="System" title="알림 발송 상태" onRefresh={load} message="">
      <div className="admin-guidance">
        <p>발송 실패는 외부 채널로 알림을 보내지 못한 건입니다. 확인/제외 처리된 실패 건은 대시보드 실패 건수에서 제외되지만 실제 발송 상태는 변경되지 않습니다.</p>
        <p>발송 대기는 worker 처리 또는 다음 재시도 시각을 기다리는 건입니다. 재발송은 다음 시도 시각만 앞당기며, attempt count는 worker 처리 시 증가합니다.</p>
        <p><strong>현재 필터:</strong> {deliveryTabLabel(activeTab)} · 채널 {channel ? deliveryChannelLabel(channel) : '전체'} · 유형 {deliveryType ? deliveryTypeLabel(deliveryType) : '전체'}</p>
      </div>
      <div className="admin-filter-panel">
        <div className="segmented-control" role="tablist" aria-label="알림 발송 상태 필터">
          {deliveryTabs.map((tab) => (
            <button
              key={tab.key}
              type="button"
              role="tab"
              aria-selected={activeTab === tab.key}
              onClick={() => {
                setActiveTab(tab.key);
                setSelectedIds([]);
              }}
            >
              {tab.label}
            </button>
          ))}
        </div>
        <label>
          채널
          <select value={channel} onChange={(event) => setChannel(event.target.value)}>
            <option value="">전체</option>
            <option value="TeamsChannel">Teams 채널</option>
            <option value="TeamsActivity">Teams Activity</option>
            <option value="Mail">메일</option>
            <option value="TeamsDirectMessage">Teams 개인 dry-run</option>
          </select>
        </label>
        <label>
          유형
          <select value={deliveryType} onChange={(event) => setDeliveryType(event.target.value)}>
            <option value="">전체</option>
            {deliveryTypeOptions.map((option) => <option key={option} value={option}>{deliveryTypeLabel(option)}</option>)}
          </select>
        </label>
      </div>
      <SelectedExportTray
        developmentUserKey={developmentUserKey}
        screen="admin-notification-deliveries"
        visibleIds={visibleIds}
        selectedIds={new Set(selectedIds)}
        allSelected={allSelected}
        busy={deliveryExportBusy}
        filters={{
          status: currentFilters.status ?? undefined,
          handlingStatus: currentFilters.handlingStatus ?? undefined,
          channel: channel || undefined,
          deliveryType: deliveryType || undefined
        }}
        onBusyChange={setDeliveryExportBusy}
        onToggleAll={(checked) => setSelectedIds(checked ? visibleIds : [])}
        onClear={() => setSelectedIds([])}
      />
      <div className="bulk-action-bar">
        <span>선택 {selectedIds.length}건</span>
        <button type="button" onClick={() => void runDeliveryAction('acknowledge')} disabled={selectedIds.length === 0}>선택 확인 처리</button>
        <button type="button" onClick={() => void runDeliveryAction('dismiss')} disabled={selectedIds.length === 0}>선택 제외 처리</button>
        <button type="button" onClick={() => void runDeliveryAction('retry')} disabled={selectedItems.length === 0 || selectedItems.some((item) => item.status !== 'Pending')}>선택 재발송</button>
        <button
          type="button"
          className="danger-button"
          onClick={() => setReprocessOpen((current) => !current)}
          disabled={selectedItems.length === 0 || selectedItems.some((item) => item.status !== 'Failed' || (item.currentGeneration ?? 1) >= 5)}
        >최종 실패 재처리</button>
        {displayedItems.some((item) => item.status === 'Processing') ? <small>발송 처리 중인 항목은 claim 소유권 보호를 위해 선택하거나 상태를 변경할 수 없습니다.</small> : null}
      </div>
      {reprocessOpen ? (
        <section className="delivery-reprocess-panel" aria-label="최종 실패 알림 재처리">
          <div>
            <strong>선택 {selectedItems.length}건을 새 generation으로 재처리</strong>
            <p>기존 시도 이력은 유지됩니다. Provider 호출 뒤 결과 저장이 끊긴 건은 이미 전달됐을 수 있으므로 중복 발송 가능성이 있습니다.</p>
          </div>
          <label>재처리 사유<textarea rows={3} maxLength={500} value={reprocessReason} onChange={(event) => setReprocessReason(event.target.value)} placeholder="장애 원인 확인 및 재처리 근거를 10자 이상 입력" /></label>
          <label className="delivery-reprocess-ack"><input type="checkbox" checked={duplicateRiskAcknowledged} onChange={(event) => setDuplicateRiskAcknowledged(event.target.checked)} /><span>외부 provider에 이미 전달되었을 가능성과 중복 위험을 확인했습니다.</span></label>
          <div className="button-row"><button type="button" className="danger-button" disabled={reprocessBusy} onClick={() => void runFailedReprocess()}>{reprocessBusy ? '재처리 중…' : '새 generation 시작'}</button><button type="button" className="secondary-button" disabled={reprocessBusy} onClick={() => setReprocessOpen(false)}>취소</button></div>
        </section>
      ) : null}
      <ActionFeedback message={message} tone={message.includes('오류') ? 'error' : message.includes('저장하고') ? 'loading' : message ? 'success' : 'neutral'} />
      {state.kind === 'loading' ? <p>발송 상태를 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="table-scroll">
          <table className="admin-table">
            <thead>
              <tr>
                <th className="admin-table__cell--checkbox">
                  <input
                    type="checkbox"
                    aria-label="알림 발송 이력 전체 선택"
                    checked={allSelected}
                    onChange={(event) => setSelectedIds(event.target.checked ? visibleIds : [])}
                  />
                </th>
                <th className="admin-table__cell--text">알림</th>
                <th className="admin-table__cell--text">대상</th>
                <th className="admin-table__cell--text">채널/유형</th>
                <th className="admin-table__cell--status">상태</th>
                <th className="admin-table__cell--number">시도</th>
                <th className="admin-table__cell--date">시각</th>
                <th className="admin-table__cell--text">오류/조치</th>
              </tr>
            </thead>
            <tbody>
              {displayedItems.map((item) => (
                <tr key={item.deliveryId}>
                  <td className="admin-table__cell--checkbox">
                    <input
                      type="checkbox"
                      aria-label={`${item.displayTitle} 선택`}
                      disabled={item.status === 'Processing'}
                      title={item.status === 'Processing' ? '발송 처리 중에는 확인, 제외 또는 재시도를 실행할 수 없습니다.' : undefined}
                      checked={selectedIds.includes(item.deliveryId)}
                      onChange={(event) => setSelectedIds((current) => (
                        event.target.checked
                          ? [...current, item.deliveryId]
                          : current.filter((id) => id !== item.deliveryId)
                      ))}
                    />
	                  </td>
	                  <td className="admin-table__cell--text">
	                    <button type="button" className="link-button" onClick={() => onOpenDetail(item.deliveryId)}>{item.displayTitle}</button>
	                    <br />
	                    <small>{item.manualNotificationKindLabel ?? item.deliveryTypeLabel}</small>
	                    <br />
	                    <small>{item.displayProject}</small>
	                  </td>
                  <td className="admin-table__cell--text">
                    {item.displayRecipient}
                    <br />
                    <small>{item.displayChannelTarget ?? item.recipientEmailMasked ?? item.recipientEmail ?? '계정 정보 없음'}</small>
                  </td>
                  <td className="admin-table__cell--text">{item.channelLabel}<br /><small>{item.manualNotificationKindLabel ?? item.deliveryTypeLabel}</small></td>
                  <td className="admin-table__cell--status">
                    <StatusBadge label={item.statusLabel} tone={item.status === 'Failed' ? 'danger' : item.status === 'Pending' || item.status === 'Processing' ? 'warning' : 'neutral'} />
                    {item.status === 'Processing' ? <><br /><small>{item.claimIsStale ? 'lease 만료 — 회수 대기' : 'claim lease 유효'}</small></> : null}
                    {shouldShowDeliveryHandlingStatus(item.status) ? (
                      <>
                        <br />
                        <StatusBadge label={item.adminHandlingStatusLabel} tone={item.adminHandlingStatus === 'Open' ? 'info' : 'neutral'} />
                      </>
                    ) : null}
                  </td>
                  <td className="admin-table__cell--number">G{item.currentGeneration ?? 1}<br /><small>이번 {item.generationAttemptCount ?? item.attemptCount}회 · 전체 {item.attemptCount}회</small></td>
                  <td className="admin-table__cell--date">
                    <small>생성 {formatNullableDateTime(item.createdAtUtc)}</small>
                    <br />
                    <small>다음 {formatNullableDateTime(item.nextAttemptAtUtc)}</small>
                    <br />
                    <small>lease {formatNullableDateTime(item.claimExpiresAtUtc)}</small>
                    <br />
                    <small>발송 {formatNullableDateTime(item.sentAtUtc)}</small>
                  </td>
                  <td className="admin-table__cell--text">
                    <strong>{item.errorCode ?? (item.status === 'Pending' ? '대기 사유' : '-')}</strong>
                    {item.errorMessage ? <><br /><small>{item.errorMessage}</small></> : null}
                    {item.pendingReason ? <><br /><small>{item.pendingReason}</small></> : null}
                    <br />
                    <small>{item.actionGuide}</small>
                    {item.adminHandledAtUtc ? <><br /><small>처리 {formatNullableDateTime(item.adminHandledAtUtc)} · {item.adminHandledByDisplayName ?? '관리자'}</small></> : null}
                    {item.adminHandlingNote ? <><br /><small>메모: {item.adminHandlingNote}</small></> : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {displayedItems.length === 0 ? <p className="muted-text">발송 이력이 없습니다.</p> : null}
        </div>
      ) : null}
    </AdminPageShell>
  );
}

function AdminNotificationDeliveryDetailPage({
  developmentUserKey,
  deliveryId,
  onBack
}: {
  developmentUserKey: string;
  deliveryId: string;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<AdminNotificationDeliveryDetail>>({ kind: 'loading' });
  const [reprocessReason, setReprocessReason] = useState('');
  const [duplicateRiskAcknowledged, setDuplicateRiskAcknowledged] = useState(false);
  const [reprocessBusy, setReprocessBusy] = useState(false);
  const [actionMessage, setActionMessage] = useState('');
  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminNotificationDelivery(developmentUserKey, deliveryId)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '알림 발송 상세를 불러올 수 없습니다.')));
  }, [deliveryId, developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    getAdminNotificationDelivery(developmentUserKey, deliveryId)
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '알림 발송 상세를 불러올 수 없습니다.'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [deliveryId, developmentUserKey]);

  const reprocessCurrent = async () => {
    if (state.kind !== 'ready') return;
    if (reprocessReason.trim().length < 10 || reprocessReason.trim().length > 500) {
      setActionMessage('재처리 사유를 10자 이상 500자 이하로 입력해 주세요.');
      return;
    }
    if (!duplicateRiskAcknowledged) {
      setActionMessage('외부 provider 중복 가능성을 확인해 주세요.');
      return;
    }
    try {
      setReprocessBusy(true);
      const result = await reprocessFailedAdminNotificationDeliveries(developmentUserKey, {
        items: [{ deliveryId: state.data.deliveryId, expectedGeneration: state.data.currentGeneration ?? 1 }],
        reason: reprocessReason.trim(),
        duplicateRiskAcknowledged
      });
      setActionMessage(result.message);
      setReprocessReason('');
      setDuplicateRiskAcknowledged(false);
      load();
    } catch (error: unknown) {
      setActionMessage(error instanceof Error ? error.message : '최종 실패 알림 재처리 중 오류가 발생했습니다.');
    } finally {
      setReprocessBusy(false);
    }
  };

  return (
    <AdminPageShell eyebrow="System" title="알림 발송 상세" onRefresh={load} message="">
      <div className="button-row">
        <button type="button" className="secondary-button" onClick={onBack}>목록으로 돌아가기</button>
      </div>
      {actionMessage ? <p role="status" className={actionMessage.includes('오류') || actionMessage.includes('확인') || actionMessage.includes('입력') ? 'error-text' : 'success-text'}>{actionMessage}</p> : null}
      {state.kind === 'loading' ? <p>상세 정보를 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <section className="subsection notification-detail-panel">
          <div className="detail-grid">
            <DetailItem label="구분" value={state.data.categoryLabel} />
            <DetailItem label="알림 유형" value={state.data.notificationKindLabel ?? '-'} />
            <DetailItem label="프로젝트명" value={state.data.projectName ?? '-'} />
            <DetailItem label="제목" value={state.data.title} />
            <DetailItem label="발송시각" value={formatNullableDateTime(state.data.manualRequestedAtUtc ?? state.data.createdAtUtc)} />
            <DetailItem label="채널" value={state.data.channelLabel} />
            <DetailItem label="수신/게시 대상" value={state.data.recipient} />
            <DetailItem label="상태" value={state.data.statusLabel} />
            <DetailItem label="재처리 Generation" value={`G${state.data.currentGeneration ?? 1}`} />
            <DetailItem label="시도 횟수" value={`이번 ${state.data.generationAttemptCount ?? state.data.attemptCount}회 · 전체 ${state.data.attemptCount}회`} />
            <DetailItem label="다음 시도" value={formatNullableDateTime(state.data.nextAttemptAtUtc)} />
            <DetailItem label="최근 시도" value={formatNullableDateTime(state.data.lastAttemptAtUtc)} />
            <DetailItem label="발송 완료" value={formatNullableDateTime(state.data.sentAtUtc)} />
            <DetailItem label="처리상태" value={shouldShowDeliveryHandlingStatus(state.data.status) ? state.data.adminHandlingStatusLabel : '-'} />
            <DetailItem label="현재 시도 시작" value={formatNullableDateTime(state.data.claimedAtUtc)} />
            <DetailItem label="Lease 만료 예정" value={formatNullableDateTime(state.data.claimExpiresAtUtc)} />
            <DetailItem label="Lease 상태" value={state.data.status === 'Processing' ? (state.data.claimIsStale ? '만료 — 회수 대기' : '유효') : '-'} />
          </div>
          <div className="notification-detail-message">
            <strong>내용</strong>
            <p>{state.data.message ?? '-'}</p>
          </div>
          <div className="admin-guidance">
            <p><strong>오류/대기 사유:</strong> {state.data.errorCode ?? '-'} {state.data.errorMessage ? `· ${state.data.errorMessage}` : ''}</p>
            <p><strong>관리자 조치 안내:</strong> {state.data.actionGuide}</p>
            {state.data.adminHandlingNote ? <p><strong>처리 메모:</strong> {state.data.adminHandlingNote}</p> : null}
          </div>
          {state.data.status === 'Failed' && (state.data.currentGeneration ?? 1) < 5 ? (
            <section className="delivery-reprocess-panel delivery-reprocess-panel--detail">
              <div><strong>최종 실패를 새 generation으로 재처리</strong><p>기존 이력은 보존됩니다. 이미 provider에 전달됐을 가능성이 있으므로 확인 후 실행하세요.</p></div>
              <label>재처리 사유<textarea rows={3} maxLength={500} value={reprocessReason} onChange={(event) => setReprocessReason(event.target.value)} placeholder="장애 원인 확인 및 재처리 근거를 10자 이상 입력" /></label>
              <label className="delivery-reprocess-ack"><input type="checkbox" checked={duplicateRiskAcknowledged} onChange={(event) => setDuplicateRiskAcknowledged(event.target.checked)} /><span>외부 provider 중복 가능성을 확인했습니다.</span></label>
              <button type="button" className="danger-button" disabled={reprocessBusy} onClick={() => void reprocessCurrent()}>{reprocessBusy ? '재처리 중…' : `G${(state.data.currentGeneration ?? 1) + 1} 재처리 시작`}</button>
            </section>
          ) : null}
          <details className="advanced-detail">
            <summary>내부 추적값</summary>
            <dl>
              <div><dt>Delivery ID</dt><dd>{state.data.deliveryId}</dd></div>
              <div><dt>Correlation ID</dt><dd>{state.data.correlationId ?? '-'}</dd></div>
              <div><dt>Provider Message ID</dt><dd>{state.data.providerMessageId ?? '-'}</dd></div>
            </dl>
          </details>
          <section className="subsection">
            <h3>발송 시도 이력</h3>
            {state.data.attempts.length === 0 ? <p className="muted-text">기록된 발송 시도가 없습니다.</p> : (
              <div className="table-scroll">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th className="admin-table__cell--number">시도</th>
                      <th className="admin-table__cell--status">결과</th>
                      <th className="admin-table__cell--date">시작/완료</th>
                      <th className="admin-table__cell--text">오류/Provider</th>
                    </tr>
                  </thead>
                  <tbody>
                    {state.data.attempts.map((attempt) => (
                      <tr key={attempt.attemptNumber}>
                        <td className="admin-table__cell--number">{attempt.attemptNumber}회<br /><small>Generation G{attempt.generation ?? 1}</small></td>
                        <td className="admin-table__cell--status"><StatusBadge label={deliveryAttemptOutcomeLabel(attempt.outcome)} tone={attempt.outcome === 'FailedPermanent' ? 'danger' : attempt.outcome === 'Processing' || attempt.outcome === 'RetryScheduled' ? 'warning' : 'neutral'} /></td>
                        <td className="admin-table__cell--date">
                          <small>시작 {formatNullableDateTime(attempt.claimedAtUtc)}</small><br />
                          <small>Provider {formatNullableDateTime(attempt.providerCallStartedAtUtc)}</small><br />
                          <small>완료 {formatNullableDateTime(attempt.completedAtUtc)}</small>
                        </td>
                        <td className="admin-table__cell--text">
                          <strong>{attempt.errorCode ?? '-'}</strong>
                          {attempt.errorMessage ? <><br /><small>{attempt.errorMessage}</small></> : null}
                          {attempt.providerMessageId ? <><br /><small>Provider {attempt.providerMessageId}</small></> : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
          <section className="subsection">
            <h3>수동 재처리 이력</h3>
            {(state.data.reprocessEvents ?? []).length === 0 ? <p className="muted-text">수동 재처리 기록이 없습니다.</p> : (
              <div className="reprocess-event-list">{(state.data.reprocessEvents ?? []).map((event) => (
                <article key={event.eventId}>
                  <div><strong>G{event.priorGeneration} → G{event.newGeneration}</strong><span>{formatNullableDateTime(event.occurredAtUtc)}</span></div>
                  <p>{event.reason}</p>
                  <small>{event.actorDisplayName} · 중복 위험 확인 완료{event.priorErrorCode ? ` · 이전 오류 ${event.priorErrorCode}` : ''}</small>
                  {event.priorAdminHandlingNote ? <small>이전 처리 메모: {event.priorAdminHandlingNote}</small> : null}
                </article>
              ))}</div>
            )}
          </section>
        </section>
      ) : null}
    </AdminPageShell>
  );
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function AdminWorkItemEscalationsPage({
  developmentUserKey,
  statusFilter,
  levelFilter
}: {
  developmentUserKey: string;
  statusFilter: string | null;
  levelFilter: string | null;
}) {
  const initialTab = escalationTabFromFilters(statusFilter, levelFilter);
  const [activeTab, setActiveTab] = useState(initialTab);
  const [state, setState] = useState<LoadState<AdminWorkItemEscalationListResponse>>({ kind: 'loading' });
  useEffect(() => {
    setActiveTab(escalationTabFromFilters(statusFilter, levelFilter));
  }, [levelFilter, statusFilter]);
  const currentFilters = escalationTabFilters(activeTab);
  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getAdminWorkItemEscalations(developmentUserKey, { status: currentFilters.status, level: currentFilters.level })
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '에스컬레이션 상태를 불러올 수 없습니다.')));
  }, [currentFilters.level, currentFilters.status, developmentUserKey]);

  useEffect(() => {
    let cancelled = false;
    getAdminWorkItemEscalations(developmentUserKey, { status: currentFilters.status, level: currentFilters.level })
      .then((data) => {
        if (!cancelled) {
          setState({ kind: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState(toLoadError(error, '에스컬레이션 상태를 불러올 수 없습니다.'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [currentFilters.level, currentFilters.status, developmentUserKey]);

  const filterText = escalationTabLabel(activeTab);
  const escalationIds = state.kind === 'ready' ? state.data.items.map((item) => item.escalationId) : [];
  const escalationSelection = useSelectedRows(escalationIds);

  return (
    <AdminPageShell eyebrow="System" title="에스컬레이션 상태" onRefresh={load} message="">
      <div className="admin-guidance">
        <p>진행 중 에스컬레이션은 예정일 임박 또는 초과 후 아직 완료/취소되지 않은 업무입니다. L0는 예정일 임박, L1~L3는 초과 단계입니다.</p>
        <p>L0는 담당자 예정일 확인, L1은 정담당자 조치 확인, L2는 부담당자/생산관리 확인, L3는 생산관리/영업 확인이 필요합니다.</p>
        <p><strong>현재 필터:</strong> {filterText}</p>
      </div>
      <div className="segmented-control" role="tablist" aria-label="에스컬레이션 상태 필터">
        {escalationTabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={activeTab === tab.key}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      {state.kind === 'loading' ? <p>에스컬레이션 상태를 불러오는 중입니다.</p> : null}
      {state.kind !== 'loading' && state.kind !== 'ready' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <div className="table-scroll">
          <SelectedExportTray
            developmentUserKey={developmentUserKey}
            screen="admin-work-item-escalations"
            visibleIds={escalationIds}
            selectedIds={escalationSelection.selectedIds}
            allSelected={escalationSelection.allSelected}
            busy={escalationSelection.busy}
            filters={{ status: currentFilters.status ?? undefined, level: currentFilters.level ?? undefined }}
            onBusyChange={escalationSelection.setBusy}
            onToggleAll={escalationSelection.toggleAll}
            onClear={escalationSelection.clear}
          />
          <table className="admin-table">
            <thead><tr><th>선택</th><th className="admin-table__cell--text">프로젝트</th><th className="admin-table__cell--text">업무</th><th className="admin-table__cell--date">예정일</th><th className="admin-table__cell--status">상태</th><th className="admin-table__cell--status">현재 단계</th><th className="admin-table__cell--date">다음 확인</th><th className="admin-table__cell--text">Delivery/조치</th></tr></thead>
            <tbody>
              {state.data.items.map((item) => (
                <tr key={item.escalationId}>
                  <td><SelectionCheckbox checked={escalationSelection.selectedIds.has(item.escalationId)} disabled={escalationSelection.busy} label={`${item.workItemTitle} 에스컬레이션 선택`} onChange={(checked) => escalationSelection.toggle(item.escalationId, checked)} /></td>
                  <td className="admin-table__cell--text"><strong>{item.projectTitle}</strong><br /><small>{item.projectCode}</small></td>
                  <td className="admin-table__cell--text">{item.workItemTitle}<br /><small>{item.workflowStageName} · {item.assignedDisplayName ?? '-'}</small></td>
                  <td className="admin-table__cell--date">{formatDate(item.dueDate)}</td>
                  <td className="admin-table__cell--status"><StatusBadge label={item.status === 'Active' ? '진행 중' : item.status} tone={item.status === 'Active' ? 'warning' : 'neutral'} /></td>
                  <td className="admin-table__cell--status"><StatusBadge label={escalationLevelLabel(item.currentLevel)} tone={item.currentLevel === 'L0' ? 'info' : item.currentLevel === 'L1' ? 'warning' : 'danger'} /></td>
                  <td className="admin-table__cell--date">{formatNullableDateTime(item.nextCheckAtUtc)}</td>
                  <td className="admin-table__cell--text">
                    {item.deliveryStatusSummary ?? '-'}
                    <br />
                    <small>{escalationActionGuide(item.currentLevel)}</small>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {state.data.items.length === 0 ? <p className="muted-text">에스컬레이션 상태가 없습니다.</p> : null}
        </div>
      ) : null}
    </AdminPageShell>
  );
}

function AdminPageShell({
  eyebrow,
  title,
  onRefresh,
  message,
  children
}: {
  eyebrow: string;
  title: string;
  onRefresh?: () => void;
  message: string;
  children: ReactNode;
}) {
  const isMobile = useIsMobileViewport();
  const [showAllMobileFields, setShowAllMobileFields] = useState(false);

  return (
    <section className={`panel-section admin-mobile-page${showAllMobileFields ? ' admin-mobile-page--all-fields' : ''}`}>
      <div className="page-header">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h2>{title}</h2>
        </div>
        {onRefresh ? <button type="button" onClick={onRefresh}>새로고침</button> : null}
      </div>
      {message ? <p role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</p> : null}
      {isMobile ? (
        <button
          type="button"
          className="mobile-admin-field-toggle"
          aria-pressed={showAllMobileFields}
          onClick={() => setShowAllMobileFields((current) => !current)}
        >
          <span>{showAllMobileFields ? '핵심 열로 보기' : '모든 관리 필드 보기'}</span>
          <small>{showAllMobileFields ? '모바일 우선 정보만 다시 표시' : '감사·기술 열까지 가로로 확인'}</small>
        </button>
      ) : null}
      {children}
    </section>
  );
}

function dashboardEscalationLevels(levels: AdminDashboardEscalationLevel[] | undefined) {
  const counts = new Map((levels ?? []).map((item) => [item.level, item.count]));
  return [
    { level: 'L0', label: 'L0 예정일 임박', count: counts.get('L0') ?? 0 },
    { level: 'L1', label: 'L1 초과', count: counts.get('L1') ?? 0 },
    { level: 'L2', label: 'L2 +2영업일 초과', count: counts.get('L2') ?? 0 },
    { level: 'L3', label: 'L3 +3영업일 초과', count: counts.get('L3') ?? 0 }
  ];
}

function escalationLevelLabel(level: string) {
  switch (level) {
    case 'None':
      return '없음';
    case 'L0':
      return '예정일 임박';
    case 'L1':
      return '예정일 초과';
    case 'L2':
      return '초과 +2영업일';
    case 'L3':
      return '초과 +3영업일';
    default:
      return level;
  }
}

function escalationActionGuide(level: string) {
  switch (level) {
    case 'L0':
      return '담당자에게 예정일 임박 상태를 확인하세요.';
    case 'L1':
      return '정담당자 조치 상태를 확인하세요.';
    case 'L2':
      return '부담당자와 생산관리 담당자 조치 상태를 확인하세요.';
    case 'L3':
      return '생산관리 담당자와 영업 담당자 확인이 필요합니다.';
    default:
      return '업무 상태와 예정일을 확인하세요.';
  }
}

function departmentToDraft(department: AdminDepartmentMaster): UpdateAdminDepartmentRequest {
  return {
    name: department.name,
    isActive: department.isActive,
    sortOrder: department.sortOrder,
    reason: null
  };
}

function hasPermissionAssignment(matrix: PermissionMatrixResponse, roleId: string, permissionId: string) {
  return matrix.assignments.some((assignment) => assignment.roleId === roleId && assignment.permissionId === permissionId);
}

function summarizeJsonChange(beforeJson: string | null, afterJson: string | null) {
  if (!beforeJson && afterJson) {
    return '생성';
  }

  if (beforeJson && !afterJson) {
    return '삭제';
  }

  if (!beforeJson && !afterJson) {
    return '-';
  }

  return '변경 전/후 기록 있음';
}

function HolidayTypeBadge({ holidayType }: { holidayType: HolidayType }) {
  return <span className="holiday-type-badge" data-type={holidayType}>{adminHolidayTypeLabel(holidayType)}</span>;
}

function createHolidayDraft(year: number): CalendarHolidayDraft {
  return {
    date: `${year}-01-01`,
    name: '',
    holidayType: 'National',
    isActive: true,
    note: ''
  };
}

function adminHolidayTypeLabel(holidayType: HolidayType) {
  return holidayTypeOptions.find((option) => option.value === holidayType)?.label ?? holidayType;
}

function calendarHolidayPreviewResultLabel(resultType: CalendarHolidayExcelPreviewResponse['rows'][number]['resultType']) {
  switch (resultType) {
    case 'Insert':
      return '신규';
    case 'Update':
      return '갱신';
    case 'Error':
      return '오류';
    default:
      return resultType;
  }
}

function isProjectWorkspace(view: View) {
  return view.kind === 'list'
    || view.kind === 'create'
    || view.kind === 'detail'
    || view.kind === 'sales-settlement'
    || view.kind === 'deleted-detail'
    || view.kind === 'edit'
    || view.kind === 'panel-info-edit'
    || view.kind === 'panel';
}

function isProductionPlanningWorkspace(view: View) {
  return view.kind === 'production-planning-dashboard'
    || view.kind === 'production-planning-settings'
    || view.kind === 'production-planning-edit';
}

function isProcurementWorkspace(view: View) {
  return view.kind === 'procurement-dashboard'
    || view.kind === 'procurement-edit'
    || view.kind === 'procurement-settings';
}

function isAdminWorkspace(view: View) {
  return view.kind === 'admin-dashboard'
    || view.kind === 'pending-types'
    || view.kind === 'admin-users'
    || view.kind === 'admin-user-notification-preferences'
    || view.kind === 'admin-departments'
    || view.kind === 'admin-calendar-holidays'
    || view.kind === 'admin-permission-matrix'
    || view.kind === 'admin-master-change-logs'
    || view.kind === 'admin-work-history'
    || view.kind === 'admin-send-notification'
    || view.kind === 'admin-notification-deliveries'
    || view.kind === 'admin-notification-delivery-detail'
    || view.kind === 'admin-notification-preference-audit'
    || view.kind === 'admin-work-item-escalations';
}

type MyWorkTab = 'All' | 'Requested' | 'InProgress' | 'Completed' | 'AssignedProjects';

const myWorkTabs: Array<{ key: MyWorkTab; label: string }> = [
  { key: 'All', label: '전체' },
  { key: 'Requested', label: '시작 전' },
  { key: 'InProgress', label: '진행 중' },
  { key: 'Completed', label: '완료' },
  { key: 'AssignedProjects', label: '담당 프로젝트' }
];

function MyWorkPage({
  developmentUserKey,
  onOpenProject,
  onBadgeRefresh
}: {
  developmentUserKey: string;
  onOpenProject: (projectId: string, linkUrl?: string | null) => void;
  onBadgeRefresh: () => void;
}) {
  const [summaryState, setSummaryState] = useState<LoadState<MyWorkSummary>>({ kind: 'loading' });
  const [itemsState, setItemsState] = useState<LoadState<MyWorkListResponse>>({ kind: 'loading' });
  const [assignedProjectsState, setAssignedProjectsState] = useState<LoadState<MyAssignedProjectsResponse>>({ kind: 'loading' });
  const [activeTab, setActiveTab] = useState<MyWorkTab>('Requested');
  const activeTabRef = useRef<MyWorkTab>('Requested');
  const loadGenerationRef = useRef(0);
  const actions = useActionFeedback();
  const isMobile = useIsMobileViewport();

  const load = useCallback(async (mode: 'replace' | 'preserve' = 'replace'): Promise<boolean> => {
    const generation = ++loadGenerationRef.current;
    const tab = activeTabRef.current;
    if (mode === 'replace') {
      setSummaryState({ kind: 'loading' });
      setItemsState({ kind: 'loading' });
      setAssignedProjectsState({ kind: 'loading' });
    }
    const workPromise = tab === 'AssignedProjects'
      ? Promise.resolve<MyWorkListResponse>({ items: [] })
      : listMyWorkItems(developmentUserKey, tab === 'All' ? undefined : tab);
    const assignedProjectsPromise = tab === 'AssignedProjects'
      ? listMyAssignedProjects(developmentUserKey)
      : Promise.resolve<MyAssignedProjectsResponse>({ items: [] });

    try {
      const [summary, items, assignedProjects] = await Promise.all([
        getMyWorkSummary(developmentUserKey),
        workPromise,
        assignedProjectsPromise
      ]);
      if (generation !== loadGenerationRef.current) {
        return true;
      }

      setSummaryState({ kind: 'ready', data: summary });
      setItemsState(items.items.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: items });
      setAssignedProjectsState(assignedProjects.items.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: assignedProjects });
      onBadgeRefresh();
      return true;
    } catch (error: unknown) {
      if (generation !== loadGenerationRef.current) {
        return true;
      }

      if (mode === 'replace') {
        setSummaryState(toLoadError(error, '내 업무 요약을 불러올 수 없습니다.'));
        setItemsState(toLoadError(error, '내 업무 목록을 불러올 수 없습니다.'));
        setAssignedProjectsState(toLoadError(error, '담당 프로젝트 목록을 불러올 수 없습니다.'));
      }
      return false;
    }
  }, [developmentUserKey, onBadgeRefresh]);

  useEffect(() => {
    queueMicrotask(() => void load('replace'));
  }, [load]);

  function selectTab(tab: MyWorkTab) {
    activeTabRef.current = tab;
    actions.reset();
    setActiveTab(tab);
    void load('replace');
  }

  function refresh() {
    actions.reset();
    void load('replace');
  }

  async function completeWorkItem(item: MyWorkItem) {
    await actions.run(`work:${item.workItemId}`, async () => {
      await completeMyWorkItem(developmentUserKey, item.workItemId);
      onBadgeRefresh();
    }, {
      loadingMessage: `${item.title} 완료 처리 중입니다.`,
      successMessage: `${item.title} 업무를 완료했습니다.`,
      partialMessage: `${item.title} 업무는 완료했지만 최신 목록을 불러오지 못했습니다. 새로고침해 주세요.`,
      errorFallback: '업무를 완료할 수 없습니다.',
      subject: item.title,
      refresh: () => load('preserve')
    });
  }

  async function openWorkItem(item: MyWorkItem) {
    if (isPendingLinkedWorkItem(item) || isDomainControlledWorkItem(item)) {
      onOpenProject(item.projectId, item.linkUrl);
      return;
    }

    if (item.status === 'Requested') {
      const result = await actions.run(`work:${item.workItemId}`, async () => {
        await startMyWorkItem(developmentUserKey, item.workItemId);
        onBadgeRefresh();
      }, {
        loadingMessage: `${item.title} 업무를 시작하는 중입니다.`,
        successMessage: `${item.title} 업무를 시작했습니다.`,
        errorFallback: '업무 시작 기록에 실패했습니다.',
        subject: item.title
      });
      if (result === 'success') {
        onOpenProject(item.projectId, item.linkUrl);
      }
      return;
    }

    onOpenProject(item.projectId, item.linkUrl);
  }

  const summary = summaryState.kind === 'ready' ? summaryState.data : null;
  const visibleWorkItemIds = itemsState.kind === 'ready' ? itemsState.data.items.map((item) => item.workItemId) : [];
  const workSelection = useSelectedRows(visibleWorkItemIds);
  const latestActionFeedback = actions.latestFeedback && (actions.latestFeedback.tone === 'success' || actions.latestFeedback.tone === 'partial')
    ? actions.latestFeedback
    : null;

  return (
    <section className={isMobile ? 'page-surface workflow-page mobile-first-page' : 'page-surface workflow-page'}>
      <div className={isMobile ? 'page-header mobile-page-header' : 'page-header'}>
        <div>
          <p className="eyebrow">{isMobile ? 'TODAY ACTIONS' : 'My Work'}</p>
          <h2>{isMobile ? '오늘 처리할 업무' : '내 업무'}</h2>
          {isMobile ? <p>긴급 업무부터 확인하고 카드 안에서 바로 처리하세요.</p> : null}
        </div>
        <div className="button-row page-export-actions">
          <button type="button" onClick={refresh}>새로고침</button>
        </div>
      </div>

      {isMobile ? (
        <section className="mobile-focus-summary" aria-label="오늘 업무 요약">
          <header><span>우선순위</span><strong>{summary?.blockingCount ? `긴급 ${summary.blockingCount}건` : '정상 진행'}</strong></header>
          <div>
            <button type="button" onClick={() => selectTab('Requested')}><span>시작 전</span><strong>{summary ? summary.requestedCount : '-'}</strong></button>
            <button type="button" onClick={() => selectTab('InProgress')}><span>진행 중</span><strong>{summary ? summary.inProgressCount : '-'}</strong></button>
            <button type="button" data-tone="danger" onClick={() => selectTab('All')}><span>차단·긴급</span><strong>{summary ? summary.blockingCount : '-'}</strong></button>
          </div>
        </section>
      ) : (
        <div className="dashboard-kpi-grid workflow-kpi-grid">
          <KpiCard label="시작 전" value={summary ? String(summary.requestedCount) : '-'} />
          <KpiCard label="진행 중" value={summary ? String(summary.inProgressCount) : '-'} />
          <KpiCard label="완료" value={summary ? String(summary.completedCount) : '-'} />
          <KpiCard label="차단/긴급" value={summary ? String(summary.blockingCount) : '-'} tone="danger" />
          <KpiCard label="담당 프로젝트" value={summary ? String(summary.assignedProjectCount) : '-'} />
        </div>
      )}

      <div className="workflow-tabs" role="tablist" aria-label="내 업무 상태">
        {myWorkTabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            className={activeTab === tab.key ? 'active-filter' : undefined}
            aria-selected={activeTab === tab.key}
            onClick={() => selectTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {summaryState.kind !== 'ready' && summaryState.kind !== 'loading' ? <StateMessage state={summaryState} /> : null}
      {latestActionFeedback ? (
        <div className="page-action-feedback" aria-label="최근 내 업무 처리 결과">
          <ActionFeedback message={latestActionFeedback.message} tone={latestActionFeedback.tone} focusOnAttention />
        </div>
      ) : null}

      {activeTab !== 'AssignedProjects' && itemsState.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="my-work"
          visibleIds={visibleWorkItemIds}
          selectedIds={workSelection.selectedIds}
          allSelected={workSelection.allSelected}
          busy={workSelection.busy}
          filters={{ status: activeTab === 'All' ? undefined : activeTab }}
          onBusyChange={workSelection.setBusy}
          onToggleAll={workSelection.toggleAll}
          onClear={workSelection.clear}
        />
      ) : null}

      {activeTab === 'AssignedProjects' ? (
        <>
          {assignedProjectsState.kind === 'loading' ? <p className="muted-text">담당 프로젝트를 불러오는 중입니다.</p> : null}
          {assignedProjectsState.kind === 'empty' ? <p className="empty-text">담당 프로젝트가 없습니다.</p> : null}
          {assignedProjectsState.kind !== 'ready' && assignedProjectsState.kind !== 'loading' && assignedProjectsState.kind !== 'empty' ? <StateMessage state={assignedProjectsState} /> : null}
          {assignedProjectsState.kind === 'ready' ? (
            <div className="workflow-project-groups">
              {assignedProjectsState.data.items.map((project) => (
                <article className="workflow-project-group" key={project.projectId}>
                  <button type="button" className="project-group-header clickable" onClick={() => onOpenProject(project.projectId)}>
                    <span>
                      <strong>{project.projectTitle}</strong>
                      <small>{project.projectCode} · Item {project.item} · 납기일 {project.deliveryDate ? formatDate(project.deliveryDate) : '-'}</small>
                    </span>
                    <StatusBadge label={project.projectStatusLabel} tone={project.projectStatus === 'OnHold' ? 'warning' : 'info'} />
                  </button>
                  <div className="responsibility-badge-list" aria-label="담당 유형">
                    {project.responsibilities.map((responsibility) => (
                      <StatusBadge key={responsibility.responsibilityType} label={responsibility.responsibilityLabel} tone="neutral" />
                    ))}
                  </div>
                </article>
              ))}
            </div>
          ) : null}
        </>
      ) : (
        <>
          {itemsState.kind === 'loading' ? <p className="muted-text">내 업무를 불러오는 중입니다.</p> : null}
          {itemsState.kind === 'empty' ? <p className="empty-text">표시할 내 업무가 없습니다.</p> : null}
          {itemsState.kind !== 'ready' && itemsState.kind !== 'loading' && itemsState.kind !== 'empty' ? <StateMessage state={itemsState} /> : null}
          {itemsState.kind === 'ready' ? (
            <div className="workflow-project-groups">
              {groupWorkItemsByProject(itemsState.data.items).map((group) => (
                <section className="workflow-project-group" key={group.projectId} aria-label={`${group.projectTitle} 내 업무`}>
                  <div className="project-group-header">
                    <span>
                      <strong>{group.projectTitle}</strong>
                      <small>{group.projectCode} · Item {group.projectItem} · 납기일 {group.projectDeliveryDate ? formatDate(group.projectDeliveryDate) : '-'} · 업무 {group.items.length}건</small>
                    </span>
                    <button type="button" onClick={() => onOpenProject(group.projectId, group.items[0]?.linkUrl)}>프로젝트로 이동</button>
                  </div>
                  {isMobile ? (
                    <div className="workflow-card-list">
                      {group.items.map((item) => (
                        <article className="workflow-card selected-export-row" key={item.workItemId}>
                          <div className="subsection-header">
                            <SelectionCheckbox checked={workSelection.selectedIds.has(item.workItemId)} disabled={workSelection.busy} label={`${item.title} 선택`} onChange={(checked) => workSelection.toggle(item.workItemId, checked)} />
                            <div>
                              <strong>{item.title}</strong>
                              <small>{displayWorkflowStageName(item.workflowStageCode, item.workflowStageName)}</small>
                            </div>
                            <StatusBadge label={item.priority === 'Blocking' ? '긴급' : item.statusLabel} tone={workItemStatusTone(item)} />
                          </div>
                          <p>{item.description ?? '처리할 업무가 있습니다.'}</p>
                          <div className="button-row">
                            <button type="button" disabled={actions.isBusy(`work:${item.workItemId}`)} onClick={() => void openWorkItem(item)}>
                              {actions.isBusy(`work:${item.workItemId}`) ? '이동 중' : isPendingLinkedWorkItem(item) ? 'Pending 열기' : isDomainControlledWorkItem(item) ? domainWorkItemActionLabel(item) : '이동'}
                            </button>
                            {!isPendingLinkedWorkItem(item) && !isDomainControlledWorkItem(item) && item.status !== 'Completed' && item.status !== 'Cancelled' ? (
                              <button type="button" disabled={actions.isBusy(`work:${item.workItemId}`)} onClick={() => void completeWorkItem(item)}>
                                {actions.isBusy(`work:${item.workItemId}`) ? '완료 처리 중' : '작업 완료'}
                              </button>
                            ) : null}
                          </div>
                          {(() => {
                            const feedback = actions.feedbackFor(`work:${item.workItemId}`);
                            return feedback && (feedback.tone === 'loading' || feedback.tone === 'error')
                              ? <ActionFeedback message={feedback.message} tone={feedback.tone} focusOnAttention={feedback.tone === 'error'} />
                              : null;
                          })()}
                        </article>
                      ))}
                    </div>
                  ) : (
                    <div className="table-wrapper">
                      <table>
                        <thead>
                          <tr>
                            <th>선택</th>
                            <th>단계</th>
                            <th>업무 제목</th>
                            <th>상태</th>
                            <th>생성일</th>
                            <th>작업</th>
                          </tr>
                        </thead>
                        <tbody>
                          {group.items.map((item) => (
                            <tr key={item.workItemId}>
                              <td><SelectionCheckbox checked={workSelection.selectedIds.has(item.workItemId)} disabled={workSelection.busy} label={`${item.title} 선택`} onChange={(checked) => workSelection.toggle(item.workItemId, checked)} /></td>
                              <td><span className="workflow-stage-badge" data-department={departmentForStageCode(item.workflowStageCode)}>{displayWorkflowStageName(item.workflowStageCode, item.workflowStageName)}</span></td>
                              <td>{item.title}</td>
                              <td><StatusBadge label={item.priority === 'Blocking' ? '긴급' : item.statusLabel} tone={workItemStatusTone(item)} /></td>
                              <td>{formatDateTime(item.createdAtUtc)}</td>
                              <td>
                                <div className="button-row">
                                  <button type="button" disabled={actions.isBusy(`work:${item.workItemId}`)} onClick={() => void openWorkItem(item)}>
                                    {actions.isBusy(`work:${item.workItemId}`) ? '이동 중' : isPendingLinkedWorkItem(item) ? 'Pending 열기' : isDomainControlledWorkItem(item) ? domainWorkItemActionLabel(item) : '이동'}
                                  </button>
                                  {!isPendingLinkedWorkItem(item) && !isDomainControlledWorkItem(item) && item.status !== 'Completed' && item.status !== 'Cancelled' ? (
                                    <button type="button" disabled={actions.isBusy(`work:${item.workItemId}`)} onClick={() => void completeWorkItem(item)}>
                                      {actions.isBusy(`work:${item.workItemId}`) ? '완료 처리 중' : '작업 완료'}
                                    </button>
                                  ) : null}
                                </div>
                                {(() => {
                                  const feedback = actions.feedbackFor(`work:${item.workItemId}`);
                                  return feedback && (feedback.tone === 'loading' || feedback.tone === 'error')
                                    ? <ActionFeedback message={feedback.message} tone={feedback.tone} focusOnAttention={feedback.tone === 'error'} />
                                    : null;
                                })()}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </section>
              ))}
            </div>
          ) : null}
        </>
      )}
    </section>
  );
}

function isPendingLinkedWorkItem(item: Pick<MyWorkItem, 'linkUrl'>) {
  return /^\/pending\/[^/?#]+(?:[?#].*)?$/u.test(item.linkUrl);
}

function isDomainControlledWorkItem(item: Pick<MyWorkItem, 'workflowStageCode'>) {
  return ['ManufacturingWork', 'LQC', 'ManufacturingCompleted', 'OQC', 'CustomerInspection', 'FAT', 'SalesSettlementCompleted'].includes(item.workflowStageCode);
}

function domainWorkItemActionLabel(item: Pick<MyWorkItem, 'workflowStageCode'>) {
  if (item.workflowStageCode === 'SalesSettlementCompleted') return '정산 화면에서 진행';
  return item.workflowStageCode === 'ManufacturingWork' || item.workflowStageCode === 'ManufacturingCompleted'
    ? '제조 화면에서 진행'
    : '품질 화면에서 진행';
}

type TeamsActivitySummary = {
  notifications: NotificationItem[];
  workItems: MyWorkItem[];
  workSummary: MyWorkSummary;
  generatedAtUtc: string;
};

function TeamsActivityAuthFallback({
  state,
  onRetry,
  onLogin,
  onLogout
}: {
  state: LoadState<CurrentUser>;
  onRetry: () => void;
  onLogin?: () => void;
  onLogout?: () => void;
}) {
  const message = state.kind === 'loading'
    ? 'Microsoft 365 인증 정보를 확인하는 중입니다.'
    : loadStateMessage(state) ?? 'Teams 앱에서 EMI 프로젝트 통합관리시스템 화면을 불러오려면 로그인이 필요합니다.';

  return (
    <section className="page-surface workflow-page teams-activity-page">
      <div className="page-header">
        <div>
          <p className="eyebrow">Teams</p>
          <h2>EMI 프로젝트 통합관리시스템 알림</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onRetry}>다시 시도</button>
          {onLogin ? <button type="button" onClick={onLogin}>Microsoft 365 로그인</button> : null}
          {onLogout ? <button type="button" onClick={onLogout}>로그아웃</button> : null}
        </div>
      </div>
      <div className="teams-activity-guide" role="note">
        <strong>Teams 알림 화면</strong>
        <p>{message}</p>
        <p>인증 확인 전에도 이 화면은 빈 화면으로 남지 않습니다. 로그인 후 최근 알림과 내 미완료 업무가 표시됩니다.</p>
      </div>
      <div className="teams-activity-grid">
        <section className="teams-activity-panel" aria-label="최근 내 알림">
          <h3>최근 알림</h3>
          <p className="empty-text">로그인 후 최근 알림이 표시됩니다.</p>
        </section>
        <section className="teams-activity-panel" aria-label="내 미완료 업무 요약">
          <h3>내 미완료 업무</h3>
          <p className="empty-text">로그인 후 내 업무 요약이 표시됩니다.</p>
        </section>
      </div>
    </section>
  );
}

function TeamsActivityPage({
  developmentUserKey,
  onOpenProject,
  onOpenNotification,
  onOpenMyWork,
  onOpenHome
}: {
  developmentUserKey: string;
  onOpenProject: (projectId: string, linkUrl?: string | null) => void;
  onOpenNotification: (notificationId: string) => void;
  onOpenMyWork: () => void;
  onOpenHome: () => void;
}) {
  const [state, setState] = useState<LoadState<TeamsActivitySummary>>({ kind: 'loading' });
  const isMobile = useIsMobileViewport();

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    Promise.all([
      listNotifications(developmentUserKey),
      listMyWorkItems(developmentUserKey),
      getMyWorkSummary(developmentUserKey)
    ])
      .then(([notifications, workItems, workSummary]) => {
        setState({
          kind: 'ready',
          data: {
            notifications: notifications.items.slice(0, 5),
            workItems: workItems.items
              .filter((item) => item.status !== 'Completed' && item.status !== 'Cancelled')
              .slice(0, 5),
            workSummary,
            generatedAtUtc: new Date().toISOString()
          }
        });
      })
      .catch((error: unknown) => {
        setState(toLoadError(error, 'Teams 알림 화면을 불러올 수 없습니다.'));
      });
  }, [developmentUserKey]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  const data = state.kind === 'ready' ? state.data : null;

  return (
    <section className={isMobile ? 'page-surface workflow-page teams-activity-page mobile-operations-page' : 'page-surface workflow-page teams-activity-page'}>
      <div className={isMobile ? 'page-header mobile-operations-header' : 'page-header'}>
        <div>
          <p className="eyebrow">{isMobile ? 'QUICK FEED' : 'Teams'}</p>
          <h2>{isMobile ? '업무 피드' : 'EMI 프로젝트 통합관리시스템 알림'}</h2>
          {isMobile ? <p>읽지 않은 알림과 미완료 업무를 한 번에 확인하세요.</p> : null}
        </div>
        <div className="button-row">
          <button type="button" onClick={onOpenHome}>시스템 홈</button>
          <button type="button" onClick={load}>새로고침</button>
        </div>
      </div>

      <div className="teams-activity-guide" role="note">
        <strong>상세 안내</strong>
        <p>Teams 알림을 선택하면 관련 업무를 확인할 수 있습니다. 상세 업무 화면은 시스템 링크에서 확인하세요.</p>
      </div>

      {state.kind === 'loading' ? <p className="muted-text">Teams 알림 화면을 불러오는 중입니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}

      {state.kind !== 'ready' ? (
        <div className="teams-activity-grid">
          <section className="teams-activity-panel" aria-label="최근 내 알림">
            <h3>최근 알림</h3>
            <p className="empty-text">표시할 알림이 없습니다. API 응답 후 최근 내 알림이 여기에 표시됩니다.</p>
          </section>
          <section className="teams-activity-panel" aria-label="내 미완료 업무 요약">
            <h3>내 미완료 업무</h3>
            <p className="empty-text">표시할 미완료 업무가 없습니다. API 응답 후 내 업무 요약이 여기에 표시됩니다.</p>
          </section>
        </div>
      ) : null}

      {data ? (
        <>
          <div className="dashboard-kpi-grid workflow-kpi-grid">
            <KpiCard label="시작 전" value={String(data.workSummary.requestedCount)} />
            <KpiCard label="진행 중" value={String(data.workSummary.inProgressCount)} />
            <KpiCard label="긴급/차단" value={String(data.workSummary.blockingCount)} tone="danger" />
            <KpiCard label="읽지 않은 알림" value={String(data.notifications.filter((item) => !item.readAtUtc).length)} />
          </div>

          <div className="teams-activity-grid">
            <section className="teams-activity-panel" aria-label="최근 내 알림">
              <div className="subsection-header">
                <div>
                  <h3>최근 알림</h3>
                  <small>Teams Activity Feed에서 선택한 알림의 업무 맥락을 확인합니다.</small>
                </div>
              </div>
              {data.notifications.length === 0 ? <p className="empty-text">최근 알림이 없습니다.</p> : null}
              <div className="teams-activity-list">
                {data.notifications.map((item) => (
                  <article className={item.readAtUtc ? 'teams-activity-card read' : 'teams-activity-card unread'} key={item.notificationId}>
                    <div className="teams-activity-card-header">
                      <div>
                        <strong>{item.title}</strong>
                        <small>{item.notificationTypeLabel} · {item.severityLabel} · {formatDateTime(item.createdAtUtc)}</small>
                      </div>
                      <NotificationStatusBadges item={item} />
                    </div>
                    <p>{summarizeText(item.message, 110)}</p>
                    <div className="teams-activity-meta">
                      {item.projectTitle ? <span>{item.projectTitle}</span> : <span>프로젝트 미연결</span>}
                      {item.projectCode ? <span>{item.projectCode}</span> : null}
                    </div>
                    <div className="button-row">
                      <button type="button" onClick={() => onOpenNotification(item.notificationId)}>
                        알림 상세
                      </button>
                      {item.projectId ? (
                        <button type="button" onClick={() => onOpenProject(item.projectId!, item.linkUrl)}>
                          관련 업무로 이동
                        </button>
                      ) : null}
                    </div>
                  </article>
                ))}
              </div>
            </section>

            <section className="teams-activity-panel" aria-label="내 미완료 업무 요약">
              <div className="subsection-header">
                <div>
                  <h3>내 미완료 업무</h3>
                  <small>시작 전과 진행 중인 업무를 우선 표시합니다.</small>
                </div>
                <button type="button" onClick={onOpenMyWork}>내 업무 전체 보기</button>
              </div>
              {data.workItems.length === 0 ? <p className="empty-text">미완료 업무가 없습니다.</p> : null}
              <div className="teams-activity-list">
                {data.workItems.map((item) => (
                  <article className="teams-activity-card" key={item.workItemId}>
                    <div className="teams-activity-card-header">
                      <div>
                        <strong>{item.title}</strong>
                        <small>{displayWorkflowStageName(item.workflowStageCode, item.workflowStageName)} · {item.responsibilityLabel}</small>
                      </div>
                      <StatusBadge label={item.priority === 'Blocking' ? '긴급' : item.statusLabel} tone={workItemStatusTone(item)} />
                    </div>
                    <p>{summarizeText(item.description ?? '처리할 업무가 있습니다.', 110)}</p>
                    <div className="teams-activity-meta">
                      <span>{item.projectTitle}</span>
                      <span>{item.projectCode}</span>
                      <span>생성 {formatDateTime(item.createdAtUtc)}</span>
                    </div>
                    <button type="button" onClick={() => onOpenProject(item.projectId, item.linkUrl)}>
                      업무로 이동
                    </button>
                  </article>
                ))}
              </div>
            </section>
          </div>

          <p className="muted-text">기준 시각: {formatDateTime(data.generatedAtUtc)}</p>
        </>
      ) : null}
    </section>
  );
}

function TeamsActivityNotificationDetailPage({
  developmentUserKey,
  notificationId,
  onBack,
  onOpenProject
}: {
  developmentUserKey: string;
  notificationId: string;
  onBack: () => void;
  onOpenProject: (projectId: string, linkUrl?: string | null) => void;
}) {
  const [state, setState] = useState<LoadState<NotificationItem>>({ kind: 'loading' });
  const [message, setMessage] = useState('');

  const load = useCallback(() => {
    setMessage('');
    setState({ kind: 'loading' });
    getNotificationDetail(developmentUserKey, notificationId)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '알림 상세를 불러올 수 없습니다.')));
  }, [developmentUserKey, notificationId]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  const markRead = async () => {
    setMessage('');
    try {
      const updated = await markNotificationRead(developmentUserKey, notificationId);
      setState({ kind: 'ready', data: updated });
      setMessage('알림을 읽음 처리했습니다.');
    } catch (error: unknown) {
      setMessage(friendlyErrorMessage(error, '알림 읽음 처리에 실패했습니다.'));
    }
  };

  return (
    <section className="page-surface workflow-page teams-activity-page">
      <div className="page-header">
        <div>
          <p className="eyebrow">Teams</p>
          <h2>알림 상세</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>전체 알림으로 돌아가기</button>
          <button type="button" onClick={load}>새로고침</button>
        </div>
      </div>

      {state.kind === 'loading' ? <p className="muted-text">알림 상세를 불러오는 중입니다.</p> : null}
      {message ? <ActionFeedback message={message} tone={message.includes('실패') ? 'error' : 'success'} /> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? (
        <div className="teams-activity-guide" role="note">
          <strong>알림 상세를 표시할 수 없습니다.</strong>
          <p>{loadStateMessage(state) ?? '알림을 찾을 수 없거나 현재 계정으로 볼 수 없습니다.'}</p>
          <p>Teams deep link로 상세 화면을 열지 못한 경우 전체 알림 화면으로 돌아가 최근 알림을 다시 선택하세요.</p>
          <button type="button" onClick={onBack}>전체 알림으로 돌아가기</button>
        </div>
      ) : null}

      {state.kind === 'ready' ? (
        <section className="teams-activity-panel notification-detail-panel" aria-label="인앱 알림 상세">
          <div className="detail-grid">
            <DetailItem label="구분" value={state.data.sourceKindLabel ?? state.data.notificationTypeLabel} />
            <DetailItem label="알림 유형" value={state.data.notificationTypeLabel} />
            <DetailItem label="프로젝트명" value={state.data.projectTitle ?? '프로젝트 없음'} />
            <DetailItem label="관련 업무" value={state.data.workItemTitle ?? '-'} />
            <DetailItem label="제목" value={state.data.title} />
            <DetailItem label="생성시각" value={formatDateTime(state.data.createdAtUtc)} />
            <DetailItem label="읽음 상태" value={state.data.readAtUtc ? '읽음' : '읽지 않음'} />
            <DetailItem label="접근 범위" value={state.data.visibilityScopeLabel ?? '-'} />
          </div>
          <div className="notification-detail-message">
            <strong>내용</strong>
            <p>{state.data.message}</p>
          </div>
          <div className="button-row">
            {!state.data.readAtUtc ? <button type="button" onClick={() => void markRead()}>읽음 처리</button> : null}
            {state.data.workItemId && state.data.projectId ? (
              <button type="button" onClick={() => onOpenProject(state.data.projectId!, state.data.linkUrl)}>
                관련 업무 보기
              </button>
            ) : null}
            {state.data.projectId ? (
              <button type="button" onClick={() => onOpenProject(state.data.projectId!, state.data.linkUrl)}>
                관련 프로젝트 보기
              </button>
            ) : null}
            {state.data.linkUrl && !state.data.projectId ? (
              <button type="button" onClick={() => { window.location.href = state.data.linkUrl!; }}>
                알림 링크 열기
              </button>
            ) : null}
          </div>
          <details className="advanced-detail">
            <summary>내부 추적값</summary>
            <dl>
              <div><dt>Notification ID</dt><dd>{state.data.notificationId}</dd></div>
              <div><dt>Work Item ID</dt><dd>{state.data.workItemId ?? '-'}</dd></div>
              <div><dt>Link URL</dt><dd>{state.data.linkUrl ?? '-'}</dd></div>
            </dl>
          </details>
        </section>
      ) : null}
    </section>
  );
}

function TeamsActivityDeliveryDetailPage({
  developmentUserKey,
  deliveryId,
  onBack
}: {
  developmentUserKey: string;
  deliveryId: string;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<AdminNotificationDeliveryDetail>>({ kind: 'loading' });

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getMyTeamsActivityDelivery(developmentUserKey, deliveryId)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '알림 상세를 불러올 수 없습니다.')));
  }, [deliveryId, developmentUserKey]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  return (
    <section className="page-surface workflow-page teams-activity-page">
      <div className="page-header">
        <div>
          <p className="eyebrow">Teams</p>
          <h2>알림 상세</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>전체 알림으로 돌아가기</button>
          <button type="button" onClick={load}>새로고침</button>
        </div>
      </div>

      {state.kind === 'loading' ? <p className="muted-text">알림 상세를 불러오는 중입니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? (
        <div className="teams-activity-guide" role="note">
          <strong>알림 상세를 표시할 수 없습니다.</strong>
          <p>{loadStateMessage(state) ?? '알림을 찾을 수 없거나 현재 계정으로 볼 수 없습니다.'}</p>
          <button type="button" onClick={onBack}>전체 알림으로 돌아가기</button>
        </div>
      ) : null}

      {state.kind === 'ready' ? (
        <section className="teams-activity-panel notification-detail-panel" aria-label="Teams Activity 알림 상세">
          <div className="detail-grid">
            <DetailItem label="구분" value={state.data.categoryLabel} />
            <DetailItem label="알림 유형" value={state.data.notificationKindLabel ?? '-'} />
            <DetailItem label="프로젝트명" value={state.data.projectName ?? '-'} />
            <DetailItem label="제목" value={state.data.title} />
            <DetailItem label="발송시각" value={formatNullableDateTime(state.data.manualRequestedAtUtc ?? state.data.createdAtUtc)} />
            <DetailItem label="채널" value={state.data.channelLabel} />
            <DetailItem label="수신자" value={state.data.recipient} />
            <DetailItem label="상태" value={state.data.statusLabel} />
            <DetailItem label="발송 완료" value={formatNullableDateTime(state.data.sentAtUtc)} />
          </div>
          <div className="notification-detail-message">
            <strong>내용</strong>
            <p>{state.data.message ?? '-'}</p>
          </div>
          <div className="admin-guidance">
            <p><strong>오류/대기 사유:</strong> {state.data.errorCode ?? '-'} {state.data.errorMessage ? `· ${state.data.errorMessage}` : ''}</p>
            <p><strong>관리자 조치 안내:</strong> {state.data.actionGuide}</p>
          </div>
          <details className="advanced-detail">
            <summary>내부 추적값</summary>
            <dl>
              <div><dt>Delivery ID</dt><dd>{state.data.deliveryId}</dd></div>
              <div><dt>Correlation ID</dt><dd>{state.data.correlationId ?? '-'}</dd></div>
              <div><dt>Provider Message ID</dt><dd>{state.data.providerMessageId ?? '-'}</dd></div>
            </dl>
          </details>
        </section>
      ) : null}
    </section>
  );
}

type NotificationTab = 'All' | 'unread' | 'read';

const notificationTabs: Array<{ key: NotificationTab; label: string }> = [
  { key: 'All', label: '전체' },
  { key: 'unread', label: '읽지 않음' },
  { key: 'read', label: '읽음' }
];

function NotificationsPage({
  developmentUserKey,
  onOpenPreferences,
  onOpenNotification,
  onOpenProject,
  onBadgeRefresh
}: {
  developmentUserKey: string;
  onOpenPreferences: () => void;
  onOpenNotification: (notificationId: string) => void;
  onOpenProject: (projectId: string, linkUrl?: string | null) => void;
  onBadgeRefresh: () => void;
}) {
  const [summaryState, setSummaryState] = useState<LoadState<NotificationSummary>>({ kind: 'loading' });
  const [itemsState, setItemsState] = useState<LoadState<NotificationListResponse>>({ kind: 'loading' });
  const [activeTab, setActiveTab] = useState<NotificationTab>('unread');
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(() => new Set());
  const activeTabRef = useRef<NotificationTab>('unread');
  const loadGenerationRef = useRef(0);
  const actions = useActionFeedback();
  const isMobile = useIsMobileViewport();

  const load = useCallback(async (mode: 'replace' | 'preserve' = 'replace'): Promise<boolean> => {
    const generation = ++loadGenerationRef.current;
    const tab = activeTabRef.current;
    if (mode === 'replace') {
      setSummaryState({ kind: 'loading' });
      setItemsState({ kind: 'loading' });
    }

    try {
      const [summary, items] = await Promise.all([
        getNotificationSummary(developmentUserKey),
        listNotifications(developmentUserKey, tab === 'All' ? undefined : tab)
      ]);
      if (generation !== loadGenerationRef.current) {
        return true;
      }

      setSummaryState({ kind: 'ready', data: summary });
      setItemsState(items.items.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: items });
      onBadgeRefresh();
      return true;
    } catch (error: unknown) {
      if (generation !== loadGenerationRef.current) {
        return true;
      }

      if (mode === 'replace') {
        setSummaryState(toLoadError(error, '알림 요약을 불러올 수 없습니다.'));
        setItemsState(toLoadError(error, '알림 목록을 불러올 수 없습니다.'));
      }
      return false;
    }
  }, [developmentUserKey, onBadgeRefresh]);

  useEffect(() => {
    queueMicrotask(() => void load('replace'));
  }, [load]);

  function selectTab(tab: NotificationTab) {
    activeTabRef.current = tab;
    actions.reset();
    setActiveTab(tab);
    void load('replace');
  }

  function refresh() {
    actions.reset();
    void load('replace');
  }

  async function read(item: NotificationItem) {
    await actions.run(`notification:${item.notificationId}`, async () => {
      await markNotificationRead(developmentUserKey, item.notificationId);
      onBadgeRefresh();
    }, {
      loadingMessage: `${item.title} 알림을 읽음 처리 중입니다.`,
      successMessage: `${item.title} 알림을 읽음 처리했습니다.`,
      partialMessage: `${item.title} 알림은 읽음 처리했지만 최신 목록을 불러오지 못했습니다. 새로고침해 주세요.`,
      errorFallback: '알림 읽음 처리에 실패했습니다.',
      subject: item.title,
      refresh: () => load('preserve'),
      conflicts: { scopes: ['notifications:all'] }
    });
  }

  async function readAll() {
    await actions.run('notifications:all', async () => {
      await markAllNotificationsRead(developmentUserKey);
      onBadgeRefresh();
    }, {
      loadingMessage: '모든 알림을 읽음 처리 중입니다.',
      successMessage: '모든 알림을 읽음 처리했습니다.',
      partialMessage: '모든 알림은 읽음 처리했지만 최신 목록을 불러오지 못했습니다. 새로고침해 주세요.',
      errorFallback: '모든 알림 읽음 처리에 실패했습니다.',
      refresh: () => load('preserve'),
      conflicts: { prefixes: ['notification:', 'notifications:project:'] }
    });
  }

  async function readProject(group: NotificationProjectGroup) {
    if (!group.projectId) return;
    await actions.run(`notifications:project:${group.projectId}`, async () => {
      await markProjectNotificationsRead(developmentUserKey, group.projectId!);
      onBadgeRefresh();
    }, {
      loadingMessage: `${group.projectTitle} 알림을 정리하는 중입니다.`,
      successMessage: `${group.projectTitle} 알림을 모두 읽음 처리했습니다.`,
      partialMessage: `${group.projectTitle} 알림은 읽음 처리했지만 최신 목록을 불러오지 못했습니다. 새로고침해 주세요.`,
      errorFallback: '프로젝트 알림 읽음 처리에 실패했습니다.',
      subject: group.projectTitle,
      refresh: () => load('preserve'),
      conflicts: { prefixes: ['notification:', 'notifications:all'] }
    });
  }

  async function openNotification(item: NotificationItem, destination: () => void) {
    if (!item.readAtUtc) {
      await read(item);
    }
    destination();
  }

  const summary = summaryState.kind === 'ready' ? summaryState.data : null;
  const visibleNotificationIds = itemsState.kind === 'ready' ? itemsState.data.items.map((item) => item.notificationId) : [];
  const notificationSelection = useSelectedRows(visibleNotificationIds);
  const allNotificationsFeedback = actions.feedbackFor('notifications:all');
  const latestRowFeedback = actions.latestFeedback?.scope.startsWith('notification:')
    && (actions.latestFeedback.tone === 'success' || actions.latestFeedback.tone === 'partial')
    ? actions.latestFeedback
    : null;
  const allNotificationsBusy = actions.isBusy('notifications:all');
  const anyNotificationBusy = actions.hasBusyPrefix('notification:');
  const notificationGroups = itemsState.kind === 'ready' ? groupNotificationsByProject(itemsState.data.items) : [];

  return (
    <section className={isMobile ? 'page-surface workflow-page mobile-first-page' : 'page-surface workflow-page'}>
      <div className={isMobile ? 'page-header mobile-page-header' : 'page-header'}>
        <div>
          <p className="eyebrow">{isMobile ? 'FIELD SIGNALS' : 'Notifications'}</p>
          <h2>{isMobile ? '업무 알림' : '알림'}</h2>
          {isMobile ? <p>읽지 않은 긴급 신호를 먼저 확인하세요.</p> : null}
        </div>
        <div className="page-action-cluster">
          <div className="button-row">
            <button type="button" className="secondary-button" onClick={onOpenPreferences}>알림 설정</button>
            <button type="button" disabled={allNotificationsBusy || anyNotificationBusy} onClick={() => void readAll()}>
              {allNotificationsBusy ? '전체 읽음 처리 중' : '전체 읽음'}
            </button>
            <button type="button" onClick={refresh}>새로고침</button>
          </div>
          {allNotificationsFeedback ? <ActionFeedback message={allNotificationsFeedback.message} tone={allNotificationsFeedback.tone} focusOnAttention /> : null}
        </div>
      </div>

      {isMobile ? (
        <section className="mobile-focus-summary mobile-focus-summary--notifications" aria-label="알림 우선순위">
          <header><span>확인 필요</span><strong>{summary?.blockingCount ? `긴급 ${summary.blockingCount}건` : '긴급 알림 없음'}</strong></header>
          <div>
            <button type="button" onClick={() => selectTab('unread')}><span>읽지 않음</span><strong>{summary ? summary.unreadCount : '-'}</strong></button>
            <button type="button" data-tone="danger" onClick={() => selectTab('All')}><span>긴급·차단</span><strong>{summary ? summary.blockingCount : '-'}</strong></button>
          </div>
        </section>
      ) : (
        <div className="dashboard-kpi-grid workflow-kpi-grid">
          <KpiCard label="읽지 않음" value={summary ? String(summary.unreadCount) : '-'} />
          <KpiCard label="긴급/차단" value={summary ? String(summary.blockingCount) : '-'} tone="danger" />
        </div>
      )}

      <div className="workflow-tabs" role="tablist" aria-label="알림 읽음 상태">
        {notificationTabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            className={activeTab === tab.key ? 'active-filter' : undefined}
            aria-selected={activeTab === tab.key}
            onClick={() => selectTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {summaryState.kind !== 'ready' && summaryState.kind !== 'loading' ? <StateMessage state={summaryState} /> : null}
      {latestRowFeedback ? (
        <div className="page-action-feedback" aria-label="최근 알림 처리 결과">
          <ActionFeedback message={latestRowFeedback.message} tone={latestRowFeedback.tone} focusOnAttention />
        </div>
      ) : null}

      {itemsState.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="notifications"
          visibleIds={visibleNotificationIds}
          selectedIds={notificationSelection.selectedIds}
          allSelected={notificationSelection.allSelected}
          busy={notificationSelection.busy}
          filters={{ readStatus: activeTab === 'All' ? undefined : activeTab }}
          onBusyChange={notificationSelection.setBusy}
          onToggleAll={notificationSelection.toggleAll}
          onClear={notificationSelection.clear}
        />
      ) : null}

      {itemsState.kind === 'loading' ? <p className="muted-text">알림을 불러오는 중입니다.</p> : null}
      {itemsState.kind === 'empty' ? <p className="empty-text">표시할 알림이 없습니다.</p> : null}
      {itemsState.kind !== 'ready' && itemsState.kind !== 'loading' && itemsState.kind !== 'empty' ? <StateMessage state={itemsState} /> : null}
      {itemsState.kind === 'ready' ? (
        <div className="workflow-project-groups">
          {notificationGroups.map((group) => {
            const expanded = expandedGroups.has(group.groupKey);
            const visibleGroupItems = expanded ? group.items : group.items.slice(0, 3);
            return (
            <section className="workflow-project-group" key={group.groupKey} aria-label={`${group.projectTitle} 알림`}>
              <div className="project-group-header">
                <span>
                  <strong>{group.projectTitle}</strong>
                  <small>
                    {group.projectCode ? `${group.projectCode} · ` : ''}
                    {group.projectItem ? `Item ${group.projectItem} · ` : ''}
                    알림 {group.items.length}건 · 읽지 않음 {group.unreadCount}건
                  </small>
                </span>
                <div className="button-row">
                  {group.projectId && group.unreadCount > 0 ? (
                    <button type="button" disabled={actions.isBusy(`notifications:project:${group.projectId}`)} onClick={() => void readProject(group)}>
                      {actions.isBusy(`notifications:project:${group.projectId}`) ? '정리 중' : '이 프로젝트 모두 읽음'}
                    </button>
                  ) : null}
                  {group.projectId ? <button type="button" onClick={() => onOpenProject(group.projectId!, group.items[0]?.linkUrl)}>프로젝트로 이동</button> : null}
                </div>
              </div>
              {isMobile ? (
                <div className="workflow-card-list">
                  {visibleGroupItems.map((item) => (
                    <article className={`${item.readAtUtc ? 'workflow-card read' : 'workflow-card unread'} selected-export-row`} key={item.notificationId}>
                      <div className="subsection-header">
                        <SelectionCheckbox checked={notificationSelection.selectedIds.has(item.notificationId)} disabled={notificationSelection.busy} label={`${item.title} 선택`} onChange={(checked) => notificationSelection.toggle(item.notificationId, checked)} />
                        <div>
                          <strong>{item.title}</strong>
                          <small>{item.notificationTypeLabel} · {item.severityLabel}</small>
                        </div>
                        <NotificationStatusBadges item={item} />
                      </div>
                      <p>{item.message}</p>
                      <div className="button-row">
                        <button type="button" onClick={() => void openNotification(item, () => onOpenNotification(item.notificationId))}>상세</button>
                        {item.projectId ? <button type="button" onClick={() => void openNotification(item, () => onOpenProject(item.projectId!, item.linkUrl))}>이동</button> : null}
                        {!item.readAtUtc ? (
                          <button type="button" disabled={allNotificationsBusy || actions.isBusy(`notification:${item.notificationId}`)} onClick={() => void read(item)}>
                            {actions.isBusy(`notification:${item.notificationId}`) ? '읽음 처리 중' : '읽음'}
                          </button>
                        ) : null}
                      </div>
                      {(() => {
                        const feedback = actions.feedbackFor(`notification:${item.notificationId}`);
                        return feedback && (feedback.tone === 'loading' || feedback.tone === 'error')
                          ? <ActionFeedback message={feedback.message} tone={feedback.tone} focusOnAttention={feedback.tone === 'error'} />
                          : null;
                      })()}
                    </article>
                  ))}
                </div>
              ) : (
                <div className="table-wrapper">
                  <table>
                    <thead>
                      <tr>
                        <th>선택</th>
                        <th>알림</th>
                        <th>유형</th>
                        <th>상태</th>
                        <th>생성일</th>
                        <th>작업</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleGroupItems.map((item) => (
                        <tr key={item.notificationId}>
                          <td><SelectionCheckbox checked={notificationSelection.selectedIds.has(item.notificationId)} disabled={notificationSelection.busy} label={`${item.title} 선택`} onChange={(checked) => notificationSelection.toggle(item.notificationId, checked)} /></td>
                          <td><strong>{item.title}</strong><br /><small>{item.message}</small></td>
                          <td>{item.notificationTypeLabel} · {item.severityLabel}</td>
                          <td><NotificationStatusBadges item={item} /></td>
                          <td>{formatDateTime(item.createdAtUtc)}</td>
                          <td>
                            <div className="button-row">
                              <button type="button" onClick={() => void openNotification(item, () => onOpenNotification(item.notificationId))}>상세</button>
                              {item.projectId ? <button type="button" onClick={() => void openNotification(item, () => onOpenProject(item.projectId!, item.linkUrl))}>이동</button> : null}
                              {!item.readAtUtc ? (
                                <button type="button" disabled={allNotificationsBusy || actions.isBusy(`notification:${item.notificationId}`)} onClick={() => void read(item)}>
                                  {actions.isBusy(`notification:${item.notificationId}`) ? '읽음 처리 중' : '읽음'}
                                </button>
                              ) : null}
                            </div>
                            {(() => {
                              const feedback = actions.feedbackFor(`notification:${item.notificationId}`);
                              return feedback && (feedback.tone === 'loading' || feedback.tone === 'error')
                                ? <ActionFeedback message={feedback.message} tone={feedback.tone} focusOnAttention={feedback.tone === 'error'} />
                                : null;
                            })()}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {group.items.length > 3 ? (
                <button
                  type="button"
                  className="notification-group-toggle"
                  aria-expanded={expanded}
                  onClick={() => setExpandedGroups((current) => {
                    const next = new Set(current);
                    if (next.has(group.groupKey)) next.delete(group.groupKey); else next.add(group.groupKey);
                    return next;
                  })}
                >
                  {expanded ? '이전 알림 접기' : `이전 알림 ${group.items.length - 3}건 보기`}
                </button>
              ) : null}
            </section>
            );
          })}
        </div>
      ) : null}
    </section>
  );
}

function NotificationStatusBadges({ item }: { item: NotificationItem }) {
  return (
    <div className="status-badge-row">
      <StatusBadge label={item.readAtUtc ? '읽음' : '읽지 않음'} tone={notificationReadTone(item.readAtUtc)} />
      {item.severity === 'Critical' || item.severity === 'Warning' ? <StatusBadge label="긴급" tone="danger" /> : null}
    </div>
  );
}

function KpiCard({ label, value, tone }: { label: string; value: string; tone?: 'danger' }) {
  return (
    <article className="dashboard-kpi-card" data-variant={tone === 'danger' ? 'warning' : undefined}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

type StatusTone = 'danger' | 'warning' | 'info' | 'success' | 'neutral';

function StatusBadge({ label, tone }: { label: string; tone?: StatusTone }) {
  return <span className="status-badge" data-tone={tone}>{label}</span>;
}

function ActionFeedback({
  message,
  tone = 'neutral',
  focusOnAttention = false
}: {
  message: string;
  tone?: ActionFeedbackTone;
  focusOnAttention?: boolean;
}) {
  const feedbackRef = useRef<HTMLParagraphElement>(null);

  useEffect(() => {
    if (focusOnAttention && message && (tone === 'error' || tone === 'partial')) {
      feedbackRef.current?.focus();
    }
  }, [focusOnAttention, message, tone]);

  if (!message) {
    return null;
  }

  return (
    <p
      ref={feedbackRef}
      className="action-feedback"
      data-tone={tone}
      role={tone === 'error' ? 'alert' : 'status'}
      aria-live={tone === 'error' ? 'assertive' : 'polite'}
      tabIndex={focusOnAttention ? -1 : undefined}
    >
      {message}
    </p>
  );
}

function formatBadgeCount(value: number) {
  return value > 99 ? '99+' : String(value);
}

function summarizeText(value: string | null | undefined, maxLength: number) {
  const normalized = (value ?? '').replace(/\s+/gu, ' ').trim();
  if (!normalized) {
    return '-';
  }

  return normalized.length <= maxLength
    ? normalized
    : `${normalized.slice(0, Math.max(0, maxLength - 3))}...`;
}

function workItemStatusTone(item: { status: string; priority: string }): StatusTone {
  if (item.priority === 'Blocking') {
    return 'danger';
  }

  switch (item.status) {
    case 'Requested':
      return 'warning';
    case 'InProgress':
      return 'info';
    case 'Completed':
      return 'success';
    case 'Cancelled':
      return 'neutral';
    default:
      return 'neutral';
  }
}

function notificationReadTone(readAtUtc: string | null): StatusTone {
  return readAtUtc ? 'neutral' : 'info';
}

interface WorkItemProjectGroup {
  projectId: string;
  projectTitle: string;
  projectCode: string;
  projectItem: string;
  projectDeliveryDate: string | null;
  items: MyWorkItem[];
}

function groupWorkItemsByProject(items: MyWorkItem[]): WorkItemProjectGroup[] {
  const groups = new Map<string, WorkItemProjectGroup>();
  for (const item of items) {
    const existing = groups.get(item.projectId);
    if (existing) {
      existing.items.push(item);
      continue;
    }

    groups.set(item.projectId, {
      projectId: item.projectId,
      projectTitle: item.projectTitle,
      projectCode: item.projectCode,
      projectItem: item.projectItem,
      projectDeliveryDate: item.projectDeliveryDate,
      items: [item]
    });
  }

  return Array.from(groups.values())
    .map((group) => ({
      ...group,
      items: [...group.items].sort((left, right) => {
        const leftSequence = workflowSequenceForStage(left.workflowStageCode);
        const rightSequence = workflowSequenceForStage(right.workflowStageCode);
        if (leftSequence !== rightSequence) {
          return leftSequence - rightSequence;
        }

        return Date.parse(right.createdAtUtc) - Date.parse(left.createdAtUtc);
      })
    }))
    .sort((left, right) => {
      const leftLatest = Math.max(...left.items.map((item) => Date.parse(item.createdAtUtc)));
      const rightLatest = Math.max(...right.items.map((item) => Date.parse(item.createdAtUtc)));
      return rightLatest - leftLatest;
    });
}

interface NotificationProjectGroup {
  groupKey: string;
  projectId: string | null;
  projectTitle: string;
  projectCode: string | null;
  projectItem: string | null;
  unreadCount: number;
  items: NotificationItem[];
}

function groupNotificationsByProject(items: NotificationItem[]): NotificationProjectGroup[] {
  const groups = new Map<string, NotificationProjectGroup>();
  for (const item of items) {
    const groupKey = item.projectId ?? 'system';
    const existing = groups.get(groupKey);
    if (existing) {
      existing.items.push(item);
      if (!item.readAtUtc) {
        existing.unreadCount += 1;
      }
      continue;
    }

    groups.set(groupKey, {
      groupKey,
      projectId: item.projectId,
      projectTitle: item.projectTitle ?? '기타 알림',
      projectCode: item.projectCode,
      projectItem: item.projectItem,
      unreadCount: item.readAtUtc ? 0 : 1,
      items: [item]
    });
  }

  return Array.from(groups.values())
    .map((group) => ({
      ...group,
      items: [...group.items].sort((left, right) => Date.parse(right.createdAtUtc) - Date.parse(left.createdAtUtc))
    }))
    .sort((left, right) => {
      const leftLatest = Math.max(...left.items.map((item) => Date.parse(item.createdAtUtc)));
      const rightLatest = Math.max(...right.items.map((item) => Date.parse(item.createdAtUtc)));
      return rightLatest - leftLatest;
    });
}

function workflowSequenceForStage(stageCode: string) {
  const sequence: Record<string, number> = {
    SalesProjectCreated: 1,
    ProductionPlanning: 2,
    DesignPanelInfo: 3,
    ProcurementInfo: 4,
    MaterialArrived: 5,
    IQC: 6,
    ReceiptConfirmed: 7,
    KittingCompleted: 8,
    ManufacturingWork: 9,
    LQC: 10,
    ManufacturingCompleted: 11,
    OQC: 12,
    CustomerInspection: 13,
    FAT: 14,
    PackingCompleted: 15,
    DepartureProcessed: 16,
    DeliveryCompleted: 17,
    SalesSettlementCompleted: 18
  };
  return sequence[stageCode] ?? 999;
}

function departmentForStageCode(stageCode: string) {
  switch (stageCode) {
    case 'SalesProjectCreated':
    case 'SalesSettlementCompleted':
      return 'sales';
    case 'DesignPanelInfo':
      return 'design';
    case 'ProductionPlanning':
      return 'production-planning';
    case 'ProcurementInfo':
      return 'procurement';
    case 'MaterialArrived':
    case 'ReceiptConfirmed':
    case 'KittingCompleted':
      return 'materials';
    case 'ManufacturingWork':
    case 'ManufacturingCompleted':
      return 'manufacturing';
    case 'IQC':
    case 'LQC':
    case 'OQC':
    case 'CustomerInspection':
    case 'FAT':
      return 'quality';
    case 'PackingCompleted':
    case 'DepartureProcessed':
    case 'DeliveryCompleted':
      return 'logistics';
    default:
      return 'manufacturing';
  }
}

function ProjectListPage({
  developmentUserKey,
  canCreate,
  canReadDeleted,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  onCreate,
  onOpen,
  onOpenPending,
  onOpenDeleted
}: {
  developmentUserKey: string;
  canCreate: boolean;
  canReadDeleted: boolean;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  onCreate: () => void;
  onOpen: (projectId: string) => void;
  onOpenPending: (projectId: string) => void;
  onOpenDeleted: (projectId: string) => void;
}) {
  const [search, setSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [tab, setTab] = useState<ProjectListTab>('All');
  const [state, setState] = useState<LoadState<Array<ProjectListItem | DeletedProjectListItem>>>({ kind: 'loading' });
  const [summaryState, setSummaryState] = useState<LoadState<ProjectDashboardSummary>>({ kind: 'loading' });
  const [showProjectExcel, setShowProjectExcel] = useState(false);
  const [projectExcelMessage, setProjectExcelMessage] = useState('');
  const [purgeMessage, setPurgeMessage] = useState('');
  const [purgeAllConfirmText, setPurgeAllConfirmText] = useState('');
  const [isPurgingAll, setIsPurgingAll] = useState(false);
  const [isDownloadingProjectTemplate, setIsDownloadingProjectTemplate] = useState(false);
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(false);
  const [draftSearch, setDraftSearch] = useState('');
  const [draftDateFrom, setDraftDateFrom] = useState('');
  const [draftDateTo, setDraftDateTo] = useState('');
  const [selectedProjectIds, setSelectedProjectIds] = useState<Set<string>>(() => new Set());
  const [isSelectedExportBusy, setIsSelectedExportBusy] = useState(false);
  const mobileFilterTriggerRef = useRef<HTMLButtonElement>(null);
  const requestIdRef = useRef(0);
  const abortControllerRef = useRef<AbortController | null>(null);
  const isMobile = useIsMobileViewport();

  const load = useCallback(() => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    const request = tab === 'Deleted'
      ? listDeletedProjects(developmentUserKey, search, { signal: controller.signal })
      : listProjects(developmentUserKey, search, tab, {
        signal: controller.signal,
        deliveryDateFrom: dateFrom,
        deliveryDateTo: dateTo
      });

    queueMicrotask(() => {
      if (requestId === requestIdRef.current && !controller.signal.aborted) {
        setState({ kind: 'loading' });
      }
    });

    request
      .then((response) => {
        if (requestId !== requestIdRef.current || controller.signal.aborted) {
          return;
        }

        setSelectedProjectIds(new Set());
        setState(response.items.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: response.items });
      })
      .catch((error: unknown) => {
        if (requestId !== requestIdRef.current || controller.signal.aborted || isAbortError(error)) {
          return;
        }

        setState(toLoadError(error, '프로젝트 목록을 불러올 수 없습니다.'));
      })
      .finally(() => {
        if (requestId === requestIdRef.current && abortControllerRef.current === controller) {
          abortControllerRef.current = null;
        }
      });
  }, [dateFrom, dateTo, developmentUserKey, search, tab]);

  useEffect(() => {
    load();
    return () => {
      abortControllerRef.current?.abort();
    };
  }, [load]);

  useEffect(() => {
    const controller = new AbortController();
    queueMicrotask(() => setSummaryState({ kind: 'loading' }));
    getProjectSummary(developmentUserKey, { signal: controller.signal })
      .then((summary) => setSummaryState({ kind: 'ready', data: summary }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted && !isAbortError(error)) {
          setSummaryState(toLoadError(error, '프로젝트 요약을 불러올 수 없습니다.'));
        }
      });

    return () => controller.abort();
  }, [developmentUserKey]);

  async function downloadProjectTemplate() {
    setIsDownloadingProjectTemplate(true);
    setProjectExcelMessage('');
    try {
      const template = await downloadProjectExcelTemplate(developmentUserKey);
      const url = URL.createObjectURL(template.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = template.fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      setProjectExcelMessage('프로젝트 Excel 양식을 다운로드했습니다.');
    } catch (error) {
      handleFormError(error, () => undefined, setProjectExcelMessage);
    } finally {
      setIsDownloadingProjectTemplate(false);
    }
  }

  async function purgeAllDeleted() {
    setPurgeMessage('');
    if (purgeAllConfirmText !== '삭제 보관함 비우기') {
      setPurgeMessage('확인 문구를 정확히 입력해 주세요.');
      return;
    }

    setIsPurgingAll(true);
    try {
      const result = await purgeAllDeletedProjects(developmentUserKey, purgeAllConfirmText);
      setPurgeAllConfirmText('');
      setPurgeMessage(result.deletedProjectCount === 0 ? '비울 삭제 프로젝트가 없습니다.' : `삭제 프로젝트 ${result.deletedProjectCount}건을 완전히 삭제했습니다.`);
      load();
    } catch (error) {
      handleFormError(error, () => undefined, setPurgeMessage);
    } finally {
      setIsPurgingAll(false);
    }
  }

  function setProjectSelected(projectId: string, selected: boolean) {
    if (isSelectedExportBusy) {
      return;
    }

    setSelectedProjectIds((current) => {
      const next = new Set(current);
      if (selected) {
        next.add(projectId);
      } else {
        next.delete(projectId);
      }
      return next;
    });
  }

  function setAllVisibleProjectsSelected(selected: boolean) {
    if (isSelectedExportBusy || state.kind !== 'ready' || tab === 'Deleted') {
      return;
    }

    setSelectedProjectIds(selected
      ? new Set(state.data.map((project) => project.projectId))
      : new Set());
  }

  return (
    <section className={isMobile ? 'page-surface mobile-first-page mobile-project-list-page' : 'page-surface'}>
      <div className={isMobile ? 'page-header mobile-page-header' : 'page-header'}>
        <div>
          <p className="eyebrow">{isMobile ? 'FIELD PROJECTS' : 'Projects'}</p>
          <h2>{isMobile ? '현장 프로젝트' : '프로젝트 목록'}</h2>
          {isMobile ? <p>병목과 납기를 먼저 보고 필요한 프로젝트를 선택하세요.</p> : null}
        </div>
        <div className={isMobile ? 'mobile-page-actions page-export-actions' : 'button-row page-export-actions'}>
          {canCreate ? (
            isMobile ? (
              <>
                <button type="button" className="primary-button" onClick={onCreate}>+ 프로젝트</button>
                <details className="mobile-secondary-actions">
                  <summary>기타 작업</summary>
                  <button type="button" onClick={downloadProjectTemplate} disabled={isDownloadingProjectTemplate}>
                    {isDownloadingProjectTemplate ? '다운로드 중' : 'Excel 양식'}
                  </button>
                  <button type="button" onClick={() => setShowProjectExcel(true)}>Excel 업로드</button>
                </details>
              </>
            ) : (
              <>
              <button type="button" onClick={downloadProjectTemplate} disabled={isDownloadingProjectTemplate}>
                {isDownloadingProjectTemplate ? '다운로드 중' : '프로젝트 Excel 양식'}
              </button>
              <button type="button" onClick={() => setShowProjectExcel(true)}>프로젝트 Excel 업로드</button>
              <button type="button" className="primary-button" onClick={onCreate}>신규 프로젝트</button>
              </>
            )
          ) : null}
        </div>
      </div>

      {isMobile ? (
        <>
          <button
            ref={mobileFilterTriggerRef}
            type="button"
            className="mobile-filter-trigger"
            aria-expanded={mobileFiltersOpen}
            disabled={isSelectedExportBusy}
            onClick={() => {
              setDraftSearch(search);
              setDraftDateFrom(dateFrom);
              setDraftDateTo(dateTo);
              setMobileFiltersOpen(true);
            }}
          >
            <span><strong>검색·필터</strong><small>{[search, dateFrom, dateTo].filter(Boolean).length > 0 ? `${[search, dateFrom, dateTo].filter(Boolean).length}개 조건 적용 중` : '전체 프로젝트 표시 중'}</small></span>
            <span aria-hidden="true">⌕</span>
          </button>
          <MobileSheet
            open={mobileFiltersOpen}
            title="프로젝트 검색·필터"
            eyebrow="PROJECT FILTER"
            description="조건을 고른 뒤 적용하면 목록이 갱신됩니다. 취소하면 기존 조건을 유지합니다."
            onClose={() => setMobileFiltersOpen(false)}
            triggerRef={mobileFilterTriggerRef}
            fullScreen
            footer={(
              <>
                <button type="button" disabled={isSelectedExportBusy} onClick={() => { setDraftSearch(''); setDraftDateFrom(''); setDraftDateTo(''); }}>초기화</button>
                <button type="button" disabled={isSelectedExportBusy} onClick={() => setMobileFiltersOpen(false)}>취소</button>
                <button
                  type="button"
                  className="primary-button"
                  disabled={isSelectedExportBusy}
                  onClick={() => {
                    setSearch(draftSearch);
                    setDateFrom(draftDateFrom);
                    setDateTo(draftDateTo);
                    setMobileFiltersOpen(false);
                  }}
                >
                  조건 적용
                </button>
              </>
            )}
          >
            <div className="mobile-filter-form">
              <label><span>검색어</span><input data-autofocus value={draftSearch} onChange={(event) => setDraftSearch(event.target.value)} placeholder="고객사, Item, Code, Title" /></label>
              <label><span>납기 시작일</span><input type="date" value={draftDateFrom} onChange={(event) => setDraftDateFrom(event.target.value)} /></label>
              <label><span>납기 종료일</span><input type="date" value={draftDateTo} onChange={(event) => setDraftDateTo(event.target.value)} /></label>
            </div>
          </MobileSheet>
        </>
      ) : (
        <form
          className="toolbar"
          onSubmit={(event) => {
            event.preventDefault();
            load();
          }}
        >
          <input
            value={search}
            disabled={isSelectedExportBusy}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="고객사, Item, PJT Code, PJT Title 검색"
          />
          <label className="date-filter-field">
            <span>시작일</span>
            <input type="date" value={dateFrom} disabled={isSelectedExportBusy} onChange={(event) => setDateFrom(event.target.value)} />
          </label>
          <label className="date-filter-field">
            <span>종료일</span>
            <input type="date" value={dateTo} disabled={isSelectedExportBusy} onChange={(event) => setDateTo(event.target.value)} />
          </label>
          <button type="button" disabled={isSelectedExportBusy} onClick={() => { setSelectedProjectIds(new Set()); setDateFrom(''); setDateTo(''); }}>필터 초기화</button>
          <button type="submit" disabled={isSelectedExportBusy}>검색</button>
        </form>
      )}

      {summaryState.kind === 'ready' ? <ProjectKpiGrid summary={summaryState.data} /> : null}
      {summaryState.kind !== 'ready' && summaryState.kind !== 'loading' && summaryState.kind !== 'empty' ? <StateMessage state={summaryState} /> : null}

      <div className="tab-row" role="tablist" aria-label="프로젝트 상태">
        {projectTabs(canReadDeleted).map((item) => (
          <button
            key={item.value}
            type="button"
            role="tab"
            aria-selected={tab === item.value}
            className={tab === item.value ? 'tab-button active' : 'tab-button'}
            disabled={isSelectedExportBusy}
            onClick={() => setTab(item.value)}
          >
            {item.label}
          </button>
        ))}
      </div>

      {tab === 'Deleted' && canPurgeDeletedProjects ? (
        <section className="danger-zone" aria-label="삭제 보관함 비우기">
          <div>
            <strong>삭제 보관함 비우기</strong>
            <p className="muted-text">삭제 보관함의 모든 프로젝트와 관련 데이터를 완전히 삭제합니다. 되돌릴 수 없습니다.</p>
          </div>
          <label className="form-field compact-field">
            <span>확인 문구: 삭제 보관함 비우기</span>
            <input value={purgeAllConfirmText} onChange={(event) => setPurgeAllConfirmText(event.target.value)} />
          </label>
          <button type="button" className="danger-button" disabled={isPurgingAll || purgeAllConfirmText !== '삭제 보관함 비우기'} onClick={purgeAllDeleted}>
            {isPurgingAll ? '삭제 중' : '삭제 보관함 비우기'}
          </button>
        </section>
      ) : null}

      {state.kind === 'loading' ? <p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">등록된 프로젝트가 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}

      {state.kind === 'ready' && tab !== 'Deleted' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="projects"
          ariaLabel="선택 프로젝트 내보내기"
          label="선택 Excel 내보내기"
          visibleIds={state.data.map((project) => project.projectId)}
          selectedIds={selectedProjectIds}
          allSelected={state.data.length > 0 && state.data.every((project) => selectedProjectIds.has(project.projectId))}
          busy={isSelectedExportBusy}
          filters={{ search, status: tab === 'All' ? undefined : tab, deliveryDateFrom: dateFrom, deliveryDateTo: dateTo }}
          onBusyChange={setIsSelectedExportBusy}
          onToggleAll={setAllVisibleProjectsSelected}
          onClear={() => setSelectedProjectIds(new Set())}
        />
      ) : null}

      {state.kind === 'ready' ? (
        <ProjectListView
          projects={state.data}
          canReadSalesAmount={canReadSalesAmount}
          canPurgeDeletedProjects={canPurgeDeletedProjects && tab === 'Deleted'}
          developmentUserKey={developmentUserKey}
          onPurged={load}
          onOpen={(projectId) => tab === 'Deleted' ? onOpenDeleted(projectId) : onOpen(projectId)}
          onOpenPending={onOpenPending}
          selectionEnabled={tab !== 'Deleted'}
          selectedProjectIds={selectedProjectIds}
          selectionDisabled={isSelectedExportBusy}
          onProjectSelectionChange={setProjectSelected}
        />
      ) : null}
      {projectExcelMessage ? <p role="alert" className={successMessage(projectExcelMessage) ? 'success-text' : 'error-text'}>{projectExcelMessage}</p> : null}
      {purgeMessage ? <p role="alert" className={successMessage(purgeMessage) ? 'success-text' : 'error-text'}>{purgeMessage}</p> : null}
      {showProjectExcel ? (
        <ProjectExcelDialog
          developmentUserKey={developmentUserKey}
          onClose={() => setShowProjectExcel(false)}
          onApplied={() => {
            setShowProjectExcel(false);
            load();
            setProjectExcelMessage('프로젝트 Excel을 저장했습니다.');
          }}
        />
      ) : null}
    </section>
  );
}

function ProjectListView({
  projects,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  developmentUserKey,
  onPurged,
  onOpen,
  onOpenPending,
  selectionEnabled,
  selectedProjectIds,
  selectionDisabled,
  onProjectSelectionChange
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  developmentUserKey: string;
  onPurged: () => void;
  onOpen: (projectId: string) => void;
  onOpenPending: (projectId: string) => void;
  selectionEnabled: boolean;
  selectedProjectIds: ReadonlySet<string>;
  selectionDisabled: boolean;
  onProjectSelectionChange: (projectId: string, selected: boolean) => void;
}) {
  const isMobile = useIsMobileViewport();

  return (
    <div className="project-list">
      {isMobile
        ? <ProjectListMobile projects={projects} canReadSalesAmount={canReadSalesAmount} canPurgeDeletedProjects={canPurgeDeletedProjects} developmentUserKey={developmentUserKey} onPurged={onPurged} onOpen={onOpen} onOpenPending={onOpenPending} selectionEnabled={selectionEnabled} selectedProjectIds={selectedProjectIds} selectionDisabled={selectionDisabled} onProjectSelectionChange={onProjectSelectionChange} />
        : <ProjectListDesktop projects={projects} canReadSalesAmount={canReadSalesAmount} canPurgeDeletedProjects={canPurgeDeletedProjects} developmentUserKey={developmentUserKey} onPurged={onPurged} onOpen={onOpen} onOpenPending={onOpenPending} selectionEnabled={selectionEnabled} selectedProjectIds={selectedProjectIds} selectionDisabled={selectionDisabled} onProjectSelectionChange={onProjectSelectionChange} />}
    </div>
  );
}

function ProjectExcelDialog({
  developmentUserKey,
  onClose,
  onApplied
}: {
  developmentUserKey: string;
  onClose: () => void;
  onApplied: () => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProjectExcelPreviewResponse | null>(null);
  const [message, setMessage] = useState('');
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const isMobile = useIsMobileViewport();

  async function runPreview() {
    if (!file) {
      setMessage('선택한 파일이 없습니다.');
      return;
    }

    setIsPreviewing(true);
    setMessage('');
    try {
      setPreview(await previewProjectExcel(developmentUserKey, file));
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPreviewing(false);
    }
  }

  async function apply() {
    if (!file || !preview || !canApplyProjectExcel(preview, file, isApplying)) {
      return;
    }

    setIsApplying(true);
    setMessage('');
    try {
      await applyProjectExcel(developmentUserKey, file, preview.fileSha256);
      onApplied();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsApplying(false);
    }
  }

  const disabledReason = projectExcelApplyDisabledReason(preview, file, isApplying);
  const canApply = !disabledReason;

  return (
    <DialogBackdrop ariaLabel="프로젝트 Excel 업로드" onClose={onClose} closeDisabled={isPreviewing || isApplying}>
      <div className="dialog wide-dialog">
        <div className="subsection-header">
          <h3>프로젝트 Excel 업로드</h3>
          <button type="button" onClick={onClose}>닫기</button>
        </div>
        <div className="toolbar">
          <input type="file" accept=".xlsx" onChange={(event) => {
            setFile(event.target.files?.[0] ?? null);
            setPreview(null);
            setMessage('');
          }} />
          <button type="button" disabled={isPreviewing} onClick={runPreview}>{isPreviewing ? '미리보기 중' : 'Preview'}</button>
        </div>

        {preview ? (
          <>
            <div className="excel-preview-action-bar">
              <div className="excel-preview-counts">
                <span>신규 {preview.newCount}건</span>
                <span className={preview.needsReviewCount > 0 ? 'negative-text' : undefined}>확인 {preview.needsReviewCount}건</span>
                <span className={preview.errorCount > 0 ? 'negative-text' : undefined}>오류 {preview.errorCount}건</span>
              </div>
              <button type="button" className="primary-button" disabled={!canApply} onClick={apply}>
                {isApplying ? '저장 중' : 'Excel 저장'}
              </button>
              {disabledReason ? <p className="warning-text">{disabledReason}</p> : null}
            </div>
            <ExcelIssueSummary rows={preview.rows} />
            {isMobile ? <ProjectExcelPreviewMobile rows={preview.rows} /> : <ProjectExcelPreviewDesktop rows={preview.rows} />}
          </>
        ) : null}
        {message ? <p role="alert" className="error-text">{message}</p> : null}
      </div>
    </DialogBackdrop>
  );
}

function DialogBackdrop({
  ariaLabel,
  closeDisabled = false,
  onClose,
  children
}: {
  ariaLabel: string;
  closeDisabled?: boolean;
  onClose: () => void;
  children: React.ReactNode;
}) {
  return (
    <div
      className="dialog-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label={ariaLabel}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !closeDisabled) {
          onClose();
        }
      }}
    >
      {children}
    </div>
  );
}

function ProjectExcelPreviewDesktop({ rows }: { rows: ProjectExcelPreviewResponse['rows'] }) {
  return (
    <div className="excel-preview-table project-excel-preview excel-preview-desktop">
      {rows.map((row) => (
        <div className="excel-preview-row project-excel-preview-row" key={`${row.excelRowNumber}-${row.projectTitle ?? 'row'}`}>
          <strong>Row {row.excelRowNumber || '-'}</strong>
          <span>{emptyDash(row.customerName)}</span>
          <span>{emptyDash(row.projectCode)}</span>
          <span>{emptyDash(row.projectTitle)}</span>
          <span>{row.panelCount ?? '-'}</span>
          <span>{emptyDash(row.deliveryDate)}</span>
          <span>{formatPackagingMethod(row.packagingMethod)}</span>
          <span>{row.fatRequired === null || row.fatRequired === undefined ? '-' : row.fatRequired ? '예' : '아니오'}</span>
          <span className={row.resultType === 'Error' || row.resultType === 'NeedsReview' ? 'negative-text' : undefined}>{projectExcelResultLabel(row.resultType)}</span>
          <small>{row.errorMessages.join(' ')}</small>
        </div>
      ))}
    </div>
  );
}

type ExcelIssueRow = {
  excelRowNumber: number;
  resultType: string;
  errorMessages: readonly string[];
  sourceProjectText?: string | null;
  sourceProjectCodeText?: string | null;
  projectTitle?: string | null;
  projectCode?: string | null;
  panelName?: string | null;
  no?: number | null;
  orderItem?: string | null;
  orderDate?: string | null;
  expectedReceiptDate?: string | null;
  packagingMethod?: string | null;
  salesOwnerText?: string | null;
  salesOwnerName?: string | null;
};

function ExcelIssueSummary({ rows }: { rows: ExcelIssueRow[] }) {
  const [filter, setFilter] = useState<'All' | 'Error' | 'NeedsReview'>('All');
  const issueRows = rows.filter((row) => row.resultType === 'Error' || row.resultType === 'NeedsReview' || row.errorMessages.length > 0);
  if (issueRows.length === 0) {
    return null;
  }

  const errorCount = issueRows.filter((row) => row.resultType === 'Error').length;
  const reviewCount = issueRows.filter((row) => row.resultType === 'NeedsReview').length;
  const visibleRows = issueRows.filter((row) => filter === 'All' || row.resultType === filter);

  return (
    <section className="excel-issue-summary" aria-label="Excel 오류 요약">
      <div className="subsection-header">
        <div>
          <h4>오류 {errorCount}건 · 확인 필요 {reviewCount}건</h4>
          <p className="muted-text">저장할 수 없는 행은 아래 내용을 수정한 뒤 다시 미리보기를 실행하세요.</p>
        </div>
        <div className="button-row">
          <button type="button" className={filter === 'All' ? 'active-filter' : undefined} onClick={() => setFilter('All')}>전체 보기</button>
          <button type="button" className={filter === 'Error' ? 'active-filter' : undefined} onClick={() => setFilter('Error')}>오류만 보기</button>
          <button type="button" className={filter === 'NeedsReview' ? 'active-filter' : undefined} onClick={() => setFilter('NeedsReview')}>확인 필요만 보기</button>
        </div>
      </div>
      <div className="excel-issue-list">
        {visibleRows.map((row) => (
          <article className="excel-issue-card" key={`${row.excelRowNumber}-${row.resultType}-${issueEntityText(row)}`}>
            <strong>{row.excelRowNumber || '-'}행</strong>
            <span>대상: {issueEntityText(row)}</span>
            <span>필드: {issueFieldText(row)}</span>
            <span>입력값: {issueInputText(row)}</span>
            <span>문제: {issueProblemText(row)}</span>
            <span>해결: {issueSolutionText(row)}</span>
          </article>
        ))}
      </div>
    </section>
  );
}

function issueEntityText(row: ExcelIssueRow) {
  if (row.sourceProjectText || row.sourceProjectCodeText) {
    return `${row.sourceProjectText ?? '-'}${row.sourceProjectCodeText ? ` / ${row.sourceProjectCodeText}` : ''}`;
  }

  if (row.projectTitle || row.projectCode) {
    return `${row.projectTitle ?? '-'}${row.projectCode ? ` / ${row.projectCode}` : ''}`;
  }

  if (row.no) {
    return `No.${row.no}${row.panelName ? ` / ${row.panelName}` : ''}`;
  }

  return row.panelName ?? '-';
}

function issueProblemText(row: ExcelIssueRow) {
  if (row.errorMessages.length > 0) {
    return row.errorMessages.join(' ');
  }

  if (row.resultType === 'NeedsReview') {
    return '확인할 항목이 있습니다.';
  }

  return '입력값을 확인해 주세요.';
}

function issueFieldText(row: ExcelIssueRow) {
  const problem = issueProblemText(row);
  if (problem.includes('프로젝트')) {
    return row.sourceProjectCodeText ? 'PJT Code' : '프로젝트명';
  }

  if (problem.includes('입고일') || problem.includes('입고예정일')) {
    return '입고예정일';
  }

  if (problem.includes('발주일')) {
    return '발주일';
  }

  if (problem.includes('포장방식')) {
    return '포장방식';
  }

  if (problem.includes('영업담당자')) {
    return '영업담당자';
  }

  return '행 전체';
}

function issueInputText(row: ExcelIssueRow) {
  const field = issueFieldText(row);
  if (field === 'PJT Code') {
    return row.sourceProjectCodeText ?? row.projectCode ?? '-';
  }

  if (field === '프로젝트명') {
    return row.sourceProjectText ?? row.projectTitle ?? '-';
  }

  if (field === '입고예정일') {
    return row.expectedReceiptDate ?? '-';
  }

  if (field === '발주일') {
    return row.orderDate ?? '-';
  }

  if (field === '포장방식') {
    return row.packagingMethod ?? '-';
  }

  if (field === '영업담당자') {
    return row.salesOwnerText ?? row.salesOwnerName ?? '-';
  }

  return row.orderItem ?? row.panelName ?? '-';
}

function issueSolutionText(row: ExcelIssueRow) {
  const problem = issueProblemText(row);
  if (problem.includes('프로젝트') || row.resultType === 'NeedsReview') {
    return '등록된 프로젝트를 선택하거나 Excel의 프로젝트명을 확인해 주세요.';
  }

  if (problem.includes('날짜')) {
    return 'yyyy-mm-dd 형식으로 입력해 주세요.';
  }

  if (problem.includes('필수') || problem.includes('누락')) {
    return '필수 입력값을 채운 뒤 다시 미리보기를 실행해 주세요.';
  }

  return '행의 값을 수정한 뒤 다시 미리보기를 실행해 주세요.';
}

function ProjectExcelPreviewMobile({ rows }: { rows: ProjectExcelPreviewResponse['rows'] }) {
  return (
    <div className="excel-preview-cards excel-preview-mobile">
      {rows.map((row) => (
        <article className="excel-preview-card" key={`${row.excelRowNumber}-${row.projectTitle ?? 'row'}-mobile`}>
          <div className="subsection-header">
            <h3>Row {row.excelRowNumber || '-'}</h3>
            <span className={row.resultType === 'Error' || row.resultType === 'NeedsReview' ? 'negative-text' : undefined}>{projectExcelResultLabel(row.resultType)}</span>
          </div>
          <dl className="mobile-detail-list">
            <div><dt>고객사</dt><dd>{emptyDash(row.customerName)}</dd></div>
            <div><dt>Code</dt><dd>{emptyDash(row.projectCode)}</dd></div>
            <div><dt>PJT Title</dt><dd>{emptyDash(row.projectTitle)}</dd></div>
            <div><dt>면수</dt><dd>{row.panelCount ?? '-'}</dd></div>
            <div><dt>납기일</dt><dd>{emptyDash(row.deliveryDate)}</dd></div>
            <div><dt>포장방식</dt><dd>{formatPackagingMethod(row.packagingMethod)}</dd></div>
            <div><dt>FAT 필요 여부</dt><dd>{row.fatRequired === null || row.fatRequired === undefined ? '-' : row.fatRequired ? '예' : '아니오'}</dd></div>
            <div><dt>영업담당자</dt><dd>{emptyDash(row.salesOwnerName ?? row.salesOwnerText)}</dd></div>
            <div><dt>오류</dt><dd>{row.errorMessages.join(' ') || '-'}</dd></div>
          </dl>
        </article>
      ))}
    </div>
  );
}

function ProjectKpiGrid({ summary }: { summary: ProjectDashboardSummary }) {
  return (
    <div className="dashboard-kpi-grid project-kpi-grid" aria-label="프로젝트 요약">
      <DashboardKpiCard title="전체 프로젝트" value={summary.totalProjectCount} helperText="완료·삭제 제외" />
      <DashboardKpiCard title="진행" value={summary.activeProjectCount} helperText="진행 프로젝트" variant="positive" />
      <DashboardKpiCard title="보류" value={summary.onHoldProjectCount} helperText="보류 프로젝트" variant="warning" />
      <DashboardKpiCard title="취소 프로젝트" value={summary.cancelledProjectCount} helperText="취소 프로젝트" />
      <DashboardKpiCard title="제조 완료 프로젝트" value={summary.manufacturingCompletedProjectCount} helperText="모든 패널 제조 완료" />
      <DashboardKpiCard title="검사 완료 프로젝트" value={summary.inspectionCompletedProjectCount} helperText="모든 패널 검사 완료" />
    </div>
  );
}

function ProjectBottleneckBadge({
  project,
  onOpenPending
}: {
  project: ProjectListItem | DeletedProjectListItem;
  onOpenPending: (projectId: string) => void;
}) {
  const bottleneck = project.bottleneck;
  if (!bottleneck) {
    return null;
  }

  return (
    <span className="project-bottleneck-inline" data-kind={bottleneck.kind}>
      <span className="project-bottleneck-label">병목 구간 · {bottleneck.label}</span>
      {bottleneck.openPendingCount !== undefined ? (
        <span className="project-bottleneck-counts">
          open {bottleneck.openPendingCount} · 재검사 {bottleneck.reinspectionPendingCount ?? 0} · 긴급 {bottleneck.urgentPendingCount ?? 0}
        </span>
      ) : null}
      {bottleneck.openPendingCount ? (
        <button
          type="button"
          className="bottleneck-pending-link"
          onClick={(event) => {
            event.stopPropagation();
            onOpenPending(project.projectId);
          }}
        >
          Pending 확인
        </button>
      ) : null}
    </span>
  );
}

function ProjectSelectionCheckbox({
  checked,
  indeterminate = false,
  disabled,
  label,
  onChange
}: {
  checked: boolean;
  indeterminate?: boolean;
  disabled: boolean;
  label: string;
  onChange: (checked: boolean) => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (inputRef.current) {
      inputRef.current.indeterminate = indeterminate;
    }
  }, [indeterminate]);

  return (
    <input
      ref={inputRef}
      type="checkbox"
      className="project-selection-checkbox"
      checked={checked}
      disabled={disabled}
      aria-label={label}
      onClick={(event) => event.stopPropagation()}
      onKeyDown={(event) => event.stopPropagation()}
      onChange={(event) => onChange(event.target.checked)}
    />
  );
}

function isInteractiveProjectRowTarget(target: EventTarget | null) {
  return target instanceof Element && target.closest('input, button, a, select, textarea, summary, [role="button"]') !== null;
}

function ProjectListDesktop({
  projects,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  developmentUserKey,
  onPurged,
  onOpen,
  onOpenPending,
  selectionEnabled,
  selectedProjectIds,
  selectionDisabled,
  onProjectSelectionChange
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  developmentUserKey: string;
  onPurged: () => void;
  onOpen: (projectId: string) => void;
  onOpenPending: (projectId: string) => void;
  selectionEnabled: boolean;
  selectedProjectIds: ReadonlySet<string>;
  selectionDisabled: boolean;
  onProjectSelectionChange: (projectId: string, selected: boolean) => void;
}) {
  return (
    <div className={selectionEnabled ? 'project-list-table project-list-desktop selectable' : 'project-list-table project-list-desktop'} role="table" aria-label="프로젝트 목록" data-testid="project-list-desktop">
      <div className="project-list-head" role="row">
        {selectionEnabled ? (
          <span className="project-selection-cell align-center" aria-hidden="true" />
        ) : null}
        <span className="align-left">프로젝트명</span>
        <span className="align-left">고객사</span>
        <span className="align-center">Code</span>
        <span className="align-left">Item</span>
        <span className="align-center">면수</span>
        <span className="align-center">납기일</span>
        <span className="align-center">상태</span>
        <span className="align-center">진행률</span>
      </div>
      {projects.map((project) => (
        <Fragment key={project.projectId}>
          <div
            className="project-list-row"
            role="row"
            tabIndex={0}
            onClick={(event) => {
              if (!isInteractiveProjectRowTarget(event.target)) {
                onOpen(project.projectId);
              }
            }}
            onKeyDown={(event) => {
              if (!isInteractiveProjectRowTarget(event.target) && (event.key === 'Enter' || event.key === ' ')) {
                event.preventDefault();
                onOpen(project.projectId);
              }
            }}
          >
            {selectionEnabled ? (
              <span className="project-selection-cell align-center">
                <ProjectSelectionCheckbox
                  checked={selectedProjectIds.has(project.projectId)}
                  disabled={selectionDisabled}
                  label={`${project.projectCode} ${project.projectTitle} 선택`}
                  onChange={(selected) => onProjectSelectionChange(project.projectId, selected)}
                />
              </span>
            ) : null}
            <span className="align-left">
              <strong>{project.projectTitle}</strong>
              {'deletedAtUtc' in project ? <small>삭제일시 {formatDateTime(project.deletedAtUtc)}</small> : null}
              {canReadSalesAmount && project.salesAmount !== undefined ? (
                <small><SalesAmountField amount={project.salesAmount} currencyCode={project.currencyCode} /></small>
              ) : null}
              <ProjectBottleneckBadge project={project} onOpenPending={onOpenPending} />
            </span>
            <span className="align-left">{project.customerName}</span>
            <span className="align-center">{project.projectCode}</span>
            <span className="align-left">{project.item}</span>
            <span className="align-center">{project.activePanelCount}면</span>
            <span className="align-center">{formatDate(project.deliveryDate)}</span>
            <span className="align-center">{formatProjectWorkStatus(project.projectWorkStatus)}</span>
            <span className="align-center">{formatProjectProgress(project.projectProgressPercent)}</span>
          </div>
          {canPurgeDeletedProjects && 'deletedAtUtc' in project ? (
            <div className="deleted-project-actions">
              <DeletedProjectRestoreControl projectId={project.projectId} developmentUserKey={developmentUserKey} onRestored={onPurged} />
              <DeletedProjectPurgeControl projectId={project.projectId} developmentUserKey={developmentUserKey} onPurged={onPurged} />
            </div>
          ) : null}
        </Fragment>
      ))}
    </div>
  );
}

function ProjectListMobile({
  projects,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  developmentUserKey,
  onPurged,
  onOpen,
  onOpenPending,
  selectionEnabled,
  selectedProjectIds,
  selectionDisabled,
  onProjectSelectionChange
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  developmentUserKey: string;
  onPurged: () => void;
  onOpen: (projectId: string) => void;
  onOpenPending: (projectId: string) => void;
  selectionEnabled: boolean;
  selectedProjectIds: ReadonlySet<string>;
  selectionDisabled: boolean;
  onProjectSelectionChange: (projectId: string, selected: boolean) => void;
}) {
  return (
    <div className="project-list-cards project-list-mobile" data-testid="project-list-mobile">
      {projects.map((project) => (
        <article key={project.projectId} className="project-list-card" data-testid="project-list-card">
          <div className="subsection-header">
            <div className="project-card-title-row">
              {selectionEnabled ? (
                <ProjectSelectionCheckbox
                  checked={selectedProjectIds.has(project.projectId)}
                  disabled={selectionDisabled}
                  label={`${project.projectCode} ${project.projectTitle} 선택`}
                  onChange={(selected) => onProjectSelectionChange(project.projectId, selected)}
                />
              ) : null}
              <h3>{project.projectTitle}</h3>
            </div>
            <button type="button" disabled={selectionDisabled} onClick={() => onOpen(project.projectId)}>상세 보기</button>
          </div>
          <dl className="mobile-detail-list">
            <div><dt>고객사</dt><dd>{project.customerName}</dd></div>
            <div><dt>Code</dt><dd>{project.projectCode}</dd></div>
            <div><dt>Item</dt><dd>{project.item}</dd></div>
            <div><dt>면수</dt><dd>{project.activePanelCount}면</dd></div>
            <div><dt>납기일</dt><dd>{formatDate(project.deliveryDate)}</dd></div>
            <div><dt>상태</dt><dd>{formatProjectWorkStatus(project.projectWorkStatus)}</dd></div>
            <div><dt>진행률</dt><dd>{formatProjectProgress(project.projectProgressPercent)}</dd></div>
            <div><dt>대표 병목</dt><dd>{project.bottleneck?.label ?? '-'}</dd></div>
            {project.bottleneck?.openPendingCount !== undefined ? <div><dt>Pending</dt><dd>open {project.bottleneck.openPendingCount}건 · 재검사 {project.bottleneck.reinspectionPendingCount ?? 0}건 · 긴급 {project.bottleneck.urgentPendingCount ?? 0}건</dd></div> : null}
            {'deletedAtUtc' in project ? <div><dt>삭제일시</dt><dd>{formatDateTime(project.deletedAtUtc)}</dd></div> : null}
            {canReadSalesAmount && project.salesAmount !== undefined ? (
              <div><dt>판매금액</dt><dd><SalesAmountField amount={project.salesAmount} currencyCode={project.currencyCode} /></dd></div>
            ) : null}
          </dl>
          {project.bottleneck?.openPendingCount ? <button type="button" className="bottleneck-pending-link" onClick={() => onOpenPending(project.projectId)}>open Pending 확인</button> : null}
          {canPurgeDeletedProjects && 'deletedAtUtc' in project ? (
            <div className="deleted-project-actions">
              <DeletedProjectRestoreControl projectId={project.projectId} developmentUserKey={developmentUserKey} onRestored={onPurged} />
              <DeletedProjectPurgeControl projectId={project.projectId} developmentUserKey={developmentUserKey} onPurged={onPurged} />
            </div>
          ) : null}
        </article>
      ))}
    </div>
  );
}

function DeletedProjectRestoreControl({
  projectId,
  developmentUserKey,
  onRestored
}: {
  projectId: string;
  developmentUserKey: string;
  onRestored: () => void;
}) {
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [isRestoring, setIsRestoring] = useState(false);

  async function restore() {
    setMessage('');
    setIsRestoring(true);
    try {
      await restoreDeletedProject(developmentUserKey, projectId, reason.trim() || null);
      setReason('');
      setMessage('프로젝트를 복구했습니다.');
      onRestored();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsRestoring(false);
    }
  }

  return (
    <div className="restore-inline-control">
      <label className="form-field compact-field">
        <span>복구 사유</span>
        <input value={reason} onChange={(event) => setReason(event.target.value)} placeholder="선택 입력" />
      </label>
      <button type="button" className="primary-button" disabled={isRestoring} onClick={restore}>
        {isRestoring ? '복구 중' : '복구'}
      </button>
      {message ? <span role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</span> : null}
    </div>
  );
}

function DeletedProjectPurgeControl({
  projectId,
  developmentUserKey,
  onPurged
}: {
  projectId: string;
  developmentUserKey: string;
  onPurged: () => void;
}) {
  const [confirmText, setConfirmText] = useState('');
  const [message, setMessage] = useState('');
  const [isPurging, setIsPurging] = useState(false);

  async function purge() {
    setMessage('');
    if (confirmText !== '완전 삭제') {
      setMessage('확인 문구를 정확히 입력해 주세요.');
      return;
    }

    setIsPurging(true);
    try {
      await purgeDeletedProject(developmentUserKey, projectId, confirmText);
      setMessage('프로젝트를 완전히 삭제했습니다.');
      onPurged();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPurging(false);
    }
  }

  return (
    <div className="purge-inline-control">
      <label className="form-field compact-field">
        <span>확인 문구: 완전 삭제</span>
        <input value={confirmText} onChange={(event) => setConfirmText(event.target.value)} />
      </label>
      <button type="button" className="danger-button" disabled={isPurging || confirmText !== '완전 삭제'} onClick={purge}>
        {isPurging ? '삭제 중' : '완전 삭제'}
      </button>
      {message ? <span role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</span> : null}
    </div>
  );
}

function ProjectCreatePage({
  developmentUserKey,
  onCancel,
  onCreated
}: {
  developmentUserKey: string;
  onCancel: () => void;
  onCreated: (projectId: string) => void;
}) {
  const [owners, setOwners] = useState<SalesOwner[]>([]);
  const [productTypes, setProductTypes] = useState<ProductionProductType[]>([]);
  const [form, setForm] = useState<ProjectFormValues>(emptyForm);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    Promise.all([
      getSalesOwners(developmentUserKey),
      listProductionProductTypes(developmentUserKey)
    ])
      .then(([ownerItems, typeItems]) => {
        setOwners(ownerItems);
        setProductTypes(typeItems);
        setForm((current) => ({ ...current, salesOwnerUserId: current.salesOwnerUserId || ownerItems[0]?.userId || '' }));
      })
      .catch((error: unknown) => setMessage(friendlyErrorMessage(error, '프로젝트 입력 기준 정보를 불러올 수 없습니다.')));
  }, [developmentUserKey]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const validation = validateProjectForm(form, false, productTypes);
    setErrors(validation);
    setMessage('');
    if (Object.keys(validation).length > 0) {
      return;
    }

    setIsSaving(true);
    try {
      const project = await createProject(developmentUserKey, toCreateRequest(form));
      onCreated(project.projectId);
    } catch (error) {
      handleFormError(error, setErrors, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Sales</p>
          <h2>프로젝트 등록</h2>
        </div>
        <button type="button" onClick={onCancel}>목록</button>
      </div>
      <ProjectForm
        form={form}
        owners={owners}
        productTypes={productTypes}
        errors={errors}
        isSaving={isSaving}
        submitLabel="등록"
        onChange={setForm}
        onSubmit={submit}
      />
      {message ? <p role="alert" className="error-text">{message}</p> : null}
    </section>
  );
}

function ProjectDetailPage({
  developmentUserKey,
  projectId,
  canUpdate,
  canHold,
  canCancel,
  canDelete,
  canReadSalesAmount,
  canUpdatePanelInfo,
  canReadAuditAll,
  canUpdateProcurement,
  canUpdateProductionPlanning,
  canUpdateMaterialReceipt,
  canUpdateManufacturing,
  canInspectQuality,
  canShipLogistics,
  isSystemAdministrator,
  initialSection,
  onBack,
  onEdit,
  onEditPanelInformation,
  onEditProductionPlanning,
  onEditProcurement,
  onOpenPanel,
  onOpenPending,
  onOpenSettlement,
  onOpenDepartmentWorkspace,
  onLoadOutcome,
  canSettleSales
}: {
  developmentUserKey: string;
  projectId: string;
  canUpdate: boolean;
  canHold: boolean;
  canCancel: boolean;
  canDelete: boolean;
  canReadSalesAmount: boolean;
  canUpdatePanelInfo: boolean;
  canReadAuditAll: boolean;
  canUpdateProcurement: boolean;
  canUpdateProductionPlanning: boolean;
  canUpdateMaterialReceipt: boolean;
  canUpdateManufacturing: boolean;
  canInspectQuality: boolean;
  canShipLogistics: boolean;
  isSystemAdministrator: boolean;
  initialSection: ProjectDetailSection;
  onBack: () => void;
  onEdit: () => void;
  onEditPanelInformation: () => void;
  onEditProductionPlanning: () => void;
  onEditProcurement: () => void;
  onOpenPanel: (panelId: string) => void;
  onOpenPending: () => void;
  onOpenSettlement: () => void;
  onOpenDepartmentWorkspace: (section: ProjectDepartmentSection, projectCode?: string) => void;
  onLoadOutcome?: (loaded: boolean) => void;
  canSettleSales: boolean;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [panelInfoState, setPanelInfoState] = useState<LoadState<PanelInformationResponse>>({ kind: 'loading' });
  const [productionPlanningState, setProductionPlanningState] = useState<LoadState<ProductionPlanningResponse>>({ kind: 'loading' });
  const [procurementState, setProcurementState] = useState<LoadState<ProcurementResponse>>({ kind: 'loading' });
  const [workflowState, setWorkflowState] = useState<LoadState<ProjectWorkflowResponse>>({ kind: 'loading' });
  const [departmentDataState, setDepartmentDataState] = useState<LoadState<ProjectDepartmentData>>({ kind: 'empty' });
  const [historyState, setHistoryState] = useState<LoadState<PanelInformationHistoryResponse>>({ kind: 'empty' });
  const [productionPlanningHistoryState, setProductionPlanningHistoryState] = useState<LoadState<ProductionPlanningHistoryResponse>>({ kind: 'empty' });
  const [procurementHistoryState, setProcurementHistoryState] = useState<LoadState<ProcurementHistoryResponse>>({ kind: 'empty' });
  const [activeDetailSection, setActiveDetailSection] = useState<ProjectDetailSection>(initialSection);
  const [dialog, setDialog] = useState<null | 'hold' | 'resume' | 'cancel' | 'reactivate' | 'delete'>(null);
  const [reason, setReason] = useState('');
  const [confirmProjectTitle, setConfirmProjectTitle] = useState('');
  const [dialogError, setDialogError] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const isMobile = useIsMobileViewport();
  const loadOutcomeRef = useRef(onLoadOutcome);
  const departmentLoadGenerationRef = useRef(0);

  useEffect(() => {
    loadOutcomeRef.current = onLoadOutcome;
  }, [onLoadOutcome]);

  const load = useCallback(() => {
    setProjectState({ kind: 'loading' });
    setPanelInfoState({ kind: 'loading' });
    setProductionPlanningState({ kind: 'loading' });
    setProcurementState({ kind: 'loading' });
    setWorkflowState({ kind: 'loading' });
    setHistoryState(canReadAuditAll ? { kind: 'loading' } : { kind: 'empty' });
    setProductionPlanningHistoryState(canReadAuditAll ? { kind: 'loading' } : { kind: 'empty' });
    setProcurementHistoryState(canReadAuditAll ? { kind: 'loading' } : { kind: 'empty' });

    Promise.all([
      getProject(developmentUserKey, projectId),
      getPanelInformation(developmentUserKey, projectId),
      getProjectProductionPlanning(developmentUserKey, projectId).catch(() => null),
      getProjectProcurement(developmentUserKey, projectId).catch(() => null),
      getProjectWorkflow(developmentUserKey, projectId).catch(() => null),
      canReadAuditAll ? getPanelInformationHistory(developmentUserKey, projectId) : Promise.resolve(null),
      canReadAuditAll ? getProjectProductionPlanningHistory(developmentUserKey, projectId) : Promise.resolve(null),
      canReadAuditAll ? getProjectProcurementHistory(developmentUserKey, projectId) : Promise.resolve(null)
    ])
      .then(([project, panelInfo, productionPlanning, procurement, workflow, history, productionPlanningHistory, procurementHistory]) => {
        setProjectState({ kind: 'ready', data: project });
        setPanelInfoState({ kind: 'ready', data: panelInfo });
        setProductionPlanningState(productionPlanning ? { kind: 'ready', data: productionPlanning } : { kind: 'empty' });
        setProcurementState(procurement ? { kind: 'ready', data: procurement } : { kind: 'empty' });
        setWorkflowState(workflow ? { kind: 'ready', data: workflow } : { kind: 'empty' });
        setHistoryState(history ? { kind: 'ready', data: history } : { kind: 'empty' });
        setProductionPlanningHistoryState(productionPlanningHistory ? { kind: 'ready', data: productionPlanningHistory } : { kind: 'empty' });
        setProcurementHistoryState(procurementHistory ? { kind: 'ready', data: procurementHistory } : { kind: 'empty' });
        const selectedSectionLoaded = initialSection === 'production-planning'
          ? productionPlanning !== null
          : initialSection === 'procurement'
            ? procurement !== null
            : true;
        loadOutcomeRef.current?.(selectedSectionLoaded);
      })
      .catch((error: unknown) => {
        const state = toLoadError<ProjectDetail>(error, '프로젝트 상세를 불러올 수 없습니다.');
        setProjectState(state);
        setPanelInfoState(toLoadError(error, '설계 정보를 불러올 수 없습니다.'));
        setProductionPlanningState(toLoadError(error, '생산계획을 불러올 수 없습니다.'));
        setProcurementState(toLoadError(error, '구매정보를 불러올 수 없습니다.'));
        setWorkflowState(toLoadError(error, 'workflow 요약을 불러올 수 없습니다.'));
        setHistoryState(toLoadError(error, '전체 이력을 불러올 수 없습니다.'));
        setProductionPlanningHistoryState(toLoadError(error, '전체 이력을 불러올 수 없습니다.'));
        setProcurementHistoryState(toLoadError(error, '전체 이력을 불러올 수 없습니다.'));
        loadOutcomeRef.current?.(false);
      });
  }, [canReadAuditAll, developmentUserKey, initialSection, projectId]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  useEffect(() => {
    setActiveDetailSection(initialSection);
  }, [initialSection, projectId]);

  useEffect(() => {
    const section = activeDetailSection;
    if (section !== 'sales' && section !== 'materials' && section !== 'manufacturing' && section !== 'quality' && section !== 'logistics') {
      return;
    }
    if (projectState.kind !== 'ready') {
      setDepartmentDataState({ kind: 'loading' });
      return;
    }

    const generation = ++departmentLoadGenerationRef.current;
    setDepartmentDataState({ kind: 'loading' });
    loadProjectDepartmentData({
      developmentUserKey,
      project: projectState.data,
      section,
      permissions: {
        sales: canSettleSales,
        materials: canUpdateMaterialReceipt,
        manufacturing: canUpdateManufacturing,
        quality: canInspectQuality,
        logistics: canShipLogistics
      }
    })
      .then((data) => {
        if (generation === departmentLoadGenerationRef.current) {
          setDepartmentDataState({ kind: 'ready', data });
        }
      })
      .catch((error: unknown) => {
        if (generation === departmentLoadGenerationRef.current) {
          setDepartmentDataState(toLoadError(error, '부서 데이터를 불러올 수 없습니다.'));
        }
      });
  }, [activeDetailSection, canInspectQuality, canSettleSales, canShipLogistics, canUpdateManufacturing, canUpdateMaterialReceipt, developmentUserKey, projectState]);

  function selectDetailSection(section: ProjectDetailSection) {
    setActiveDetailSection(section);
    if (typeof window !== 'undefined') {
      const nextPath = `/projects/${projectId}${section === 'panels' ? '' : `?section=${section}`}`;
      if (`${window.location.pathname}${window.location.search}` !== nextPath) {
        window.history.replaceState(null, '', nextPath);
      }
    }
  }

  async function submitStatusChange() {
    if (!dialog || dialog === 'delete') {
      return;
    }

    if (!reason.trim()) {
      setDialogError('사유는 필수입니다.');
      return;
    }

    setIsSaving(true);
    setDialogError('');
    try {
      await changeProjectStatus(developmentUserKey, projectId, dialog, { reason });
      setDialog(null);
      setReason('');
      load();
    } catch (error) {
      setDialogError(error instanceof Error ? error.message : '상태 변경에 실패했습니다.');
    } finally {
      setIsSaving(false);
    }
  }

  async function submitDelete() {
    if (!reason.trim()) {
      setDialogError('삭제 사유는 필수입니다.');
      return;
    }

    if (!confirmProjectTitle.trim()) {
      setDialogError('PJT Title 확인 입력은 필수입니다.');
      return;
    }

    setIsSaving(true);
    setDialogError('');
    try {
      await deleteProject(developmentUserKey, projectId, {
        reason,
        confirmProjectTitle
      });
      setDialog(null);
      onBack();
    } catch (error) {
      setDialogError(error instanceof Error ? error.message : '삭제에 실패했습니다.');
    } finally {
      setIsSaving(false);
    }
  }

  if (projectState.kind === 'loading') {
    return <section className="page-surface"><p className="muted-text">Loading</p></section>;
  }

  if (projectState.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={projectState} /></section>;
  }

  const project = projectState.data;
  const canShowEdit = canUpdate;
  const isOnHold = project.status === 'OnHold';
  const isCancelled = project.status === 'Cancelled';
  const projectActions = (
    <>
      {canShowEdit ? <button type="button" onClick={onEdit}>수정</button> : null}
      {canHold && project.status === 'Active' ? <button type="button" onClick={() => setDialog('hold')}>보류</button> : null}
      {canUpdate && isOnHold ? <button type="button" onClick={() => setDialog('resume')}>보류 해제</button> : null}
      {canCancel && (project.status === 'Active' || isOnHold) ? <button type="button" onClick={() => setDialog('cancel')}>취소</button> : null}
      {canUpdate && isCancelled ? <button type="button" onClick={() => setDialog('reactivate')}>재활성</button> : null}
      {project.status === 'Active' || project.status === 'Completed' ? <button type="button" className={canSettleSales && project.status === 'Active' ? 'primary-button' : undefined} onClick={onOpenSettlement}>{project.status === 'Completed' ? '완료 내역' : '정산·완료'}</button> : null}
      {canDelete && project.status !== 'Completed' ? <button type="button" className="danger-button" onClick={() => setDialog('delete')}>삭제</button> : null}
    </>
  );
  const detailTabs: Array<{ section: ProjectDetailSection; label: string }> = [
    { section: 'workflow', label: '전체 흐름' },
    { section: 'sales', label: '영업' },
    { section: 'production-planning', label: '생산관리' },
    { section: 'panels', label: '설계' },
    { section: 'procurement', label: '구매' },
    { section: 'materials', label: '자재' },
    { section: 'manufacturing', label: '제조' },
    { section: 'quality', label: '품질' },
    { section: 'logistics', label: '물류' }
  ];

  return (
    <section className={isMobile ? 'page-surface mobile-first-page mobile-project-detail-page' : 'page-surface'}>
      <div className={isMobile ? 'mobile-detail-hero' : 'page-header'}>
        <div>
          {isMobile ? <button type="button" className="mobile-back-button" onClick={onBack}>← 프로젝트</button> : null}
          <p className="eyebrow">{isMobile ? project.projectCode : 'Project Detail'}</p>
          <h2>{project.projectTitle}</h2>
          {isMobile ? <div className="mobile-detail-hero-meta"><ProjectStatusBadge status={project.status} /><span>{formatProjectProgress(project.projectProgressPercent)} 진행</span></div> : null}
        </div>
        {isMobile ? (
          <details className="mobile-secondary-actions mobile-project-actions">
            <summary>프로젝트 작업</summary>
            {projectActions}
          </details>
        ) : (
          <div className="button-row">
            <button type="button" onClick={onBack}>목록</button>
            {projectActions}
          </div>
        )}
      </div>

      {isMobile ? (
        <>
          <ProjectBottleneckOverview
            project={project}
            onOpenPending={onOpenPending}
            onOpenPanels={() => selectDetailSection('panels')}
            onOpenWorkflow={() => selectDetailSection('workflow')}
          />
          <ProjectSummary project={project} canReadSalesAmount={canReadSalesAmount} />
        </>
      ) : (
        <>
          <ProjectSummary project={project} canReadSalesAmount={canReadSalesAmount} />
          <ProjectBottleneckOverview
            project={project}
            onOpenPending={onOpenPending}
            onOpenPanels={() => selectDetailSection('panels')}
            onOpenWorkflow={() => selectDetailSection('workflow')}
          />
        </>
      )}

      <div className="section-switcher project-department-tabs" role="tablist" aria-label="프로젝트 상세 섹션">
        {detailTabs.map((tab) => (
          <button
            type="button"
            role="tab"
            key={tab.section}
            aria-selected={activeDetailSection === tab.section}
            className={activeDetailSection === tab.section ? 'secondary-button active' : 'secondary-button'}
            onClick={() => selectDetailSection(tab.section)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeDetailSection === 'workflow' ? (
        <ProjectWorkflowSummary state={workflowState} />
      ) : null}

      {activeDetailSection === 'sales' || activeDetailSection === 'materials' || activeDetailSection === 'manufacturing'
        || activeDetailSection === 'quality' || activeDetailSection === 'logistics' ? (
          <ProjectDepartmentDataSection
            section={activeDetailSection}
            state={departmentDataState}
            onOpen={() => onOpenDepartmentWorkspace(activeDetailSection, project.projectCode)}
          />
        ) : null}

      {activeDetailSection === 'panels' ? (
        <PanelInformationSection
          developmentUserKey={developmentUserKey}
          project={project}
          state={panelInfoState}
          canUpdatePanelInfo={canUpdatePanelInfo}
          onEdit={onEditPanelInformation}
          onOpenPanel={onOpenPanel}
          isSystemAdministrator={isSystemAdministrator}
        />
      ) : null}

      {activeDetailSection === 'procurement' ? (
        <ProcurementSection
          state={procurementState}
          canUpdateProcurement={canUpdateProcurement && project.status === 'Active'}
          onEdit={onEditProcurement}
        />
      ) : null}

      {activeDetailSection === 'production-planning' ? (
        <ProductionPlanningSection
          developmentUserKey={developmentUserKey}
          state={productionPlanningState}
          canUpdateProductionPlanning={canUpdateProductionPlanning && project.status === 'Active'}
          onEdit={onEditProductionPlanning}
        />
      ) : null}

      {canReadAuditAll ? (
        <section className="subsection">
          <h3>전체 이력</h3>
          {historyState.kind === 'loading' || productionPlanningHistoryState.kind === 'loading' || procurementHistoryState.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
          {historyState.kind !== 'ready' && historyState.kind !== 'loading' && historyState.kind !== 'empty' ? <StateMessage state={historyState} /> : null}
          {productionPlanningHistoryState.kind !== 'ready' && productionPlanningHistoryState.kind !== 'loading' && productionPlanningHistoryState.kind !== 'empty' ? <StateMessage state={productionPlanningHistoryState} /> : null}
          {procurementHistoryState.kind !== 'ready' && procurementHistoryState.kind !== 'loading' && procurementHistoryState.kind !== 'empty' ? <StateMessage state={procurementHistoryState} /> : null}
          {historyState.kind === 'ready' || productionPlanningHistoryState.kind === 'ready' || procurementHistoryState.kind === 'ready' ? (
            <div className="history-stack">
              {historyState.kind === 'ready' ? <GroupedHistory groups={historyState.data.groups} emptyText={productionPlanningHistoryState.kind === 'ready' && productionPlanningHistoryState.data.groups.length > 0 || procurementHistoryState.kind === 'ready' && procurementHistoryState.data.groups.length > 0 ? null : '전체 이력이 없습니다.'} /> : null}
              {productionPlanningHistoryState.kind === 'ready' ? <ProductionPlanningGroupedHistory groups={productionPlanningHistoryState.data.groups} /> : null}
              {procurementHistoryState.kind === 'ready' ? <ProcurementGroupedHistory groups={procurementHistoryState.data.groups} /> : null}
            </div>
          ) : null}
        </section>
      ) : null}

      {dialog && dialog !== 'delete' ? (
        <StatusReasonDialog
          action={dialog}
          reason={reason}
          error={dialogError}
          isSaving={isSaving}
          onReasonChange={setReason}
          onCancel={() => {
            setDialog(null);
            setReason('');
            setConfirmProjectTitle('');
            setDialogError('');
          }}
          onSubmit={submitStatusChange}
        />
      ) : null}
      {dialog === 'delete' ? (
        <DeleteProjectDialog
          projectTitle={project.projectTitle}
          reason={reason}
          confirmProjectTitle={confirmProjectTitle}
          error={dialogError}
          isSaving={isSaving}
          onReasonChange={setReason}
          onConfirmProjectTitleChange={setConfirmProjectTitle}
          onCancel={() => {
            setDialog(null);
            setReason('');
            setConfirmProjectTitle('');
            setDialogError('');
          }}
          onSubmit={submitDelete}
        />
      ) : null}
    </section>
  );
}

async function loadProjectDepartmentData({
  developmentUserKey,
  project,
  section,
  permissions
}: {
  developmentUserKey: string;
  project: ProjectDetail;
  section: ProjectDepartmentSection;
  permissions: Record<ProjectDepartmentSection, boolean>;
}): Promise<ProjectDepartmentData> {
  if (section === 'sales') {
    const settlement = await getSalesSettlement(developmentUserKey, project.projectId);
    return {
      canMutate: permissions.sales && settlement.canMutate,
      metrics: [
        { label: '납품 패널', value: `${settlement.deliveredPanelCount}/${settlement.activePanelCount}` },
        { label: '미해결 Pending', value: `${settlement.openPendingCount}건`, tone: settlement.openPendingCount > 0 ? 'danger' : 'success' },
        { label: '정산 상태', value: salesSettlementStatusLabel(settlement.settlementStatus), tone: settlement.settlementStatus === 'Completed' ? 'success' : 'info' },
        { label: '발행 요청', value: settlement.billingRequestStatus.accountingIssueConfirmed ? '회계 확인' : settlement.billingRequestStatus.requested ? '요청 완료' : '미요청', tone: settlement.billingRequestStatus.requested ? 'success' : 'neutral' }
      ],
      records: [
        {
          key: 'sales-project-input',
          title: '프로젝트 영업 입력',
          subtitle: '프로젝트 생성·수정 화면에서 저장된 값',
          status: project.status === 'Completed' ? '완료' : '진행 중',
          tone: project.status === 'Completed' ? 'success' : 'info',
          fields: [
            { label: '고객사', value: project.customerName },
            { label: 'Item', value: project.item },
            { label: 'PJT Code', value: project.projectCode },
            { label: 'PJT Title', value: project.projectTitle },
            { label: '면수', value: `${project.activePanelCount}` },
            { label: '납기일', value: formatDepartmentDate(project.deliveryDate) },
            { label: '영업담당자', value: project.salesOwnerName },
            { label: '포장방식', value: packagingMethodLabel(project.packagingMethod) },
            { label: '판매금액', value: project.salesAmount === undefined ? '권한에 따라 비공개' : formatDepartmentAmount(project.salesAmount, project.currencyCode) },
            { label: '납품장소', value: displayDepartmentValue(project.deliveryLocation) },
            { label: 'FAT 필요', value: yesNo(project.fatRequired) },
            { label: '등록 시각', value: formatDepartmentDateTime(project.createdAt) },
            { label: '최종 수정', value: formatDepartmentDateTime(project.updatedAt) }
          ]
        },
        {
          key: 'sales-settlement-input',
          title: '정산·회계 발행 확인',
          subtitle: '납품 후 영업담당자가 저장한 최종 정산 값',
          status: salesSettlementStatusLabel(settlement.settlementStatus),
          tone: settlement.settlementStatus === 'Completed' ? 'success' : 'info',
          fields: [
            { label: '납품 패널', value: `${settlement.deliveredPanelCount}/${settlement.activePanelCount}` },
            { label: '미해결 Pending', value: `${settlement.openPendingCount}건` },
            { label: '발행 요청', value: settlement.billingRequestStatus.requested ? '요청 완료' : '미요청' },
            { label: '발행 요청 번호', value: displayDepartmentValue(settlement.billingRequestStatus.requestNumber) },
            { label: '발행 요청 시각', value: formatDepartmentDateTime(settlement.billingRequestStatus.requestedAtUtc) },
            { label: '회계 발행 확인', value: yesNo(settlement.billingRequestStatus.accountingIssueConfirmed) },
            { label: '회계 발행 확인일', value: formatDepartmentDate(settlement.invoiceIssuedDate) },
            { label: '세금계산서 번호', value: displayDepartmentValue(settlement.invoiceNumber) },
            { label: '회계 확인 메모', value: displayDepartmentValue(settlement.note) },
            { label: '완료 담당', value: displayDepartmentValue(settlement.completedByName) },
            { label: '완료 시각', value: formatDepartmentDateTime(settlement.completedAtUtc) },
            { label: '저장 버전', value: `v${settlement.version}` }
          ]
        }
      ]
    };
  }

  if (section === 'materials') {
    const [receipts, kittingQueue] = await Promise.all([
      getMaterialReceipts(developmentUserKey, project.projectCode, true),
      getPanelKittingQueue(developmentUserKey, project.projectId)
    ]);
    const items = receipts.items.filter((item) => item.projectId === project.projectId);
    const kitting = kittingQueue.projects.find((item) => item.projectId === project.projectId);
    const materialRecords: ProjectDepartmentRecord[] = items.flatMap((item) => {
      const blocked = item.receipts.some((receipt) => receipt.status === 'FailedBlocked');
      const waitingIqc = item.receipts.some((receipt) => receipt.status === 'IqcRequested');
      const status = item.receiptCompleted ? '입고 완료' : blocked ? '부적합 차단' : waitingIqc ? 'IQC 대기' : item.arrivedQuantity ? '입고 진행' : '도착 대기';
      const itemRecord: ProjectDepartmentRecord = {
        key: `material-item:${item.itemId}`,
        title: item.orderItem ?? '구매 품목',
        subtitle: '자재 입고 항목',
        status,
        tone: item.receiptCompleted ? 'success' : blocked ? 'danger' : 'warning',
        fields: [
          { label: '공급 구분', value: materialSupplyTypeLabel(item.supplyType) },
          { label: '공급처', value: displayDepartmentValue(item.supplierName) },
          { label: '입고 예정일', value: formatDepartmentDate(item.expectedReceiptDate) },
          { label: '발주 수량', value: formatDepartmentQuantity(item.orderQuantity, item.orderUnit) },
          { label: '도착 수량', value: formatDepartmentQuantity(item.arrivedQuantity, item.orderUnit) },
          { label: '확정 수량', value: formatDepartmentQuantity(item.confirmedQuantity, item.orderUnit) },
          { label: '처리 중 수량', value: formatDepartmentQuantity(item.processingQuantity, item.orderUnit) },
          { label: '잔여 수량', value: formatDepartmentQuantity(item.remainingQuantity, item.orderUnit) },
          { label: '도착 마감', value: yesNo(item.arrivalsClosed) },
          { label: '마감 시각', value: formatDepartmentDateTime(item.arrivalsClosedAtUtc) },
          { label: '입고 완료', value: yesNo(item.receiptCompleted) },
          { label: '사급 지연', value: yesNo(item.customerSupplyOverdue) }
        ]
      };
      const receiptRecords = item.receipts.map((receipt, index): ProjectDepartmentRecord => ({
        key: `material-receipt:${receipt.receiptId}`,
        title: `${item.orderItem ?? '구매 품목'} · 입고 ${index + 1}회차`,
        subtitle: '도착·IQC·입고 확정 입력',
        status: materialReceiptStatusLabel(receipt.status),
        tone: receipt.status === 'Confirmed' ? 'success' : receipt.status === 'FailedBlocked' ? 'danger' : 'info',
        fields: [
          { label: '입고 수량', value: formatDepartmentQuantity(receipt.quantity, receipt.unit) },
          { label: '도착일', value: formatDepartmentDate(receipt.arrivalDate) },
          { label: '입고 메모', value: displayDepartmentValue(receipt.note) },
          { label: 'Legacy 기록', value: yesNo(receipt.isLegacy) },
          { label: '등록 시각', value: formatDepartmentDateTime(receipt.createdAtUtc) },
          { label: '확정 시각', value: formatDepartmentDateTime(receipt.confirmedAtUtc) },
          { label: '취소 사유', value: displayDepartmentValue(receipt.cancellationReason) },
          { label: '저장 버전', value: `v${receipt.version}` }
        ],
        items: receipt.iqcAttempts.map((attempt) => ({
          key: attempt.attemptId,
          label: `IQC ${attempt.attemptNumber}차 · ${attempt.status}`,
          value: `${attempt.decisionMode === 'Detailed' ? '디지털 성적서' : '기존 판정'} · 요청 ${formatDepartmentDateTime(attempt.requestedAtUtc)} · 판정 ${formatDepartmentDateTime(attempt.decidedAtUtc)}`,
          note: [attempt.reason, attempt.reportStatus ? `성적서 ${attempt.reportStatus}` : null, attempt.pdfStatus ? `PDF ${attempt.pdfStatus}` : null, attempt.pendingIssueId ? '연결 Pending 있음' : null].filter(Boolean).join(' · ') || undefined
        }))
      }));
      return [itemRecord, ...receiptRecords];
    });
    const kittingRecords: ProjectDepartmentRecord[] = (kitting?.panels ?? []).map((panel) => ({
      key: `kitting:${panel.panelId}`,
      title: `${panel.displayCode} 키팅`,
      subtitle: panel.panelName ?? '패널명 미입력',
      status: panel.kittingCompleted ? '키팅 완료' : panel.panelInfoCompleted ? '키팅 대기' : '설계정보 대기',
      tone: panel.kittingCompleted ? 'success' : 'warning',
      fields: [
        { label: '설계정보 완료', value: yesNo(panel.panelInfoCompleted) },
        { label: '키팅 완료', value: yesNo(panel.kittingCompleted) },
        { label: '완료 담당', value: displayDepartmentValue(panel.completedByDisplayName) },
        { label: '완료 시각', value: formatDepartmentDateTime(panel.completedAtUtc) }
      ]
    }));
    return {
      canMutate: permissions.materials,
      metrics: [
        { label: '발주 품목', value: `${items.length}건` },
        { label: '입고 완료', value: `${items.filter((item) => item.receiptCompleted).length}건`, tone: 'success' },
        { label: '입고 진행', value: `${items.filter((item) => !item.receiptCompleted).length}건`, tone: 'warning' },
        { label: '키팅 완료', value: `${kitting?.completedPanelCount ?? 0}/${kitting?.panels.length ?? 0}` }
      ],
      records: [...materialRecords, ...kittingRecords]
    };
  }

  if (section === 'manufacturing') {
    const queue = await getManufacturingQueue(developmentUserKey, project.projectId);
    const data = queue.projects.find((item) => item.projectId === project.projectId);
    const panels = data?.panels ?? [];
    const executions = await Promise.all(panels.map(async (panel) => {
      try {
        return await getManufacturingPanel(developmentUserKey, panel.panelId);
      } catch {
        return { panel, steps: [], events: [] };
      }
    }));
    return {
      canMutate: permissions.manufacturing && panels.some((panel) => panel.canMutate),
      metrics: [
        { label: '착수 대기', value: `${data?.readyCount ?? 0}대` },
        { label: '제조 중', value: `${data?.inProgressCount ?? 0}대`, tone: 'info' },
        { label: '중단', value: `${data?.blockedCount ?? 0}대`, tone: (data?.blockedCount ?? 0) > 0 ? 'danger' : 'neutral' },
        { label: '완료', value: `${data?.completedCount ?? 0}대`, tone: 'success' }
      ],
      records: executions.map((execution) => {
        const panel = execution.panel;
        return {
          key: panel.panelId,
          title: `${panel.displayCode} ${panel.panelName ?? ''}`.trim(),
          subtitle: '패널 제조 실행 기록',
          status: manufacturingStatusLabel(panel.status),
          tone: panel.status === 'Completed' ? 'success' : panel.status === 'Blocked' ? 'danger' : panel.status === 'InProgress' ? 'info' : 'neutral',
          fields: [
            { label: 'Workflow 단계', value: panel.workflowStage },
            { label: '내 업무 상태', value: panel.workItemStatus },
            { label: '제조 상태', value: manufacturingStatusLabel(panel.status) },
            { label: '작업 체크', value: `${panel.checkedStepCount}/${panel.totalStepCount}` },
            { label: '시작 시각', value: formatDepartmentDateTime(panel.startedAtUtc) },
            { label: '완료 시각', value: formatDepartmentDateTime(panel.completedAtUtc) },
            { label: '활성 Pending', value: panel.activePendingNumber ? `#${panel.activePendingNumber}` : '-' },
            { label: '조치 부서', value: displayDepartmentValue(panel.actionDepartmentCode) },
            { label: '저장 버전', value: `v${panel.version}` }
          ],
          items: [
            ...execution.steps.map((step) => ({
              key: `step:${step.stepId}`,
              label: `${step.sequenceNumber}. ${step.stepName}`,
              value: step.checked ? `완료 · ${displayDepartmentValue(step.checkedByDisplayName)} · ${formatDepartmentDateTime(step.checkedAtUtc)}` : '미완료'
            })),
            ...execution.events.map((event) => ({
              key: `event:${event.eventId}`,
              label: `이력 · ${event.eventLabel}`,
              value: `${event.actorDisplayName} · ${formatDepartmentDateTime(event.createdAtUtc)}`,
              note: [event.stopReasonCode, event.stopDescription, event.pendingId ? '연결 Pending 있음' : null].filter(Boolean).join(' · ') || undefined
            }))
          ]
        } satisfies ProjectDepartmentRecord;
      })
    };
  }

  if (section === 'quality') {
    const stages: QualityInspectionStage[] = ['LQC', 'OQC', 'CustomerInspection', 'FAT'];
    const queues = await Promise.all(stages.map((stage) => getQualityInspectionQueue(developmentUserKey, stage, project.projectId)));
    const stageProjects = queues.map((queue, index) => ({ stage: stages[index], project: queue.projects.find((item) => item.projectId === project.projectId) }));
    const panels = stageProjects.flatMap(({ stage, project: stageProject }) => (stageProject?.panels ?? []).map((panel) => ({ stage, panel })));
    const inspections = await Promise.all(panels.map(async ({ stage, panel }) => {
      try {
        return { stage, detail: await getQualityInspectionPanel(developmentUserKey, panel.panelId, stage) };
      } catch {
        return {
          stage,
          detail: {
            panel,
            reportId: null,
            reportStatus: null,
            reportVersion: null,
            result: null,
            reason: null,
            pdfStatus: null,
            items: [],
            responses: [],
            photos: [],
            history: []
          }
        };
      }
    }));
    return {
      canMutate: permissions.quality && panels.some(({ panel }) => panel.canMutate),
      metrics: stageProjects.map(({ stage, project: stageProject }) => ({
        label: qualityStageLabel(stage),
        value: `${stageProject?.completedCount ?? 0}/${stageProject?.panels.length ?? 0}`,
        tone: stageProject && stageProject.panels.length > 0 && stageProject.completedCount === stageProject.panels.length ? 'success' : 'neutral'
      })),
      records: inspections.map(({ stage, detail }) => {
        const panel = detail.panel;
        const responseByItem = new Map(detail.responses.map((response) => [response.templateItemId, response]));
        return {
          key: `${stage}:${panel.panelId}`,
          title: `${qualityStageLabel(stage)} · ${panel.displayCode}`,
          subtitle: panel.panelName ?? '패널명 미입력',
          status: qualityStatusLabel(panel.status),
          tone: panel.status === 'Completed' || panel.status === 'Passed' || panel.status === 'Confirmed' ? 'success' : panel.status === 'Failed' ? 'danger' : 'info',
          fields: [
            { label: '검사 차수', value: panel.attemptNumber > 0 ? `${panel.attemptNumber}차` : '검사 대기' },
            { label: '검사 상태', value: qualityStatusLabel(panel.status) },
            { label: '성적서 상태', value: displayDepartmentValue(detail.reportStatus) },
            { label: '최종 판정', value: qualityResultLabel(detail.result) },
            { label: '판정 사유', value: displayDepartmentValue(detail.reason) },
            { label: 'PDF 상태', value: displayDepartmentValue(detail.pdfStatus) },
            { label: '연결 Pending', value: panel.pendingNumber ? `#${panel.pendingNumber}` : '-' },
            { label: '조치 부서', value: displayDepartmentValue(panel.actionDepartmentCode) },
            { label: '저장 버전', value: detail.reportVersion ? `v${detail.reportVersion}` : '-' }
          ],
          items: [
            ...detail.items.map((item) => {
              const response = responseByItem.get(item.itemId);
              const value = item.responseType === 'Check'
                ? qualityCheckResultLabel(response?.checkResult ?? null)
                : displayDepartmentValue(response?.textValue);
              return {
                key: `response:${item.itemId}`,
                label: `${item.displayOrder}. ${item.label}${item.isRequired ? ' · 필수' : ''}`,
                value,
                note: [response?.note, item.guidance].filter(Boolean).join(' · ') || undefined
              };
            }),
            ...detail.photos.map((photo) => ({
              key: `photo:${photo.photoId}`,
              label: `사진 · ${photo.displayName}`,
              value: `${photo.altText} · ${formatFileSize(photo.byteSize)}`,
              note: `${photo.normalizedMime} · ${formatDepartmentDateTime(photo.createdAtUtc)}`
            })),
            ...detail.history.map((history) => ({
              key: `history:${history.attemptId}`,
              label: `검사 이력 ${history.attemptNumber}차`,
              value: `${history.status} · ${formatDepartmentDateTime(history.completedAtUtc)}`,
              note: history.pendingNumber ? `Pending #${history.pendingNumber}` : undefined
            }))
          ]
        } satisfies ProjectDepartmentRecord;
      })
    };
  }

  const stages: LogisticsStage[] = ['packing', 'departure', 'delivery'];
  const [queues, history] = await Promise.all([
    Promise.all(stages.map((stage) => getLogisticsQueue(developmentUserKey, stage, project.projectId))),
    getLogisticsProjectHistory(developmentUserKey, project.projectId)
  ]);
  const entries = queues.flatMap((queue) => {
    const stageProject = queue.projects.find((item) => item.projectId === project.projectId);
    return (stageProject?.items ?? []).map((item) => ({ stage: queue.stage, item }));
  });
  const completedRank = logisticsCompletionRank(project.projectWorkStatus, project.status);
  const orderedHistory = [...history.items].sort((left, right) => {
    const stageOrder = stages.indexOf(left.stage) - stages.indexOf(right.stage);
    return stageOrder !== 0 ? stageOrder : left.displayCode.localeCompare(right.displayCode, 'ko');
  });
  const historyRecords: ProjectDepartmentRecord[] = orderedHistory.map((item) => ({
    key: `history:${item.stage}:${item.targetId}`,
    title: `${logisticsStageLabel(item.stage)} · ${item.displayCode}`,
    subtitle: '물류 실행 입력·증빙 기록',
    status: logisticsOwnerStatusLabel(item.status),
    tone: item.status === 'Finalized' ? 'success' : item.status === 'Cancelled' ? 'neutral' : 'info',
    fields: [
      { label: '처리 상태', value: logisticsOwnerStatusLabel(item.status) },
      { label: '포함 패널', value: item.panelCodes.join(', ') || '-' },
      { label: '포장 단위', value: item.unitCodes.join(', ') || (item.stage === 'packing' ? item.displayCode : '-') },
      { label: '포장 비고', value: displayDepartmentValue(item.note) },
      { label: '포장 규격', value: displayDepartmentValue(item.specification) },
      { label: '중량', value: displayDepartmentValue(item.weightText) },
      { label: '출발일', value: formatDepartmentDate(item.departureDate) },
      { label: '등록 담당', value: item.createdByName },
      { label: '등록 시각', value: formatDepartmentDateTime(item.createdAtUtc) },
      { label: '확정 담당', value: displayDepartmentValue(item.finalizedByName) },
      { label: '확정 시각', value: formatDepartmentDateTime(item.finalizedAtUtc) },
      { label: '취소 담당', value: displayDepartmentValue(item.cancelledByName) },
      { label: '취소 시각', value: formatDepartmentDateTime(item.cancelledAtUtc) },
      { label: '저장 버전', value: `v${item.version}` }
    ],
    items: item.evidence.map((evidence) => ({
      key: evidence.evidenceId,
      label: `증빙 · ${evidence.displayName}`,
      value: `${displayDepartmentValue(evidence.altText)} · ${formatFileSize(evidence.byteSize)}`,
      note: `${logisticsEvidenceTypeLabel(evidence.ownerType)} · ${evidence.normalizedMime} · ${formatDepartmentDateTime(evidence.createdAtUtc)}`
    }))
  }));
  const queueRecords: ProjectDepartmentRecord[] = historyRecords.length === 0 ? entries.map(({ stage, item }) => ({
    key: `queue:${stage}:${item.targetId}`,
    title: `${logisticsStageLabel(stage)} · ${item.displayCode}`,
    subtitle: item.title,
    status: item.hasOpenPending ? 'Pending 확인' : logisticsStatusLabel(item.status),
    tone: item.hasOpenPending ? 'danger' : 'info',
    fields: [
      { label: '대상 구분', value: item.targetType === 'Panel' ? '패널' : '포장 단위' },
      { label: '포함 패널', value: item.panelCodes.join(', ') || '-' },
      { label: '대기 정보', value: item.supportingText }
    ]
  })) : [];
  return {
    canMutate: permissions.logistics && entries.some(({ item }) => item.canMutate),
    metrics: stages.map((stage) => ({
      label: logisticsStageLabel(stage),
      value: history.items.some((item) => item.stage === stage)
        ? `${history.items.filter((item) => item.stage === stage && item.status === 'Finalized').length}/${history.items.filter((item) => item.stage === stage).length}`
        : entries.length > 0
          ? `${entries.filter((entry) => entry.stage === stage && isLogisticsComplete(entry.item.status)).length}/${entries.filter((entry) => entry.stage === stage).length}`
          : `${completedRank >= stages.indexOf(stage) + 1 ? project.activePanelCount : 0}/${project.activePanelCount}`,
      tone: history.items.some((item) => item.stage === stage && item.status === 'Finalized') || completedRank >= stages.indexOf(stage) + 1 ? 'success' : 'neutral'
    })),
    records: [...historyRecords, ...queueRecords]
  };
}

function ProjectDepartmentDataSection({
  section,
  state,
  onOpen
}: {
  section: ProjectDepartmentSection;
  state: LoadState<ProjectDepartmentData>;
  onOpen: () => void;
}) {
  const labels = {
    sales: { title: '영업', action: '영업 후속 업무 열기', description: '프로젝트 생성과 납품 후 발행 요청 준비를 확인합니다.' },
    materials: { title: '자재', action: '자재 연속 흐름 열기', description: '도착·IQC·입고 확정·키팅 상태를 한 흐름으로 확인합니다.' },
    manufacturing: { title: '제조', action: '제조 업무 열기', description: '패널별 제조 착수·중단·완료 상태를 확인합니다.' },
    quality: { title: '품질', action: '품질 업무 열기', description: 'LQC·OQC·전진검수·FAT 결과를 확인합니다.' },
    logistics: { title: '물류', action: '물류 업무 열기', description: '포장·출발·납품 완료 상태를 확인합니다.' }
  } as const;
  const department = labels[section];
  const data = state.kind === 'ready' ? state.data : null;

  return (
    <section className="subsection project-department-section" data-department={section}>
      <div className="subsection-header">
        <div>
          <p className="eyebrow">PROJECT DEPARTMENT</p>
          <h3>{department.title}</h3>
          <p>{department.description}</p>
        </div>
        <div className="project-department-action">
          {data && !data.canMutate ? <small>조회 전용 · 담당자만 수정할 수 있습니다.</small> : null}
          <button type="button" className={data?.canMutate ? 'primary-button' : 'secondary-button'} onClick={onOpen}>
            {data?.canMutate ? department.action.replace(' 열기', ' 수정') : '업무 화면에서 조회'}
          </button>
        </div>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">이 프로젝트의 부서 데이터를 불러오는 중입니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {data ? (
        <>
          <div className="project-department-metrics" aria-label={`${department.title} 프로젝트 지표`}>
            {data.metrics.map((metric) => (
              <article key={metric.label}><span>{metric.label}</span><strong>{metric.value}</strong>{metric.tone ? <i data-tone={metric.tone} /> : null}</article>
            ))}
          </div>
          {data.records.length > 0 ? (
            <div className="project-department-records" aria-label={`${department.title} 입력 데이터`}>
              {data.records.map((record) => (
                <article className="project-department-record" key={record.key}>
                  <header>
                    <div><strong>{record.title}</strong>{record.subtitle ? <small>{record.subtitle}</small> : null}</div>
                    <StatusBadge label={record.status} tone={record.tone} />
                  </header>
                  <dl className="project-department-field-grid">
                    {record.fields.map((field) => (
                      <div key={`${record.key}:${field.label}`}><dt>{field.label}</dt><dd>{field.value}</dd></div>
                    ))}
                  </dl>
                  {record.items && record.items.length > 0 ? (
                    <div className="project-department-record-items">
                      {record.items.map((item) => (
                        <div key={item.key}>
                          <span><strong>{item.label}</strong><small>{item.note}</small></span>
                          <b>{item.value}</b>
                        </div>
                      ))}
                    </div>
                  ) : null}
                </article>
              ))}
            </div>
          ) : <p className="empty-text">이 프로젝트에 등록된 {department.title} 데이터가 아직 없습니다.</p>}
        </>
      ) : null}
    </section>
  );
}

function salesSettlementStatusLabel(status: string) {
  return ({ NotStarted: '미시작', Draft: '작성 중', Completed: '완료', Cancelled: '취소' } as Record<string, string>)[status] ?? status;
}

function manufacturingStatusLabel(status: string) {
  return ({ Ready: '착수 대기', InProgress: '제조 중', Blocked: '중단', Completed: '완료', Cancelled: '취소' } as Record<string, string>)[status] ?? status;
}

function qualityStageLabel(stage: QualityInspectionStage) {
  return ({ LQC: 'LQC', OQC: 'OQC', CustomerInspection: '입회검사', FAT: 'FAT' } as const)[stage];
}

function qualityStatusLabel(status: string) {
  return ({ Ready: '검사 대기', Requested: '검사 요청', InProgress: '검사 중', Passed: '합격', Failed: '부적합', Completed: '완료', Confirmed: '확정' } as Record<string, string>)[status] ?? status;
}

function logisticsStageLabel(stage: LogisticsStage) {
  return ({ packing: '포장', departure: '출발', delivery: '납품' } as const)[stage];
}

function logisticsStatusLabel(status: string) {
  return ({ Ready: '작업 대기', Draft: '작성 중', Completed: '완료', Packed: '포장 완료', Departed: '출발 완료', Delivered: '납품 완료' } as Record<string, string>)[status] ?? status;
}

function isLogisticsComplete(status: string) {
  return status === 'Completed' || status === 'Packed' || status === 'Departed' || status === 'Delivered';
}

function logisticsCompletionRank(workStatus: ProjectWorkStatus, projectStatus: ProjectStatus) {
  if (projectStatus === 'Completed' || workStatus === 'Completed' || workStatus === 'SalesSettlementCompleted' || workStatus === 'ShipmentCompleted') return 3;
  if (workStatus === 'DeliveryCompleted') return 3;
  if (workStatus === 'DepartureProcessed') return 2;
  if (workStatus === 'PackingCompleted' || workStatus === 'ReadyForShipment') return 1;
  return 0;
}

function packagingMethodLabel(method: PackagingMethod | null) {
  return ({ WoodenCrate: '목포장', StretchWrap: '랩포장', HeavyDutyBox: '강화박스' } as Record<string, string>)[method ?? ''] ?? '포장방식 미입력';
}

function displayDepartmentValue(value: unknown) {
  if (value === null || value === undefined || value === '') return '-';
  return String(value);
}

function formatDepartmentDate(value: string | null | undefined) {
  return value ? formatDate(value) : '-';
}

function formatDepartmentDateTime(value: string | null | undefined) {
  return value ? formatKoreanDateTime(value) : '-';
}

function formatDepartmentQuantity(value: number | null, unit: string | null) {
  if (value === null) return '-';
  return `${new Intl.NumberFormat('ko-KR', { maximumFractionDigits: 3 }).format(value)}${unit ? ` ${unit}` : ''}`;
}

function formatDepartmentAmount(value: number, currencyCode?: string) {
  return `${new Intl.NumberFormat('ko-KR', { maximumFractionDigits: 0 }).format(value)}${currencyCode ? ` ${currencyCode}` : ''}`;
}

function yesNo(value: boolean) {
  return value ? '예' : '아니오';
}

function formatFileSize(value: number) {
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

function materialSupplyTypeLabel(value: string) {
  return ({ Purchased: '구매품', CustomerSupplied: '고객 지급품' } as Record<string, string>)[value] ?? value;
}

function materialReceiptStatusLabel(value: string) {
  return ({
    Arrived: '도착 등록',
    IqcRequested: 'IQC 요청',
    Passed: 'IQC 합격',
    FailedBlocked: 'IQC 부적합',
    Confirmed: '입고 확정',
    Cancelled: '취소'
  } as Record<string, string>)[value] ?? value;
}

function qualityResultLabel(value: 'Passed' | 'Failed' | null) {
  return value === 'Passed' ? '합격' : value === 'Failed' ? '부적합' : '-';
}

function qualityCheckResultLabel(value: 'Pass' | 'Fail' | 'NotApplicable' | null) {
  return ({ Pass: '적합', Fail: '부적합', NotApplicable: '해당 없음' } as Record<string, string>)[value ?? ''] ?? '-';
}

function logisticsOwnerStatusLabel(value: string) {
  return ({
    Draft: '작성 중',
    Finalized: '확정 완료',
    Packed: '포장 완료',
    Departed: '출발 완료',
    Delivered: '납품 완료',
    Completed: '완료',
    Cancelled: '취소'
  } as Record<string, string>)[value] ?? value;
}

function logisticsEvidenceTypeLabel(value: string) {
  return ({
    PackingPhoto: '포장 사진',
    DeparturePhoto: '출발 사진',
    DeliveryDocument: '납품 문서',
    PackingUnit: '포장 증빙',
    DepartureBatch: '출발 증빙',
    DeliveryBatch: '납품 증빙'
  } as Record<string, string>)[value] ?? '첨부 증빙';
}

type PanelInformationRowForm = {
  panelId: string;
  sequenceNumber: number;
  panelNumber: string;
  displayCode: string;
  panelInfoVersion: number;
  original: PanelInformationPanel;
  originalPanelName: string;
  currentPanelName: string;
  panelNameDirty: boolean;
  originalWidthMm: string | null;
  originalHeightMm: string | null;
  originalDepthMm: string | null;
  widthInput: string;
  heightInput: string;
  depthInput: string;
  sizeDirty: boolean;
  sizeClearRequested: boolean;
  sizeInputUnit: PanelInputUnit;
};

type PanelNameDuplicateGroup = {
  name: string;
  panelNumbers: string[];
};

function PanelInformationSection({
  developmentUserKey,
  project,
  state,
  canUpdatePanelInfo,
  onEdit,
  onOpenPanel,
  isSystemAdministrator
}: {
  developmentUserKey: string;
  project: ProjectDetail;
  state: LoadState<PanelInformationResponse>;
  canUpdatePanelInfo: boolean;
  onEdit: () => void;
  onOpenPanel: (panelId: string) => void;
  isSystemAdministrator: boolean;
}) {
  const canShowEdit = canUpdatePanelInfo && project.status === 'Active';
  const [displayUnit, setDisplayUnit] = useState<PanelInputUnit>(() => readDisplayUnit());

  const changeDisplayUnit = (unit: PanelInputUnit) => {
    setDisplayUnit(unit);
    window.localStorage.setItem('emi-qms-panel-display-unit', unit);
  };

  return (
    <section className="page-surface panel-info-section">
      <div className="subsection-header">
        <div>
          <h3>설계</h3>
          <span>{formatPackagingMethod(project.packagingMethod)}</span>
        </div>
        <div className="button-row">
          <div className="unit-toggle" role="group" aria-label="표시 단위">
            <button
              type="button"
              className={displayUnit === 'Mm' ? 'secondary-button active' : 'secondary-button'}
              aria-pressed={displayUnit === 'Mm'}
              onClick={() => changeDisplayUnit('Mm')}
            >
              mm
            </button>
            <button
              type="button"
              className={displayUnit === 'Inch' ? 'secondary-button active' : 'secondary-button'}
              aria-pressed={displayUnit === 'Inch'}
              onClick={() => changeDisplayUnit('Inch')}
            >
              inch
            </button>
          </div>
          {canShowEdit ? <button type="button" className="primary-button" onClick={onEdit}>패널명·사이즈 수정</button> : null}
        </div>
      </div>

      {state.kind === 'loading' ? <p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <>
          <div className="panel-info-summary project-workflow-summary">
            <StatusChip label="QR 가능" value={`${project.qrEligibleCount}/${project.activePanelCount}`} />
            <StatusChip label="제조 완료" value={`${project.manufacturingCompletedCount}/${project.activePanelCount}`} />
            <StatusChip label="검사 완료" value={`${project.inspectionCompletedCount}/${project.activePanelCount}`} />
          </div>
          {state.data.panelInformationStatusMessage ? (
            <p role="status" className="warning-text">{state.data.panelInformationStatusMessage}</p>
          ) : null}
          <ProjectPanelList
            panels={state.data.panels}
            packagingMethod={state.data.packagingMethod}
            displayUnit={displayUnit}
            onOpenPanel={onOpenPanel}
          />
          <PanelQrManager
            developmentUserKey={developmentUserKey}
            projectId={project.projectId}
            canIssue={canShowEdit}
            isSystemAdministrator={isSystemAdministrator}
          />
        </>
      ) : null}
    </section>
  );
}

type ProcurementRowForm = {
  itemId: string | null;
  rowVersion: number | null;
  sourceProjectText: string;
  sourceProjectCodeText: string;
  standardLeadTime: string;
  orderItem: string;
  supplierName: string;
  technicalOwner: string;
  orderDate: string;
  expectedReceiptDate: string;
  shipmentDisplayDate: string | null;
  issueNote: string;
  supplyType: ProcurementSupplyType;
  orderQuantity: string;
  orderUnit: string;
  receiptCompleted: boolean;
  receiptCompletedAtUtc: string;
  receiptCompletionNote: string;
  dDayText: string;
};

type ProductionPlanRowForm = {
  itemId: string | null;
  templateStepId: string | null;
  sequenceNumber: number;
  stepName: string;
  isRequired: boolean;
  isCustom: boolean;
  isDeleted: boolean;
  plannedDate: string;
  note: string;
  rowVersion: number;
};

type ProjectAssigneeForm = {
  assigneeId: string | null;
  responsibilityType: ResponsibilityType;
  responsibilityLabel: string;
  assignedUserId: string;
  note: string;
  rowVersion: number;
};

type AssigneeGroupDefinition = {
  title: string;
  primary: ResponsibilityType;
  secondary: ResponsibilityType;
  tone: 'sales' | 'design' | 'production' | 'procurement' | 'materials' | 'manufacturing' | 'logistics' | 'quality';
};

const departmentAssigneeGroups: AssigneeGroupDefinition[] = [
  { title: '영업', primary: 'SalesPrimary', secondary: 'SalesSecondary', tone: 'sales' },
  { title: '설계', primary: 'DesignPrimary', secondary: 'DesignSecondary', tone: 'design' },
  { title: '생산관리', primary: 'ProductionPlanningPrimary', secondary: 'ProductionPlanningSecondary', tone: 'production' },
  { title: '구매', primary: 'ProcurementPrimary', secondary: 'ProcurementSecondary', tone: 'procurement' },
  { title: '자재', primary: 'MaterialsPrimary', secondary: 'MaterialsSecondary', tone: 'materials' },
  { title: '제조', primary: 'ManufacturingPrimary', secondary: 'ManufacturingSecondary', tone: 'manufacturing' },
  { title: '물류', primary: 'LogisticsPrimary', secondary: 'LogisticsSecondary', tone: 'logistics' }
];

const qualityAssigneeGroups: AssigneeGroupDefinition[] = [
  { title: 'IQC 수입검사', primary: 'QualityIQC', secondary: 'QualityIQCSecondary', tone: 'quality' },
  { title: 'LQC', primary: 'QualityLQC', secondary: 'QualityLQCSecondary', tone: 'quality' },
  { title: 'OQC 자체검수', primary: 'QualityOQC', secondary: 'QualityOQCSecondary', tone: 'quality' },
  { title: '전진검수/FAT', primary: 'QualityCustomerInspection', secondary: 'QualityCustomerInspectionSecondary', tone: 'quality' }
];

function ProductionPlanningDashboardPage({
  developmentUserKey,
  canUpdateProductionPlanning,
  onBack,
  onOpenSettings,
  onOpenProject,
  onEditProject
}: {
  developmentUserKey: string;
  canUpdateProductionPlanning: boolean;
  onBack: () => void;
  onOpenSettings: () => void;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
}) {
  const [search, setSearch] = useState('');
  const [summaryState, setSummaryState] = useState<LoadState<ProductionPlanningSummary>>({ kind: 'loading' });
  const [state, setState] = useState<LoadState<ProductionPlanningProjectListResponse>>({ kind: 'loading' });
  const [expandedProjectId, setExpandedProjectId] = useState<string | null>(null);
  const [expandedPlanState, setExpandedPlanState] = useState<LoadState<ProductionPlanningResponse>>({ kind: 'empty' });
  const [showExcelDialog, setShowExcelDialog] = useState(false);
  const [excelMessage, setExcelMessage] = useState('');
  const isMobile = useIsMobileViewport();
  const productionVisibleIds = state.kind === 'ready' ? state.data.projects.map((project) => project.projectId) : [];
  const productionSelection = useSelectedRows(productionVisibleIds);

  const load = useCallback(() => {
    setSummaryState({ kind: 'loading' });
    setState({ kind: 'loading' });
    Promise.all([
      getProductionPlanningSummary(developmentUserKey),
      listProductionPlanningProjects(developmentUserKey, search)
    ])
      .then(([summary, projects]) => {
        setSummaryState({ kind: 'ready', data: summary });
        setState({ kind: 'ready', data: projects });
        setExpandedProjectId((current) => current && projects.projects.some((project) => project.projectId === current) ? current : null);
      })
      .catch((error: unknown) => {
        setSummaryState(toLoadError(error, '생산계획 요약을 불러올 수 없습니다.'));
        setState(toLoadError(error, '생산계획 프로젝트 목록을 불러올 수 없습니다.'));
      });
  }, [developmentUserKey, search]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  useEffect(() => {
    if (!expandedProjectId) {
      return;
    }

    const controller = new AbortController();
    queueMicrotask(() => {
      if (!controller.signal.aborted) {
        setExpandedPlanState({ kind: 'loading' });
      }
    });
    getProjectProductionPlanning(developmentUserKey, expandedProjectId, controller.signal)
      .then((plan) => {
        if (!controller.signal.aborted) {
          setExpandedPlanState({ kind: 'ready', data: plan });
        }
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setExpandedPlanState(toLoadError(error, '선택 프로젝트 생산계획을 불러올 수 없습니다.'));
        }
      });

    return () => controller.abort();
  }, [developmentUserKey, expandedProjectId]);

  async function downloadBulkTemplate() {
    setExcelMessage('');
    try {
      const template = await downloadProductionPlanningBulkTemplate(developmentUserKey);
      const url = URL.createObjectURL(template.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = template.fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      setExcelMessage('생산계획 Excel 양식을 다운로드했습니다.');
    } catch (error) {
      handleFormError(error, () => undefined, setExcelMessage);
    }
  }

  return (
    <section className={isMobile ? 'page-surface mobile-operations-page production-mobile-page' : 'page-surface'}>
      <div className={isMobile ? 'page-header mobile-operations-header' : 'page-header'}>
        <div>
          <p className="eyebrow">{isMobile ? 'TODAY PLAN' : 'Production Planning'}</p>
          <h2>생산계획</h2>
          {isMobile ? <p>미등록·지연 프로젝트를 먼저 확인하세요.</p> : null}
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>프로젝트 목록</button>
          {canUpdateProductionPlanning && !isMobile ? <button type="button" onClick={onOpenSettings}>생산계획 단계 설정</button> : null}
          {canUpdateProductionPlanning && !isMobile ? <button type="button" onClick={downloadBulkTemplate}>Excel 양식 다운로드</button> : null}
          {canUpdateProductionPlanning && !isMobile ? <button type="button" className="primary-button" onClick={() => setShowExcelDialog(true)}>Excel 업로드</button> : null}
        </div>
      </div>
      {excelMessage ? <p role="alert" className={successMessage(excelMessage) ? 'success-text' : 'error-text'}>{excelMessage}</p> : null}
      {showExcelDialog ? (
        <ProductionPlanningExcelDialog
          developmentUserKey={developmentUserKey}
          onClose={() => setShowExcelDialog(false)}
          onApplied={() => {
            setShowExcelDialog(false);
            load();
          }}
        />
      ) : null}

      {summaryState.kind === 'ready' ? (
        <div className="dashboard-kpi-grid" aria-label="생산계획 요약">
          <DashboardKpiCard title="생산계획 미등록" value={summaryState.data.notPlannedCount} helperText="진행 프로젝트 기준" variant="warning" />
          <DashboardKpiCard title="작성 중" value={summaryState.data.planningCount} helperText="필수 일정 미완료" />
          <DashboardKpiCard title="계획 완료" value={summaryState.data.plannedCount} helperText="필수 일정 입력 완료" variant="positive" />
          <DashboardKpiCard title="담당자 미지정 프로젝트" value={summaryState.data.missingAssigneeProjectCount} helperText="5개 역할 기준" variant="warning" />
        </div>
      ) : null}
      {summaryState.kind !== 'ready' && summaryState.kind !== 'loading' && summaryState.kind !== 'empty' ? <StateMessage state={summaryState} /> : null}

      <form className="toolbar" onSubmit={(event) => { event.preventDefault(); load(); }}>
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="프로젝트명, 고객사, Code, Item 검색" />
        <button type="submit">검색</button>
      </form>

      {state.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="production-planning"
          visibleIds={productionVisibleIds}
          selectedIds={productionSelection.selectedIds}
          allSelected={productionSelection.allSelected}
          busy={productionSelection.busy}
          filters={{ search }}
          onBusyChange={productionSelection.setBusy}
          onToggleAll={productionSelection.toggleAll}
          onClear={productionSelection.clear}
        />
      ) : null}

      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' && state.data.projects.length === 0 ? <p className="empty-text">표시할 생산계획 프로젝트가 없습니다.</p> : null}
      {state.kind === 'ready' && state.data.projects.length > 0 ? (
        isMobile ? (
          <div className="procurement-project-cards production-planning-mobile">
            {state.data.projects.map((project) => (
              <article key={project.projectId} className={`${project.projectId === expandedProjectId ? 'procurement-project-card active' : 'procurement-project-card'} selected-export-row`}>
                <div className="subsection-header">
                  <SelectionCheckbox checked={productionSelection.selectedIds.has(project.projectId)} disabled={productionSelection.busy} label={`${project.projectTitle} 선택`} onChange={(checked) => productionSelection.toggle(project.projectId, checked)} />
                  <div>
                    <small>{project.projectCode} · {project.item}</small>
                    <h3>{project.projectTitle}</h3>
                  </div>
                  <ProductionPlanStatusBadge status={project.planStatus} label={project.planStatusLabel} />
                </div>
                <dl className="mobile-priority-grid">
                  <div><dt>납기일</dt><dd>{emptyDash(project.deliveryDate)}</dd></div>
                  <div><dt>설계 면수</dt><dd>{project.activePanelCount}면</dd></div>
                  <div><dt>계획</dt><dd>{project.planStatusLabel}</dd></div>
                </dl>
                <div className="mobile-card-actions">
                  <button type="button" onClick={() => onOpenProject(project.projectId)}>프로젝트</button>
                  <button type="button" className="primary-button" onClick={() => setExpandedProjectId((current) => current === project.projectId ? null : project.projectId)}>
                    {project.projectId === expandedProjectId ? '접기' : '계획 보기'}
                  </button>
                </div>
                {project.projectId === expandedProjectId ? (
                  <ProductionPlanningExpanded
                    developmentUserKey={developmentUserKey}
                    projectId={project.projectId}
                    state={expandedPlanState}
                    canUpdateProductionPlanning={canUpdateProductionPlanning && project.projectStatus === 'Active'}
                    onOpenProject={onOpenProject}
                    onEditProject={onEditProject}
                  />
                ) : null}
              </article>
            ))}
          </div>
        ) : (
          <div className="production-project-table procurement-desktop" role="table" aria-label="생산계획 프로젝트 목록">
            <div className="production-project-head" role="row">
              <span>선택</span><span>프로젝트명</span><span>Code</span><span>Item</span><span>면수</span><span>납기일</span><span>생산계획 상태</span>
            </div>
            {state.data.projects.map((project) => (
              <Fragment key={project.projectId}>
                <div
                  role="row"
                  tabIndex={0}
                  className={project.projectId === expandedProjectId ? 'production-project-row active' : 'production-project-row'}
                  aria-expanded={project.projectId === expandedProjectId}
                  onClick={() => setExpandedProjectId((current) => current === project.projectId ? null : project.projectId)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault();
                      setExpandedProjectId((current) => current === project.projectId ? null : project.projectId);
                    }
                  }}
                >
                  <span><SelectionCheckbox checked={productionSelection.selectedIds.has(project.projectId)} disabled={productionSelection.busy} label={`${project.projectTitle} 선택`} onChange={(checked) => productionSelection.toggle(project.projectId, checked)} /></span>
                  <span>{project.projectTitle}</span>
                  <span>{project.projectCode}</span>
                  <span>{project.item}</span>
                  <span>{project.activePanelCount}</span>
                  <span>{emptyDash(project.deliveryDate)}</span>
                  <span><ProductionPlanStatusBadge status={project.planStatus} label={project.planStatusLabel} /></span>
                </div>
                {project.projectId === expandedProjectId ? (
                  <ProductionPlanningExpanded
                    developmentUserKey={developmentUserKey}
                    projectId={project.projectId}
                    state={expandedPlanState}
                    canUpdateProductionPlanning={canUpdateProductionPlanning && project.projectStatus === 'Active'}
                    onOpenProject={onOpenProject}
                    onEditProject={onEditProject}
                  />
                ) : null}
              </Fragment>
            ))}
          </div>
        )
      ) : null}
    </section>
  );
}

function ProductionPlanningExpanded({
  developmentUserKey,
  projectId,
  state,
  canUpdateProductionPlanning,
  onOpenProject,
  onEditProject
}: {
  developmentUserKey: string;
  projectId: string;
  state: LoadState<ProductionPlanningResponse>;
  canUpdateProductionPlanning: boolean;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
}) {
  return (
    <section className="procurement-project-expanded" aria-label="선택 프로젝트 생산계획">
      <div className="button-row">
        <button type="button" onClick={() => onOpenProject(projectId)}>프로젝트 상세에서 보기</button>
        {canUpdateProductionPlanning ? <button type="button" className="primary-button" onClick={() => onEditProject(projectId)}>생산계획 수정</button> : null}
      </div>
      {state.kind === 'loading' ? <p className="muted-text">생산계획을 불러오는 중입니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? <ProductionPlanningReadOnly developmentUserKey={developmentUserKey} plan={state.data} showCalendar={false} /> : null}
    </section>
  );
}

function ProductionPlanningSettingsPage({
  developmentUserKey,
  canUpdateProductionPlanning,
  onBack
}: {
  developmentUserKey: string;
  canUpdateProductionPlanning: boolean;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<ProductionTemplateSettings[]>>({ kind: 'loading' });
  const [selectedProductTypeId, setSelectedProductTypeId] = useState('');
  const [steps, setSteps] = useState<ProductionTemplateSettingsStep[]>([]);
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    setErrors({});
    setMessage('');
    listProductionTemplateSettings(developmentUserKey)
      .then((templates) => {
        setState({ kind: 'ready', data: templates });
        const nextSelected = selectedProductTypeId && templates.some((template) => template.productTypeId === selectedProductTypeId)
          ? selectedProductTypeId
          : templates[0]?.productTypeId ?? '';
        setSelectedProductTypeId(nextSelected);
        const selected = templates.find((template) => template.productTypeId === nextSelected);
        setSteps((selected?.steps ?? []).map(copyTemplateSettingsStep));
      })
      .catch((error: unknown) => setState(toLoadError(error, '생산계획 단계 설정을 불러올 수 없습니다.')));
  }, [developmentUserKey, selectedProductTypeId]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  if (!canUpdateProductionPlanning) {
    return <section className="page-surface"><StateMessage state={{ kind: 'forbidden', message: '권한이 없습니다.' }} /></section>;
  }

  const templates = state.kind === 'ready' ? state.data : [];
  const selected = templates.find((template) => template.productTypeId === selectedProductTypeId);

  function selectProductType(productTypeId: string) {
    const template = templates.find((item) => item.productTypeId === productTypeId);
    setSelectedProductTypeId(productTypeId);
    setSteps((template?.steps ?? []).map(copyTemplateSettingsStep));
    setErrors({});
    setMessage('');
  }

  function updateStep(index: number, next: Partial<ProductionTemplateSettingsStep>) {
    setSteps((current) => current.map((step, stepIndex) => stepIndex === index ? { ...step, ...next } : step));
  }

  function addStep() {
    setSteps((current) => [
      ...current,
      {
        templateStepId: null,
        sequenceNumber: current.length === 0 ? 1 : Math.max(...current.map((step) => step.sequenceNumber)) + 1,
        stepName: '',
        isRequired: false,
        isActive: true
      }
    ]);
  }

  async function save() {
    if (!selected) {
      return;
    }

    const validation = validateTemplateSettingsSteps(steps);
    setErrors(validation);
    setMessage('');
    if (Object.keys(validation).length > 0) {
      return;
    }

    setIsSaving(true);
    try {
      const updated = await updateProductionTemplateSettings(developmentUserKey, selected.productTypeId, {
        steps: steps.map((step) => ({
          templateStepId: step.templateStepId,
          sequenceNumber: Number(step.sequenceNumber),
          stepName: step.stepName.trim(),
          isRequired: step.isRequired,
          isActive: step.isActive
        })),
        reason: reason.trim() || null
      });
      setState({ kind: 'ready', data: updated });
      const nextSelected = updated.find((template) => template.productTypeId === selected.productTypeId);
      setSteps((nextSelected?.steps ?? []).map(copyTemplateSettingsStep));
      setReason('');
      setErrors({});
      setMessage('생산계획 단계 설정을 저장했습니다.');
    } catch (error) {
      handleFormError(error, setErrors, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Production Planning</p>
          <h2>생산계획 단계 설정</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>생산관리</button>
          <button type="button" className="primary-button" disabled={isSaving || !selected} onClick={save}>{isSaving ? '저장 중' : '저장'}</button>
        </div>
      </div>
      <p className="info-text">
        생산계획 단계 설정은 이후 새로 작성되는 생산계획부터 적용됩니다. 이미 작성된 프로젝트 생산계획은 자동으로 변경되지 않습니다.
      </p>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <>
          <div className="settings-product-type-tabs" role="tablist" aria-label="Item">
            {templates.map((template) => (
              <button
                type="button"
                role="tab"
                aria-selected={template.productTypeId === selectedProductTypeId}
                className={template.productTypeId === selectedProductTypeId ? 'active' : undefined}
                key={template.productTypeId}
                onClick={() => selectProductType(template.productTypeId)}
              >
                {template.code}
              </button>
            ))}
          </div>
          <FormErrorSummary errors={errors} />
          {selected ? (
            <section className="subsection">
              <div className="subsection-header">
                <div>
                  <h3>{selected.code} 단계</h3>
                  <span>현재 Item의 최신 설정입니다.</span>
                </div>
                <button type="button" onClick={addStep}>행 추가</button>
              </div>
              <div className="template-settings-table" role="table" aria-label={`${selected.code} 생산계획 단계 설정`}>
                <div className="template-settings-head" role="row">
                  <span>순서</span><span>생산계획 단계</span><span>필수</span><span>사용</span>
                </div>
                {steps.map((step, index) => (
                  <div className="template-settings-row" role="row" key={`${step.templateStepId ?? 'new'}-${index}`}>
                    <div className="grid-field">
                      <input
                        aria-label="순서"
                        name={`steps[${index}].sequenceNumber`}
                        type="number"
                        min={1}
                        value={step.sequenceNumber}
                        className={fieldError(errors, `steps[${index}].sequenceNumber`) ? 'field-invalid' : undefined}
                        onChange={(event) => updateStep(index, { sequenceNumber: Number(event.target.value) })}
                      />
                      <FieldErrorMessage field={`steps[${index}].sequenceNumber`} message={fieldError(errors, `steps[${index}].sequenceNumber`)} />
                    </div>
                    <div className="grid-field">
                      <input
                        aria-label="생산계획 단계"
                        name={`steps[${index}].stepName`}
                        value={step.stepName}
                        className={fieldError(errors, `steps[${index}].stepName`) ? 'field-invalid' : undefined}
                        onChange={(event) => updateStep(index, { stepName: event.target.value })}
                      />
                      <FieldErrorMessage field={`steps[${index}].stepName`} message={fieldError(errors, `steps[${index}].stepName`)} />
                    </div>
                    <label className="inline-check">
                      <input type="checkbox" checked={step.isRequired} onChange={(event) => updateStep(index, { isRequired: event.target.checked })} />
                      <span>필수</span>
                    </label>
                    <label className="inline-check">
                      <input type="checkbox" checked={step.isActive} onChange={(event) => updateStep(index, { isActive: event.target.checked })} />
                      <span>사용</span>
                    </label>
                  </div>
                ))}
              </div>
              <label className="form-field">
                <span>변경 사유</span>
                <textarea name="reason" value={reason} onChange={(event) => setReason(event.target.value)} />
              </label>
            </section>
          ) : <p className="empty-text">설정할 Item이 없습니다.</p>}
        </>
      ) : null}
      {message ? <p role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</p> : null}
    </section>
  );
}

function ProcurementRequiredItemSettingsPage({
  developmentUserKey,
  canUpdateProcurement,
  onBack
}: {
  developmentUserKey: string;
  canUpdateProcurement: boolean;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<ProcurementRequiredItemSettings[]>>({ kind: 'loading' });
  const [selectedItemCode, setSelectedItemCode] = useState<string | null>(null);
  const [rows, setRows] = useState<ProcurementRequiredItemSettingsRow[]>([]);
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    listProcurementRequiredItemSettings(developmentUserKey)
      .then((items) => {
        setState(items.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: items });
        const nextSelected = selectedItemCode && items.some((item) => item.itemCode === selectedItemCode)
          ? selectedItemCode
          : items[0]?.itemCode ?? null;
        setSelectedItemCode(nextSelected);
        const selected = items.find((item) => item.itemCode === nextSelected);
        setRows(selected?.rows.length ? selected.rows.map(cloneProcurementRequiredRow) : defaultProcurementRequiredRows());
      })
      .catch((error: unknown) => setState(toLoadError(error, '구매 필수 항목 설정을 불러올 수 없습니다.')));
  }, [developmentUserKey, selectedItemCode]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  const settings = state.kind === 'ready' ? state.data : [];
  const selected = settings.find((item) => item.itemCode === selectedItemCode) ?? null;

  function selectItem(itemCode: string) {
    const target = settings.find((item) => item.itemCode === itemCode);
    setSelectedItemCode(itemCode);
    setRows(target?.rows.length ? target.rows.map(cloneProcurementRequiredRow) : defaultProcurementRequiredRows());
    setErrors({});
    setMessage('');
  }

  function updateRow(index: number, patch: Partial<ProcurementRequiredItemSettingsRow>) {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, ...patch } : row));
  }

  function addRow() {
    setRows((current) => [
      ...current,
      {
        templateRowId: null,
        sequenceNumber: current.length + 1,
        itemName: '',
        isRequired: true,
        isActive: true
      }
    ]);
  }

  async function save() {
    if (!selectedItemCode || !canUpdateProcurement) {
      return;
    }

    setErrors({});
    setMessage('');
    const request: UpdateProcurementRequiredItemSettingsRequest = {
      rows: rows.map((row) => ({
        ...row,
        itemName: row.itemName.trim()
      })),
      reason: reason.trim() || null
    };

    try {
      const updated = await updateProcurementRequiredItemSettings(developmentUserKey, selectedItemCode, request);
      setState({ kind: 'ready', data: updated });
      const selectedUpdated = updated.find((item) => item.itemCode === selectedItemCode);
      setRows(selectedUpdated?.rows.length ? selectedUpdated.rows.map(cloneProcurementRequiredRow) : defaultProcurementRequiredRows());
      setReason('');
      setMessage('구매 필수 항목 설정을 저장했습니다.');
    } catch (error) {
      handleFormError(error, setErrors, setMessage);
    }
  }

  return (
    <section className="page-surface production-settings-page">
      <div className="page-header">
        <div>
          <p className="eyebrow">Procurement Settings</p>
          <h2>구매 필수 항목 설정</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>구매 페이지</button>
          {canUpdateProcurement ? <button type="button" className="primary-button" onClick={save}>저장</button> : null}
        </div>
      </div>

      <p className="info-text">
        Item별 필수 구매 항목 설정은 이후 새 프로젝트의 구매정보 기본 row와 구매정보 완료 여부 판단에 사용됩니다. 기존 프로젝트 구매 row는 자동으로 변경되지 않습니다.
      </p>

      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">설정할 Item이 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}

      {state.kind === 'ready' ? (
        <>
          <div className="settings-product-type-tabs" role="tablist" aria-label="Item">
            {settings.map((item) => (
              <button
                type="button"
                role="tab"
                aria-selected={item.itemCode === selectedItemCode}
                className={item.itemCode === selectedItemCode ? 'active' : undefined}
                key={item.itemCode}
                onClick={() => selectItem(item.itemCode)}
              >
                {item.itemCode}
              </button>
            ))}
          </div>
          <FormErrorSummary errors={errors} />
          {selected ? (
            <section className="subsection">
              <div className="subsection-header">
                <div>
                  <h3>{selected.itemCode} 필수 구매 항목</h3>
                  <span>{selected.rows.length > 0 ? '현재 Item의 최신 설정입니다.' : '아직 저장된 설정이 없습니다.'}</span>
                </div>
                {canUpdateProcurement ? <button type="button" onClick={addRow}>행 추가</button> : null}
              </div>
              <div className="template-settings-table" role="table" aria-label={`${selected.itemCode} 구매 필수 항목 설정`}>
                <div className="template-settings-head" role="row">
                  <span>순서</span><span>필수 구매 항목</span><span>필수</span><span>사용</span>
                </div>
                {rows.map((row, index) => (
                  <div className="template-settings-row" role="row" key={`${row.templateRowId ?? 'new'}-${index}`}>
                    <div className="grid-field">
                      <input
                        aria-label="순서"
                        name={`rows[${index}].sequenceNumber`}
                        type="number"
                        min={1}
                        value={row.sequenceNumber}
                        className={fieldError(errors, `rows[${index}].sequenceNumber`) ? 'field-invalid' : undefined}
                        onChange={(event) => updateRow(index, { sequenceNumber: Number(event.target.value) })}
                        disabled={!canUpdateProcurement}
                      />
                      <FieldErrorMessage field={`rows[${index}].sequenceNumber`} message={fieldError(errors, `rows[${index}].sequenceNumber`)} />
                    </div>
                    <div className="grid-field">
                      <input
                        aria-label="필수 구매 항목"
                        name={`rows[${index}].itemName`}
                        value={row.itemName}
                        className={fieldError(errors, `rows[${index}].itemName`) ? 'field-invalid' : undefined}
                        onChange={(event) => updateRow(index, { itemName: event.target.value })}
                        disabled={!canUpdateProcurement}
                      />
                      <FieldErrorMessage field={`rows[${index}].itemName`} message={fieldError(errors, `rows[${index}].itemName`)} />
                    </div>
                    <label className="inline-check">
                      <input type="checkbox" checked={row.isRequired} onChange={(event) => updateRow(index, { isRequired: event.target.checked })} disabled={!canUpdateProcurement} />
                      <span>필수</span>
                    </label>
                    <label className="inline-check">
                      <input type="checkbox" checked={row.isActive} onChange={(event) => updateRow(index, { isActive: event.target.checked })} disabled={!canUpdateProcurement} />
                      <span>사용</span>
                    </label>
                  </div>
                ))}
              </div>
              <label className="form-field">
                <span>변경 사유</span>
                <textarea name="reason" value={reason} onChange={(event) => setReason(event.target.value)} disabled={!canUpdateProcurement} />
              </label>
            </section>
          ) : <p className="empty-text">설정할 Item이 없습니다.</p>}
        </>
      ) : null}
      {message ? <p role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</p> : null}
    </section>
  );
}

function cloneProcurementRequiredRow(row: ProcurementRequiredItemSettingsRow): ProcurementRequiredItemSettingsRow {
  return { ...row };
}

function defaultProcurementRequiredRows(): ProcurementRequiredItemSettingsRow[] {
  return [
    { templateRowId: null, sequenceNumber: 1, itemName: '차단기', isRequired: true, isActive: true },
    { templateRowId: null, sequenceNumber: 2, itemName: '외함', isRequired: true, isActive: true },
    { templateRowId: null, sequenceNumber: 3, itemName: '부자재', isRequired: false, isActive: true }
  ];
}

function ProductionPlanningExcelDialog({
  developmentUserKey,
  projectContext,
  onClose,
  onApplied
}: {
  developmentUserKey: string;
  projectContext?: { projectId: string; projectTitle: string };
  onClose: () => void;
  onApplied: () => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProductionPlanningExcelPreviewResponse | null>(null);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [messageTone, setMessageTone] = useState<ActionFeedbackTone>('neutral');
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isApplying, setIsApplying] = useState(false);

  async function runPreview() {
    if (!file) {
      setMessageTone('error');
      setMessage('선택한 파일이 없습니다.');
      return;
    }
    setIsPreviewing(true);
    setMessageTone('loading');
    setMessage('Excel 내용을 확인하는 중입니다.');
    try {
      setPreview(projectContext
        ? await previewProjectProductionPlanningExcel(developmentUserKey, projectContext.projectId, file)
        : await previewProductionPlanningExcel(developmentUserKey, file));
      setMessageTone('success');
      setMessage('미리보기를 완료했습니다. 저장 가능한 항목을 확인해 주세요.');
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPreviewing(false);
    }
  }

  async function apply() {
    if (!file || !preview) {
      setMessageTone('error');
      setMessage('먼저 미리보기를 실행해 주세요.');
      return;
    }
    setIsApplying(true);
    setMessageTone('loading');
    setMessage('저장 가능한 항목을 적용하는 중입니다.');
    try {
      const result = projectContext
        ? await applyProjectProductionPlanningExcel(developmentUserKey, projectContext.projectId, file, preview.fileSha256, reason.trim() || null)
        : await applyProductionPlanningExcel(developmentUserKey, file, preview.fileSha256, reason.trim() || null);
      setMessage(`저장 가능한 항목 ${result.appliedRowCount}건을 반영했습니다.`);
      setMessageTone('success');
      onApplied();
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsApplying(false);
    }
  }

  const canApply = preview !== null && preview.saveableCount > 0 && !isApplying;

  return (
    <DialogBackdrop ariaLabel="생산계획 Excel 업로드" onClose={onClose} closeDisabled={isPreviewing || isApplying}>
      <div className="dialog wide-dialog production-excel-dialog">
        <div className="subsection-header">
          <div>
            <h3>생산계획 Excel 업로드</h3>
            {projectContext ? <p className="muted-text">현재 프로젝트: {projectContext.projectTitle}</p> : null}
          </div>
          <button type="button" onClick={onClose}>닫기</button>
        </div>
        <div className="toolbar">
          <input type="file" accept=".xlsx" onChange={(event) => {
            setFile(event.target.files?.[0] ?? null);
            setPreview(null);
            setMessage('');
            setMessageTone('neutral');
          }} />
          <button type="button" disabled={isPreviewing || isApplying} onClick={runPreview}>{isPreviewing ? '미리보기 중' : 'Preview'}</button>
        </div>
        {preview ? (
          <>
            <div className="excel-preview-action-bar">
              <p className="muted-text">저장 가능한 항목만 반영됩니다. 저장 불가능한 항목은 수정 후 다시 업로드해 주세요.</p>
              <label className="form-field excel-preview-reason">
                <span>수정사유</span>
                <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
              </label>
              <button type="button" className="primary-button" disabled={!canApply} onClick={apply}>{isApplying ? '저장 중' : '저장 가능한 항목 적용'}</button>
              {!canApply ? <p className="warning-text">저장 가능한 생산계획 항목이 없습니다.</p> : null}
            </div>
            <ProductionPlanningExcelPreview preview={preview} />
          </>
        ) : null}
        {message ? <ActionFeedback message={message} tone={messageTone} focusOnAttention /> : null}
      </div>
    </DialogBackdrop>
  );
}

function ProductionPlanningExcelPreview({ preview }: { preview: ProductionPlanningExcelPreviewResponse }) {
  const isMobile = useIsMobileViewport();
  const saveableRows = preview.rows.filter((row) => row.resultType === 'New' || row.resultType === 'Changed' || row.resultType === 'CustomStep');
  const blockedRows = preview.rows.filter((row) => row.resultType === 'NeedsReview' || row.resultType === 'Error');
  const sections = [
    { title: '저장 가능한 데이터 목록', rows: saveableRows, kind: 'saveable' },
    { title: '저장 불가능한 데이터 목록', rows: blockedRows, kind: 'blocked' }
  ];

  if (isMobile) {
    return (
      <div className="excel-preview-cards excel-preview-mobile">
        {sections.map((section) => (
          <section className={`excel-preview-section ${section.kind}`} key={section.title}>
            <h4>{section.title} {section.rows.length}건</h4>
            {section.rows.map((row) => (
              <article className="excel-preview-card" key={`${row.excelRowNumber}-${row.stepName}`}>
                <h3>{row.excelRowNumber}행 · {emptyDash(row.stepName)}</h3>
                <dl className="mobile-detail-list">
                  <div><dt>프로젝트</dt><dd>{emptyDash(row.projectTitle)}</dd></div>
                  <div><dt>Code</dt><dd>{emptyDash(row.projectCode)}</dd></div>
                  <div><dt>Item</dt><dd>{emptyDash(row.productTypeCode)}</dd></div>
                  <div><dt>생산단계</dt><dd>{emptyDash(row.stepName)}{row.isCustomStep ? ' · 사용자 추가' : ''}</dd></div>
                  <div><dt>필수 여부</dt><dd>{row.isRequired === null || row.isRequired === undefined ? '-' : row.isRequired ? '예' : '아니오'}</dd></div>
                  <div><dt>예정일</dt><dd>{emptyDash(row.plannedDate)}</dd></div>
                  <div><dt>비고</dt><dd>{emptyDash(row.note)}</dd></div>
                  {section.kind === 'blocked' ? <div><dt>사유</dt><dd>{row.errorMessages.join(', ')}</dd></div> : null}
                </dl>
              </article>
            ))}
            {section.rows.length === 0 ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
          </section>
        ))}
      </div>
    );
  }

  return (
    <div className="excel-preview-table excel-preview-desktop">
      {sections.map((section) => (
        <section className={`excel-preview-section ${section.kind}`} key={section.title}>
          <h4>{section.title} {section.rows.length}건</h4>
          <div className="excel-preview-grid production-excel-preview-grid" role="table" aria-label={section.title}>
            <div className="excel-preview-head" role="row">
              <span>Excel 행</span>
              <span>프로젝트</span>
              <span>Code</span>
              <span>Item</span>
              <span>생산단계</span>
              <span>필수 여부</span>
              <span>예정일</span>
              <span>비고</span>
            </div>
            {section.rows.map((row) => (
              <div className="excel-preview-row-group" key={`${row.excelRowNumber}-${row.stepName}`}>
                <div className="excel-preview-row" data-result={row.resultType} role="row">
                  <strong>{row.excelRowNumber}</strong>
                  <span>{emptyDash(row.projectTitle)}</span>
                  <span>{emptyDash(row.projectCode)}</span>
                  <span>{emptyDash(row.productTypeCode)}</span>
                  <span>{emptyDash(row.stepName)}{row.isCustomStep ? ' · 사용자 추가' : ''}</span>
                  <span>{row.isRequired === null || row.isRequired === undefined ? '-' : row.isRequired ? '예' : '아니오'}</span>
                  <span>{emptyDash(row.plannedDate)}</span>
                  <span>{emptyDash(row.note)}</span>
                </div>
                {section.kind === 'blocked' ? (
                  <div className="excel-preview-row-reasons">
                    <strong>사유</strong>
                    <ul>
                      {row.errorMessages.map((message) => <li key={message}>{message}</li>)}
                    </ul>
                  </div>
                ) : null}
              </div>
            ))}
          </div>
          {section.rows.length === 0 ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
        </section>
      ))}
    </div>
  );
}

function ProductionPlanningSection({
  developmentUserKey,
  state,
  canUpdateProductionPlanning,
  onEdit
}: {
  developmentUserKey: string;
  state: LoadState<ProductionPlanningResponse>;
  canUpdateProductionPlanning: boolean;
  onEdit: () => void;
}) {
  return (
    <section className="subsection production-planning-section">
      <div className="subsection-header">
        <div>
          <h3>생산계획</h3>
          <span>프로젝트 단위 계획과 담당자 지정</span>
        </div>
        {canUpdateProductionPlanning ? <button type="button" className="primary-button" onClick={onEdit}>생산계획 수정</button> : null}
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">생산계획을 불러올 수 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? <ProductionPlanningReadOnly developmentUserKey={developmentUserKey} plan={state.data} showCalendar /> : null}
    </section>
  );
}

function ProductionPlanningReadOnly({
  developmentUserKey,
  plan,
  showCalendar = false
}: {
  developmentUserKey?: string;
  plan: ProductionPlanningResponse;
  showCalendar?: boolean;
}) {
  const isMobile = useIsMobileViewport();
  const displayItems = sortProductionPlanItems(plan.items);
  const [businessCalendarState, setBusinessCalendarState] = useState<LoadState<BusinessCalendarDay[]>>({ kind: 'empty' });
  const calendarRange = showCalendar ? productionCalendarDateRange(plan.items) : null;
  const calendarDateFrom = calendarRange?.dateFrom;
  const calendarDateTo = calendarRange?.dateTo;

  useEffect(() => {
    if (!showCalendar || !calendarDateFrom || !calendarDateTo) {
      return;
    }

    const controller = new AbortController();
    queueMicrotask(() => {
      if (!controller.signal.aborted) {
        setBusinessCalendarState({ kind: 'loading' });
      }
    });
    getBusinessCalendar(developmentUserKey, {
      countryCode: 'KR',
      from: calendarDateFrom,
      to: calendarDateTo,
      signal: controller.signal
    })
      .then((calendar) => setBusinessCalendarState({ kind: 'ready', data: calendar.days }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setBusinessCalendarState(toLoadError(error, '영업일 정보를 불러올 수 없습니다.'));
        }
      });

    return () => controller.abort();
  }, [calendarDateFrom, calendarDateTo, developmentUserKey, showCalendar]);

  const businessCalendarDays = businessCalendarState.kind === 'ready' ? businessCalendarState.data : [];
  const assigneesByType = new Map(plan.assignees.map((assignee) => [assignee.responsibilityType, assignee]));
  const assigneeName = (responsibilityType: ResponsibilityType) => assigneesByType.get(responsibilityType)?.assignedUserName ?? '-';
  const renderReadonlyDepartmentCard = (group: AssigneeGroupDefinition) => (
    <article className="assignee-card readonly-assignee-card" data-tone={group.tone} aria-label={`${group.title} 담당자`} key={group.title}>
      <h4>{group.title}</h4>
      <dl className="assignee-summary-list">
        <div><dt>정</dt><dd>{assigneeName(group.primary)}</dd></div>
        <div><dt>부</dt><dd>{assigneeName(group.secondary)}</dd></div>
      </dl>
    </article>
  );

  return (
    <div className="production-plan-readonly">
      <div className="panel-info-summary project-workflow-summary">
        <StatusChip label="Item" value={plan.productTypeName ?? plan.productTypeCode ?? '-'} />
        <StatusChip label="계획 상태" value={plan.planStatusLabel} />
        <StatusChip label="필수 일정" value={`${plan.items.filter((item) => item.isRequired && item.plannedDate).length}/${plan.items.filter((item) => item.isRequired).length}`} />
      </div>
      <section className="production-schedule-priority" aria-label="생산계획 일정">
        <div className="production-priority-heading">
          <div><span>PRODUCTION SCHEDULE</span><h4>계획 항목과 일정</h4></div>
          <small>계획 항목과 날짜를 먼저 확인합니다.</small>
        </div>
        {displayItems.length === 0 ? <p className="empty-text">등록된 생산계획 항목이 없습니다.</p> : isMobile ? (
          <div className="procurement-cards">
          {displayItems.map((item) => (
            <article className="procurement-card" key={`${item.templateStepId ?? item.sequenceNumber}`}>
              <h3>{item.sequenceNumber}. {item.stepName}</h3>
              <dl className="mobile-detail-list">
                <div><dt>필수</dt><dd>{item.isRequired ? '예' : '아니오'}</dd></div>
                <div><dt>예정일</dt><dd>{emptyDash(item.plannedDate)}</dd></div>
                <div><dt>비고</dt><dd>{emptyDash(item.note)}</dd></div>
              </dl>
            </article>
          ))}
          </div>
        ) : (
          <div className="production-plan-table procurement-desktop" role="table" aria-label="생산계획 항목">
          <div className="production-plan-head" role="row">
            <span>계획 항목</span><span>필수</span><span>예정일</span><span>비고</span>
          </div>
          {displayItems.map((item) => (
            <div className="production-plan-row" role="row" key={`${item.templateStepId ?? item.sequenceNumber}`}>
              <span>{item.stepName}</span>
              <span>{item.isRequired ? '예' : '아니오'}</span>
              <span>{emptyDash(item.plannedDate)}</span>
              <span>{emptyDash(item.note)}</span>
            </div>
          ))}
          </div>
        )}
        {showCalendar ? (
          <>
          {businessCalendarState.kind !== 'ready' && businessCalendarState.kind !== 'loading' && businessCalendarState.kind !== 'empty' ? <StateMessage state={businessCalendarState} /> : null}
          <ProductionPlanningTimeline items={plan.items} businessCalendarDays={businessCalendarDays} />
          </>
        ) : null}
      </section>
      <section className="production-assignee-summary-section" aria-label="담당자 지정 현황">
        <div className="production-priority-heading">
          <div><span>PROJECT OWNERS</span><h4>부서별 담당자</h4></div>
          <small>정담당자는 실행, 부담당자는 참조·대체 인계 역할입니다.</small>
        </div>
        <div className="assignee-grid readonly-assignee-grid">
          {departmentAssigneeGroups.map(renderReadonlyDepartmentCard)}
          <article className="assignee-card readonly-assignee-card quality-readonly-assignee-card" data-tone="quality" aria-label="품질 담당자">
            <h4>품질</h4>
            <div className="quality-assignee-summary">
              {qualityAssigneeGroups.map((group) => (
                <section className="quality-assignee-summary-stage" key={group.title}>
                  <h5>{group.title}</h5>
                  <dl className="assignee-summary-list">
                    <div><dt>정</dt><dd>{assigneeName(group.primary)}</dd></div>
                    <div><dt>부</dt><dd>{assigneeName(group.secondary)}</dd></div>
                  </dl>
                </section>
              ))}
            </div>
          </article>
        </div>
      </section>
    </div>
  );
}

function ProductionPlanningTimeline({
  items,
  businessCalendarDays = []
}: {
  items: ProductionPlanningResponse['items'];
  businessCalendarDays?: ProductionCalendarBusinessDay[];
}) {
  const isMobile = useIsMobileViewport();
  if (items.length === 0) {
    return null;
  }

  const calendar = buildProductionCalendar(items, businessCalendarDays);
  const stageColumnWidth = getProductionCalendarStageColumnWidth(items);
  const calendarStyle = {
    '--production-calendar-stage-column-width': `${stageColumnWidth}px`
  } as CSSProperties;
  if (calendar.dateColumns.length === 0) {
    return (
      <section className="production-plan-calendar" aria-label="생산계획 캘린더">
        <h4>생산계획 캘린더</h4>
        <p className="empty-text">생산계획 예정일이 입력되면 캘린더가 표시됩니다.</p>
        {calendar.unscheduledItems.length > 0 ? <UnscheduledProductionItems items={calendar.unscheduledItems} /> : null}
      </section>
    );
  }

  return (
    <section className="production-plan-calendar" aria-label="생산계획 캘린더">
      <h4>생산계획 캘린더</h4>
      {isMobile ? (
        <div className="production-calendar-mobile">
          {calendar.dateColumns.map((date) => (
            <article className={dateClassName('production-calendar-card', date)} key={date.date} title={calendarDateTitle(date)}>
              <strong>{date.label} {date.weekday}</strong>
              {date.holidayName ? <small>{formatCalendarHolidayLabel(date)}</small> : null}
              <span>
                {calendar.rows.filter((row) => row.plannedDate === date.date).map((row) => `✓ ${row.stepName}`).join(', ') || '-'}
              </span>
            </article>
          ))}
          {calendar.unscheduledItems.length > 0 ? <UnscheduledProductionItems items={calendar.unscheduledItems} /> : null}
        </div>
      ) : (
        <div className="production-calendar-table-wrap">
          <table className="production-calendar-table" aria-label="생산계획 캘린더 표" style={calendarStyle}>
            <colgroup>
              <col className="production-calendar-stage-col" />
              {calendar.dateColumns.map((date) => (
                <col className="production-calendar-date-col" key={date.date} />
              ))}
            </colgroup>
            <thead>
              <tr>
                <th className="production-calendar-stage-cell" data-testid="production-calendar-stage-header" scope="col">생산단계</th>
                {calendar.dateColumns.map((date) => (
                  <th scope="col" className={dateClassName('production-calendar-date-cell', date)} key={date.date} title={calendarDateTitle(date)}>
                    <span>{date.label}</span>
                    <small>{date.weekday}</small>
                    {date.holidayName ? <small>{formatCalendarHolidayLabel(date)}</small> : null}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {calendar.rows.map((row) => (
                <tr key={`${row.key}-${row.stepName}`}>
                  <th className="production-calendar-stage-cell" data-testid="production-calendar-stage-cell" scope="row">{row.stepName}</th>
                  {calendar.dateColumns.map((date) => (
                    <td className={dateClassName('production-calendar-date-cell', date)} key={date.date} title={calendarDateTitle(date)}>
                      {row.cells[date.date] ? <span aria-label={`${row.stepName} ${date.label} 예정`}>✓</span> : null}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
          {calendar.unscheduledItems.length > 0 ? <UnscheduledProductionItems items={calendar.unscheduledItems} /> : null}
        </div>
      )}
    </section>
  );
}

function UnscheduledProductionItems({ items }: { items: ProductionCalendarUnscheduledItem[] }) {
  return (
    <div className="production-calendar-unscheduled" aria-label="날짜 미입력 생산단계">
      <strong>날짜 미입력 생산단계</strong>
      <ul>
        {items.map((item) => (
          <li className={item.isRequired ? 'missing-required' : undefined} key={item.key}>
            {item.stepName} {item.isRequired ? <span>필수 미입력</span> : <span>미입력</span>}
          </li>
        ))}
      </ul>
    </div>
  );
}

function ProductionPlanningEditPage({
  developmentUserKey,
  projectId,
  canUpdateProductionPlanning,
  onBack,
  onSaved
}: {
  developmentUserKey: string;
  projectId: string;
  canUpdateProductionPlanning: boolean;
  onBack: () => void;
  onSaved: (feedback: ActionFeedbackState) => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [state, setState] = useState<LoadState<ProductionPlanningResponse>>({ kind: 'loading' });
  const [typesState, setTypesState] = useState<LoadState<ProductionProductType[]>>({ kind: 'loading' });
  const [selectedProductTypeId, setSelectedProductTypeId] = useState('');
  const [rows, setRows] = useState<ProductionPlanRowForm[]>([]);
  const [assignees, setAssignees] = useState<ProjectAssigneeForm[]>([]);
  const [notes, setNotes] = useState('');
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const [messageTone, setMessageTone] = useState<ActionFeedbackTone>('neutral');
  const [showExcelDialog, setShowExcelDialog] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);
  const loadRequestIdRef = useRef(0);

  const load = useCallback(() => {
    const requestId = ++loadRequestIdRef.current;
    setProjectState({ kind: 'loading' });
    setState({ kind: 'loading' });
    setTypesState({ kind: 'loading' });
    setErrors({});
    setMessage('');
    setMessageTone('neutral');
    Promise.all([
      getProject(developmentUserKey, projectId),
      getProjectProductionPlanning(developmentUserKey, projectId),
      listProductionProductTypes(developmentUserKey)
    ])
      .then(([project, plan, productTypes]) => {
        if (requestId !== loadRequestIdRef.current) return;
        const projectProductType = findProductTypeForProjectItem(productTypes, project.item);
        setProjectState({ kind: 'ready', data: project });
        setState({ kind: 'ready', data: plan });
        setTypesState({ kind: 'ready', data: productTypes });
        setSelectedProductTypeId(projectProductType?.productTypeId ?? '');
        setRows(plan.items.map(productionPlanItemToForm));
        setAssignees(plan.assignees.map(projectAssigneeToForm));
        setNotes(plan.notes ?? '');
      })
      .catch((error: unknown) => {
        if (requestId !== loadRequestIdRef.current) return;
        setProjectState(toLoadError(error, '프로젝트 정보를 불러오지 못했습니다.'));
        setState(toLoadError(error, '생산계획을 불러올 수 없습니다.'));
        setTypesState(toLoadError(error, 'Item을 불러올 수 없습니다.'));
      });
  }, [developmentUserKey, projectId]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  useEffect(() => {
    if (typesState.kind !== 'ready' || state.kind !== 'ready') {
      return;
    }
    const selected = typesState.data.find((item) => item.productTypeId === selectedProductTypeId);
    if (!selected) {
      return;
    }
    if (state.data.productTypeId === selectedProductTypeId && state.data.items.length > 0) {
      return;
    }
    setRows(selected.steps.map((step) => ({
      itemId: null,
      templateStepId: step.templateStepId,
      sequenceNumber: step.sequenceNumber,
      stepName: step.stepName,
      isRequired: step.isRequired,
      isCustom: false,
      isDeleted: false,
      plannedDate: '',
      note: '',
      rowVersion: 0
    })));
  }, [selectedProductTypeId, state, typesState]);

  if (!canUpdateProductionPlanning) {
    return <section className="page-surface"><StateMessage state={{ kind: 'forbidden', message: '권한이 없습니다.' }} /></section>;
  }

  function updateRow(index: number, next: Partial<ProductionPlanRowForm>) {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, ...next } : row));
  }

  function addCustomRow() {
    setRows((current) => [
      ...current,
      {
        itemId: null,
        templateStepId: null,
        sequenceNumber: current.filter((row) => !row.isDeleted).length + 1,
        stepName: '',
        isRequired: false,
        isCustom: true,
        isDeleted: false,
        plannedDate: '',
        note: '',
        rowVersion: 0
      }
    ]);
  }

  function deleteCustomRow(index: number) {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, isDeleted: true } : row));
  }

  function updateAssignee(index: number, next: Partial<ProjectAssigneeForm>) {
    setAssignees((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, ...next } : row));
  }

  async function save() {
    if (state.kind !== 'ready') {
      return;
    }
    const validation = validateProductionPlanningForm(selectedProductTypeId, rows);
    setErrors(validation);
    setMessage('');
    setMessageTone('neutral');
    if (Object.keys(validation).length > 0) {
      setMessageTone('error');
      setMessage('입력값을 확인하고 첫 번째 오류 항목을 수정해 주세요.');
      return;
    }
    setIsSaving(true);
    try {
      await updateProjectProductionPlanning(developmentUserKey, projectId, {
        productTypeId: selectedProductTypeId || null,
        expectedRowVersion: state.data.rowVersion,
        notes: notes.trim() || null,
        reason: reason.trim() || null,
        items: rows.map((row) => ({
          itemId: row.itemId,
          templateStepId: row.templateStepId,
          stepName: row.stepName,
          sequenceNumber: row.sequenceNumber,
          isRequired: row.isRequired,
          expectedRowVersion: row.rowVersion,
          plannedDate: row.plannedDate || null,
          note: row.note.trim() || null,
          isDeleted: row.isDeleted
        })),
        assignees: assignees.map((assignee) => ({
          responsibilityType: assignee.responsibilityType,
          assigneeId: assignee.assigneeId,
          expectedRowVersion: assignee.rowVersion,
          assignedUserId: assignee.assignedUserId || null,
          note: assignee.note.trim() || null
        }))
      });
      onSaved({ tone: 'success', message: '생산계획을 저장했습니다.' });
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, setErrors, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  async function downloadTemplate() {
    if (!selectedProductTypeId) {
      setMessageTone('error');
      setMessage('Item을 먼저 확인해 주세요.');
      return;
    }
    setIsDownloading(true);
    setMessageTone('loading');
    setMessage('Excel 양식을 생성하는 중입니다.');
    try {
      const template = await downloadProductionPlanningTemplate(developmentUserKey, projectId, selectedProductTypeId);
      const triggered = triggerExcelDownload(template);
      setMessageTone(triggered ? 'success' : 'partial');
      setMessage(triggered
        ? 'Excel 양식을 다운로드했습니다.'
        : 'Excel 양식 생성은 완료됐지만 다운로드를 시작하지 못했습니다. 다시 시도해 주세요.');
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsDownloading(false);
    }
  }

  const plan = state.kind === 'ready' ? state.data : null;
  const productTypes = typesState.kind === 'ready' ? typesState.data : [];
  const project = projectState.kind === 'ready' ? projectState.data : null;
  const selectedProductType = productTypes.find((item) => item.productTypeId === selectedProductTypeId);
  const hasInvalidProjectItem = projectState.kind === 'ready' && typesState.kind === 'ready' && !selectedProductType;
  const initialDataReady = projectState.kind === 'ready' && state.kind === 'ready' && typesState.kind === 'ready';

  return (
    <section className="page-surface production-planning-section">
      {projectState.kind === 'loading' ? <p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p> : null}
      {projectState.kind === 'ready' ? <ProjectContextSummary project={projectState.data} /> : null}
      {projectState.kind !== 'ready' && projectState.kind !== 'loading' ? <StateMessage state={projectState} /> : null}
      <div className="subsection-header">
        <div>
          <p className="eyebrow">Production Planning</p>
          <h2>생산계획 수정</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>상세</button>
          <button type="button" onClick={downloadTemplate} disabled={!initialDataReady || isDownloading || !selectedProductTypeId}>{isDownloading ? '다운로드 중' : 'Excel 양식 다운로드'}</button>
          <button type="button" onClick={() => setShowExcelDialog(true)} disabled={!initialDataReady}>Excel 업로드</button>
          <button type="button" className="primary-button" disabled={!initialDataReady || isSaving || hasInvalidProjectItem} onClick={save}>{isSaving ? '저장 중' : '저장'}</button>
        </div>
      </div>
      {showExcelDialog && project ? (
        <ProductionPlanningExcelDialog
          developmentUserKey={developmentUserKey}
          projectContext={{ projectId, projectTitle: project.projectTitle }}
          onClose={() => setShowExcelDialog(false)}
          onApplied={() => {
            setShowExcelDialog(false);
            void load();
          }}
        />
      ) : null}
      {!initialDataReady && (projectState.kind === 'loading' || state.kind === 'loading' || typesState.kind === 'loading') ? <p className="production-input-lock-note" role="status">프로젝트·생산계획·담당자 정보를 확인하는 동안 입력을 잠갔습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {typesState.kind !== 'ready' && typesState.kind !== 'loading' ? <StateMessage state={typesState} /> : null}
      {plan && typesState.kind === 'ready' ? (
        <fieldset className="production-edit-lock" disabled={!initialDataReady || isSaving}>
          <legend className="sr-only">생산계획 입력</legend>
          <FormErrorSummary errors={errors} />
          <div className="production-edit-controls">
            <div className={fieldError(errors, 'productTypeId') || hasInvalidProjectItem ? 'readonly-field has-error' : 'readonly-field'} data-field="productTypeId" tabIndex={-1}>
              <span>Item</span>
              <strong>{selectedProductType?.code ?? project?.item ?? '-'}</strong>
              {hasInvalidProjectItem ? <small role="alert" className="field-error-message">현재 프로젝트의 Item이 등록된 Item 기준값과 일치하지 않습니다. 프로젝트 정보를 수정한 후 생산계획을 입력해 주세요.</small> : null}
              <FieldErrorMessage field="productTypeId" message={fieldError(errors, 'productTypeId')} />
            </div>
            <label className="form-field">
              <span>비고</span>
              <input name="notes" value={notes} onChange={(event) => setNotes(event.target.value)} />
            </label>
            <label className={fieldError(errors, 'reason') ? 'form-field panel-reason-field has-error' : 'form-field panel-reason-field'}>
              <span>수정사유</span>
              <textarea name="reason" value={reason} onChange={(event) => setReason(event.target.value)} />
              <FieldErrorMessage field="reason" message={fieldError(errors, 'reason')} />
            </label>
          </div>
          <ProductionPlanningEditableList rows={rows} errors={errors} onChange={updateRow} onAddRow={addCustomRow} onDeleteRow={deleteCustomRow} />
          <ProductionAssigneeEditor plan={plan} assignees={assignees} errors={errors} onChange={updateAssignee} />
        </fieldset>
      ) : null}
      {message ? <ActionFeedback message={message} tone={messageTone} focusOnAttention /> : null}
    </section>
  );
}

function ProductionPlanningEditableList({
  rows,
  errors,
  onChange,
  onAddRow,
  onDeleteRow
}: {
  rows: ProductionPlanRowForm[];
  errors: Record<string, string>;
  onChange: (index: number, next: Partial<ProductionPlanRowForm>) => void;
  onAddRow: () => void;
  onDeleteRow: (index: number) => void;
}) {
  const isMobile = useIsMobileViewport();
  const visibleRows = sortProductionPlanItems(rows.filter((row) => !row.isDeleted));
  if (isMobile) {
    return (
      <section className="subsection">
        <div className="subsection-header">
          <div>
            <h3>생산계획표</h3>
            <p>이 화면에서 수정한 단계명과 필수 여부는 현재 프로젝트에만 적용됩니다.</p>
          </div>
          <button type="button" onClick={onAddRow}>행 추가</button>
        </div>
        <div className="procurement-cards">
          {visibleRows.map((row) => {
            const index = rows.indexOf(row);
            return (
            <article className="procurement-card" key={`${row.templateStepId ?? row.itemId ?? row.sequenceNumber}`}>
              <label className={fieldError(errors, `items[${index}].stepName`) ? 'form-field has-error' : 'form-field'}>
                <span>계획 항목</span>
                <input name={`items[${index}].stepName`} value={row.stepName} onChange={(event) => onChange(index, { stepName: event.target.value })} />
                <FieldErrorMessage field={`items[${index}].stepName`} message={fieldError(errors, `items[${index}].stepName`)} />
              </label>
              <label className="checkbox-row">
                <input type="checkbox" checked={row.isRequired} onChange={(event) => onChange(index, { isRequired: event.target.checked })} />
                <span>필수 항목</span>
              </label>
              <label className={fieldError(errors, `items[${index}].plannedDate`) ? 'form-field has-error' : 'form-field'}>
                <span>예정일</span>
                <input name={`items[${index}].plannedDate`} type="date" value={row.plannedDate} onChange={(event) => onChange(index, { plannedDate: event.target.value })} />
                <FieldErrorMessage field={`items[${index}].plannedDate`} message={fieldError(errors, `items[${index}].plannedDate`)} />
              </label>
              <label className={fieldError(errors, `items[${index}].note`) ? 'form-field has-error' : 'form-field'}>
                <span>비고</span>
                <textarea name={`items[${index}].note`} value={row.note} onChange={(event) => onChange(index, { note: event.target.value })} />
                <FieldErrorMessage field={`items[${index}].note`} message={fieldError(errors, `items[${index}].note`)} />
              </label>
              {row.isCustom ? <button type="button" className="secondary-button" onClick={() => onDeleteRow(index)}>삭제</button> : null}
            </article>
            );
          })}
        </div>
      </section>
    );
  }

  return (
    <section className="subsection">
      <div className="subsection-header">
        <div>
          <h3>생산계획표</h3>
          <p>이 화면에서 수정한 단계명과 필수 여부는 현재 프로젝트에만 적용됩니다.</p>
        </div>
        <button type="button" onClick={onAddRow}>행 추가</button>
      </div>
      <div className="production-plan-table procurement-desktop" role="table" aria-label="생산계획 수정">
        <div className="production-plan-head editable" role="row">
          <span>계획 항목</span><span>필수</span><span>예정일</span><span>비고</span><span>작업</span>
        </div>
        {visibleRows.map((row) => {
          const index = rows.indexOf(row);
          return (
            <div className="production-plan-row editable" role="row" key={`${row.templateStepId ?? row.itemId ?? row.sequenceNumber}`}>
              <div className="grid-field">
                <input
                  aria-label="계획 항목"
                  className={fieldError(errors, `items[${index}].stepName`) ? 'field-invalid' : undefined}
                  name={`items[${index}].stepName`}
                  value={row.stepName}
                  onChange={(event) => onChange(index, { stepName: event.target.value })}
                />
                <FieldErrorMessage field={`items[${index}].stepName`} message={fieldError(errors, `items[${index}].stepName`)} />
              </div>
              <label className="checkbox-row">
                <input type="checkbox" checked={row.isRequired} onChange={(event) => onChange(index, { isRequired: event.target.checked })} />
                <span>필수</span>
              </label>
              <div className="grid-field">
                <input
                  className={fieldError(errors, `items[${index}].plannedDate`) ? 'field-invalid' : undefined}
                  name={`items[${index}].plannedDate`}
                  type="date"
                  value={row.plannedDate}
                  onChange={(event) => onChange(index, { plannedDate: event.target.value })}
                />
                <FieldErrorMessage field={`items[${index}].plannedDate`} message={fieldError(errors, `items[${index}].plannedDate`)} />
              </div>
              <div className="grid-field">
                <input
                  className={fieldError(errors, `items[${index}].note`) ? 'field-invalid' : undefined}
                  name={`items[${index}].note`}
                  value={row.note}
                  onChange={(event) => onChange(index, { note: event.target.value })}
                />
                <FieldErrorMessage field={`items[${index}].note`} message={fieldError(errors, `items[${index}].note`)} />
              </div>
              <span>{row.isCustom ? <button type="button" className="secondary-button" onClick={() => onDeleteRow(index)}>삭제</button> : '-'}</span>
            </div>
          );
        })}
      </div>
    </section>
  );
}

function ProductionAssigneeEditor({
  plan,
  assignees,
  errors,
  onChange
}: {
  plan: ProductionPlanningResponse;
  assignees: ProjectAssigneeForm[];
  errors: Record<string, string>;
  onChange: (index: number, next: Partial<ProjectAssigneeForm>) => void;
}) {
  function renderAssigneeSlot(responsibilityType: ResponsibilityType, roleLabel: string) {
    const index = assignees.findIndex((item) => item.responsibilityType === responsibilityType);
    if (index < 0) {
      return null;
    }

    const assignee = assignees[index];
    const candidates = plan.assigneeCandidates.find((item) => item.responsibilityType === responsibilityType)?.users ?? [];
    const assigneeError = fieldError(errors, responsibilityType, responsibilityType[0].toLowerCase() + responsibilityType.slice(1), `assignees[${index}].assignedUserId`);
    return (
      <div className="assignee-role-row" key={responsibilityType}>
        <label className={assigneeError ? 'form-field has-error' : 'form-field'}>
          <span>{roleLabel}</span>
          <select
            aria-label={assignee.responsibilityLabel}
            name={`assignees[${index}].assignedUserId`}
            value={assignee.assignedUserId}
            onChange={(event) => onChange(index, { assignedUserId: event.target.value })}
          >
            <option value="">미지정</option>
            {candidates.map((user) => <option key={user.userId} value={user.userId}>{user.displayName}</option>)}
          </select>
          <FieldErrorMessage message={assigneeError} />
        </label>
      </div>
    );
  }

  function renderAssigneeGroup(group: AssigneeGroupDefinition) {
    return (
      <article className="assignee-card assignee-group-card" data-tone={group.tone} aria-label={`${group.title} 담당자 지정`} key={group.title}>
        <h4>{group.title}</h4>
        {renderAssigneeSlot(group.primary, '정 담당자')}
        {renderAssigneeSlot(group.secondary, '부 담당자')}
      </article>
    );
  }

  return (
    <section className="subsection">
      <h3>프로젝트 담당자 지정</h3>
      <div className="assignee-section" aria-label="부서별 담당자">
        <h4>부서별 담당자</h4>
        <div className="assignee-edit-grid">
          {departmentAssigneeGroups.map(renderAssigneeGroup)}
        </div>
      </div>
      <div className="assignee-section" aria-label="품질 검사 담당자">
        <h4>품질 검사 담당자</h4>
        <div className="assignee-edit-grid quality-assignee-edit-grid">
          {qualityAssigneeGroups.map(renderAssigneeGroup)}
        </div>
      </div>
    </section>
  );
}

function ProductionPlanStatusBadge({ status, label }: { status: string; label: string }) {
  return <span className="status-badge production-plan-status" data-status={status}>{label}</span>;
}

function ProcurementDashboardPage({
  developmentUserKey,
  canUpdateProcurement,
  onBack,
  onOpenSettings,
  onOpenProject,
  onEditProject
}: {
  developmentUserKey: string;
  canUpdateProcurement: boolean;
  onBack: () => void;
  onOpenSettings: () => void;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
}) {
  const [search, setSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [state, setState] = useState<LoadState<ProcurementDashboardResponse>>({ kind: 'loading' });
  const [expandedProjectId, setExpandedProjectId] = useState<string | null>(null);
  const [expandedProcurementState, setExpandedProcurementState] = useState<LoadState<ProcurementResponse>>({ kind: 'empty' });
  const [showExcel, setShowExcel] = useState(false);
  const [downloadMessage, setDownloadMessage] = useState<string | null>(null);
  const [isDownloadingTemplate, setIsDownloadingTemplate] = useState(false);
  const procurementRequestIdRef = useRef(0);
  const isMobile = useIsMobileViewport();
  const procurementVisibleIds = state.kind === 'ready' ? state.data.projects.map((project) => project.projectId) : [];
  const procurementSelection = useSelectedRows(procurementVisibleIds);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getProcurementDashboard(developmentUserKey, search, dateFrom, dateTo)
      .then((response) => {
        setState({ kind: 'ready', data: response });
        setExpandedProjectId((current) => current && response.projects.some((project) => project.projectId === current)
          ? current
          : null);
      })
      .catch((error: unknown) => setState(toLoadError(error, '구매 목록을 불러올 수 없습니다.')));
  }, [dateFrom, dateTo, developmentUserKey, search]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  useEffect(() => {
    const requestId = procurementRequestIdRef.current + 1;
    procurementRequestIdRef.current = requestId;
    if (!expandedProjectId) {
      queueMicrotask(() => setExpandedProcurementState({ kind: 'empty' }));
      return;
    }

    queueMicrotask(() => setExpandedProcurementState({ kind: 'loading' }));
    getProjectProcurement(developmentUserKey, expandedProjectId)
      .then((response) => {
        if (requestId === procurementRequestIdRef.current) {
          setExpandedProcurementState({ kind: 'ready', data: response });
        }
      })
      .catch((error: unknown) => {
        if (requestId === procurementRequestIdRef.current) {
          setExpandedProcurementState(toLoadError(error, '선택 프로젝트 구매정보를 불러올 수 없습니다.'));
        }
      });
  }, [developmentUserKey, expandedProjectId]);

  function toggleExpandedProject(projectId: string) {
    setExpandedProjectId((current) => current === projectId ? null : projectId);
  }

  async function downloadTemplate() {
    setDownloadMessage(null);
    setIsDownloadingTemplate(true);
    try {
      const template = await downloadProcurementDashboardTemplate(developmentUserKey);
      const url = URL.createObjectURL(template.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = template.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
      setDownloadMessage('Excel 양식을 다운로드했습니다.');
    } catch (error) {
      setDownloadMessage(error instanceof ApiError ? error.message : 'Excel 양식을 다운로드할 수 없습니다.');
    } finally {
      setIsDownloadingTemplate(false);
    }
  }

  return (
    <section className={isMobile ? 'page-surface procurement-section mobile-operations-page procurement-mobile-page' : 'page-surface procurement-section'}>
      <div className={isMobile ? 'page-header mobile-operations-header' : 'page-header'}>
        <div>
          <p className="eyebrow">{isMobile ? 'SUPPLY WATCH' : 'Procurement'}</p>
          <h2>구매</h2>
          {isMobile ? <p>입고 지연과 미완료 품목을 먼저 확인하세요.</p> : null}
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>프로젝트 목록</button>
          {canUpdateProcurement && !isMobile ? (
            <>
              <button type="button" onClick={onOpenSettings}>구매 필수 항목 설정</button>
              <button type="button" onClick={downloadTemplate} disabled={isDownloadingTemplate}>
                {isDownloadingTemplate ? '다운로드 중' : 'Excel 양식 다운로드'}
              </button>
              <button type="button" className="primary-button" onClick={() => setShowExcel(true)}>Excel 업로드</button>
            </>
          ) : null}
        </div>
      </div>
      {downloadMessage ? <p className="form-message">{downloadMessage}</p> : null}
      {state.kind === 'ready' && state.data.truncated ? (
        <p className="warning-text" role="status">화면에는 앞선 500개 프로젝트만 표시됩니다. 검색 조건을 좁혀 주세요.</p>
      ) : null}

      {state.kind === 'ready' ? <DashboardKpiGrid summary={state.data.summary} /> : null}
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}

      <form className="toolbar" onSubmit={(event) => { event.preventDefault(); load(); }}>
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="프로젝트명, 고객사, Code, 발주품목 검색" />
        <label className="date-filter-field">
          <span>입고예정 시작일</span>
          <input type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} />
        </label>
        <label className="date-filter-field">
          <span>입고예정 종료일</span>
          <input type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} />
        </label>
        <button type="button" onClick={() => { setDateFrom(''); setDateTo(''); }}>필터 초기화</button>
        <button type="submit">검색</button>
      </form>

      {state.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="procurement"
          visibleIds={procurementVisibleIds}
          selectedIds={procurementSelection.selectedIds}
          allSelected={procurementSelection.allSelected}
          busy={procurementSelection.busy}
          filters={{ search, expectedReceiptDateFrom: dateFrom, expectedReceiptDateTo: dateTo }}
          onBusyChange={procurementSelection.setBusy}
          onToggleAll={procurementSelection.toggleAll}
          onClear={procurementSelection.clear}
        />
      ) : null}

      {state.kind === 'ready' && state.data.projects.length === 0 ? <p className="empty-text">표시할 구매 프로젝트가 없습니다.</p> : null}
      {state.kind === 'ready' && state.data.projects.length > 0 ? (
        <div className="procurement-dashboard-layout">
          {isMobile ? (
            <ProcurementProjectCards
              projects={state.data.projects}
              expandedProjectId={expandedProjectId}
              expandedState={expandedProcurementState}
              canUpdateProcurement={canUpdateProcurement}
              onSelect={toggleExpandedProject}
              onOpenProject={onOpenProject}
              onEditProject={onEditProject}
              selectedIds={procurementSelection.selectedIds}
              selectionBusy={procurementSelection.busy}
              onSelectionChange={procurementSelection.toggle}
            />
          ) : (
            <ProcurementProjectTable
              projects={state.data.projects}
              expandedProjectId={expandedProjectId}
              expandedState={expandedProcurementState}
              canUpdateProcurement={canUpdateProcurement}
              onSelect={toggleExpandedProject}
              onOpenProject={onOpenProject}
              onEditProject={onEditProject}
              selectedIds={procurementSelection.selectedIds}
              selectionBusy={procurementSelection.busy}
              onSelectionChange={procurementSelection.toggle}
            />
          )}
        </div>
      ) : null}

      {showExcel ? (
        <ProcurementExcelDialog
          developmentUserKey={developmentUserKey}
          onClose={() => setShowExcel(false)}
          onApplied={() => {
            setShowExcel(false);
            load();
            if (expandedProjectId) {
              getProjectProcurement(developmentUserKey, expandedProjectId)
                .then((response) => setExpandedProcurementState({ kind: 'ready', data: response }))
                .catch((error: unknown) => setExpandedProcurementState(toLoadError(error, '선택 프로젝트 구매정보를 불러올 수 없습니다.')));
            }
          }}
        />
      ) : null}
    </section>
  );
}

function DashboardKpiGrid({ summary }: { summary: ProcurementDashboardResponse['summary'] }) {
  return (
    <div className="dashboard-kpi-grid" aria-label="구매 요약">
      <DashboardKpiCard title="입고대기품목" value={summary.pendingReceiptCount} helperText="완료 체크되지 않은 구매품목" variant="warning" />
      <DashboardKpiCard title="입고완료품목" value={summary.receiptCompletedCount} helperText="완료 체크된 구매품목" variant="positive" />
      <DashboardKpiCard title="입고예정일 경과 품목" value={summary.pastExpectedReceiptDateCount} helperText="상태가 아닌 날짜 참고값" variant="warning" />
    </div>
  );
}

function DashboardKpiCard({
  title,
  value,
  helperText,
  variant = 'neutral'
}: {
  title: string;
  value: string | number;
  helperText: string;
  variant?: 'neutral' | 'positive' | 'warning';
}) {
  return (
    <article className="dashboard-kpi-card" data-variant={variant}>
      <span>{title}</span>
      <strong>{value}</strong>
      <small>{helperText}</small>
    </article>
  );
}

function ProcurementProjectTable({
  projects,
  expandedProjectId,
  expandedState,
  canUpdateProcurement,
  onSelect,
  onOpenProject,
  onEditProject,
  selectedIds,
  selectionBusy,
  onSelectionChange
}: {
  projects: ProcurementProjectSummary[];
  expandedProjectId: string | null;
  expandedState: LoadState<ProcurementResponse>;
  canUpdateProcurement: boolean;
  onSelect: (projectId: string) => void;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
  selectedIds: ReadonlySet<string>;
  selectionBusy: boolean;
  onSelectionChange: (projectId: string, selected: boolean) => void;
}) {
  return (
    <div className="procurement-project-table procurement-desktop" role="table" aria-label="구매 프로젝트 목록">
      <div className="procurement-project-head" role="row">
        <span>선택</span><span>프로젝트명</span><span>Code</span><span>Item</span><span>면수</span><span>납기일</span><span>구매품목</span><span>입고완료</span>
      </div>
      {projects.map((project) => (
        <Fragment key={project.projectId}>
          <div
            role="row"
            tabIndex={0}
            className={project.projectId === expandedProjectId ? 'procurement-project-row active' : 'procurement-project-row'}
            aria-expanded={project.projectId === expandedProjectId}
            onClick={() => onSelect(project.projectId)}
            onDoubleClick={() => onOpenProject(project.projectId)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                onSelect(project.projectId);
              }
            }}
          >
            <span><SelectionCheckbox checked={selectedIds.has(project.projectId)} disabled={selectionBusy} label={`${project.projectTitle} 선택`} onChange={(checked) => onSelectionChange(project.projectId, checked)} /></span>
            <span>{project.projectTitle}</span>
            <span>{project.projectCode}</span>
            <span>{project.item}</span>
            <span>{project.activePanelCount}면</span>
            <span>{emptyDash(project.deliveryDate)}</span>
            <span>{project.procurementItemCount}건</span>
            <span>{project.receiptCompletedCount}건</span>
          </div>
          {project.projectId === expandedProjectId ? (
            <ProcurementProjectExpanded
              project={project}
              state={expandedState}
              canUpdateProcurement={canUpdateProcurement}
              onOpenProject={onOpenProject}
              onEditProject={onEditProject}
            />
          ) : null}
        </Fragment>
      ))}
    </div>
  );
}

function ProcurementProjectCards({
  projects,
  expandedProjectId,
  expandedState,
  canUpdateProcurement,
  onSelect,
  onOpenProject,
  onEditProject,
  selectedIds,
  selectionBusy,
  onSelectionChange
}: {
  projects: ProcurementProjectSummary[];
  expandedProjectId: string | null;
  expandedState: LoadState<ProcurementResponse>;
  canUpdateProcurement: boolean;
  onSelect: (projectId: string) => void;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
  selectedIds: ReadonlySet<string>;
  selectionBusy: boolean;
  onSelectionChange: (projectId: string, selected: boolean) => void;
}) {
  return (
    <div className="procurement-project-cards procurement-mobile" data-testid="procurement-dashboard-mobile">
      {projects.map((project) => (
        <article key={project.projectId} className={project.projectId === expandedProjectId ? 'procurement-project-card active' : 'procurement-project-card'}>
          <div className="subsection-header">
            <SelectionCheckbox checked={selectedIds.has(project.projectId)} disabled={selectionBusy} label={`${project.projectTitle} 선택`} onChange={(checked) => onSelectionChange(project.projectId, checked)} />
            <div>
              <small>{project.projectCode} · {project.item}</small>
              <h3>{project.projectTitle}</h3>
            </div>
            <span className="mobile-project-dday" data-overdue={project.dDayText.includes('경과')}>{project.dDayText}</span>
          </div>
          <dl className="mobile-priority-grid">
            <div><dt>미완료</dt><dd>{Math.max(project.procurementItemCount - project.receiptCompletedCount, 0)}건</dd></div>
            <div><dt>완료</dt><dd>{project.receiptCompletedCount}/{project.procurementItemCount}</dd></div>
            <div><dt>최근 입고</dt><dd>{emptyDash(project.nearestExpectedReceiptDate)}</dd></div>
          </dl>
          <details className="mobile-card-details">
            <summary>프로젝트 정보</summary>
            <dl className="mobile-detail-list">
              <div><dt>고객사</dt><dd>{project.customerName}</dd></div>
              <div><dt>Code</dt><dd>{project.projectCode}</dd></div>
              <div><dt>Item</dt><dd>{project.item}</dd></div>
            </dl>
          </details>
          <div className="mobile-card-actions">
            <button type="button" onClick={() => onOpenProject(project.projectId)}>프로젝트</button>
            <button
              type="button"
              className="primary-button"
              aria-expanded={project.projectId === expandedProjectId}
              onClick={() => onSelect(project.projectId)}
            >
              {project.projectId === expandedProjectId ? '접기' : '구매정보'}
            </button>
          </div>
          {project.projectId === expandedProjectId ? (
            <ProcurementProjectExpanded
              project={project}
              state={expandedState}
              canUpdateProcurement={canUpdateProcurement}
              onOpenProject={onOpenProject}
              onEditProject={onEditProject}
            />
          ) : null}
        </article>
      ))}
    </div>
  );
}

function ProcurementProjectExpanded({
  project,
  state,
  canUpdateProcurement,
  onOpenProject,
  onEditProject
}: {
  project: ProcurementProjectSummary;
  state: LoadState<ProcurementResponse>;
  canUpdateProcurement: boolean;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
}) {
  return (
    <section className="procurement-project-expanded" aria-label={`${project.projectTitle} 구매정보`}>
      <div className="subsection-header">
        <div>
          <h3>{project.projectTitle}</h3>
          <span>선택한 프로젝트 구매정보</span>
        </div>
        <div className="button-row">
          <button type="button" onClick={() => onOpenProject(project.projectId)}>프로젝트 상세</button>
          {canUpdateProcurement ? <button type="button" className="primary-button" onClick={() => onEditProject(project.projectId)}>구매정보 수정</button> : null}
        </div>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">프로젝트를 선택하세요.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        state.data.items.length === 0
          ? <p className="empty-text">등록된 구매정보가 없습니다.</p>
          : <ProcurementReadOnlyList items={state.data.items} />
      ) : null}
    </section>
  );
}

function ProcurementSection({
  state,
  canUpdateProcurement,
  onEdit
}: {
  state: LoadState<ProcurementResponse>;
  canUpdateProcurement: boolean;
  onEdit: () => void;
}) {
  return (
    <section className="subsection procurement-section">
      <div className="subsection-header">
        <div>
          <h3>구매정보</h3>
          <span>입고예정 정보</span>
        </div>
        {canUpdateProcurement ? <button type="button" className="primary-button" onClick={onEdit}>구매정보 수정</button> : null}
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">등록된 구매정보가 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        state.data.items.length === 0
          ? <p className="empty-text">등록된 구매정보가 없습니다.</p>
          : <ProcurementReadOnlyList items={state.data.items} />
      ) : null}
    </section>
  );
}

function ProcurementReadOnlyList({ items }: { items: ProcurementItem[] }) {
  const isMobile = useIsMobileViewport();
  return isMobile
    ? <ProcurementCards items={items} editable={false} onChange={() => undefined} />
    : (
      <div className="procurement-table procurement-readonly-table procurement-desktop" role="table" aria-label="구매정보">
        <div className="procurement-table-head" role="row">
          <span>통상납기</span>
          <span>발주품목</span>
          <span>업체</span>
          <span>기술 담당자</span>
          <span>발주일</span>
          <span>입고예정일</span>
          <span>이슈사항</span>
          <span>공급 방식</span>
          <span>입고 상태</span>
        </div>
        {items.map((item) => (
          <div className="procurement-table-row" role="row" key={item.itemId}>
            <span>{emptyDash(item.standardLeadTime)}</span>
            <span className="order-item-badge">{emptyDash(item.orderItem)}</span>
            <span>{emptyDash(item.supplierName)}</span>
            <span>{emptyDash(item.technicalOwner)}</span>
            <span>{emptyDash(item.orderDate)}</span>
            <span>{emptyDash(item.expectedReceiptDate)}</span>
            <span>{emptyDash(item.issueNote)}</span>
            <div className="procurement-supply-cell">
              <SupplyTypeBadge supplyType={item.supplyType} />
              {item.supplyType === 'CustomerSupplied' ? <small>{formatSupplyQuantity(item.orderQuantity, item.orderUnit)}</small> : null}
            </div>
            <ReceiptCompletionBadge completed={item.receiptCompleted} completedAtUtc={item.receiptCompletedAtUtc} />
          </div>
        ))}
      </div>
    );
}

function ReceiptCompletionBadge({ completed, completedAtUtc }: { completed: boolean; completedAtUtc?: string | null }) {
  return (
    <span className="receipt-completion-badge" data-completed={completed ? 'true' : 'false'}>
      {formatReceiptCompleted(completed, completedAtUtc)}
    </span>
  );
}

function SupplyTypeBadge({ supplyType }: { supplyType: ProcurementSupplyType }) {
  return <span className="supply-type-badge" data-supply-type={supplyType}>{supplyType === 'CustomerSupplied' ? '사급 · 고객 제공' : '일반 구매'}</span>;
}

function ProcurementEditPage({
  developmentUserKey,
  projectId,
  canUpdateProcurement,
  onBack,
  onSaved
}: {
  developmentUserKey: string;
  projectId: string;
  canUpdateProcurement: boolean;
  onBack: () => void;
  onSaved: (feedback: ActionFeedbackState) => void;
}) {
  const [state, setState] = useState<LoadState<ProcurementResponse>>({ kind: 'loading' });
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [rows, setRows] = useState<ProcurementRowForm[]>([]);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [messageTone, setMessageTone] = useState<ActionFeedbackTone>('neutral');
  const [isSaving, setIsSaving] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);
  const [showExcel, setShowExcel] = useState(false);
  const loadRequestIdRef = useRef(0);

  const load = useCallback(() => {
    const requestId = loadRequestIdRef.current + 1;
    loadRequestIdRef.current = requestId;
    setState({ kind: 'loading' });
    setProjectState({ kind: 'loading' });
    Promise.all([
      getProjectProcurement(developmentUserKey, projectId),
      getProject(developmentUserKey, projectId)
    ])
      .then(([response, project]) => {
        if (requestId !== loadRequestIdRef.current) {
          return;
        }

        setState({ kind: 'ready', data: response });
        setProjectState({ kind: 'ready', data: project });
        setRows(response.items.map(procurementItemToForm));
      })
      .catch((error: unknown) => {
        if (requestId !== loadRequestIdRef.current) {
          return;
        }

        setState(toLoadError(error, '구매정보를 불러올 수 없습니다.'));
        setProjectState(toLoadError(error, '프로젝트 정보를 불러오지 못했습니다.'));
      });
  }, [developmentUserKey, projectId]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  if (!canUpdateProcurement) {
    return <section className="page-surface"><StateMessage state={{ kind: 'forbidden', message: '권한이 없습니다.' }} /></section>;
  }

  function updateRow(index: number, next: Partial<ProcurementRowForm>) {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, ...next } : row));
  }

  function addRow() {
    const projectDeliveryDate = state.kind === 'ready' ? state.data.projectDeliveryDate : null;
    setRows((current) => [...current, emptyProcurementRow(projectDeliveryDate)]);
  }

  async function save() {
    const invalidCustomerSupply = rows.find((row) => row.supplyType === 'CustomerSupplied'
      && (!(Number(row.orderQuantity) > 0) || row.orderUnit.trim().length < 1 || row.orderUnit.trim().length > 20));
    if (invalidCustomerSupply) {
      setMessageTone('error');
      setMessage('사급 품목은 제공 예정 수량과 1~20자 단위를 함께 입력해 주세요.');
      return;
    }
    setIsSaving(true);
    setMessage('');
    setMessageTone('loading');
    try {
      const response = await updateProjectProcurement(developmentUserKey, projectId, {
        reason: reason.trim() || null,
        items: rows.map(procurementFormToRequest)
      });
      setState({ kind: 'ready', data: response });
      setRows(response.items.map(procurementItemToForm));
      setReason('');
      onSaved({ tone: 'success', message: '구매정보를 저장했습니다.' });
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  async function downloadTemplate() {
    setIsDownloading(true);
    setMessageTone('loading');
    setMessage('Excel 양식을 생성하는 중입니다.');
    try {
      const template = await downloadProcurementTemplate(developmentUserKey, projectId);
      const triggered = triggerExcelDownload(template);
      setMessageTone(triggered ? 'success' : 'partial');
      setMessage(triggered
        ? 'Excel 양식을 다운로드했습니다.'
        : 'Excel 양식 생성은 완료됐지만 다운로드를 시작하지 못했습니다. 다시 시도해 주세요.');
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsDownloading(false);
    }
  }

  return (
    <section className="page-surface procurement-section">
      {projectState.kind === 'loading' ? <p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p> : null}
      {projectState.kind === 'ready' ? <ProjectContextSummary project={projectState.data} /> : null}
      {projectState.kind !== 'ready' && projectState.kind !== 'loading' ? <StateMessage state={projectState} /> : null}
      <div className="subsection-header">
        <div>
          <p className="eyebrow">Procurement</p>
          <h2>구매정보 수정</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>상세</button>
          <button type="button" onClick={addRow}>행 추가</button>
          <button type="button" onClick={downloadTemplate} disabled={isDownloading}>{isDownloading ? '다운로드 중' : 'Excel 양식 다운로드'}</button>
          <button type="button" onClick={() => setShowExcel(true)}>Excel 업로드</button>
          <button type="button" className="primary-button" disabled={isSaving} onClick={save}>{isSaving ? '저장 중' : '저장'}</button>
        </div>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <>
          <label className="form-field panel-reason-field">
            <span>수정사유</span>
            <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
          </label>
          <ProcurementEditableList rows={rows} onChange={updateRow} />
        </>
      ) : null}
      {message ? <ActionFeedback message={message} tone={messageTone} focusOnAttention /> : null}
      {showExcel ? (
        <ProcurementExcelDialog
          developmentUserKey={developmentUserKey}
          onClose={() => setShowExcel(false)}
          onApplied={() => {
            setShowExcel(false);
            onSaved({ tone: 'success', message: '구매 Excel 변경사항을 적용했습니다.' });
          }}
        />
      ) : null}
    </section>
  );
}

function ProcurementEditableList({
  rows,
  onChange
}: {
  rows: ProcurementRowForm[];
  onChange: (index: number, next: Partial<ProcurementRowForm>) => void;
}) {
  const isMobile = useIsMobileViewport();
  return isMobile
    ? <ProcurementCards items={rows} editable onChange={onChange} />
    : (
      <div className="procurement-table procurement-desktop" role="table" aria-label="구매정보 수정">
        <div className="procurement-table-head editable" role="row">
          <span>통상납기</span>
          <span>발주품목</span>
          <span>업체</span>
          <span>기술 담당자</span>
          <span>발주일</span>
          <span>입고예정일</span>
          <span>이슈사항</span>
          <span>공급 방식</span>
          <span>입고 완료</span>
        </div>
        {rows.map((row, index) => (
          <div className="procurement-table-row editable" role="row" key={row.itemId ?? `new-${index}`}>
            <input value={row.standardLeadTime} onChange={(event) => onChange(index, { standardLeadTime: event.target.value })} />
            <input className="order-item-input" value={row.orderItem} onChange={(event) => onChange(index, { orderItem: event.target.value })} />
            <input value={row.supplierName} onChange={(event) => onChange(index, { supplierName: event.target.value })} />
            <input value={row.technicalOwner} onChange={(event) => onChange(index, { technicalOwner: event.target.value })} />
            <input type="date" value={row.orderDate} onChange={(event) => onChange(index, { orderDate: event.target.value })} />
            <input type="date" value={row.expectedReceiptDate} onChange={(event) => onChange(index, { expectedReceiptDate: event.target.value })} />
            <input value={row.issueNote} onChange={(event) => onChange(index, { issueNote: event.target.value })} />
            <div className="procurement-supply-editor">
              <select aria-label="공급 방식" value={row.supplyType} onChange={(event) => onChange(index, { supplyType: event.target.value as ProcurementSupplyType })}>
                <option value="Purchased">일반 구매</option>
                <option value="CustomerSupplied">사급 · 고객 제공</option>
              </select>
              {row.supplyType === 'CustomerSupplied' ? (
                <div className="procurement-supply-measurement">
                  <input aria-label="제공 예정 수량" inputMode="decimal" value={row.orderQuantity} onChange={(event) => onChange(index, { orderQuantity: event.target.value })} placeholder="예정 수량" />
                  <input aria-label="제공 예정 단위" value={row.orderUnit} onChange={(event) => onChange(index, { orderUnit: event.target.value })} placeholder="단위" maxLength={20} />
                </div>
              ) : <small>도착 등록 시 수량 입력</small>}
            </div>
            <div className="receipt-input-cell receipt-input-cell--derived">
              <ReceiptCompletionBadge completed={row.receiptCompleted} completedAtUtc={row.receiptCompletedAtUtc} />
              <small>자재 입고에서 자동 계산</small>
            </div>
          </div>
        ))}
      </div>
    );
}

function ProcurementCards({
  items,
  editable,
  onChange
}: {
  items: ProcurementItem[] | ProcurementRowForm[];
  editable: boolean;
  onChange: (index: number, next: Partial<ProcurementRowForm>) => void;
}) {
  return (
    <div className="procurement-cards procurement-mobile" data-testid="procurement-mobile">
      {items.map((item, index) => {
        const row = isProcurementForm(item) ? item : procurementItemToForm(item);
        return (
          <article className="procurement-card" key={row.itemId ?? `new-${index}`}>
            {editable ? (
              <>
                <FormField label="발주품목"><input className="order-item-input" value={row.orderItem} onChange={(event) => onChange(index, { orderItem: event.target.value })} /></FormField>
                <FormField label="업체"><input value={row.supplierName} onChange={(event) => onChange(index, { supplierName: event.target.value })} /></FormField>
                <FormField label="기술 담당자"><input value={row.technicalOwner} onChange={(event) => onChange(index, { technicalOwner: event.target.value })} /></FormField>
                <FormField label="통상납기"><input value={row.standardLeadTime} onChange={(event) => onChange(index, { standardLeadTime: event.target.value })} /></FormField>
                <FormField label="발주일"><input type="date" value={row.orderDate} onChange={(event) => onChange(index, { orderDate: event.target.value })} /></FormField>
                <FormField label="입고예정일"><input type="date" value={row.expectedReceiptDate} onChange={(event) => onChange(index, { expectedReceiptDate: event.target.value })} /></FormField>
                <div className="readonly-field"><span>프로젝트 납품예정일</span><strong>{emptyDash(row.shipmentDisplayDate)}</strong></div>
                <FormField label="이슈사항"><input value={row.issueNote} onChange={(event) => onChange(index, { issueNote: event.target.value })} /></FormField>
                <FormField label="공급 방식">
                  <select value={row.supplyType} onChange={(event) => onChange(index, { supplyType: event.target.value as ProcurementSupplyType })}>
                    <option value="Purchased">일반 구매</option>
                    <option value="CustomerSupplied">사급 · 고객 제공</option>
                  </select>
                </FormField>
                {row.supplyType === 'CustomerSupplied' ? (
                  <div className="mobile-supply-measurement">
                    <FormField label="제공 예정 수량"><input inputMode="decimal" value={row.orderQuantity} onChange={(event) => onChange(index, { orderQuantity: event.target.value })} /></FormField>
                    <FormField label="단위"><input value={row.orderUnit} onChange={(event) => onChange(index, { orderUnit: event.target.value })} maxLength={20} /></FormField>
                  </div>
                ) : null}
                <div className="receipt-input-cell receipt-input-cell--derived">
                  <ReceiptCompletionBadge completed={row.receiptCompleted} completedAtUtc={row.receiptCompletedAtUtc} />
                  <small>자재 입고 흐름에서 자동 계산</small>
                </div>
              </>
            ) : (
              <>
                <div className="mobile-card-title-row">
                  <h3 className="order-item-badge">{emptyDash(row.orderItem)}</h3>
                  <SupplyTypeBadge supplyType={row.supplyType} />
                </div>
                <dl className="mobile-priority-grid mobile-priority-grid--procurement">
                  <div><dt>입고예정</dt><dd>{emptyDash(row.expectedReceiptDate)}</dd></div>
                  <div><dt>입고 상태</dt><dd><ReceiptCompletionBadge completed={row.receiptCompleted} completedAtUtc={row.receiptCompletedAtUtc} /></dd></div>
                  <div><dt>{row.supplyType === 'CustomerSupplied' ? '제공 예정' : '업체'}</dt><dd>{row.supplyType === 'CustomerSupplied' ? formatSupplyQuantity(Number(row.orderQuantity) || null, row.orderUnit || null) : emptyDash(row.supplierName)}</dd></div>
                </dl>
                {row.issueNote ? <p className="mobile-card-alert">{row.issueNote}</p> : null}
                <details className="mobile-card-details">
                  <summary>발주 상세</summary>
                  <dl className="mobile-detail-list">
                    <div><dt>기술 담당자</dt><dd>{emptyDash(row.technicalOwner)}</dd></div>
                    <div><dt>업체</dt><dd>{emptyDash(row.supplierName)}</dd></div>
                    <div><dt>통상납기</dt><dd>{emptyDash(row.standardLeadTime)}</dd></div>
                    <div><dt>발주일</dt><dd>{emptyDash(row.orderDate)}</dd></div>
                    {row.issueNote ? <div><dt>이슈사항</dt><dd>{row.issueNote}</dd></div> : null}
                  </dl>
                </details>
              </>
            )}
          </article>
        );
      })}
    </div>
  );
}

function ProcurementExcelDialog({
  developmentUserKey,
  onClose,
  onApplied
}: {
  developmentUserKey: string;
  onClose: () => void;
  onApplied: () => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProcurementExcelPreviewResponse | null>(null);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [messageTone, setMessageTone] = useState<ActionFeedbackTone>('neutral');
  const [selections, setSelections] = useState<Record<number, string>>({});
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const [actionBarOffset, setActionBarOffset] = useState(144);
  const actionBarRef = useRef<HTMLDivElement | null>(null);

  const selectionArray = Object.entries(selections)
    .filter(([, projectId]) => projectId)
    .map(([sourceGroupSequence, projectId]) => ({ sourceGroupSequence: Number(sourceGroupSequence), projectId }));

  async function runPreview() {
    if (!file) {
      setMessageTone('error');
      setMessage('Excel 파일을 선택하세요.');
      return;
    }

    setIsPreviewing(true);
    setMessageTone('loading');
    setMessage('Excel 내용을 확인하는 중입니다.');
    try {
      setPreview(await previewProcurementExcel(developmentUserKey, file, selectionArray));
      setMessageTone('success');
      setMessage('미리보기를 완료했습니다. 저장 가능한 항목을 확인해 주세요.');
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPreviewing(false);
    }
  }

  async function apply() {
    if (!file || !preview) {
      return;
    }

    setIsApplying(true);
    setMessageTone('loading');
    setMessage('저장 가능한 구매 항목을 적용하는 중입니다.');
    try {
      await applyProcurementExcel(
        developmentUserKey,
        file,
        preview.fileSha256,
        reason.trim() || null,
        selectionArray,
        preview.expectedVersions);
      setMessageTone('success');
      onApplied();
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsApplying(false);
    }
  }

  const canApply = preview !== null
    && preview.newCount + preview.changedCount > 0
    && (!preview.reasonRequired || reason.trim().length > 0)
    && !isApplying;
  const applyDisabledReason = procurementApplyDisabledReason(preview, file, reason, isApplying);

  useEffect(() => {
    const actionBar = actionBarRef.current;
    if (!preview || !actionBar) {
      setActionBarOffset(144);
      return;
    }

    const updateOffset = () => setActionBarOffset(Math.ceil(actionBar.getBoundingClientRect().height + 16));
    updateOffset();

    if (typeof ResizeObserver === 'undefined') {
      return undefined;
    }

    const observer = new ResizeObserver(updateOffset);
    observer.observe(actionBar);
    return () => observer.disconnect();
  }, [preview, reason]);

  return (
    <DialogBackdrop ariaLabel="구매 Excel 업로드" onClose={onClose} closeDisabled={isPreviewing || isApplying}>
      <div
        className="dialog wide-dialog"
        style={{ '--excel-action-bar-offset': `${actionBarOffset}px` } as React.CSSProperties}
      >
        <div className="subsection-header">
          <h3>구매 Excel 업로드</h3>
          <button type="button" onClick={onClose}>닫기</button>
        </div>
        <div className="toolbar">
          <input type="file" accept=".xlsx" onChange={(event) => {
            setFile(event.target.files?.[0] ?? null);
            setPreview(null);
            setMessage('');
            setMessageTone('neutral');
          }} />
          <button type="button" disabled={isPreviewing || isApplying} onClick={runPreview}>{isPreviewing ? '미리보기 중' : 'Preview'}</button>
        </div>
        {preview ? (
          <>
            <div className="excel-preview-action-bar" ref={actionBarRef}>
              {(preview.errorCount > 0 || preview.needsReviewCount > 0) && preview.newCount + preview.changedCount > 0 ? (
                <p className="warning-text">저장 가능한 항목만 반영됩니다. 저장 불가능한 항목은 수정 후 다시 업로드해 주세요.</p>
              ) : null}
              {preview.reasonRequired ? (
                <label className="form-field excel-preview-reason">
                  <span>수정사유*</span>
                  <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
                </label>
              ) : null}
              <button type="button" className="primary-button" disabled={!canApply} onClick={apply}>{isApplying ? '저장 중' : '저장 가능한 항목 적용'}</button>
              {!canApply && applyDisabledReason ? <p className="warning-text">{applyDisabledReason}</p> : null}
            </div>
            <div className="procurement-match-list">
              {preview.projectMatches.map((match) => (
                <div className="procurement-match-card" key={match.sourceGroupSequence}>
                  <strong>{match.excelProjectTitle ?? '-'}</strong>
                  <span>{procurementMatchStatusLabel(match.matchStatus)}</span>
                  {match.matchStatus !== 'Matched' && match.candidates.length > 0 ? (
                    <select
                      value={selections[match.sourceGroupSequence] ?? ''}
                      onChange={(event) => setSelections((current) => ({ ...current, [match.sourceGroupSequence]: event.target.value }))}
                    >
                      <option value="">프로젝트 선택</option>
                      {match.candidates.map((candidate) => (
                        <option key={candidate.projectId} value={candidate.projectId}>{candidate.projectTitle} ({candidate.projectCode})</option>
                      ))}
                    </select>
                  ) : <span>{match.matchedProjectTitle ?? '-'}</span>}
                </div>
              ))}
            </div>
            <ProcurementPreview rows={preview.rows} />
          </>
        ) : null}
        {message ? <ActionFeedback message={message} tone={messageTone} focusOnAttention /> : null}
      </div>
    </DialogBackdrop>
  );
}

function ProcurementPreview({ rows }: { rows: ProcurementExcelPreviewResponse['rows'] }) {
  const isMobile = useIsMobileViewport();
  const saveableRows = rows.filter((row) => row.resultType === 'New' || row.resultType === 'Changed');
  const blockedRows = rows.filter((row) => row.resultType === 'NeedsReview' || row.resultType === 'Error');
  const sections = [
    { title: '저장 가능한 데이터 목록', rows: saveableRows, kind: 'saveable' },
    { title: '저장 불가능한 데이터 목록', rows: blockedRows, kind: 'blocked' }
  ];

  if (isMobile) {
    return (
      <div className="excel-preview-cards excel-preview-mobile">
        {sections.map((section) => (
          <section className={`excel-preview-section ${section.kind}`} key={section.title}>
            <h4>{section.title} {section.rows.length}건</h4>
            {section.rows.map((row) => (
              <article className="excel-preview-card" key={`${row.excelRowNumber}-${row.sourceGroupSequence}`}>
                <div className="subsection-header">
                  <h3>Row {row.excelRowNumber || '-'}</h3>
                  <span className={row.resultType === 'Error' || row.resultType === 'NeedsReview' ? 'negative-text' : undefined}>{procurementResultLabel(row.resultType)}</span>
                </div>
                <dl className="mobile-detail-list">
                  <div><dt>프로젝트</dt><dd>{emptyDash(row.sourceProjectText)}</dd></div>
                  <div><dt>Code</dt><dd>{emptyDash(row.sourceProjectCodeText)}</dd></div>
                  <div><dt>통상납기</dt><dd>{emptyDash(row.standardLeadTime)}</dd></div>
                  <div><dt>발주품목</dt><dd className="order-item-badge">{emptyDash(row.orderItem)}</dd></div>
                  <div><dt>업체</dt><dd>{emptyDash(row.supplierName)}</dd></div>
                  <div><dt>기술 담당자</dt><dd>{emptyDash(row.technicalOwner)}</dd></div>
                  <div><dt>발주일</dt><dd>{emptyDash(row.orderDate)}</dd></div>
                  <div><dt>입고예정일</dt><dd>{emptyDash(row.expectedReceiptDate)}</dd></div>
                  <div><dt>이슈사항</dt><dd>{emptyDash(row.issueNote)}</dd></div>
                  <div><dt>입고 완료</dt><dd>{formatReceiptCompleted(row.receiptCompleted)}</dd></div>
                  {section.kind === 'blocked' ? (
                    <div className="excel-preview-row-reasons">
                      <strong>사유</strong>
                      <dl>
                        <div><dt>필드</dt><dd>{issueFieldText(row)}</dd></div>
                        <div><dt>입력값</dt><dd>{issueInputText(row)}</dd></div>
                        <div><dt>문제</dt><dd>{issueProblemText(row)}</dd></div>
                      </dl>
                    </div>
                  ) : null}
                </dl>
              </article>
            ))}
            {section.rows.length === 0 ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
          </section>
        ))}
      </div>
    );
  }

  return (
    <div className="excel-preview-table excel-preview-desktop">
      <section className="excel-preview-section saveable" key="saveable">
        <h4>저장 가능한 데이터 목록 {saveableRows.length}건</h4>
        <div className="excel-preview-grid saveable" role="table" aria-label="저장 가능한 데이터 목록">
          <div className="excel-preview-head" role="row">
            <span>Excel 행</span>
            <span>프로젝트</span>
            <span>Code</span>
            <span>통상납기</span>
            <span>발주품목</span>
            <span>업체</span>
            <span>기술 담당자</span>
            <span>발주일</span>
            <span>입고예정일</span>
            <span>이슈사항</span>
            <span>입고 완료</span>
          </div>
          {saveableRows.map((row) => (
            <div className="excel-preview-row" data-result={row.resultType} role="row" key={`${row.excelRowNumber}-${row.sourceGroupSequence}`}>
              <strong>{row.excelRowNumber || '-'}</strong>
              <span>{emptyDash(row.sourceProjectText)}</span>
              <span>{emptyDash(row.sourceProjectCodeText)}</span>
              <span>{emptyDash(row.standardLeadTime)}</span>
              <span className="order-item-badge">{emptyDash(row.orderItem)}</span>
              <span>{emptyDash(row.supplierName)}</span>
              <span>{emptyDash(row.technicalOwner)}</span>
              <span>{emptyDash(row.orderDate)}</span>
              <span>{emptyDash(row.expectedReceiptDate)}</span>
              <span>{emptyDash(row.issueNote)}</span>
              <span>{formatReceiptCompleted(row.receiptCompleted)}</span>
            </div>
          ))}
        </div>
        {saveableRows.length === 0 ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
      </section>
      <section className="excel-preview-section blocked" key="blocked">
        <h4>저장 불가능한 데이터 목록 {blockedRows.length}건</h4>
        <div className="excel-preview-grid blocked" role="table" aria-label="저장 불가능한 데이터 목록">
          <div className="excel-preview-head" role="row">
            <span>Excel 행</span>
            <span>프로젝트</span>
            <span>Code</span>
            <span>통상납기</span>
            <span>발주품목</span>
            <span>업체</span>
            <span>기술 담당자</span>
            <span>발주일</span>
            <span>입고예정일</span>
            <span>이슈사항</span>
            <span>입고 완료</span>
          </div>
          {blockedRows.map((row) => (
            <div className="excel-preview-row-group" key={`${row.excelRowNumber}-${row.sourceGroupSequence}`}>
              <div className="excel-preview-row" data-result={row.resultType} role="row">
                <strong>{row.excelRowNumber || '-'}</strong>
                <span>{emptyDash(row.sourceProjectText)}</span>
                <span>{emptyDash(row.sourceProjectCodeText)}</span>
                <span>{emptyDash(row.standardLeadTime)}</span>
                <span className="order-item-badge">{emptyDash(row.orderItem)}</span>
                <span>{emptyDash(row.supplierName)}</span>
                <span>{emptyDash(row.technicalOwner)}</span>
                <span>{emptyDash(row.orderDate)}</span>
                <span>{emptyDash(row.expectedReceiptDate)}</span>
                <span>{emptyDash(row.issueNote)}</span>
                <span>{formatReceiptCompleted(row.receiptCompleted)}</span>
              </div>
              <div className="excel-preview-row-reasons">
                <strong>사유</strong>
                <dl>
                  <div><dt>필드</dt><dd>{issueFieldText(row)}</dd></div>
                  <div><dt>입력값</dt><dd>{issueInputText(row)}</dd></div>
                  <div><dt>문제</dt><dd>{issueProblemText(row)}</dd></div>
                </dl>
              </div>
            </div>
          ))}
        </div>
        {blockedRows.length === 0 ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
      </section>
    </div>
  );
}

function formatReceiptCompleted(value: boolean | null, completedAtUtc?: string | null) {
  if (value === null) {
    return '-';
  }

  if (!value) {
    return '미완료';
  }

  const completedAt = formatReceiptCompletedAt(completedAtUtc);
  return completedAt ? `완료(${completedAt})` : '완료';
}

function formatReceiptCompletedAt(value?: string | null) {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return `${date.getMonth() + 1}/${date.getDate()} ${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
}

function formatSupplyQuantity(quantity: number | null, unit: string | null) {
  return quantity === null ? '예정량 미입력' : `${quantity.toLocaleString('ko-KR', { maximumFractionDigits: 3 })} ${unit ?? ''}`.trim();
}

function procurementResultLabel(resultType: string) {
  switch (resultType) {
    case 'New': return '신규';
    case 'Changed': return '변경';
    case 'Unchanged': return '동일';
    case 'Skipped': return '건너뜀';
    case 'MissingFromUpload': return '업로드 누락';
    case 'NeedsReview': return '확인 필요';
    case 'Error': return '오류';
    default: return resultType;
  }
}

function procurementMatchStatusLabel(status: string) {
  switch (status) {
    case 'Matched': return '매칭 완료';
    case 'NeedsReview': return '확인 필요';
    case 'Unmatched': return '등록되지 않은 프로젝트';
    case 'Error': return '오류';
    default: return status;
  }
}

// Legacy pre-TASK-008A renderer retained temporarily for isolated fixture compatibility.
// eslint-disable-next-line @typescript-eslint/no-unused-vars
function MaterialReceiptsPage({
  developmentUserKey,
  canAccessMaterialReceipt,
  canUpdateMaterialReceipt,
  onBack
}: {
  developmentUserKey: string;
  canAccessMaterialReceipt: boolean;
  canUpdateMaterialReceipt: boolean;
  onBack: () => void;
}) {
  const [search, setSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [state, setState] = useState<LoadState<ProcurementItem[]>>({ kind: 'loading' });
  const [items, setItems] = useState<ProcurementItem[]>([]);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [includeCompleted, setIncludeCompleted] = useState(false);
  const isMobile = useIsMobileViewport();

  const load = useCallback(() => {
    if (!canAccessMaterialReceipt) {
      setState({ kind: 'forbidden', message: '권한이 없습니다.' });
      return;
    }

    setState({ kind: 'loading' });
    getMaterialReceipts(developmentUserKey, search, includeCompleted, dateFrom, dateTo)
      .then((response) => {
        const legacyItems = response.items as unknown as ProcurementItem[];
        setItems(legacyItems);
        setState(legacyItems.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: legacyItems });
      })
      .catch((error: unknown) => setState(toLoadError(error, '자재 입고 처리 항목을 불러올 수 없습니다.')));
  }, [canAccessMaterialReceipt, dateFrom, dateTo, developmentUserKey, includeCompleted, search]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  if (!canAccessMaterialReceipt) {
    return <section className="page-surface"><StateMessage state={{ kind: 'forbidden', message: '권한이 없습니다.' }} /></section>;
  }

  function setReceipt(itemId: string, next: Partial<ProcurementItem>) {
    setItems((current) => current.map((item) => item.itemId === itemId ? { ...item, ...next } : item));
  }

  async function save() {
    setMessage('');
    try {
      await updateMaterialReceipts(developmentUserKey, {
        reason: reason.trim() || null,
        items: items.map((item) => ({
          itemId: item.itemId,
          expectedRowVersion: item.rowVersion,
          receiptCompleted: item.receiptCompleted,
          receiptCompletedAtUtc: item.receiptCompletedAtUtc,
          receiptCompletionNote: item.receiptCompletionNote
        }))
      });
      setReason('');
      onBack();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    }
  }

  return (
    <section className="page-surface procurement-section">
      <div className="subsection-header">
        <div>
          <p className="eyebrow">Materials</p>
          <h2>자재 입고 처리</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>프로젝트 목록</button>
          {canUpdateMaterialReceipt ? <button type="button" className="primary-button" onClick={save}>저장</button> : null}
        </div>
      </div>
      <form className="toolbar" onSubmit={(event) => { event.preventDefault(); load(); }}>
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="프로젝트 또는 발주품목 검색" />
        <label className="date-filter-field">
          <span>입고예정 시작일</span>
          <input type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} />
        </label>
        <label className="date-filter-field">
          <span>입고예정 종료일</span>
          <input type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} />
        </label>
        <button type="button" onClick={() => { setDateFrom(''); setDateTo(''); }}>필터 초기화</button>
        <button type="submit">검색</button>
      </form>
      <div className="inline-help-row">
        <p className="muted-text">
          현재 구매품목 입고 처리 대상만 표시됩니다. 완료된 항목은 저장 후 기본 목록에서 사라집니다.
          {!canUpdateMaterialReceipt ? ' System Administrator 조회 접근이며 입고 처리는 기존 담당 권한이 필요합니다.' : ''}
        </p>
        <label className="checkbox-field">
          <input
            type="checkbox"
            checked={includeCompleted}
            onChange={(event) => setIncludeCompleted(event.target.checked)}
          />
          완료 항목 포함
        </label>
      </div>
      <label className="form-field panel-reason-field">
        <span>수정사유</span>
        <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
      </label>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <MaterialReceiptGroups items={items} onChange={setReceipt} isMobile={isMobile} canEdit={canUpdateMaterialReceipt} />
      ) : null}
      {message ? <p role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</p> : null}
    </section>
  );
}

function MaterialReceiptGroups({
  items,
  onChange,
  isMobile,
  canEdit
}: {
  items: ProcurementItem[];
  onChange: (itemId: string, next: Partial<ProcurementItem>) => void;
  isMobile: boolean;
  canEdit: boolean;
}) {
  const groups = groupMaterialReceiptItems(items);
  return (
    <div className={isMobile ? 'material-receipt-groups procurement-mobile' : 'material-receipt-groups procurement-desktop'} data-testid="material-receipt-mobile">
      {groups.map((group) => (
        <section className="material-receipt-group" key={group.projectId}>
          <div className="material-receipt-group-header">
            <strong>{group.projectTitle}</strong>
            <span>PJT Code: {group.projectCode}</span>
            <span>납품예정일: {emptyDash(group.shipmentDisplayDate)}</span>
          </div>
          <div className="material-receipt-items" role="table" aria-label={`${group.projectTitle} 자재 입고 처리`}>
            <div className="material-receipt-head" role="row">
              <span>발주품목</span><span>업체</span><span>기술 담당자</span><span>입고예정일</span><span>입고 완료</span><span>완료일</span><span>완료 비고</span>
            </div>
            {group.items.map((item) => (
              <div className="material-receipt-row" role="row" key={item.itemId}>
                <span className="order-item-badge">{emptyDash(item.orderItem)}</span>
                <span>{emptyDash(item.supplierName)}</span>
                <span>{emptyDash(item.technicalOwner)}</span>
                <span>{emptyDash(item.expectedReceiptDate)}</span>
                <div className="receipt-input-cell">
                  <label className="checkbox-field">
                    <input type="checkbox" checked={item.receiptCompleted} disabled={!canEdit} onChange={(event) => onChange(item.itemId, { receiptCompleted: event.target.checked })} />
                    입고 완료
                  </label>
                  <ReceiptCompletionBadge completed={item.receiptCompleted} completedAtUtc={item.receiptCompletedAtUtc} />
                </div>
                <input type="datetime-local" value={toDateTimeLocal(item.receiptCompletedAtUtc ?? '')} disabled={!canEdit} onChange={(event) => onChange(item.itemId, { receiptCompletedAtUtc: fromDateTimeLocal(event.target.value) })} />
                <textarea value={item.receiptCompletionNote ?? ''} disabled={!canEdit} onChange={(event) => onChange(item.itemId, { receiptCompletionNote: event.target.value })} />
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}

function groupMaterialReceiptItems(items: ProcurementItem[]) {
  const groups: Array<{
    projectId: string;
    projectTitle: string;
    projectCode: string;
    shipmentDisplayDate: string | null;
    items: ProcurementItem[];
  }> = [];

  for (const item of items) {
    let group = groups.find((candidate) => candidate.projectId === item.projectId);
    if (!group) {
      group = {
        projectId: item.projectId,
        projectTitle: item.projectTitle,
        projectCode: item.projectCode,
        shipmentDisplayDate: formatShipmentDisplayDate(item),
        items: []
      };
      groups.push(group);
    }

    group.items.push(item);
  }

  return groups;
}

function PanelInformationEditPage({
  developmentUserKey,
  projectId,
  canUpdatePanelInfo,
  onBack,
  onSaved
}: {
  developmentUserKey: string;
  projectId: string;
  canUpdatePanelInfo: boolean;
  onBack: () => void;
  onSaved: (feedback: ActionFeedbackState) => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [state, setState] = useState<LoadState<PanelInformationResponse>>({ kind: 'loading' });
  const [rows, setRows] = useState<PanelInformationRowForm[]>([]);
  const [editInputUnit, setEditInputUnit] = useState<PanelInputUnit>('Mm');
  const [displayUnit, setDisplayUnit] = useState<PanelInputUnit>(() => readDisplayUnit());
  const [filter, setFilter] = useState<'All' | 'Completed' | 'Pending' | 'QrEligible'>('All');
  const [search, setSearch] = useState('');
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [messageTone, setMessageTone] = useState<ActionFeedbackTone>('neutral');
  const [isSaving, setIsSaving] = useState(false);
  const [isDownloadingTemplate, setIsDownloadingTemplate] = useState(false);
  const [showExcel, setShowExcel] = useState(false);
  const [duplicateConfirm, setDuplicateConfirm] = useState<PanelNameDuplicateGroup[] | null>(null);
  const requestIdRef = useRef(0);
  const dirtyRef = useRef(false);
  const editInputUnitRef = useRef<PanelInputUnit>('Mm');

  useEffect(() => {
    editInputUnitRef.current = editInputUnit;
  }, [editInputUnit]);

  const load = useCallback(() => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    dirtyRef.current = false;
    setState({ kind: 'loading' });
    setProjectState({ kind: 'loading' });
    setMessage('');
    setMessageTone('neutral');
    setDuplicateConfirm(null);

    Promise.all([
      getProject(developmentUserKey, projectId),
      getPanelInformation(developmentUserKey, projectId)
    ])
      .then(([project, panelInfo]) => {
        if (requestId !== requestIdRef.current) {
          return;
        }

        setProjectState({ kind: 'ready', data: project });
        setState({ kind: 'ready', data: panelInfo });
        setRows(panelInfo.panels.map((panel) => panelToRowForm(panel, editInputUnitRef.current)));
      })
      .catch((error: unknown) => {
        if (requestId !== requestIdRef.current) {
          return;
        }

        setState(toLoadError(error, '설계 정보를 불러올 수 없습니다.'));
        setProjectState(toLoadError(error, '프로젝트를 불러올 수 없습니다.'));
      });
  }, [developmentUserKey, projectId]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    window.localStorage.setItem('emi-qms-panel-display-unit', displayUnit);
  }, [displayUnit]);

  const data = state.kind === 'ready' ? state.data : null;
  const project = projectState.kind === 'ready' ? projectState.data : null;
  const canEdit = canUpdatePanelInfo && project?.status === 'Active';
  const visibleRows = rows.filter((row) => {
    const panel = row.original;
    const matchesFilter = filter === 'All'
      || (filter === 'Completed' && panel.panelInfoCompleted)
      || (filter === 'Pending' && !panel.panelInfoCompleted)
      || (filter === 'QrEligible' && panel.qrEligible);
    const query = search.trim().toLowerCase();
    const matchesSearch = !query
      || row.panelNumber.toLowerCase().includes(query)
      || row.displayCode.toLowerCase().includes(query)
      || row.currentPanelName.toLowerCase().includes(query)
      || (panel.panelName ?? '').toLowerCase().includes(query);
    return matchesFilter && matchesSearch;
  });
  const reasonRequired = rows.some(panelRowNeedsReason);
  const hasChanges = rows.some(panelRowChanged);

  function setPanelName(panelId: string, value: string) {
    dirtyRef.current = true;
    setRows((current) => current.map((row) => row.panelId === panelId
      ? { ...row, currentPanelName: value, panelNameDirty: true }
      : row));
  }

  function setSizeInput(panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) {
    dirtyRef.current = true;
    setRows((current) => current.map((row) => {
      if (row.panelId !== panelId) {
        return row;
      }

      const next = { ...row, [field]: value, sizeDirty: true, sizeInputUnit: editInputUnit };
      return { ...next, sizeClearRequested: !next.widthInput.trim() && !next.heightInput.trim() && !next.depthInput.trim() };
    }));
  }

  function changeEditInputUnit(nextUnit: PanelInputUnit) {
    if (rows.some((row) => row.sizeDirty)) {
      setMessageTone('error');
      setMessage('저장되지 않은 사이즈 입력이 있습니다. 저장하거나 변경을 취소한 후 단위를 변경해 주세요.');
      return;
    }

    setEditInputUnit(nextUnit);
    setRows((current) => current.map((row) => rowWithSizeInputs(row, nextUnit)));
  }

  async function save(allowDuplicatePanelNames = false) {
    if (!canEdit || !data) {
      return;
    }

    if (reasonRequired && !reason.trim()) {
      setMessageTone('error');
      setMessage('기존 설계 정보를 변경하려면 수정사유가 필요합니다.');
      return;
    }

    const duplicateGroups = findPanelNameDuplicateGroups(rows);
    if (!allowDuplicatePanelNames && duplicateGroups.length > 0) {
      setDuplicateConfirm(duplicateGroups);
      return;
    }

    setIsSaving(true);
    setMessage('');
    setMessageTone('loading');
    setDuplicateConfirm(null);
    try {
      const saved = await updatePanelInformation(developmentUserKey, projectId, {
        reason: reason.trim() || null,
        panels: rows.filter(panelRowChanged).map(panelRowToUpdateRequest)
      });
      dirtyRef.current = false;
      setReason('');
      setState({ kind: 'ready', data: saved });
      setRows(saved.panels.map((panel) => panelToRowForm(panel, editInputUnit)));
      onSaved({ tone: 'success', message: '패널 설계 정보를 저장했습니다.' });
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  async function downloadTemplate() {
    if (!canUpdatePanelInfo) {
      return;
    }

    setIsDownloadingTemplate(true);
    setMessageTone('loading');
    setMessage('Excel 양식을 생성하는 중입니다.');
    try {
      const template = await downloadPanelInformationTemplate(developmentUserKey, projectId, editInputUnit);
      const triggered = triggerExcelDownload(template);
      setMessageTone(triggered ? 'success' : 'partial');
      setMessage(triggered
        ? 'Excel 양식을 다운로드했습니다.'
        : 'Excel 양식 생성은 완료됐지만 다운로드를 시작하지 못했습니다. 다시 시도해 주세요.');
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsDownloadingTemplate(false);
    }
  }

  if (!canUpdatePanelInfo) {
    return <section className="page-surface"><StateMessage state={{ kind: 'forbidden', message: '권한이 없습니다.' }} /></section>;
  }

  if (projectState.kind === 'loading' || state.kind === 'loading') {
    return <section className="page-surface"><p className="muted-text">Loading</p></section>;
  }

  if (projectState.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={projectState} /></section>;
  }

  if (state.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={state} /></section>;
  }

  return (
    <section className="page-surface panel-info-section">
      <div className="subsection-header">
        <div>
          <h3>설계 정보 입력</h3>
          <span>{formatPackagingMethod(projectState.data.packagingMethod)}</span>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>상세</button>
          <button type="button" onClick={load}>새로고침</button>
          {canUpdatePanelInfo ? (
            <button type="button" onClick={downloadTemplate} disabled={isDownloadingTemplate}>
              {isDownloadingTemplate ? '다운로드 중' : 'Excel 양식 다운로드'}
            </button>
          ) : null}
          <button type="button" onClick={() => setShowExcel(true)} disabled={!canEdit}>Excel 업로드</button>
          <button type="button" className="primary-button" disabled={!canEdit || isSaving || !hasChanges} onClick={() => void save()}>
            {isSaving ? '저장 중' : '직접 입력 저장'}
          </button>
        </div>
      </div>
      {data ? (
        <>
          <div className="panel-info-summary">
            <StatusChip label="입력 완료" value={`${data.panelInfoCompletedCount}/${data.activePanelCount}`} />
            <StatusChip
              label="입력 미완료"
              value={`${data.panelInfoPendingCount}/${data.activePanelCount}`}
              tone={data.panelInfoPendingCount > 0 ? 'danger' : undefined}
            />
            <StatusChip label="QR 가능" value={String(data.qrEligibleCount)} />
            <StatusChip label="동일명칭" value={String(data.duplicatePanelNameGroupCount)} />
          </div>

          {data.panelInformationStatusMessage ? (
            <p role="status" className="warning-text">{data.panelInformationStatusMessage}</p>
          ) : null}
          {!canUpdatePanelInfo ? <p className="muted-text">읽기 전용</p> : null}
          {canUpdatePanelInfo && projectState.data.status !== 'Active' ? (
            <p role="alert" className="warning-text">현재 프로젝트 상태에서는 설계 정보를 수정할 수 없습니다.</p>
          ) : null}

          <div className="toolbar panel-toolbar">
            <label>
              <span>입력 단위</span>
              <select value={editInputUnit} onChange={(event) => changeEditInputUnit(event.target.value as PanelInputUnit)}>
                <option value="Mm">mm</option>
                <option value="Inch">inch</option>
              </select>
            </label>
            <label>
              <span>표시 단위</span>
              <select value={displayUnit} onChange={(event) => setDisplayUnit(event.target.value as PanelInputUnit)}>
                <option value="Mm">mm</option>
                <option value="Inch">inch</option>
              </select>
            </label>
            <label>
              <span>필터</span>
              <select value={filter} onChange={(event) => setFilter(event.target.value as typeof filter)}>
                <option value="All">전체</option>
                <option value="Completed">완료</option>
                <option value="Pending">미완료</option>
                <option value="QrEligible">QR 가능</option>
              </select>
            </label>
            <label>
              <span>검색</span>
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="No 또는 패널명" />
            </label>
          </div>

          {canUpdatePanelInfo ? (
            <div className="panel-template-help">
              <strong>입력 단위: {editInputUnit === 'Inch' ? 'inch' : 'mm'}</strong>
              <span>No는 수정하지 마세요.</span>
              <span>도번은 업로드 시 저장되지 않습니다.</span>
              <span>일부 입력 상태에서도 저장할 수 있습니다.</span>
              <span>일반 포장은 패널명 입력 시 설계 단계가 완료됩니다.</span>
              <span>목포장은 패널명과 W/H/D 입력 시 설계 단계가 완료됩니다.</span>
              <span>사이즈를 입력하는 경우 W/H/D를 모두 입력해야 합니다.</span>
            </div>
          ) : null}

          {reasonRequired ? (
            <label className="form-field panel-reason-field">
              <span>수정사유*</span>
              <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
            </label>
          ) : null}

          <PanelInfoEditDesktop
            rows={visibleRows}
            displayUnit={displayUnit}
            canEdit={canEdit}
            onPanelNameChange={setPanelName}
            onSizeChange={setSizeInput}
          />

          <PanelInfoEditMobile
            rows={visibleRows}
            displayUnit={displayUnit}
            canEdit={canEdit}
            onPanelNameChange={setPanelName}
            onSizeChange={setSizeInput}
          />

          {message ? <ActionFeedback message={message} tone={messageTone} focusOnAttention /> : null}

        </>
      ) : null}

      {showExcel && data ? (
        <PanelInformationExcelDialog
          developmentUserKey={developmentUserKey}
          projectId={projectId}
          defaultInputUnit={editInputUnit}
          onClose={() => setShowExcel(false)}
          onApplied={(next) => {
            setShowExcel(false);
            dirtyRef.current = false;
            setReason('');
            setState({ kind: 'ready', data: next });
            setRows(next.panels.map((panel) => panelToRowForm(panel, editInputUnit)));
            onSaved({ tone: 'success', message: '패널 Excel 변경사항을 적용했습니다.' });
          }}
        />
      ) : null}
      {duplicateConfirm ? (
        <PanelDuplicateNameConfirmDialog
          groups={duplicateConfirm}
          isSaving={isSaving}
          onCancel={() => setDuplicateConfirm(null)}
          onConfirm={() => void save(true)}
        />
      ) : null}
    </section>
  );
}

function PanelInfoEditDesktop({
  rows,
  displayUnit,
  canEdit,
  onPanelNameChange,
  onSizeChange
}: {
  rows: PanelInformationRowForm[];
  displayUnit: PanelInputUnit;
  canEdit: boolean;
  onPanelNameChange: (panelId: string, value: string) => void;
  onSizeChange: (panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) => void;
}) {
  return (
    <div className="panel-info-table panel-info-edit-desktop" role="table" aria-label="설계 정보 직접 입력" data-testid="panel-info-edit-desktop">
      <div className="panel-info-table-head" role="row">
        <span>No</span>
        <span>패널명</span>
        <span>W</span>
        <span>H</span>
        <span>D</span>
        <span>패널정보</span>
        <span>QR</span>
      </div>
      {rows.map((row) => (
        <PanelInformationEditableRow
          key={row.panelId}
          row={row}
          displayUnit={displayUnit}
          canEdit={canEdit && row.original.panelStatus === 'Active'}
          onPanelNameChange={onPanelNameChange}
          onSizeChange={onSizeChange}
        />
      ))}
    </div>
  );
}

function PanelInfoEditMobile({
  rows,
  displayUnit,
  canEdit,
  onPanelNameChange,
  onSizeChange
}: {
  rows: PanelInformationRowForm[];
  displayUnit: PanelInputUnit;
  canEdit: boolean;
  onPanelNameChange: (panelId: string, value: string) => void;
  onSizeChange: (panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) => void;
}) {
  return (
    <div className="panel-info-cards panel-info-edit-mobile" data-testid="panel-info-edit-mobile">
      {rows.map((row) => (
        <PanelInformationCard
          key={row.panelId}
          row={row}
          displayUnit={displayUnit}
          canEdit={canEdit && row.original.panelStatus === 'Active'}
          onPanelNameChange={onPanelNameChange}
          onSizeChange={onSizeChange}
        />
      ))}
    </div>
  );
}

function PanelInformationEditableRow({
  row,
  displayUnit,
  canEdit,
  onPanelNameChange,
  onSizeChange
}: {
  row: PanelInformationRowForm;
  displayUnit: PanelInputUnit;
  canEdit: boolean;
  onPanelNameChange: (panelId: string, value: string) => void;
  onSizeChange: (panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) => void;
}) {
  return (
    <div className="panel-info-table-row" role="row">
      <strong>{row.sequenceNumber}<small>{row.displayCode}</small></strong>
      <input aria-label={`${row.panelNumber} 패널명`} value={row.currentPanelName} disabled={!canEdit} onChange={(event) => onPanelNameChange(row.panelId, event.target.value)} />
      <input aria-label={`${row.panelNumber} W`} inputMode="decimal" value={row.widthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'widthInput', event.target.value)} />
      <input aria-label={`${row.panelNumber} H`} inputMode="decimal" value={row.heightInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'heightInput', event.target.value)} />
      <input aria-label={`${row.panelNumber} D`} inputMode="decimal" value={row.depthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'depthInput', event.target.value)} />
      <span className={row.original.panelInfoCompleted ? undefined : 'negative-text'}>{row.original.panelInfoCompleted ? '입력 완료' : '미입력'}</span>
      <span className={row.original.qrEligible ? undefined : 'negative-text'}>{row.original.qrEligible ? '생성 가능' : '생성 불가'}</span>
      <small className="panel-display-size">{formatPanelSizeInUnit(row.original, displayUnit)}</small>
    </div>
  );
}

function PanelInformationCard({
  row,
  displayUnit,
  canEdit,
  onPanelNameChange,
  onSizeChange
}: {
  row: PanelInformationRowForm;
  displayUnit: PanelInputUnit;
  canEdit: boolean;
  onPanelNameChange: (panelId: string, value: string) => void;
  onSizeChange: (panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) => void;
}) {
  return (
    <article className="panel-info-card" data-testid="panel-info-edit-card">
      <div className="subsection-header">
        <h3>{row.panelNumber}</h3>
        <span>{row.displayCode}</span>
      </div>
      <FormField label="패널명">
        <input value={row.currentPanelName} disabled={!canEdit} onChange={(event) => onPanelNameChange(row.panelId, event.target.value)} />
      </FormField>
      <div className="dimension-grid">
        <FormField label="W">
          <input inputMode="decimal" value={row.widthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'widthInput', event.target.value)} />
        </FormField>
        <FormField label="H">
          <input inputMode="decimal" value={row.heightInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'heightInput', event.target.value)} />
        </FormField>
        <FormField label="D">
          <input inputMode="decimal" value={row.depthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'depthInput', event.target.value)} />
        </FormField>
      </div>
      <dl className="mini-status-grid">
        <div><dt>패널정보</dt><dd className={row.original.panelInfoCompleted ? undefined : 'negative-text'}>{row.original.panelInfoCompleted ? '입력 완료' : '미입력'}</dd></div>
        <div><dt>QR</dt><dd className={row.original.qrEligible ? undefined : 'negative-text'}>{row.original.qrEligible ? '생성 가능' : '생성 불가'}</dd></div>
        <div><dt>표시</dt><dd>{formatPanelSizeInUnit(row.original, displayUnit)}</dd></div>
      </dl>
      {row.original.hasDuplicateName ? <p className="muted-text">동일 명칭 {row.original.duplicateNameCount}면</p> : null}
    </article>
  );
}

function PanelDuplicateNameConfirmDialog({
  groups,
  isSaving,
  onCancel,
  onConfirm
}: {
  groups: PanelNameDuplicateGroup[];
  isSaving: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <DialogBackdrop ariaLabel="중복 패널명 확인" onClose={onCancel} closeDisabled={isSaving}>
      <div className="dialog" data-testid="duplicate-panel-name-dialog">
        <div className="subsection-header">
          <h3>중복된 패널명이 있습니다.</h3>
          <button type="button" onClick={onCancel} disabled={isSaving}>닫기</button>
        </div>
        <p className="warning-text">패널명이 중복되어도 저장하시겠습니까?</p>
        <div className="duplicate-panel-name-list">
          <strong>중복 패널명</strong>
          <ul>
            {groups.map((group) => (
              <li key={group.name}>
                {group.name}: {group.panelNumbers.join(', ')}
              </li>
            ))}
          </ul>
        </div>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel} disabled={isSaving}>취소</button>
          <button type="button" className="primary-button" onClick={onConfirm} disabled={isSaving}>
            {isSaving ? '저장 중' : '중복이어도 저장'}
          </button>
        </div>
      </div>
    </DialogBackdrop>
  );
}

function PanelInformationExcelDialog({
  developmentUserKey,
  projectId,
  defaultInputUnit,
  onClose,
  onApplied
}: {
  developmentUserKey: string;
  projectId: string;
  defaultInputUnit: PanelInputUnit;
  onClose: () => void;
  onApplied: (response: PanelInformationResponse) => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [inputUnit, setInputUnit] = useState<PanelInputUnit>(defaultInputUnit);
  const [preview, setPreview] = useState<PanelInformationExcelPreviewResponse | null>(null);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [messageTone, setMessageTone] = useState<ActionFeedbackTone>('neutral');
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isApplying, setIsApplying] = useState(false);

  async function previewFile() {
    if (!file) {
      setMessageTone('error');
      setMessage('Excel 파일을 선택하세요.');
      return;
    }

    setIsPreviewing(true);
    setMessageTone('loading');
    setMessage('Excel 내용을 확인하는 중입니다.');
    try {
      setPreview(await previewPanelInformationExcel(developmentUserKey, projectId, file, inputUnit));
      setMessageTone('success');
      setMessage('미리보기를 완료했습니다. 저장 가능한 항목을 확인해 주세요.');
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPreviewing(false);
    }
  }

  async function applyFile() {
    if (!file || !preview) {
      return;
    }

    if (preview.errorCount > 0) {
      setMessageTone('error');
      setMessage('오류가 있는 Excel은 적용할 수 없습니다.');
      return;
    }

    if (preview.reasonRequired && !reason.trim()) {
      setMessageTone('error');
      setMessage('기존 설계 정보를 변경하려면 수정사유가 필요합니다.');
      return;
    }

    if (preview.newCount + preview.changedCount === 0) {
      setMessageTone('error');
      setMessage('적용할 변경사항이 없습니다.');
      return;
    }

    setIsApplying(true);
    setMessageTone('loading');
    setMessage('패널 변경사항을 적용하는 중입니다.');
    try {
      const expectedVersions = preview.rows
        .filter((row) => row.panelId && row.expectedPanelInfoVersion !== null)
        .map((row) => ({
          panelId: row.panelId!,
          expectedPanelInfoVersion: row.expectedPanelInfoVersion!
        }));
      const previewExpectedVersions = preview.expectedPanelInfoVersions.length > 0
        ? preview.expectedPanelInfoVersions
        : expectedVersions;
      const response = await applyPanelInformationExcel(
        developmentUserKey,
        projectId,
        file,
        inputUnit,
        preview.fileSha256,
        preview.expectedPackagingMethod,
        reason.trim() || null,
        previewExpectedVersions);
      onApplied(response);
    } catch (error) {
      setMessageTone('error');
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsApplying(false);
    }
  }

  const canApplyPreview = preview !== null
    && preview.errorCount === 0
    && preview.newCount + preview.changedCount > 0
    && (!preview.reasonRequired || reason.trim().length > 0)
    && !isApplying;
  const applyDisabledReason = panelExcelApplyDisabledReason(preview, file, reason, isApplying);

  return (
    <DialogBackdrop ariaLabel="Excel 업로드" onClose={onClose} closeDisabled={isPreviewing || isApplying}>
      <div className="dialog wide-dialog">
        <div className="subsection-header">
          <h3>Excel 업로드</h3>
          <button type="button" onClick={onClose}>닫기</button>
        </div>
        <div className="toolbar">
          <label className="form-field">
            <span>파일</span>
            <input type="file" accept=".xlsx" onChange={(event) => {
              setFile(event.target.files?.[0] ?? null);
              setPreview(null);
              setMessage('');
              setMessageTone('neutral');
            }} />
          </label>
          <label className="form-field">
            <span>파일 단위</span>
            <select value={inputUnit} onChange={(event) => setInputUnit(event.target.value as PanelInputUnit)}>
              <option value="Mm">mm</option>
              <option value="Inch">inch</option>
            </select>
          </label>
          <button type="button" onClick={previewFile} disabled={isPreviewing || isApplying}>{isPreviewing ? '미리보기 중' : 'Preview'}</button>
        </div>

        {preview ? (
          <>
            <div className="excel-preview-action-bar">
              <div className="excel-preview-counts">
                <span>신규 {preview.newCount}건</span>
                <span>변경 {preview.changedCount}건</span>
                <span>동일 {preview.unchangedCount}건</span>
                <span>건너뜀 {preview.skippedCount}건</span>
                <span className={preview.errorCount > 0 ? 'negative-text' : undefined}>오류 {preview.errorCount}건</span>
              </div>
              {preview.reasonRequired ? (
                <label className="form-field excel-preview-reason">
                  <span>수정사유*</span>
                  <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
                </label>
              ) : null}
              {preview.newCount + preview.changedCount === 0 ? (
                <p className="muted-text">적용할 변경사항이 없습니다.</p>
              ) : null}
              <button type="button" className="primary-button" disabled={!canApplyPreview} onClick={applyFile}>
                {isApplying ? '저장 중' : 'Excel 저장'}
              </button>
              {!canApplyPreview && applyDisabledReason ? <p className="warning-text">{applyDisabledReason}</p> : null}
            </div>
            <ExcelIssueSummary rows={preview.rows} />
            <ExcelPreviewDesktop rows={preview.rows} />
            <ExcelPreviewMobile rows={preview.rows} />
          </>
        ) : null}
        {message ? <ActionFeedback message={message} tone={messageTone} focusOnAttention /> : null}
      </div>
    </DialogBackdrop>
  );
}

function ExcelPreviewDesktop({ rows }: { rows: PanelInformationExcelPreviewResponse['rows'] }) {
  return (
    <div className="excel-preview-table excel-preview-desktop" data-testid="excel-preview-desktop">
      {rows.map((row) => (
        <div key={`${row.excelRowNumber}-${row.no ?? 'no'}`} className="excel-preview-row" data-result={row.resultType}>
          <strong>Row {row.excelRowNumber}</strong>
          <span>{row.no ? `No.${row.no}` : 'No 없음'}</span>
          <span>{row.panelName ?? '패널명 없음'}</span>
          <span>{row.widthMm ?? '-'} / {row.heightMm ?? '-'} / {row.depthMm ?? '-'}</span>
          <span>{row.resultType}</span>
          <small>{row.errorMessages.join(' ')}</small>
        </div>
      ))}
    </div>
  );
}

function ExcelPreviewMobile({ rows }: { rows: PanelInformationExcelPreviewResponse['rows'] }) {
  return (
    <div className="excel-preview-cards excel-preview-mobile" data-testid="excel-preview-mobile">
      {rows.map((row) => (
        <article key={`${row.excelRowNumber}-${row.no ?? 'no'}-mobile`} className="excel-preview-card" data-result={row.resultType}>
          <div className="subsection-header">
            <h3>{row.no ? `No.${row.no}` : `Row ${row.excelRowNumber}`}</h3>
            <span className={row.resultType === 'Error' ? 'negative-text' : undefined}>결과: {row.resultType}</span>
          </div>
          <dl className="mobile-detail-list">
            <div>
              <dt>패널명</dt>
              <dd>
                <span>기존: {row.currentValue?.panelName ?? '-'}</span>
                <span>변경: {row.panelName ?? '-'}</span>
              </dd>
            </div>
            <div>
              <dt>W/H/D</dt>
              <dd>
                <span>기존: {formatPreviewSize(row.currentValue)}</span>
                <span>변경: {formatPreviewSize(row)}</span>
              </dd>
            </div>
            <div>
              <dt>상태</dt>
              <dd className={row.resultType === 'Error' ? 'negative-text' : undefined}>
                {row.errorMessages.length > 0 ? row.errorMessages.join(' ') : previewResultLabel(row.resultType)}
              </dd>
            </div>
          </dl>
        </article>
      ))}
    </div>
  );
}

function ProjectEditPage({
  developmentUserKey,
  projectId,
  onCancel,
  onSaved
}: {
  developmentUserKey: string;
  projectId: string;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [owners, setOwners] = useState<SalesOwner[]>([]);
  const [productTypes, setProductTypes] = useState<ProductionProductType[]>([]);
  const [panels, setPanels] = useState<PanelPlaceholder[]>([]);
  const [form, setForm] = useState<ProjectFormValues>(emptyForm);
  const [selectedCancelPanels, setSelectedCancelPanels] = useState<string[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const initializedProjectIdRef = useRef<string | null>(null);
  const isDirtyRef = useRef(false);
  const loadRequestIdRef = useRef(0);

  useEffect(() => {
    let isCurrent = true;
    const requestId = loadRequestIdRef.current + 1;
    loadRequestIdRef.current = requestId;
    initializedProjectIdRef.current = null;
    isDirtyRef.current = false;
    setProjectState({ kind: 'loading' });
    setOwners([]);
    setProductTypes([]);
    setPanels([]);
    setSelectedCancelPanels([]);
    setErrors({});
    setMessage('');
    setForm(emptyForm);

    Promise.all([
      getProject(developmentUserKey, projectId),
      getSalesOwners(developmentUserKey),
      listProductionProductTypes(developmentUserKey),
      listPanels(developmentUserKey, projectId)
    ])
      .then(([project, ownerItems, typeItems, panelItems]) => {
        if (!isCurrent || requestId !== loadRequestIdRef.current) {
          return;
        }

        setProjectState({ kind: 'ready', data: project });
        setOwners(ownerItems);
        setProductTypes(typeItems);
        setPanels(panelItems);
        if (initializedProjectIdRef.current !== projectId && !isDirtyRef.current) {
          initializedProjectIdRef.current = projectId;
          setForm(projectToForm(project));
        }
      })
      .catch((error: unknown) => {
        if (!isCurrent || requestId !== loadRequestIdRef.current) {
          return;
        }

        setProjectState(toLoadError(error, '프로젝트 수정 정보를 불러올 수 없습니다.'));
      });

    return () => {
      isCurrent = false;
    };
  }, [developmentUserKey, projectId]);

  function handleFormChange(values: ProjectFormValues) {
    isDirtyRef.current = true;
    setForm(values);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    const validation = validateProjectForm(form, true, productTypes);
    const targetPanelCount = Number(form.panelCount);
    const activePanelCount = panels.filter((panel) => panel.panelStatus === 'Active').length;
    const currentPanelCount = projectState.kind === 'ready' ? activePanelCount : targetPanelCount;
    if (targetPanelCount < currentPanelCount && selectedCancelPanels.length !== currentPanelCount - targetPanelCount) {
      validation.cancelPanelIds = '감소 면수만큼 취소할 패널을 선택하세요.';
    }

    setErrors(validation);
    setMessage('');
    if (Object.keys(validation).length > 0) {
      return;
    }

    setIsSaving(true);
    try {
      await updateProject(developmentUserKey, projectId, toUpdateRequest(form));
      if (targetPanelCount !== currentPanelCount) {
        await changePanelCount(developmentUserKey, projectId, {
          panelCount: targetPanelCount,
          expectedActivePanelCount: currentPanelCount,
          cancelPanelIds: targetPanelCount < currentPanelCount ? selectedCancelPanels : [],
          reason: form.reason
        });
      }
      isDirtyRef.current = false;
      onSaved();
    } catch (error) {
      handleFormError(error, setErrors, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  if (projectState.kind === 'loading') {
    return <section className="page-surface"><p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p></section>;
  }

  if (projectState.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={projectState} /></section>;
  }

  const project = projectState.data;
  const activePanels = panels.filter((panel) => panel.panelStatus === 'Active');
  const targetPanelCount = Number(form.panelCount || project.activePanelCount);
  const isDecrease = Number.isFinite(targetPanelCount) && targetPanelCount < activePanels.length;

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Sales</p>
          <h2>프로젝트 수정</h2>
        </div>
        <button type="button" onClick={onCancel}>상세</button>
      </div>

      <ProjectForm
        form={form}
        owners={owners}
        productTypes={productTypes}
        errors={errors}
        isSaving={isSaving}
        submitLabel="저장"
        includeReason
        onChange={handleFormChange}
        onSubmit={submit}
      />

      {project.status === 'Cancelled' ? (
        <p role="alert" className="error-text">취소된 프로젝트는 재활성 후 면수를 변경할 수 있습니다.</p>
      ) : null}

      {isDecrease ? (
        <PanelCancellationSelector
          panels={activePanels}
          selectedPanelIds={selectedCancelPanels}
          onChange={setSelectedCancelPanels}
          error={errors.cancelPanelIds}
        />
      ) : null}

      {message ? <p role="alert" className="error-text">{message}</p> : null}
    </section>
  );
}

function PanelPlaceholderDetailPage({
  developmentUserKey,
  projectId,
  panelId,
  onBack
}: {
  developmentUserKey: string;
  projectId: string;
  panelId: string;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<{ project: ProjectDetail; panel: PanelPlaceholder }>>({ kind: 'loading' });

  useEffect(() => {
    Promise.all([
      getProject(developmentUserKey, projectId),
      getPanel(developmentUserKey, projectId, panelId)
    ])
      .then(([project, panel]) => setState({ kind: 'ready', data: { project, panel } }))
      .catch((error: unknown) => setState(toLoadError(error, '패널 상세를 불러올 수 없습니다.')));
  }, [developmentUserKey, panelId, projectId]);

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">설계</p>
          <h2>{state.kind === 'ready' ? `${state.data.panel.displayCode} 패널 상세` : '패널 상세'}</h2>
        </div>
        <button type="button" onClick={onBack}>프로젝트</button>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <>
          <ProjectContextSummary project={state.data.project} />
          <section className="project-context-summary product-context-summary" aria-label="패널 요약">
            <div><span>패널</span><strong>No.{state.data.panel.sequenceNumber} · {state.data.panel.panelName ?? '패널명 미입력'}</strong></div>
            <div><span>사이즈</span><strong>{formatSize(state.data.panel)}</strong></div>
            <div><span>패널 상태</span><strong>{formatWorkflowStage(state.data.panel.workflowStage)}</strong></div>
            <div><span>설계 정보</span><strong>{state.data.panel.panelInfoCompleted ? '입력 완료' : '미입력'}</strong></div>
            <div><span>QR</span><strong>{state.data.panel.qrEligible ? '생성 가능' : '생성 불가'}</strong></div>
          </section>
        </>
      ) : null}
    </section>
  );
}

function DeletedProjectDetailPage({
  developmentUserKey,
  projectId,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  onBack
}: {
  developmentUserKey: string;
  projectId: string;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<DeletedProjectDetail>>({ kind: 'loading' });
  const [confirmText, setConfirmText] = useState('');
  const [restoreReason, setRestoreReason] = useState('');
  const [message, setMessage] = useState('');
  const [isPurging, setIsPurging] = useState(false);
  const [isRestoring, setIsRestoring] = useState(false);

  useEffect(() => {
    getDeletedProject(developmentUserKey, projectId)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '삭제 프로젝트 상세를 불러올 수 없습니다.')));
  }, [developmentUserKey, projectId]);

  if (state.kind === 'loading') {
    return <section className="page-surface"><p className="muted-text">Loading</p></section>;
  }

  if (state.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={state} /></section>;
  }

  const project = state.data;
  async function restore() {
    setMessage('');
    setIsRestoring(true);
    try {
      await restoreDeletedProject(developmentUserKey, projectId, restoreReason.trim() || null);
      onBack();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsRestoring(false);
    }
  }

  async function purge() {
    setMessage('');
    if (confirmText !== '완전 삭제') {
      setMessage('확인 문구를 정확히 입력해 주세요.');
      return;
    }

    setIsPurging(true);
    try {
      await purgeDeletedProject(developmentUserKey, projectId, confirmText);
      onBack();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPurging(false);
    }
  }

  return (
    <section className="page-surface deleted-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Deleted Archive</p>
          <h2>{project.projectTitle}</h2>
        </div>
        <button type="button" onClick={onBack}>목록</button>
      </div>

      <ProjectSummary project={project} canReadSalesAmount={canReadSalesAmount} />
      <dl className="detail-grid">
        <div><dt>삭제일시</dt><dd>{formatDateTime(project.deletedAtUtc)}</dd></div>
        <div><dt>삭제자</dt><dd>{project.deletedByUserName ?? project.deletedByUserId ?? '-'}</dd></div>
        <div><dt>삭제 사유</dt><dd>{project.deleteReason}</dd></div>
      </dl>

      {canPurgeDeletedProjects ? (
        <>
          <section className="restore-zone" aria-label="삭제 프로젝트 복구">
            <div>
              <strong>복구</strong>
              <p className="muted-text">복구하면 일반 프로젝트 목록에 다시 표시됩니다. 삭제 전 프로젝트 상태는 유지됩니다.</p>
            </div>
            <label className="form-field compact-field">
              <span>복구 사유</span>
              <input value={restoreReason} onChange={(event) => setRestoreReason(event.target.value)} placeholder="선택 입력" />
            </label>
            <button type="button" className="primary-button" disabled={isRestoring} onClick={restore}>
              {isRestoring ? '복구 중' : '복구'}
            </button>
          </section>
          <section className="danger-zone" aria-label="삭제 프로젝트 완전 삭제">
            <div>
              <strong>완전 삭제</strong>
              <p className="muted-text">이 프로젝트와 연결된 구매정보, 패널, 감사이력을 완전히 삭제합니다. 되돌릴 수 없습니다.</p>
            </div>
            <label className="form-field compact-field">
              <span>확인 문구: 완전 삭제</span>
              <input value={confirmText} onChange={(event) => setConfirmText(event.target.value)} />
            </label>
            <button type="button" className="danger-button" disabled={isPurging || confirmText !== '완전 삭제'} onClick={purge}>
              {isPurging ? '삭제 중' : '완전 삭제'}
            </button>
            {message ? <p role="alert" className="error-text">{message}</p> : null}
          </section>
        </>
      ) : null}

      <section className="subsection">
        <h3>보존된 설계 정보</h3>
        <PanelPlaceholderList panels={project.panels} onOpenPanel={() => undefined} />
      </section>

      <section className="subsection">
        <h3>삭제 프로젝트 변경이력</h3>
        <AuditHistory events={project.auditHistory} />
      </section>
    </section>
  );
}

function ProjectForm({
  form,
  owners,
  productTypes,
  errors,
  isSaving,
  submitLabel,
  includeReason = false,
  onChange,
  onSubmit
}: {
  form: ProjectFormValues;
  owners: SalesOwner[];
  productTypes: ProductionProductType[];
  errors: Record<string, string>;
  isSaving: boolean;
  submitLabel: string;
  includeReason?: boolean;
  onChange: (values: ProjectFormValues) => void;
  onSubmit: (event: FormEvent) => void;
}) {
  const setField = (field: keyof ProjectFormValues, value: string) => onChange({ ...form, [field]: value });
  const activeProductTypes = productTypes.filter((item) => item.isActive);
  const currentItemIsKnown = !form.item || activeProductTypes.some((item) => item.code === form.item);
  const currentPackagingMethodIsKnown = !form.packagingMethod
    || packagingMethodOptions.some((item) => item.value === form.packagingMethod);

  return (
    <form className="project-form" noValidate onSubmit={onSubmit}>
      <FormErrorSummary errors={errors} />
      <FormField label="고객사*" error={errors.customerName}>
        <input name="customerName" value={form.customerName} onChange={(event) => setField('customerName', event.target.value)} />
      </FormField>
      <FormField label="Item*" error={errors.item}>
        <select name="item" value={form.item} onChange={(event) => setField('item', event.target.value)}>
          <option value="">Item 선택</option>
          {!currentItemIsKnown ? <option value={form.item}>현재값: {form.item}</option> : null}
          {activeProductTypes.map((item) => (
            <option key={item.productTypeId} value={item.code}>{item.code}</option>
          ))}
        </select>
        {!currentItemIsKnown ? <small className="warning-text">현재 Item은 등록된 Item 기준값이 아닙니다. 저장하려면 Item을 선택해 주세요.</small> : null}
      </FormField>
      <FormField label="PJT Code*" error={errors.projectCode}>
        <input name="projectCode" value={form.projectCode} onChange={(event) => setField('projectCode', event.target.value)} />
      </FormField>
      <FormField label="PJT Title*" error={errors.projectTitle}>
        <input name="projectTitle" value={form.projectTitle} onChange={(event) => setField('projectTitle', event.target.value)} />
      </FormField>
      <FormField label="면수*" error={errors.panelCount}>
        <input
          name="panelCount"
          min="1"
          max={maxPanelsPerProject}
          type="number"
          value={form.panelCount}
          onChange={(event) => setField('panelCount', event.target.value)}
        />
      </FormField>
      <FormField label="납기일*" error={errors.deliveryDate}>
        <input name="deliveryDate" type="date" value={form.deliveryDate} onChange={(event) => setField('deliveryDate', event.target.value)} />
      </FormField>
      <FormField label="영업담당자*" error={errors.salesOwnerUserId}>
        <select name="salesOwnerUserId" value={form.salesOwnerUserId} onChange={(event) => setField('salesOwnerUserId', event.target.value)}>
          <option value="">선택</option>
          {owners.map((owner) => (
            <option key={owner.userId} value={owner.userId}>{owner.displayName}</option>
          ))}
        </select>
      </FormField>
      <FormField label="포장방식*" error={errors.packagingMethod}>
        <select name="packagingMethod" value={form.packagingMethod} onChange={(event) => setField('packagingMethod', event.target.value)}>
          <option value="">선택</option>
          {!currentPackagingMethodIsKnown ? <option value={form.packagingMethod}>현재값: {formatPackagingMethod(form.packagingMethod)}</option> : null}
          {packagingMethodOptions.map((method) => (
            <option key={method.value} value={method.value}>{method.label}</option>
          ))}
        </select>
        {!currentPackagingMethodIsKnown ? <small className="warning-text">현재 포장방식은 허용된 기준값이 아닙니다. 저장하려면 포장방식을 선택해 주세요.</small> : null}
      </FormField>
      <FormField label="판매금액" error={errors.salesAmount}>
        <input name="salesAmount" value={form.salesAmount} inputMode="decimal" onChange={(event) => setField('salesAmount', event.target.value)} />
      </FormField>
      <FormField label="통화" error={errors.currencyCode}>
        <input name="currencyCode" maxLength={3} value={form.currencyCode} onChange={(event) => setField('currencyCode', event.target.value.toUpperCase())} />
      </FormField>
      <FormField label="납품장소" error={errors.deliveryLocation}>
        <input name="deliveryLocation" value={form.deliveryLocation} onChange={(event) => setField('deliveryLocation', event.target.value)} />
      </FormField>
      <FormField label="FAT 필요 여부" error={errors.fatRequired}>
        <select name="fatRequired" value={form.fatRequired} onChange={(event) => setField('fatRequired', event.target.value)}>
          <option value="false">아니오</option>
          <option value="true">예</option>
        </select>
      </FormField>
      {includeReason ? (
        <FormField label="수정사유*" error={errors.reason}>
          <textarea name="reason" value={form.reason} onChange={(event) => setField('reason', event.target.value)} />
        </FormField>
      ) : null}
      <div className="form-actions">
        <button type="submit" className="primary-button" disabled={isSaving}>
          {isSaving ? '저장 중' : submitLabel}
        </button>
      </div>
    </form>
  );
}

function FormField({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <label className={error ? 'form-field has-error' : 'form-field'}>
      <span>{label}</span>
      {children}
      {error ? <small role="alert">{error}</small> : null}
    </label>
  );
}

function FormErrorSummary({ errors }: { errors: Record<string, string> }) {
  const entries = Object.entries(errors).filter(([, message]) => Boolean(message));
  const errorKey = entries.map(([field]) => field).join('|');

  useEffect(() => {
    if (!errorKey) return;
    queueMicrotask(() => focusFirstFieldError(errors));
  }, [errorKey, errors]);

  if (entries.length === 0) {
    return null;
  }

  return (
    <div className="form-error-summary" role="alert">
      <strong>입력값을 확인해 주세요.</strong>
      <ul>
        {entries.map(([field, message]) => (
          <li key={field}>
            <button type="button" onClick={() => focusField(field)}>
              {fieldLabel(field)}: {message}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

function FieldErrorMessage({ message, field }: { message?: string; field?: string }) {
  const descriptionId = field ? fieldErrorId(field) : undefined;

  useEffect(() => {
    if (!message || !field || !descriptionId) return;
    const target = fieldTarget(field);
    if (!target) return;
    target.setAttribute('aria-invalid', 'true');
    const existing = target.getAttribute('aria-describedby')?.split(/\s+/).filter(Boolean) ?? [];
    target.setAttribute('aria-describedby', [...new Set([...existing, descriptionId])].join(' '));
    return () => {
      target.removeAttribute('aria-invalid');
      const remaining = (target.getAttribute('aria-describedby')?.split(/\s+/).filter(Boolean) ?? [])
        .filter((id) => id !== descriptionId);
      if (remaining.length > 0) target.setAttribute('aria-describedby', remaining.join(' '));
      else target.removeAttribute('aria-describedby');
    };
  }, [descriptionId, field, message]);

  return message ? <small id={descriptionId} role={field ? undefined : 'alert'} className="field-error-message">{message}</small> : null;
}

function focusField(field: string) {
  const target = fieldTarget(field);
  target?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
  target?.focus();
}

function focusFirstFieldError(errors: Record<string, string>, orderedFields?: readonly string[]) {
  const fields = orderedFields ?? Object.keys(errors);
  const first = fields.find((field) => Boolean(errors[field]) && Boolean(fieldTarget(field)));
  if (first) focusField(first);
}

function fieldTarget(field: string) {
  const escaped = typeof CSS !== 'undefined' && 'escape' in CSS ? CSS.escape(field) : field.replace(/"/g, '\\"');
  return document.querySelector<HTMLElement>(`[name="${escaped}"], [data-field="${escaped}"]`);
}

function fieldErrorId(field: string) {
  return `field-error-${normalizeFieldPath(field).replace(/[^a-zA-Z0-9_-]+/g, '-')}`;
}

function useIsMobileViewport() {
  return useAdaptiveLayout().isMobile;
}

function ProjectBottleneckOverview({
  project,
  onOpenPending,
  onOpenPanels,
  onOpenWorkflow
}: {
  project: ProjectListItem;
  onOpenPending: () => void;
  onOpenPanels: () => void;
  onOpenWorkflow: () => void;
}) {
  const bottleneck = project.bottleneck;
  if (!bottleneck) {
    return null;
  }

  const action = bottleneck.nextAction === 'Pending'
    ? { label: '프로젝트 Pending 열기', run: onOpenPending }
    : bottleneck.nextAction === 'Panels'
      ? { label: '병목 패널 보기', run: onOpenPanels }
      : bottleneck.nextAction === 'Workflow'
        ? { label: 'Workflow 보기', run: onOpenWorkflow }
        : null;

  return (
    <section className="project-bottleneck-overview" aria-label="프로젝트 병목 현황" data-kind={bottleneck.kind}>
      <div className="project-bottleneck-hero">
        <div>
          <p className="eyebrow">NEXT ATTENTION</p>
          <h3>다음 확인 대상</h3>
          <strong>{bottleneck.label}</strong>
          <p>{bottleneck.nextActionLabel}</p>
          <small>{bottleneck.sortReason === 'open-pending' ? 'open Pending 차단을 우선해 정렬했습니다.' : '가장 뒤처진 필수 구간을 기준으로 표시했습니다.'}</small>
        </div>
        {action ? <button type="button" className="primary-button" onClick={action.run}>{action.label}</button> : null}
      </div>

      {bottleneck.openPendingCount !== undefined ? (
        <div className="project-bottleneck-pending" aria-label="Pending 차단 집계">
          <StatusChip label="open Pending" value={`${bottleneck.openPendingCount}`} />
          <StatusChip label="재검사 대기" value={`${bottleneck.reinspectionPendingCount ?? 0}`} />
          <StatusChip label="긴급" value={`${bottleneck.urgentPendingCount ?? 0}`} />
        </div>
      ) : null}

      <div className="project-bottleneck-matrix" aria-label="패널 병목 구간 matrix">
        {bottleneck.panelDistribution.map((item) => (
          <button
            key={item.stageCode}
            type="button"
            data-active={item.isBottleneck}
            onClick={onOpenPanels}
            aria-label={`${item.stageLabel} 패널 ${item.panelCount}면`}
          >
            <span>{item.stageLabel}</span>
            <strong>{item.panelCount}면</strong>
          </button>
        ))}
      </div>
    </section>
  );
}

function ProjectSummary({ project, canReadSalesAmount }: { project: ProjectListItem; canReadSalesAmount: boolean }) {
  return (
    <dl className="detail-grid">
      <div><dt>상태</dt><dd><ProjectStatusBadge status={project.status} /></dd></div>
      <div><dt>고객사</dt><dd>{project.customerName}</dd></div>
      <div><dt>Item</dt><dd>{project.item}</dd></div>
      <div><dt>PJT Code</dt><dd>{project.projectCode}</dd></div>
      <div><dt>면수</dt><dd>{project.activePanelCount}</dd></div>
      <div><dt>납기일</dt><dd>{formatDate(project.deliveryDate)}</dd></div>
      <div><dt>영업담당자</dt><dd>{project.salesOwnerName}</dd></div>
      <div><dt>포장방식</dt><dd>{formatPackagingMethod(project.packagingMethod)}</dd></div>
      <div><dt>납품장소</dt><dd>{project.deliveryLocation ?? '-'}</dd></div>
      <div><dt>FAT 필요 여부</dt><dd>{project.fatRequired ? '예' : '아니오'}</dd></div>
      {canReadSalesAmount && project.salesAmount !== undefined ? (
        <div><dt>판매금액</dt><dd><SalesAmountField amount={project.salesAmount} currencyCode={project.currencyCode} /></dd></div>
      ) : null}
      <div><dt>진행률</dt><dd>{formatProjectProgress(project.projectProgressPercent)}</dd></div>
    </dl>
  );
}

function ProjectWorkflowSummary({ state }: { state: LoadState<ProjectWorkflowResponse> }) {
  if (state.kind === 'loading') {
    return (
      <section className="subsection project-process-summary">
        <div className="subsection-header">
          <div>
            <h3>Workflow</h3>
          <p>18단계 workflow 요약을 불러오는 중입니다.</p>
          </div>
        </div>
      </section>
    );
  }

  if (state.kind === 'empty') {
    return null;
  }

  if (state.kind !== 'ready') {
    return (
      <section className="subsection project-process-summary">
        <StateMessage state={state} />
      </section>
    );
  }

  const activeStage = state.data.stages.find((stage) => stage.stageCode === state.data.currentStageCode)
    ?? state.data.stages.find((stage) => stage.status === 'InProgress' || stage.status === 'Requested');
  const nextStage = state.data.stages.find((stage) => stage.status === 'NotStarted');
  const activeStageLabel = activeStage
    ? displayWorkflowStageLabel(activeStage.departmentLabel, activeStage.stageCode, activeStage.stageName)
    : displayWorkflowStageLabel(state.data.currentDepartmentLabel, state.data.currentStageCode, state.data.currentStageName);

  return (
    <section className="subsection project-process-summary project-workflow-board" aria-label="프로젝트 workflow 요약">
      <div className="subsection-header">
        <div>
          <span className="workflow-board-eyebrow">END-TO-END WORKFLOW</span>
          <h3>프로젝트 전체 흐름</h3>
          <p>영업 등록부터 세금계산서 완료까지 18단계의 현재 위치와 부서 인계를 한눈에 확인합니다.</p>
        </div>
        <div className="button-row workflow-summary-meta">
          <StatusChip label="진행률" value={`${state.data.progressPercent}%`} />
          <StatusChip label="완료" value={`${state.data.completedRequiredStageCount}/${state.data.requiredStageCount}`} />
          <StatusChip label="내 업무" value={`${state.data.generatedWorkItemCount}`} />
        </div>
      </div>

      <div className="workflow-progress-track" aria-label={`전체 진행률 ${state.data.progressPercent}%`}>
        <span style={{ width: `${state.data.progressPercent}%` }} />
      </div>

      <dl className="detail-grid workflow-current-grid">
        <div><dt>현재 단계</dt><dd>{activeStageLabel}</dd></div>
        <div><dt>다음 예정</dt><dd>{nextStage ? displayWorkflowStageLabel(nextStage.departmentLabel, nextStage.stageCode, nextStage.stageName) : '-'}</dd></div>
      </dl>

      <ol className="workflow-stage-list">
        {state.data.stages.map((stage) => (
          <li
            className="workflow-stage-item"
            data-status={stage.status}
            data-department={stage.departmentCode}
            data-implemented-input={hasImplementedStageInput(stage.stageCode) ? 'true' : 'false'}
            data-current={stage.stageCode === activeStage?.stageCode ? 'true' : 'false'}
            title={hasImplementedStageInput(stage.stageCode) ? undefined : '전용 입력 화면은 후속 단계에서 제공됩니다.'}
            key={stage.stageCode}
          >
            <span className="workflow-stage-number">{stage.sequenceNumber}</span>
            <div>
              <strong>{displayWorkflowStageLabel(stage.departmentLabel, stage.stageCode, stage.stageName)}{stage.isOptional ? ' (선택)' : ''}</strong>
              <small>{stage.statusLabel}{stage.workItemCount > 0 ? ` · 내 업무 ${stage.workItemCount}건` : ''}</small>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}

function ProjectContextSummary({ project }: { project: ProjectListItem }) {
  return (
    <section className="project-context-summary" aria-label="프로젝트 요약" data-testid="project-context-summary">
      <div className="project-context-title">
        <span>프로젝트</span>
        <strong>{project.projectTitle}</strong>
      </div>
      <div><span>고객사</span><strong>{project.customerName}</strong></div>
      <div><span>Code</span><strong>{project.projectCode}</strong></div>
      <div><span>Item</span><strong>{project.item}</strong></div>
      <div><span>납기일</span><strong>{formatDate(project.deliveryDate)}</strong></div>
      <div><span>포장방식</span><strong>{formatPackagingMethod(project.packagingMethod)}</strong></div>
      <div><span>FAT 필요 여부</span><strong>{project.fatRequired ? '예' : '아니오'}</strong></div>
      <div><span>상태</span><strong>{formatProjectStatus(project.status)}</strong></div>
    </section>
  );
}

function ProjectStatusBadge({ status }: { status: ProjectStatus }) {
  return <span className="status-badge" data-status={status}>{formatProjectStatus(status)}</span>;
}

function SalesAmountField({ amount, currencyCode }: { amount: number; currencyCode?: string }) {
  return <span>{currencyCode ?? ''} {amount.toLocaleString()}</span>;
}

function PanelPlaceholderList({
  panels,
  onOpenPanel
}: {
  panels: PanelPlaceholder[];
  onOpenPanel: (panelId: string) => void;
}) {
  if (panels.length === 0) {
    return <p className="empty-text">패널이 없습니다.</p>;
  }

  return (
    <div className="panel-list">
      {panels.map((panel) => (
        <button key={panel.panelId} type="button" className="panel-row" onClick={() => onOpenPanel(panel.panelId)}>
          <strong>{panel.displayCode}</strong>
          <span>{panel.panelName ?? '패널명 미입력'}</span>
          <span>{panel.panelInfoCompleted ? '설계 정보 완료' : '설계 정보 대기'}</span>
          <span>{panel.qrEligible ? 'QR 생성 조건 충족' : 'QR 생성 조건 미충족'}</span>
          <span>{formatPanelStatus(panel.panelStatus)}</span>
        </button>
      ))}
    </div>
  );
}

function ProjectPanelList({
  panels,
  packagingMethod,
  displayUnit,
  onOpenPanel
}: {
  panels: PanelInformationPanel[];
  packagingMethod: PackagingMethod | null;
  displayUnit: PanelInputUnit;
  onOpenPanel: (panelId: string) => void;
}) {
  if (panels.length === 0) {
    return <p className="empty-text">패널이 없습니다.</p>;
  }

  return (
    <>
      <PanelListDesktop
        panels={panels}
        packagingMethod={packagingMethod}
        displayUnit={displayUnit}
        onOpenPanel={onOpenPanel}
      />
      <PanelListMobile
        panels={panels}
        packagingMethod={packagingMethod}
        displayUnit={displayUnit}
        onOpenPanel={onOpenPanel}
      />
    </>
  );
}

function PanelListDesktop({
  panels,
  packagingMethod,
  displayUnit,
  onOpenPanel
}: {
  panels: PanelInformationPanel[];
  packagingMethod: PackagingMethod | null;
  displayUnit: PanelInputUnit;
  onOpenPanel: (panelId: string) => void;
}) {
  return (
    <div className="product-panel-table product-panel-desktop" role="table" aria-label="설계" data-testid="project-panel-list-desktop">
      <div className="product-panel-table-head" role="row">
        <span>No</span>
        <span>패널명</span>
        <span>사이즈</span>
        <span>패널정보</span>
        <span>QR</span>
        <span>상태</span>
      </div>
      {panels.map((panel) => (
        <button key={panel.panelId} type="button" className="product-panel-row" role="row" onClick={() => onOpenPanel(panel.panelId)}>
          <span>{panel.sequenceNumber}</span>
          <span>
            <strong className={panel.panelName ? undefined : 'negative-text'}>{panel.panelName ?? '미입력'}</strong>
            {panel.hasDuplicateName ? <small>동일 명칭 {panel.duplicateNameCount}면</small> : null}
          </span>
          <span className={panelSizeClass(panel, packagingMethod)}>
            {formatPanelSizeForPackaging(panel, displayUnit, packagingMethod)}
          </span>
          <span className={panel.panelInfoCompleted ? undefined : 'negative-text'}>
            {panel.panelInfoCompleted ? '입력 완료' : '미입력'}
          </span>
          <span className={panel.qrEligible ? undefined : 'negative-text'}>
            {panel.qrEligible ? '생성 가능' : '생성 불가'}
          </span>
          <span>{formatWorkflowStage(panel.workflowStage)}</span>
        </button>
      ))}
    </div>
  );
}

function PanelListMobile({
  panels,
  packagingMethod,
  displayUnit,
  onOpenPanel
}: {
  panels: PanelInformationPanel[];
  packagingMethod: PackagingMethod | null;
  displayUnit: PanelInputUnit;
  onOpenPanel: (panelId: string) => void;
}) {
  return (
    <div className="product-panel-cards product-panel-mobile" data-testid="project-panel-list-mobile">
      {panels.map((panel) => (
        <article key={panel.panelId} className="product-panel-card" data-testid="project-panel-card">
          <div className="subsection-header">
            <h3>{panel.panelNumber}</h3>
            <button type="button" onClick={() => onOpenPanel(panel.panelId)}>상세 보기</button>
          </div>
          <dl className="mobile-detail-list">
            <div>
              <dt>패널명</dt>
              <dd>
                <strong className={panel.panelName ? undefined : 'negative-text'}>{panel.panelName ?? '미입력'}</strong>
                {panel.hasDuplicateName ? <small>동일 명칭 {panel.duplicateNameCount}면</small> : null}
              </dd>
            </div>
            <div>
              <dt>사이즈</dt>
              <dd className={panelSizeClass(panel, packagingMethod)}>{formatPanelSizeForPackaging(panel, displayUnit, packagingMethod)}</dd>
            </div>
            <div>
              <dt>패널정보</dt>
              <dd className={panel.panelInfoCompleted ? undefined : 'negative-text'}>{panel.panelInfoCompleted ? '입력 완료' : '미입력'}</dd>
            </div>
            <div>
              <dt>QR</dt>
              <dd className={panel.qrEligible ? undefined : 'negative-text'}>{panel.qrEligible ? '생성 가능' : '생성 불가'}</dd>
            </div>
            <div>
              <dt>상태</dt>
              <dd>{formatWorkflowStage(panel.workflowStage)}</dd>
            </div>
          </dl>
        </article>
      ))}
    </div>
  );
}

function PanelCancellationSelector({
  panels,
  selectedPanelIds,
  error,
  onChange
}: {
  panels: PanelPlaceholder[];
  selectedPanelIds: string[];
  error?: string;
  onChange: (ids: string[]) => void;
}) {
  return (
    <section className="subsection">
      <h3>취소할 패널 선택</h3>
      <div className="checkbox-grid">
        {panels.map((panel) => (
          <label key={panel.panelId}>
            <input
              type="checkbox"
              checked={selectedPanelIds.includes(panel.panelId)}
              onChange={(event) => {
                onChange(event.target.checked
                  ? [...selectedPanelIds, panel.panelId]
                  : selectedPanelIds.filter((id) => id !== panel.panelId));
              }}
            />
            <span>{panel.displayCode}</span>
          </label>
        ))}
      </div>
      {error ? <p role="alert" className="error-text">{error}</p> : null}
    </section>
  );
}

function StatusReasonDialog({
  action,
  reason,
  error,
  isSaving,
  onReasonChange,
  onCancel,
  onSubmit
}: {
  action: 'hold' | 'resume' | 'cancel' | 'reactivate';
  reason: string;
  error: string;
  isSaving: boolean;
  onReasonChange: (reason: string) => void;
  onCancel: () => void;
  onSubmit: () => void;
}) {
  const title = {
    hold: '프로젝트 보류',
    resume: '보류 해제',
    cancel: '프로젝트 취소',
    reactivate: '프로젝트 재활성'
  }[action];

  return (
    <DialogBackdrop ariaLabel={title} onClose={onCancel} closeDisabled={isSaving}>
      <div className="dialog">
        <h3>{title}</h3>
        <label className="form-field">
          <span>사유*</span>
          <textarea value={reason} onChange={(event) => onReasonChange(event.target.value)} />
          {error ? <small role="alert">{error}</small> : null}
        </label>
        <div className="button-row">
          <button type="button" onClick={onCancel}>닫기</button>
          <button type="button" className="primary-button" disabled={isSaving} onClick={onSubmit}>
            {isSaving ? '처리 중' : '확인'}
          </button>
        </div>
      </div>
    </DialogBackdrop>
  );
}

function DeleteProjectDialog({
  projectTitle,
  reason,
  confirmProjectTitle,
  error,
  isSaving,
  onReasonChange,
  onConfirmProjectTitleChange,
  onCancel,
  onSubmit
}: {
  projectTitle: string;
  reason: string;
  confirmProjectTitle: string;
  error: string;
  isSaving: boolean;
  onReasonChange: (reason: string) => void;
  onConfirmProjectTitleChange: (value: string) => void;
  onCancel: () => void;
  onSubmit: () => void;
}) {
  return (
    <DialogBackdrop ariaLabel="프로젝트 삭제" onClose={onCancel} closeDisabled={isSaving}>
      <div className="dialog">
        <h3>프로젝트 삭제</h3>
        <p className="warning-text">
          삭제는 오등록·중복등록 프로젝트를 일반 업무목록에서 제거하는 기능입니다. 실제로 중단된 프로젝트는 취소 기능을 사용해 주세요.
        </p>
        <p className="muted-text">확인할 PJT Title: {projectTitle}</p>
        <label className="form-field">
          <span>삭제 사유*</span>
          <textarea value={reason} onChange={(event) => onReasonChange(event.target.value)} />
        </label>
        <label className="form-field">
          <span>PJT Title 확인 입력*</span>
          <input value={confirmProjectTitle} onChange={(event) => onConfirmProjectTitleChange(event.target.value)} />
        </label>
        {error ? <p role="alert" className="error-text">{error}</p> : null}
        <div className="button-row">
          <button type="button" onClick={onCancel}>닫기</button>
          <button type="button" className="danger-button" disabled={isSaving} onClick={onSubmit}>
            {isSaving ? '삭제 중' : '삭제'}
          </button>
        </div>
      </div>
    </DialogBackdrop>
  );
}

function AuditHistory({ events }: { events: AuditEvent[] }) {
  if (events.length === 0) {
    return <p className="empty-text">변경이력이 없습니다.</p>;
  }

  return (
    <ol className="audit-list">
      {events.map((event) => (
        <li key={event.auditEventId}>
          <strong>{event.panelDisplayName ?? event.action}</strong>
          <span>{event.fieldName ? `${event.fieldName}: ${event.oldValue ?? '-'} → ${event.newValue ?? '-'}` : event.reason ?? '-'}</span>
          {event.entityType === 'Panel' ? <small>입력 방식: {formatInputSource(event.inputSource)}</small> : null}
          {event.importFileName ? <small>입력 파일: {event.importFileName}</small> : null}
          {event.inputUnit ? <small>입력 단위: {formatInputUnit(event.inputUnit)}</small> : null}
          {event.originalInputValue ? (
            <small>입력값: {event.originalInputValue}{event.inputUnit ? ` ${formatInputUnit(event.inputUnit)}` : ''}</small>
          ) : null}
          {event.fieldName?.endsWith('Mm') && event.newValue ? <small>저장값: {event.newValue} mm</small> : null}
          {event.importBatchId && event.importFileName ? <small>Excel Batch: {event.importFileName}</small> : null}
          {event.reason ? <small>수정사유: {event.reason}</small> : null}
          <small>{event.changedByUserName ?? event.changedByUserId ?? '-'} · {formatDateTime(event.changedAtUtc)}</small>
        </li>
      ))}
    </ol>
  );
}

function GroupedHistory({
  groups,
  emptyText = '전체 이력이 없습니다.'
}: {
  groups: PanelInformationHistoryResponse['groups'];
  emptyText?: string | null;
}) {
  if (groups.length === 0) {
    return emptyText ? <p className="empty-text">{emptyText}</p> : null;
  }

  return (
    <ol className="audit-list grouped-audit-list">
      {groups.map((group) => (
        <li key={group.groupId}>
          <strong>{formatInputSource(group.inputSource)} · 대상 패널 {group.affectedPanelCount}면</strong>
          <span>변경항목 {group.changeCount}건</span>
          {group.importFileName ? <small>입력 파일: {group.importFileName}</small> : null}
          {group.reason ? <small>수정사유: {group.reason}</small> : null}
          <small>{group.changedByName ?? group.changedByUserId ?? '-'} · {formatDateTime(group.changedAtUtc)}</small>
          <details>
            <summary>변경 상세</summary>
            <ol className="audit-change-list">
              {group.changes.map((change, index) => (
                <li key={`${group.groupId}-${change.entityId}-${change.fieldName ?? index}-${index}`}>
                  <strong>{change.panelDisplayName ?? change.panelNumber ?? change.displayCode ?? change.entityType}</strong>
                  <span>{change.fieldName ?? '-'}: {change.oldValue ?? '-'} → {change.newValue ?? '-'}</span>
                  {change.originalInputValue ? (
                    <small>원본 입력값: {change.originalInputValue}{change.inputUnit ? ` ${formatInputUnit(change.inputUnit)}` : ''}</small>
                  ) : null}
                  {change.inputUnit ? <small>입력단위: {formatInputUnit(change.inputUnit)}</small> : null}
                </li>
              ))}
            </ol>
          </details>
        </li>
      ))}
    </ol>
  );
}

function ProcurementGroupedHistory({ groups }: { groups: ProcurementHistoryResponse['groups'] }) {
  if (groups.length === 0) {
    return null;
  }

  return (
    <ol className="audit-list grouped-audit-list procurement-audit-list">
      {groups.map((group) => (
        <li key={group.groupId}>
          <strong>{formatInputSource(group.inputSource)} · 대상 구매품목 {group.affectedItemCount}건</strong>
          <span>변경항목 {group.changeCount}건</span>
          {group.importFileName ? <small>입력 파일: {group.importFileName}</small> : null}
          {group.reason ? <small>수정사유: {group.reason}</small> : null}
          <small>{group.changedByName ?? group.changedByUserId ?? '-'} · {formatDateTime(group.changedAtUtc)}</small>
          <details>
            <summary>변경 상세</summary>
            <ol className="audit-change-list">
              {group.changes.map((change, index) => (
                <li key={`${group.groupId}-${change.entityId}-${change.fieldName ?? index}-${index}`}>
                  <strong>{change.sequenceNumber ? `구매품목 ${change.sequenceNumber}` : '구매품목'}</strong>
                  <span>{formatProcurementFieldName(change.fieldName)}: {change.oldValue ?? '-'} → {change.newValue ?? '-'}</span>
                </li>
              ))}
            </ol>
          </details>
        </li>
      ))}
    </ol>
  );
}

function ProductionPlanningGroupedHistory({ groups }: { groups: ProductionPlanningHistoryResponse['groups'] }) {
  if (groups.length === 0) {
    return null;
  }

  return (
    <ol className="audit-list grouped-audit-list production-planning-audit-list">
      {groups.map((group) => (
        <li key={group.groupId}>
          <strong>생산계획 · 대상 {group.affectedItemCount}건</strong>
          <span>변경항목 {group.changeCount}건</span>
          {group.reason ? <small>수정사유: {group.reason}</small> : null}
          <small>{group.changedByName ?? group.changedByUserId ?? '-'} · {formatDateTime(group.changedAtUtc)}</small>
          <details>
            <summary>변경 상세</summary>
            <ol className="audit-change-list">
              {group.changes.map((change, index) => (
                <li key={`${group.groupId}-${change.entityId}-${change.fieldName ?? index}-${index}`}>
                  <strong>{change.entityType === 'ProjectAssignee' ? '담당자' : '생산계획'}</strong>
                  <span>{change.fieldName ?? '-'}: {change.oldValue ?? '-'} → {change.newValue ?? '-'}</span>
                </li>
              ))}
            </ol>
          </details>
        </li>
      ))}
    </ol>
  );
}

function formatInputSource(source: AuditEvent['inputSource']) {
  if (source === 'Direct') {
    return '직접 입력';
  }

  if (source === 'Excel') {
    return 'Excel 입력';
  }

  return '기존 이력';
}

function formatInputUnit(unit: PanelInputUnit) {
  return unit === 'Inch' ? 'inch' : 'mm';
}

function formatProcurementFieldName(fieldName: string | null) {
  switch (fieldName) {
    case 'StandardLeadTime':
      return '통상납기';
    case 'OrderItem':
      return '발주품목';
    case 'SupplierName':
      return '업체';
    case 'TechnicalOwner':
      return '기술 담당자';
    case 'OrderDate':
      return '발주일';
    case 'ExpectedReceiptDate':
      return '입고예정일';
    case 'ShipmentText':
      return '납품예정일';
    case 'IssueNote':
      return '이슈사항';
    case 'SupplyType':
      return '공급 방식';
    case 'OrderQuantity':
      return '제공 예정 수량';
    case 'OrderUnit':
      return '제공 예정 단위';
    case 'ReceiptCompleted':
      return '입고 완료';
    case 'ReceiptCompletedAtUtc':
      return '입고 완료일';
    case 'ReceiptCompletedByUserId':
      return '입고 완료자';
    case 'ReceiptCompletionNote':
      return '완료 비고';
    default:
      return fieldName ?? '-';
  }
}

function StatusChip({ label, value, tone }: { label: string; value: string; tone?: 'danger' }) {
  return (
    <div className="status-chip" data-tone={tone}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function StateMessage<T>({ state }: { state: LoadState<T> }) {
  if (state.kind === 'forbidden') {
    return <p role="alert" className="error-text">권한이 없습니다.</p>;
  }

  if (state.kind === 'not-found') {
    return <p role="alert" className="error-text">대상을 찾을 수 없습니다.</p>;
  }

  if (state.kind === 'error') {
    return <p role="alert" className="error-text">{state.message}</p>;
  }

  return null;
}

function validateProjectForm(form: ProjectFormValues, includeReason: boolean, productTypes: ProductionProductType[] = []): Record<string, string> {
  const errors: Record<string, string> = {};
  const required: Array<keyof ProjectFormValues> = [
    'customerName',
    'item',
    'projectCode',
    'projectTitle',
    'panelCount',
    'deliveryDate',
    'salesOwnerUserId'
  ];

  for (const field of required) {
    if (!form[field].trim()) {
      errors[field] = '필수 입력값입니다.';
    }
  }

  if (Number(form.panelCount) < 1 || !Number.isInteger(Number(form.panelCount))) {
    errors.panelCount = '1 이상의 정수여야 합니다.';
  }

  if (Number(form.panelCount) > maxPanelsPerProject) {
    errors.panelCount = `1 이상 ${maxPanelsPerProject} 이하의 정수여야 합니다.`;
  }

  if (!form.packagingMethod.trim()) {
    errors.packagingMethod = '포장방식은 필수 선택값입니다.';
  }

  const activeProductCodes = new Set(productTypes.filter((item) => item.isActive).map((item) => item.code));
  if (form.item.trim() && activeProductCodes.size > 0 && !activeProductCodes.has(form.item.trim())) {
    errors.item = 'Item은 등록된 Item 기준값 중 하나여야 합니다.';
  }

  if (form.salesAmount.trim() && Number(form.salesAmount) < 0) {
    errors.salesAmount = '0 이상의 금액이어야 합니다.';
  }

  if (form.salesAmount.trim() && !/^[A-Z]{3}$/.test(form.currencyCode.trim())) {
    errors.currencyCode = '통화는 3자리 대문자여야 합니다.';
  }

  if (includeReason && !form.reason.trim()) {
    errors.reason = '수정사유는 필수입니다.';
  }

  return errors;
}

function validateProductionPlanningForm(productTypeId: string, rows: ProductionPlanRowForm[]): Record<string, string> {
  const errors: Record<string, string> = {};
  if (!productTypeId) {
    errors.productTypeId = 'Item을 확인해 주세요.';
  }

  const activeNames = new Map<string, number>();
  rows.forEach((row, index) => {
    if (row.isDeleted) {
      return;
    }

    const normalizedName = row.stepName.trim().replace(/\s+/g, ' ').toLowerCase();
    if (!row.templateStepId) {
      if (!row.stepName.trim()) {
        errors[`items[${index}].stepName`] = '계획 항목명을 입력해 주세요.';
      } else if (row.stepName.trim().length > 120) {
        errors[`items[${index}].stepName`] = '계획 항목명은 120자 이하로 입력해 주세요.';
      }
    }

    if (normalizedName) {
      const existingIndex = activeNames.get(normalizedName);
      if (existingIndex !== undefined) {
        errors[`items[${index}].stepName`] = `${existingIndex + 1}번째 항목과 계획 항목명이 중복됩니다.`;
      } else {
        activeNames.set(normalizedName, index);
      }
    }
  });

  return errors;
}

function copyTemplateSettingsStep(step: ProductionTemplateSettingsStep): ProductionTemplateSettingsStep {
  return {
    templateStepId: step.templateStepId,
    sequenceNumber: step.sequenceNumber,
    stepName: step.stepName,
    isRequired: step.isRequired,
    isActive: step.isActive
  };
}

function validateTemplateSettingsSteps(steps: ProductionTemplateSettingsStep[]): Record<string, string> {
  const errors: Record<string, string> = {};
  const sequenceNumbers = new Map<number, number>();
  const activeNames = new Map<string, number>();
  let activeCount = 0;

  steps.forEach((step, index) => {
    const sequenceNumber = Number(step.sequenceNumber);
    if (!Number.isInteger(sequenceNumber) || sequenceNumber < 1) {
      errors[`steps[${index}].sequenceNumber`] = '순서는 1 이상의 정수여야 합니다.';
    } else if (sequenceNumbers.has(sequenceNumber)) {
      errors[`steps[${index}].sequenceNumber`] = '같은 순서를 중복 사용할 수 없습니다.';
      const previousIndex = sequenceNumbers.get(sequenceNumber);
      if (previousIndex !== undefined) {
        errors[`steps[${previousIndex}].sequenceNumber`] = '같은 순서를 중복 사용할 수 없습니다.';
      }
    } else {
      sequenceNumbers.set(sequenceNumber, index);
    }

    const normalizedName = step.stepName.trim().replace(/\s+/gu, ' ').toLowerCase();
    if (!normalizedName) {
      errors[`steps[${index}].stepName`] = '생산계획 단계명을 입력해 주세요.';
    } else if (step.stepName.trim().length > 120) {
      errors[`steps[${index}].stepName`] = '생산계획 단계명은 120자 이하로 입력해 주세요.';
    } else if (step.isActive) {
      const previousIndex = activeNames.get(normalizedName);
      if (previousIndex !== undefined) {
        errors[`steps[${index}].stepName`] = '사용 중인 단계명은 중복될 수 없습니다.';
        errors[`steps[${previousIndex}].stepName`] = '사용 중인 단계명은 중복될 수 없습니다.';
      } else {
        activeNames.set(normalizedName, index);
      }
    }

    if (step.isActive) {
      activeCount++;
    }
  });

  if (activeCount === 0) {
    errors.steps = '사용 중인 단계가 최소 1개 필요합니다.';
  }

  return errors;
}

function findProductTypeForProjectItem(productTypes: ProductionProductType[], projectItem: string | null | undefined) {
  const normalized = projectItem?.trim().toUpperCase();
  if (!normalized) {
    return undefined;
  }

  return productTypes.find((item) => item.isActive && item.code.toUpperCase() === normalized);
}

function toCreateRequest(form: ProjectFormValues) {
  return {
    customerName: form.customerName.trim(),
    item: form.item.trim(),
    projectCode: form.projectCode.trim(),
    projectTitle: form.projectTitle.trim(),
    panelCount: Number(form.panelCount),
    deliveryDate: form.deliveryDate,
    salesOwnerUserId: form.salesOwnerUserId,
    packagingMethod: toPackagingMethod(form.packagingMethod),
    salesAmount: form.salesAmount.trim() ? Number(form.salesAmount) : null,
    currencyCode: form.salesAmount.trim() ? form.currencyCode.trim().toUpperCase() : null,
    deliveryLocation: form.deliveryLocation.trim() || null,
    fatRequired: form.fatRequired === 'true'
  };
}

function toPackagingMethod(value: string): PackagingMethod | null {
  const trimmed = value.trim();
  return packagingMethodOptions.some((option) => option.value === trimmed)
    ? trimmed as PackagingMethod
    : null;
}

function projectToForm(project: ProjectDetail): ProjectFormValues {
  return {
    customerName: project.customerName,
    item: project.item,
    projectCode: project.projectCode,
    projectTitle: project.projectTitle,
    panelCount: String(project.activePanelCount),
    deliveryDate: project.deliveryDate,
    salesOwnerUserId: project.salesOwnerUserId,
    packagingMethod: project.packagingMethod ?? '',
    salesAmount: project.salesAmount === undefined ? '' : String(project.salesAmount),
    currencyCode: project.currencyCode ?? 'KRW',
    deliveryLocation: project.deliveryLocation ?? '',
    fatRequired: project.fatRequired ? 'true' : 'false',
    reason: ''
  };
}

function toUpdateRequest(form: ProjectFormValues) {
  return {
    ...toCreateRequest(form),
    reason: form.reason.trim()
  };
}

function handleFormError(
  error: unknown,
  setErrors: (errors: Record<string, string>) => void,
  setMessage: (message: string) => void
) {
  if (error instanceof ApiError && error.errors) {
    setErrors(mapValidationErrorsToFieldErrors(error.errors));
    setMessage(friendlyErrorMessage(error, '입력값을 확인해 주세요.'));
    return;
  }

  setMessage(friendlyErrorMessage(error, '요청을 처리할 수 없습니다.'));
}

function toLoadError<T>(error: unknown, fallback: string): LoadState<T> {
  if (error instanceof ApiError) {
    if (error.status === 403) {
      return { kind: 'forbidden', message: friendlyErrorMessage(error, '권한이 없습니다.') };
    }

    if (error.status === 404) {
      return { kind: 'not-found', message: friendlyErrorMessage(error, '대상을 찾을 수 없습니다.') };
    }

    return { kind: 'error', message: friendlyErrorMessage(error, fallback) };
  }

  return { kind: 'error', message: friendlyErrorMessage(error, fallback) };
}

function toAuthenticationLoadError<T>(error: unknown, isDevMode: boolean): LoadState<T> {
  if (error instanceof ApiError) {
    if (!isDevMode && error.status === 401) {
      return {
        kind: 'error',
        message: '로그인이 만료되었거나 인증 정보를 확인할 수 없습니다. 다시 로그인해 주세요.'
      };
    }

    if (!isDevMode && error.status === 403) {
      return {
        kind: 'forbidden',
        message: '시스템 관리자에게 문의하세요.'
      };
    }
  }

  return toLoadError(error, isDevMode ? '개발 사용자를 확인할 수 없습니다.' : '로그인 사용자를 확인할 수 없습니다.');
}

function loadStateMessage<T>(state: LoadState<T>) {
  return 'message' in state ? state.message : undefined;
}

function isAuthenticationExpiredState<T>(state: LoadState<T>) {
  return state.kind === 'error'
    && 'message' in state
    && state.message.includes('다시 로그인');
}

function isOperationalAccessBlocked(user: CurrentUser | null) {
  if (!user) {
    return false;
  }

  return !user.isActive
    || user.approvalPending
    || (user.authProvider === 'EntraId' && user.permissions.length === 0);
}

function labelForDevelopmentUser(userKey: string) {
  switch (userKey) {
    case 'dev-admin':
      return 'System Administrator';
    case 'dev-sales':
      return '영업';
    case 'dev-production':
      return '생산관리';
    case 'dev-procurement':
      return '구매';
    case 'dev-materials':
      return '자재';
    case 'dev-manufacturing':
      return '제조';
    case 'dev-quality':
      return '품질';
    case 'dev-logistics':
      return '물류';
    case 'dev-viewer':
      return '조회 전용';
    default:
      return userKey;
  }
}

function friendlyErrorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    if (error.status === 0) {
      return '서버에 연결할 수 없습니다. 서버 실행 상태를 확인해 주세요.';
    }

    return sanitizeUserMessage(error.message, fallback);
  }

  if (error instanceof TypeError && error.message.includes('fetch')) {
    return '서버에 연결할 수 없습니다. 서버 실행 상태를 확인해 주세요.';
  }

  if (error instanceof Error) {
    return sanitizeUserMessage(error.message, fallback);
  }

  return fallback;
}

function fieldErrorsFromApiError(error: unknown) {
  if (error instanceof ApiError && error.errors) {
    return mapValidationErrorsToFieldErrors(error.errors);
  }

  return {};
}

function sanitizeUserMessage(message: string, fallback: string) {
  const forbidden = [
    'One or more validation errors occurred',
    'Failed to fetch',
    'Internal Server Error',
    'Bad Request',
    'Unauthorized',
    'Forbidden',
    'Conflict',
    'Stack Trace',
    'SQL'
  ];
  if (!message || forbidden.some((text) => message.includes(text))) {
    return fallback;
  }

  return message.replaceAll('QMS', '시스템');
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError';
}

function mapValidationErrorsToFieldErrors(errors: Record<string, string[]>) {
  return Object.fromEntries(Object.entries(errors).map(([key, value]) => [
    normalizeFieldPath(key),
    value[0] ?? '입력값을 확인하세요.'
  ]));
}

function normalizeFieldPath(key: string) {
  const withoutPrefix = key.replace(/^\$\.?/u, '').replace(/^request\./iu, '');
  return withoutPrefix
    .replace(/\.([A-Z])/gu, (_, value: string) => `.${value.toLowerCase()}`)
    .replace(/^([A-Z])/u, (_, value: string) => value.toLowerCase());
}

function fieldLabel(field: string): string {
  const normalized = normalizeFieldPath(field);
  const labels: Record<string, string> = {
    customerName: '고객사',
    item: 'Item',
    projectCode: 'PJT Code',
    projectTitle: 'PJT Title',
    panelCount: '면수',
    deliveryDate: '납기일',
    salesOwnerUserId: '영업담당자',
    packagingMethod: '포장방식',
    salesAmount: '판매금액',
    currencyCode: '통화',
    deliveryLocation: '납품장소',
    reason: '수정사유',
    productTypeId: 'Item',
    notes: '비고',
    file: '파일',
    expectedFileSha256: '파일 검증값',
    procurement: '구매 담당자',
    productionPlanning: '생산관리 담당자',
    manufacturing: '제조 담당자',
    quality: '품질 담당자',
    logistics: '물류 담당자',
    receiptCompletedAtUtc: '완료일',
    receiptCompletionNote: '완료 비고',
    orderItem: '구매품목',
    sequenceNumber: '순서',
    stepName: '생산계획 단계',
    isRequired: '필수',
    isActive: '사용'
  };

  const itemMatch = /^items\[(\d+)\]\.(.+)$/u.exec(normalized);
  if (itemMatch) {
    return `${Number(itemMatch[1]) + 1}번째 생산계획 ${labels[itemMatch[2]] ?? fieldLabel(itemMatch[2])}`;
  }

  const assigneeMatch = /^assignees\[(\d+)\]\.(.+)$/u.exec(normalized);
  if (assigneeMatch) {
    return `${Number(assigneeMatch[1]) + 1}번째 담당자 ${labels[assigneeMatch[2]] ?? fieldLabel(assigneeMatch[2])}`;
  }

  const stepMatch = /^steps\[(\d+)\]\.(.+)$/u.exec(normalized);
  if (stepMatch) {
    return `${Number(stepMatch[1]) + 1}번째 단계 ${labels[stepMatch[2]] ?? fieldLabel(stepMatch[2])}`;
  }

  return labels[normalized] ?? normalized;
}

function fieldError(errors: Record<string, string>, ...keys: string[]) {
  for (const key of keys) {
    const normalized = normalizeFieldPath(key);
    if (errors[normalized]) {
      return errors[normalized];
    }
  }
  return undefined;
}

function formatDate(value: string) {
  return value;
}

function formatShortDate(value: string) {
  const [, , month, day] = /^(\d{4})-(\d{2})-(\d{2})$/u.exec(value) ?? [];
  return month && day ? `${Number(month)}/${Number(day)}` : value;
}

type ProductionCalendarBusinessDay = BusinessCalendarDay;

type ProductionCalendarDateColumn = {
  date: string;
  label: string;
  weekday: string;
  isSaturday: boolean;
  isSunday: boolean;
  isHoliday: boolean;
  isCompanyHoliday: boolean;
  isBusinessDay: boolean;
  holidayName: string | null;
  holidayType: string | null;
};

type ProductionCalendarRow = {
  key: string;
  stepName: string;
  plannedDate: string | null;
  isRequired: boolean;
  cells: Record<string, boolean>;
};

type ProductionCalendarUnscheduledItem = {
  key: string;
  stepName: string;
  isRequired: boolean;
};

function getProductionCalendarStageColumnWidth(items: ProductionPlanningResponse['items']) {
  const longestWeightedLength = items.reduce((maxLength, item) => Math.max(maxLength, weightedCalendarLabelLength(item.stepName)), 0);
  return Math.min(260, Math.max(140, Math.ceil(longestWeightedLength * 10 + 36)));
}

function weightedCalendarLabelLength(value: string) {
  return Array.from(value.trim()).reduce((length, character) => {
    return length + (/[\u1100-\u11ff\u3130-\u318f\uac00-\ud7af]/u.test(character) ? 1.2 : 1);
  }, 0);
}

function sortProductionPlanItems<T extends { plannedDate?: string | null; sequenceNumber: number }>(items: readonly T[]): T[] {
  return items.slice().sort((left, right) => {
    if (left.plannedDate && !right.plannedDate) {
      return -1;
    }

    if (!left.plannedDate && right.plannedDate) {
      return 1;
    }

    if (left.plannedDate && right.plannedDate && left.plannedDate !== right.plannedDate) {
      return left.plannedDate.localeCompare(right.plannedDate);
    }

    return left.sequenceNumber - right.sequenceNumber;
  });
}

function productionCalendarDateRange(items: ProductionPlanningResponse['items']) {
  const scheduledDates = items
    .map((item) => item.plannedDate)
    .filter((value): value is string => Boolean(value))
    .sort((left, right) => left.localeCompare(right));

  return scheduledDates.length === 0
    ? null
    : {
      dateFrom: scheduledDates[0],
      dateTo: scheduledDates[scheduledDates.length - 1]
    };
}

function buildProductionCalendar(items: ProductionPlanningResponse['items'], businessCalendarDays: ProductionCalendarBusinessDay[] = []) {
  const sortedItems = sortProductionPlanItems(items);
  const scheduledDates = sortedItems
    .map((item) => item.plannedDate)
    .filter((value): value is string => Boolean(value))
    .sort((left, right) => left.localeCompare(right));
  const businessCalendarMap = new Map(businessCalendarDays.map((day) => [day.date, day]));
  const dateColumns = scheduledDates.length === 0
    ? []
    : enumerateDates(scheduledDates[0], scheduledDates[scheduledDates.length - 1]).map((date) => {
      const weekdayIndex = weekdayIndexForDate(date);
      const day = businessCalendarMap.get(date);
      const isSaturday = weekdayIndex === 6;
      const isSunday = weekdayIndex === 0;
      const isHoliday = day?.isHoliday ?? false;
      return {
        date,
        label: formatShortDate(date),
        weekday: weekdayLabel(weekdayIndex),
        isSaturday,
        isSunday,
        isHoliday,
        isCompanyHoliday: day?.isCompanyHoliday ?? false,
        isBusinessDay: day?.isBusinessDay ?? !(isSaturday || isSunday || isHoliday),
        holidayName: day?.holidayName ?? null,
        holidayType: day?.holidayType ?? null
      };
    });
  const rows: ProductionCalendarRow[] = sortedItems
    .filter((item) => item.plannedDate)
    .map((item) => ({
      key: item.itemId ?? item.templateStepId ?? String(item.sequenceNumber),
      stepName: item.stepName,
      plannedDate: item.plannedDate,
      isRequired: item.isRequired,
      cells: Object.fromEntries(dateColumns.map((date) => [date.date, item.plannedDate === date.date]))
    }));
  const unscheduledItems: ProductionCalendarUnscheduledItem[] = sortedItems
    .filter((item) => !item.plannedDate)
    .map((item) => ({
      key: item.itemId ?? item.templateStepId ?? String(item.sequenceNumber),
      stepName: item.stepName,
      isRequired: item.isRequired
    }));

  return { dateColumns, rows, unscheduledItems };
}

function enumerateDates(start: string, end: string) {
  const dates: string[] = [];
  const cursor = parseDateOnly(start);
  const last = parseDateOnly(end);
  while (cursor <= last) {
    dates.push(formatDateOnly(cursor));
    cursor.setUTCDate(cursor.getUTCDate() + 1);
  }
  return dates;
}

function parseDateOnly(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

function formatDateOnly(value: Date) {
  return `${value.getUTCFullYear()}-${String(value.getUTCMonth() + 1).padStart(2, '0')}-${String(value.getUTCDate()).padStart(2, '0')}`;
}

function weekdayIndexForDate(value: string) {
  return parseDateOnly(value).getUTCDay();
}

function weekdayLabel(index: number) {
  return ['일', '월', '화', '수', '목', '금', '토'][index] ?? '';
}

function dateClassName(base: string, date: ProductionCalendarDateColumn) {
  return [
    base,
    date.isSaturday ? 'calendar-saturday' : '',
    date.isSunday || date.isHoliday ? 'calendar-red-day' : '',
    date.isCompanyHoliday ? 'calendar-company-holiday' : ''
  ].filter(Boolean).join(' ');
}

function formatCalendarHolidayLabel(date: ProductionCalendarDateColumn) {
  if (!date.holidayName) {
    return null;
  }

  const holidayType = holidayTypeLabel(date.holidayType);
  return holidayType ? `${date.holidayName} · ${holidayType}` : date.holidayName;
}

function calendarDateTitle(date: ProductionCalendarDateColumn) {
  const holidayLabel = formatCalendarHolidayLabel(date);
  if (holidayLabel) {
    return `${date.date} ${holidayLabel}`;
  }

  return date.isBusinessDay ? `${date.date} 영업일` : `${date.date} 비영업일`;
}

function holidayTypeLabel(value: string | null) {
  if (value === 'Company') {
    return '회사휴일';
  }

  if (value === 'Temporary') {
    return '임시공휴일';
  }

  if (value === 'Substitute') {
    return '대체공휴일';
  }

  if (value === 'National') {
    return '국가공휴일';
  }

  return null;
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}

function formatKoreanDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Seoul',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false
  }).formatToParts(date);
  const valueByType = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${valueByType.year}-${valueByType.month}-${valueByType.day} ${valueByType.hour}:${valueByType.minute}`;
}

function formatNullableDateTime(value: string | null | undefined) {
  return value ? formatDateTime(value) : '-';
}

function formatPackagingMethod(value: string | null) {
  if (value === 'WoodenCrate') {
    return '목포장';
  }

  if (value === 'StretchWrap') {
    return '청랩포장';
  }

  if (value === 'HeavyDutyBox') {
    return '고강도박스포장';
  }

  return value ?? '미지정';
}

function formatProjectStatus(status: ProjectStatus) {
  return {
    Active: '진행',
    OnHold: '보류',
    Completed: '완료',
    Cancelled: '취소'
  }[status];
}

function formatProjectWorkStatus(status: ProjectWorkStatus) {
  return {
    SalesProjectCreated: '프로젝트 생성',
    ProductionPlanning: '생산관리',
    DesignPanelInfo: '설계',
    ProcurementInfo: '구매정보',
    MaterialArrived: '자재 도착',
    IQC: '수입검사',
    ReceiptConfirmed: '입고 확정',
    KittingCompleted: '키팅 완료',
    ManufacturingWork: '제조 작업',
    LQC: 'LQC',
    ManufacturingCompleted: '제조 완료',
    OQC: '자체검수',
    CustomerInspection: '전진검수',
    FAT: 'FAT',
    PackingCompleted: '포장 완료',
    DepartureProcessed: '출발 처리',
    DeliveryCompleted: '납품 완료',
    SalesSettlementCompleted: '영업 정산',
    BeforeManufacturing: '제조 전',
    ManufacturingInProgress: '제조 중',
    InspectionInProgress: '검사 중',
    InspectionCompleted: '검사 완료',
    ReadyForShipment: '납품 준비',
    ShipmentCompleted: '납품 완료',
    OnHold: '보류',
    Completed: '완료',
    Cancelled: '취소'
  }[status];
}

function projectExcelResultLabel(resultType: ProjectExcelPreviewResponse['rows'][number]['resultType']) {
  return {
    New: '신규',
    NeedsReview: '확인 필요',
    Error: '오류'
  }[resultType];
}

function canApplyProjectExcel(
  preview: ProjectExcelPreviewResponse | null,
  file: File | null,
  isApplying: boolean
) {
  return !projectExcelApplyDisabledReason(preview, file, isApplying);
}

function projectExcelApplyDisabledReason(
  preview: ProjectExcelPreviewResponse | null,
  file: File | null,
  isApplying: boolean
) {
  if (!file) {
    return '선택한 파일이 없습니다.';
  }

  if (!preview) {
    return '미리보기를 먼저 실행해 주세요.';
  }

  if (isApplying) {
    return '저장 중입니다.';
  }

  if (preview.errorCount > 0) {
    return '오류 행이 있습니다.';
  }

  if (preview.needsReviewCount > 0) {
    return '확인할 항목이 있습니다. 내용을 확인해 주세요.';
  }

  if (preview.newCount === 0) {
    return '변경사항이 없습니다.';
  }

  return '';
}

function procurementApplyDisabledReason(
  preview: ProcurementExcelPreviewResponse | null,
  file: File | null,
  reason: string,
  isApplying: boolean
) {
  if (!file) {
    return '선택한 파일이 없습니다.';
  }

  if (!preview) {
    return '미리보기를 먼저 실행해 주세요.';
  }

  if (isApplying) {
    return '저장 중입니다.';
  }

  if (preview.newCount + preview.changedCount === 0) {
    if (preview.errorCount > 0) {
      return '저장 가능한 항목이 없습니다. 오류 행을 수정해 주세요.';
    }

    if (preview.needsReviewCount > 0) {
      return '저장 가능한 항목이 없습니다. 확인할 프로젝트를 선택해 주세요.';
    }

    return '변경사항이 없습니다.';
  }

  if (preview.reasonRequired && reason.trim().length === 0) {
    return '수정사유를 입력해 주세요.';
  }

  return '';
}

function panelExcelApplyDisabledReason(
  preview: PanelInformationExcelPreviewResponse | null,
  file: File | null,
  reason: string,
  isApplying: boolean
) {
  if (!file) {
    return '선택한 파일이 없습니다.';
  }

  if (!preview) {
    return '미리보기를 먼저 실행해 주세요.';
  }

  if (isApplying) {
    return '저장 중입니다.';
  }

  if (preview.errorCount > 0) {
    return '오류 행이 있습니다.';
  }

  if (preview.newCount + preview.changedCount === 0) {
    return '변경사항이 없습니다.';
  }

  if (preview.reasonRequired && reason.trim().length === 0) {
    return '수정사유를 입력해 주세요.';
  }

  return '';
}

function formatProjectProgress(progressPercent: number | null) {
  return progressPercent === null ? '-' : `${progressPercent}%`;
}

function formatPanelStatus(status: PanelInformationPanel['panelStatus'] | PanelPlaceholder['panelStatus']) {
  return status === 'Active' ? '진행' : '취소';
}

function formatWorkflowStage(stage: ProductWorkflowStage) {
  return {
    BeforeManufacturing: '제조 전',
    ManufacturingInProgress: '제조 중',
    ManufacturingCompleted: '제조 완료',
    InspectionInProgress: '검사 중',
    InspectionCompleted: '검사 완료',
    PackingCompleted: '포장 완료',
    ShipmentCompleted: '납품 완료'
  }[stage];
}

function displayWorkflowStageName(stageCode: string, stageName: string) {
  return stageCode === 'DesignPanelInfo' ? '패널명·사이즈' : stageName;
}

function hasImplementedStageInput(stageCode: string) {
  return stageCode === 'ProductionPlanning'
    || stageCode === 'DesignPanelInfo'
    || stageCode === 'ProcurementInfo';
}

function displayWorkflowStageLabel(departmentLabel: string, stageCode: string, stageName: string) {
  if (stageCode === 'DesignPanelInfo') {
    return departmentLabel;
  }

  return `${departmentLabel} / ${displayWorkflowStageName(stageCode, stageName)}`;
}

function previewResultLabel(resultType: PanelInformationExcelPreviewResponse['rows'][number]['resultType']) {
  return {
    New: '적용 예정',
    Changed: '적용 예정',
    Unchanged: '동일',
    Skipped: '건너뜀',
    Error: '오류'
  }[resultType];
}

function formatPreviewSize(value: Pick<PanelInformationPanel, 'widthMm' | 'heightMm' | 'depthMm'> | PanelInformationExcelPreviewResponse['rows'][number] | null) {
  if (!value || value.widthMm === null || value.heightMm === null || value.depthMm === null) {
    return '-';
  }

  return `${canonicalMmString(value.widthMm)} / ${canonicalMmString(value.heightMm)} / ${canonicalMmString(value.depthMm)}`;
}

function readDisplayUnit(): PanelInputUnit {
  return window.localStorage.getItem('emi-qms-panel-display-unit') === 'Inch' ? 'Inch' : 'Mm';
}

function panelToRowForm(panel: PanelInformationPanel, inputUnit: PanelInputUnit): PanelInformationRowForm {
  const originalWidthMm = canonicalMmString(panel.widthMm);
  const originalHeightMm = canonicalMmString(panel.heightMm);
  const originalDepthMm = canonicalMmString(panel.depthMm);
  return {
    panelId: panel.panelId,
    sequenceNumber: panel.sequenceNumber,
    panelNumber: panel.panelNumber,
    displayCode: panel.displayCode,
    panelInfoVersion: panel.panelInfoVersion,
    original: panel,
    originalPanelName: panel.panelName ?? '',
    currentPanelName: panel.panelName ?? '',
    panelNameDirty: false,
    originalWidthMm,
    originalHeightMm,
    originalDepthMm,
    widthInput: formatInputDimension(originalWidthMm, inputUnit),
    heightInput: formatInputDimension(originalHeightMm, inputUnit),
    depthInput: formatInputDimension(originalDepthMm, inputUnit),
    sizeDirty: false,
    sizeClearRequested: false,
    sizeInputUnit: inputUnit
  };
}

function rowWithSizeInputs(row: PanelInformationRowForm, inputUnit: PanelInputUnit): PanelInformationRowForm {
  return {
    ...row,
    widthInput: formatInputDimension(row.originalWidthMm, inputUnit),
    heightInput: formatInputDimension(row.originalHeightMm, inputUnit),
    depthInput: formatInputDimension(row.originalDepthMm, inputUnit),
    sizeInputUnit: inputUnit,
    sizeDirty: false,
    sizeClearRequested: false
  };
}

function canonicalMmString(valueMm: number | null) {
  return valueMm === null ? null : trimTrailingZeros(valueMm.toFixed(3));
}

function formatInputDimension(valueMm: string | null, unit: PanelInputUnit) {
  if (valueMm === null) {
    return '';
  }

  const numericMm = Number(valueMm);
  const value = unit === 'Inch' ? numericMm / 25.4 : numericMm;
  return trimTrailingZeros(value.toFixed(unit === 'Inch' ? 3 : 3));
}

function decimalOrNull(value: string) {
  const trimmed = value.trim();
  return trimmed ? Number(trimmed) : null;
}

function panelRowChanged(row: PanelInformationRowForm) {
  return panelNameActuallyChanged(row) || sizeActuallyChanged(row);
}

function findPanelNameDuplicateGroups(rows: PanelInformationRowForm[]): PanelNameDuplicateGroup[] {
  const groups = new Map<string, PanelNameDuplicateGroup>();
  for (const row of rows) {
    if (row.original.panelStatus !== 'Active') {
      continue;
    }

    const normalized = normalizePanelDuplicateName(row.currentPanelName);
    if (!normalized) {
      continue;
    }

    const group = groups.get(normalized) ?? {
      name: row.currentPanelName.trim().replace(/\s+/g, ' '),
      panelNumbers: []
    };
    group.panelNumbers.push(row.panelNumber);
    groups.set(normalized, group);
  }

  return [...groups.values()].filter((group) => group.panelNumbers.length > 1);
}

function normalizePanelDuplicateName(value: string) {
  const normalized = value.trim().replace(/\s+/g, ' ').toUpperCase();
  return normalized || null;
}

function panelRowNeedsReason(row: PanelInformationRowForm) {
  if (!panelRowChanged(row)) {
    return false;
  }

  return (panelNameActuallyChanged(row) && row.originalPanelName !== '')
    || (sizeActuallyChanged(row) && (
      row.originalWidthMm !== null
      || row.originalHeightMm !== null
      || row.originalDepthMm !== null
    ));
}

function panelNameActuallyChanged(row: PanelInformationRowForm) {
  return row.panelNameDirty && row.currentPanelName.trim() !== row.originalPanelName;
}

function sizeActuallyChanged(row: PanelInformationRowForm) {
  if (!row.sizeDirty) {
    return false;
  }

  return row.originalWidthMm !== canonicalInputToMmString(decimalOrNull(row.widthInput), row.sizeInputUnit)
    || row.originalHeightMm !== canonicalInputToMmString(decimalOrNull(row.heightInput), row.sizeInputUnit)
    || row.originalDepthMm !== canonicalInputToMmString(decimalOrNull(row.depthInput), row.sizeInputUnit);
}

function canonicalInputToMmString(value: number | null, unit: PanelInputUnit) {
  if (value === null) {
    return null;
  }

  return trimTrailingZeros(round3(unit === 'Inch' ? value * 25.4 : value).toFixed(3));
}

function round3(value: number) {
  return Math.round((value + Number.EPSILON) * 1000) / 1000;
}

function panelRowToUpdateRequest(row: PanelInformationRowForm) {
  const request: {
    panelId: string;
    expectedPanelInfoVersion: number;
    panelNameUpdate?: { isChanged: boolean; value: string | null };
    sizeUpdate?: {
      isChanged: boolean;
      clear: boolean;
      inputUnit: PanelInputUnit | null;
      width: number | null;
      height: number | null;
      depth: number | null;
    };
  } = {
    panelId: row.panelId,
    expectedPanelInfoVersion: row.panelInfoVersion
  };

  if (panelNameActuallyChanged(row)) {
    request.panelNameUpdate = {
      isChanged: true,
      value: row.currentPanelName.trim() || null
    };
  }

  if (row.sizeDirty) {
    request.sizeUpdate = {
      isChanged: true,
      clear: row.sizeClearRequested,
      inputUnit: row.sizeClearRequested ? null : row.sizeInputUnit,
      width: row.sizeClearRequested ? null : decimalOrNull(row.widthInput),
      height: row.sizeClearRequested ? null : decimalOrNull(row.heightInput),
      depth: row.sizeClearRequested ? null : decimalOrNull(row.depthInput)
    };
  }

  return request;
}

function trimTrailingZeros(value: string) {
  return value.replace(/\.?0+$/, '');
}

function emptyDash(value: string | null | undefined) {
  return value && value.trim() ? value : '-';
}

function productionPlanItemToForm(item: ProductionPlanningResponse['items'][number]): ProductionPlanRowForm {
  return {
    itemId: item.itemId,
    templateStepId: item.templateStepId,
    sequenceNumber: item.sequenceNumber,
    stepName: item.stepName,
    isRequired: item.isRequired,
    isCustom: item.isCustom,
    isDeleted: false,
    plannedDate: item.plannedDate ?? '',
    note: item.note ?? '',
    rowVersion: item.rowVersion
  };
}

function projectAssigneeToForm(assignee: ProjectAssignee): ProjectAssigneeForm {
  return {
    assigneeId: assignee.assigneeId,
    responsibilityType: assignee.responsibilityType,
    responsibilityLabel: assignee.responsibilityLabel,
    assignedUserId: assignee.assignedUserId ?? '',
    note: assignee.note ?? '',
    rowVersion: assignee.rowVersion
  };
}

function formatShipmentDisplayDate(item: ProcurementItem) {
  return item.shipmentDisplayDate ?? item.projectDeliveryDate;
}

function procurementItemToForm(item: ProcurementItem): ProcurementRowForm {
  return {
    itemId: item.itemId,
    rowVersion: item.rowVersion,
    sourceProjectText: item.sourceProjectText ?? item.projectTitle,
    sourceProjectCodeText: item.sourceProjectCodeText ?? item.projectCode,
    standardLeadTime: item.standardLeadTime ?? '',
    orderItem: item.orderItem ?? '',
    supplierName: item.supplierName ?? '',
    technicalOwner: item.technicalOwner ?? '',
    orderDate: item.orderDate ?? '',
    expectedReceiptDate: item.expectedReceiptDate ?? '',
    shipmentDisplayDate: formatShipmentDisplayDate(item),
    issueNote: item.issueNote ?? '',
    supplyType: item.supplyType,
    orderQuantity: item.orderQuantity?.toString() ?? '',
    orderUnit: item.orderUnit ?? '',
    receiptCompleted: item.receiptCompleted,
    receiptCompletedAtUtc: item.receiptCompletedAtUtc ?? '',
    receiptCompletionNote: item.receiptCompletionNote ?? '',
    dDayText: item.dDayText
  };
}

function emptyProcurementRow(projectDeliveryDate: string | null = null): ProcurementRowForm {
  return {
    itemId: null,
    rowVersion: null,
    sourceProjectText: '',
    sourceProjectCodeText: '',
    standardLeadTime: '',
    orderItem: '',
    supplierName: '',
    technicalOwner: '',
    orderDate: '',
    expectedReceiptDate: '',
    shipmentDisplayDate: projectDeliveryDate,
    issueNote: '',
    supplyType: 'Purchased',
    orderQuantity: '',
    orderUnit: '',
    receiptCompleted: false,
    receiptCompletedAtUtc: '',
    receiptCompletionNote: '',
    dDayText: '-'
  };
}

function procurementFormToRequest(row: ProcurementRowForm) {
  return {
    itemId: row.itemId,
    expectedRowVersion: row.rowVersion,
    standardLeadTime: row.standardLeadTime.trim() || null,
    orderItem: row.orderItem.trim() || null,
    supplierName: row.supplierName.trim() || null,
    technicalOwner: row.technicalOwner.trim() || null,
    orderDate: row.orderDate || null,
    expectedReceiptDate: row.expectedReceiptDate || null,
    issueNote: row.issueNote.trim() || null,
    supplyType: row.supplyType,
    orderQuantity: row.supplyType === 'CustomerSupplied' && row.orderQuantity.trim() ? Number(row.orderQuantity) : null,
    orderUnit: row.supplyType === 'CustomerSupplied' ? row.orderUnit.trim() || null : null
  };
}

function isProcurementForm(value: ProcurementItem | ProcurementRowForm): value is ProcurementRowForm {
  return !('projectId' in value);
}

function toDateTimeLocal(value: string | null) {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

function fromDateTimeLocal(value: string) {
  return value ? new Date(value).toISOString() : '';
}

function successMessage(message: string) {
  return message.includes('저장했습니다') || message.includes('다운로드했습니다');
}

function triggerExcelDownload(file: { blob: Blob; fileName: string }) {
  let url: string | null = null;
  try {
    url = URL.createObjectURL(file.blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = file.fileName;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    return true;
  } catch {
    return false;
  } finally {
    if (url) URL.revokeObjectURL(url);
  }
}

function formatPanelSizeInUnit(panel: PanelInformationPanel, unit: PanelInputUnit) {
  if (panel.widthMm === null || panel.heightMm === null || panel.depthMm === null) {
    return '미입력';
  }

  if (unit === 'Inch') {
    return `${(panel.widthMm / 25.4).toFixed(2)} × ${(panel.heightMm / 25.4).toFixed(2)} × ${(panel.depthMm / 25.4).toFixed(2)} inch`;
  }

  return `${trimTrailingZeros(panel.widthMm.toFixed(3))} × ${trimTrailingZeros(panel.heightMm.toFixed(3))} × ${trimTrailingZeros(panel.depthMm.toFixed(3))} mm`;
}

function formatPanelSizeForPackaging(
  panel: PanelInformationPanel,
  unit: PanelInputUnit,
  packagingMethod: PackagingMethod | null
) {
  if (panel.widthMm !== null && panel.heightMm !== null && panel.depthMm !== null) {
    return formatPanelSizeInUnit(panel, unit);
  }

  return packagingMethod === 'WoodenCrate' ? '미입력' : '선택사항';
}

function panelSizeClass(panel: PanelInformationPanel, packagingMethod: PackagingMethod | null) {
  return packagingMethod === 'WoodenCrate'
    && (panel.widthMm === null || panel.heightMm === null || panel.depthMm === null)
    ? 'negative-text'
    : undefined;
}

function projectTabs(canReadDeleted: boolean): Array<{ value: ProjectListTab; label: string }> {
  const tabs: Array<{ value: ProjectListTab; label: string }> = [
    { value: 'All', label: '전체' },
    { value: 'Active', label: '진행' },
    { value: 'OnHold', label: '보류' },
    { value: 'Completed', label: '완료' },
    { value: 'Cancelled', label: '취소' }
  ];

  if (canReadDeleted) {
    tabs.push({ value: 'Deleted', label: '삭제 보관함' });
  }

  return tabs;
}

function formatSize(panel: PanelPlaceholder) {
  if (panel.width === null || panel.height === null || panel.depth === null) {
    return '미입력';
  }

  return `${panel.width} × ${panel.height} × ${panel.depth} mm`;
}
