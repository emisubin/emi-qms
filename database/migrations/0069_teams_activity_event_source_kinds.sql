alter table notifications
    drop constraint if exists ck_notifications_source_kind;

alter table notifications
    add constraint ck_notifications_source_kind
    check (source_kind in (
        'Automatic',
        'Manual',
        'ChannelNotice',
        'WorkAssignment',
        'PendingAssignment',
        'ProjectCreated',
        'ProjectDeliveryDateChanged',
        'ProjectStatusChanged',
        'ReinspectionRequested',
        'ProjectCompletion',
        'DailyDigest',
        'Escalation',
        'System'
    ));
