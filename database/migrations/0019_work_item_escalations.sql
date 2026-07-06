create table if not exists work_item_escalations (
    id uuid primary key default uuid_generate_v4(),
    work_item_id uuid not null references work_items(id) on delete cascade,
    project_id uuid not null references projects(id) on delete cascade,
    workflow_stage_code text not null references workflow_stages(stage_code),
    assigned_user_id uuid null references qms_users(id) on delete set null,
    due_date date not null,
    status text not null default 'Active',
    current_level text not null default 'None',
    last_escalated_at_utc timestamptz null,
    next_check_at_utc timestamptz null,
    l0_sent_at_utc timestamptz null,
    l1_sent_at_utc timestamptz null,
    l2_sent_at_utc timestamptz null,
    l3_sent_at_utc timestamptz null,
    resolved_at_utc timestamptz null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ux_work_item_escalations_work_item unique (work_item_id),
    constraint ck_work_item_escalations_status check (status in ('Active', 'Resolved', 'Cancelled')),
    constraint ck_work_item_escalations_current_level check (current_level in ('None', 'L0', 'L1', 'L2', 'L3'))
);

create index if not exists ix_work_item_escalations_status_next_check
    on work_item_escalations(status, next_check_at_utc, updated_at_utc);

create index if not exists ix_work_item_escalations_due_date
    on work_item_escalations(due_date);

create index if not exists ix_work_item_escalations_project
    on work_item_escalations(project_id);

create index if not exists ix_work_item_escalations_assigned_user
    on work_item_escalations(assigned_user_id);

do $$
begin
    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_delivery_type;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_delivery_type
        check (delivery_type in (
            'WorkItemCreated',
            'ReferenceDigest',
            'UrgentBlocking',
            'DailyDigest',
            'ProjectCompletion',
            'ManualTest',
            'DueSoonL0',
            'OverdueL1',
            'OverdueL2',
            'OverdueL3'
        ));
end $$;
