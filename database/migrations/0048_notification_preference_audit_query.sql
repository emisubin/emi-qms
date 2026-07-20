create index if not exists ix_user_notification_preference_audit_occurred
    on user_notification_preference_audit_events(occurred_at_utc desc, id desc);

alter table data_export_events
    drop constraint if exists ck_data_export_events_kind;

alter table data_export_events
    add constraint ck_data_export_events_kind
    check (export_kind in (
        'Projects',
        'ProjectsSelected',
        'ProcurementDashboard',
        'MyWork',
        'MyWorkSelected',
        'ProductionPlanningSelected',
        'ProcurementDashboardSelected',
        'MaterialReceiptsSelected',
        'PanelKittingSelected',
        'ManufacturingSelected',
        'QualityIqcSelected',
        'QualityInspectionsSelected',
        'LogisticsSelected',
        'PendingSelected',
        'NotificationsSelected',
        'AdminUsersSelected',
        'AdminDepartmentsSelected',
        'AdminCalendarHolidaysSelected',
        'AdminPermissionMatrixSelected',
        'AdminMasterChangeLogsSelected',
        'AdminWorkHistorySelected',
        'AdminNotificationDeliveriesSelected',
        'AdminNotificationPreferenceAuditSelected',
        'AdminWorkItemEscalationsSelected'
    ));
