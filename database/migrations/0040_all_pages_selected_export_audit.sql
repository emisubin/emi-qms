alter table data_export_events
    drop constraint ck_data_export_events_kind;

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
        'AdminWorkItemEscalationsSelected'
    ));
