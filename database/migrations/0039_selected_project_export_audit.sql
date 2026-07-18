alter table data_export_events
    drop constraint ck_data_export_events_kind;

alter table data_export_events
    add constraint ck_data_export_events_kind
    check (export_kind in ('Projects', 'ProjectsSelected', 'ProcurementDashboard', 'MyWork'));

alter table data_export_events
    drop constraint ck_data_export_events_sensitive_columns;

alter table data_export_events
    add constraint ck_data_export_events_sensitive_columns
    check (
        not sensitive_sales_amount_included
        or export_kind in ('Projects', 'ProjectsSelected')
    );
