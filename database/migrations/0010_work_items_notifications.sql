create table if not exists workflow_stages (
    id uuid primary key default uuid_generate_v4(),
    stage_code text not null unique,
    sequence_number integer not null unique,
    department_code text not null,
    stage_name text not null,
    is_optional boolean not null default false,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now(),
    constraint ck_workflow_stages_sequence_positive check (sequence_number >= 1),
    constraint ck_workflow_stages_code_not_blank check (btrim(stage_code) <> ''),
    constraint ck_workflow_stages_department_not_blank check (btrim(department_code) <> ''),
    constraint ck_workflow_stages_name_not_blank check (btrim(stage_name) <> '')
);

insert into workflow_stages (stage_code, sequence_number, department_code, stage_name, is_optional, is_active)
values
    ('SalesProjectCreated', 1, 'sales', '프로젝트 생성', false, true),
    ('ProductionPlanning', 2, 'production-planning', '생산계획·담당자', false, true),
    ('DesignPanelInfo', 3, 'design', '제품명·사이즈', false, true),
    ('ProcurementInfo', 4, 'procurement', '구매정보', false, true),
    ('MaterialArrived', 5, 'materials', '자재 도착', false, true),
    ('IQC', 6, 'quality', '수입검사', false, true),
    ('ReceiptConfirmed', 7, 'materials', '입고 확정', false, true),
    ('KittingCompleted', 8, 'materials', '키팅 완료', false, true),
    ('ManufacturingWork', 9, 'manufacturing', '제조 작업', false, true),
    ('LQC', 10, 'quality', 'LQC', false, true),
    ('ManufacturingCompleted', 11, 'manufacturing', '제조 완료', false, true),
    ('OQC', 12, 'quality', '자체검수', false, true),
    ('CustomerInspection', 13, 'quality', '전진검수', false, true),
    ('FAT', 14, 'quality', 'FAT 선택', true, true),
    ('PackingCompleted', 15, 'logistics', '포장 완료', false, true),
    ('DepartureProcessed', 16, 'logistics', '출발 처리', false, true),
    ('DeliveryCompleted', 17, 'logistics', '납품 완료', false, true),
    ('SalesSettlementCompleted', 18, 'sales', '세금계산서·완료', false, true)
on conflict (stage_code) do update
set sequence_number = excluded.sequence_number,
    department_code = excluded.department_code,
    stage_name = excluded.stage_name,
    is_optional = excluded.is_optional,
    is_active = excluded.is_active;

do $$
begin
    alter table project_assignees drop constraint if exists ck_project_assignees_responsibility_type;

    alter table project_assignees add constraint ck_project_assignees_responsibility_type
        check (responsibility_type in (
            'Procurement',
            'ProductionPlanning',
            'Manufacturing',
            'Quality',
            'Logistics',
            'SalesPrimary',
            'SalesSecondary',
            'DesignPrimary',
            'DesignSecondary',
            'ProductionPlanningPrimary',
            'ProductionPlanningSecondary',
            'ProcurementPrimary',
            'ProcurementSecondary',
            'MaterialsPrimary',
            'MaterialsSecondary',
            'ManufacturingPrimary',
            'ManufacturingSecondary',
            'LogisticsPrimary',
            'LogisticsSecondary',
            'QualityIQC',
            'QualityIQCSecondary',
            'QualityLQC',
            'QualityLQCSecondary',
            'QualityOQC',
            'QualityOQCSecondary',
            'QualityCustomerInspection',
            'QualityCustomerInspectionSecondary'
        ));
exception
    when duplicate_object then null;
end $$;

create table if not exists project_workflow_events (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete cascade,
    stage_code text not null references workflow_stages(stage_code),
    event_type text not null,
    event_status text not null,
    source_type text null,
    source_id uuid null,
    correlation_id text null,
    created_by_user_id uuid null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    note text null,
    constraint ck_project_workflow_events_type check (event_type in ('StageStarted', 'StageCompleted', 'StageReopened', 'StageSkipped', 'WorkGenerated')),
    constraint ck_project_workflow_events_status check (event_status in ('Succeeded', 'Skipped', 'Blocked')),
    constraint ck_project_workflow_events_source_type_not_blank check (source_type is null or btrim(source_type) <> '')
);

create index if not exists ix_project_workflow_events_project
    on project_workflow_events(project_id, created_at_utc desc);

create index if not exists ix_project_workflow_events_stage
    on project_workflow_events(project_id, stage_code, event_type);

create table if not exists work_items (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete cascade,
    target_type text not null,
    target_id uuid null,
    workflow_stage_code text not null references workflow_stages(stage_code),
    responsibility_type text not null,
    assigned_user_id uuid not null references qms_users(id),
    assigned_role_code text null,
    title text not null,
    description text null,
    status text not null default 'Requested',
    priority text not null default 'Normal',
    due_date date null,
    generated_by_event_id uuid null references project_workflow_events(id) on delete set null,
    idempotency_key text not null unique,
    created_at_utc timestamptz not null default now(),
    created_by_user_id uuid null references qms_users(id),
    started_at_utc timestamptz null,
    completed_at_utc timestamptz null,
    cancelled_at_utc timestamptz null,
    constraint ck_work_items_target_type check (target_type in ('Project', 'Panel', 'ProcurementItem', 'ProductionPlan', 'Pending', 'Inspection')),
    constraint ck_work_items_status check (status in ('Requested', 'InProgress', 'Completed', 'Cancelled')),
    constraint ck_work_items_priority check (priority in ('Normal', 'Blocking')),
    constraint ck_work_items_title_not_blank check (btrim(title) <> ''),
    constraint ck_work_items_responsibility_type_not_blank check (btrim(responsibility_type) <> '')
);

create index if not exists ix_work_items_assigned_user
    on work_items(assigned_user_id, status, created_at_utc desc);

create index if not exists ix_work_items_project
    on work_items(project_id, workflow_stage_code);

create table if not exists notifications (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid null references projects(id) on delete cascade,
    notification_type text not null,
    severity text not null,
    title text not null,
    message text not null,
    link_url text null,
    generated_by_event_id uuid null references project_workflow_events(id) on delete set null,
    idempotency_key text not null unique,
    created_at_utc timestamptz not null default now(),
    constraint ck_notifications_type check (notification_type in ('Reference', 'Blocking', 'Info')),
    constraint ck_notifications_severity check (severity in ('Info', 'Warning', 'Critical')),
    constraint ck_notifications_title_not_blank check (btrim(title) <> ''),
    constraint ck_notifications_message_not_blank check (btrim(message) <> '')
);

create index if not exists ix_notifications_project
    on notifications(project_id, created_at_utc desc);

create table if not exists notification_recipients (
    id uuid primary key default uuid_generate_v4(),
    notification_id uuid not null references notifications(id) on delete cascade,
    user_id uuid not null references qms_users(id),
    read_at_utc timestamptz null,
    created_at_utc timestamptz not null default now(),
    constraint ux_notification_recipients_notification_user unique (notification_id, user_id)
);

create index if not exists ix_notification_recipients_user
    on notification_recipients(user_id, read_at_utc, created_at_utc desc);
