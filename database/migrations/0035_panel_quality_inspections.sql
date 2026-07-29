create table if not exists panel_quality_template_versions (
    id uuid primary key,
    stage_code text not null references workflow_stages(stage_code),
    version_number integer not null,
    display_name text not null,
    is_active boolean not null default false,
    activated_at_utc timestamptz null,
    created_at_utc timestamptz not null default now(),
    constraint ux_panel_quality_template_versions unique (stage_code, version_number),
    constraint ck_panel_quality_template_versions_stage check (
        stage_code in ('LQC', 'OQC', 'CustomerInspection', 'FAT')
    ),
    constraint ck_panel_quality_template_versions_name check (
        char_length(btrim(display_name)) between 2 and 100
    ),
    constraint ck_panel_quality_template_versions_activation check (
        (is_active and activated_at_utc is not null)
        or (not is_active and activated_at_utc is null)
    )
);

create unique index if not exists ux_panel_quality_template_versions_active
    on panel_quality_template_versions(stage_code)
    where is_active;

create table if not exists panel_quality_template_items (
    id uuid primary key,
    template_version_id uuid not null references panel_quality_template_versions(id) on delete restrict,
    item_code text not null,
    display_order integer not null,
    label text not null,
    guidance text null,
    response_type text not null,
    is_required boolean not null default true,
    requires_photo boolean not null default false,
    max_text_length integer null,
    constraint ux_panel_quality_template_items_code unique (template_version_id, item_code),
    constraint ux_panel_quality_template_items_order unique (template_version_id, display_order),
    constraint ck_panel_quality_template_items_code check (item_code ~ '^[A-Z0-9_]{2,40}$'),
    constraint ck_panel_quality_template_items_order check (display_order >= 1),
    constraint ck_panel_quality_template_items_label check (char_length(btrim(label)) between 1 and 200),
    constraint ck_panel_quality_template_items_guidance check (
        guidance is null or char_length(btrim(guidance)) between 1 and 500
    ),
    constraint ck_panel_quality_template_items_type check (response_type in ('Check', 'Text')),
    constraint ck_panel_quality_template_items_text_length check (
        (response_type = 'Check' and max_text_length is null)
        or (response_type = 'Text' and max_text_length between 1 and 2000)
    ),
    constraint ck_panel_quality_template_items_photo check (not requires_photo or response_type = 'Check')
);

create table if not exists panel_quality_inspection_attempts (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id),
    panel_id uuid not null references panel_placeholders(id),
    stage_code text not null references workflow_stages(stage_code),
    attempt_number integer not null,
    status text not null default 'Requested',
    work_item_id uuid not null references work_items(id),
    linked_pending_issue_id uuid null references pending_issues(id) on delete set null,
    version integer not null default 1,
    started_by_user_id uuid null references qms_users(id),
    started_at_utc timestamptz null,
    completed_by_user_id uuid null references qms_users(id),
    completed_at_utc timestamptz null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ux_panel_quality_inspection_attempts_number unique (panel_id, stage_code, attempt_number),
    constraint ck_panel_quality_inspection_attempts_stage check (
        stage_code in ('LQC', 'OQC', 'CustomerInspection', 'FAT')
    ),
    constraint ck_panel_quality_inspection_attempts_number check (attempt_number >= 1),
    constraint ck_panel_quality_inspection_attempts_status check (
        status in ('Requested', 'InProgress', 'Passed', 'Failed', 'Cancelled')
    ),
    constraint ck_panel_quality_inspection_attempts_version check (version >= 1),
    constraint ck_panel_quality_inspection_attempts_started check (
        (status = 'Requested' and started_by_user_id is null and started_at_utc is null)
        or (status <> 'Requested' and started_by_user_id is not null and started_at_utc is not null)
    ),
    constraint ck_panel_quality_inspection_attempts_completed check (
        (status in ('Passed', 'Failed', 'Cancelled') and completed_at_utc is not null)
        or (status in ('Requested', 'InProgress') and completed_at_utc is null and completed_by_user_id is null)
    )
);

create unique index if not exists ux_panel_quality_inspection_attempts_active
    on panel_quality_inspection_attempts(panel_id, stage_code)
    where status in ('Requested', 'InProgress');

create index if not exists ix_panel_quality_inspection_attempts_queue
    on panel_quality_inspection_attempts(project_id, stage_code, status, updated_at_utc desc);

create unique index if not exists ux_panel_quality_inspection_attempts_linked_pending
    on panel_quality_inspection_attempts(linked_pending_issue_id)
    where linked_pending_issue_id is not null and status = 'Failed';

create table if not exists panel_quality_reports (
    id uuid primary key default uuid_generate_v4(),
    attempt_id uuid not null unique references panel_quality_inspection_attempts(id) on delete restrict,
    template_version_id uuid not null references panel_quality_template_versions(id) on delete restrict,
    status text not null default 'Draft',
    version integer not null default 1,
    result text null,
    reason text null,
    finalized_by_user_id uuid null references qms_users(id),
    finalized_at_utc timestamptz null,
    snapshot_text text null,
    snapshot_sha256 text null,
    pdf_status text null,
    pdf_error_code text null,
    pdf_last_attempt_at_utc timestamptz null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    constraint ck_panel_quality_reports_status check (status in ('Draft', 'Finalized')),
    constraint ck_panel_quality_reports_version check (version >= 1),
    constraint ck_panel_quality_reports_result check (result is null or result in ('Passed', 'Failed')),
    constraint ck_panel_quality_reports_pdf_status check (
        pdf_status is null or pdf_status in ('Pending', 'Ready', 'Failed')
    ),
    constraint ck_panel_quality_reports_pdf_error check (
        pdf_error_code is null or pdf_error_code ~ '^[a-z0-9_]{3,60}$'
    ),
    constraint ck_panel_quality_reports_snapshot_hash check (
        snapshot_sha256 is null or snapshot_sha256 ~ '^[a-f0-9]{64}$'
    ),
    constraint ck_panel_quality_reports_lifecycle check (
        (status = 'Draft' and result is null and reason is null and finalized_by_user_id is null
            and finalized_at_utc is null and snapshot_text is null and snapshot_sha256 is null
            and pdf_status is null and pdf_error_code is null and pdf_last_attempt_at_utc is null)
        or
        (status = 'Finalized' and result in ('Passed', 'Failed')
            and char_length(btrim(reason)) between 3 and 1000
            and finalized_by_user_id is not null and finalized_at_utc is not null
            and snapshot_text is not null and snapshot_sha256 is not null and pdf_status is not null)
    )
);

create index if not exists ix_panel_quality_reports_status
    on panel_quality_reports(status, updated_at_utc desc);

create table if not exists panel_quality_report_responses (
    report_id uuid not null references panel_quality_reports(id) on delete restrict,
    template_item_id uuid not null references panel_quality_template_items(id) on delete restrict,
    check_result text null,
    text_value text null,
    note text null,
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    primary key (report_id, template_item_id),
    constraint ck_panel_quality_report_responses_check check (
        check_result is null or check_result in ('Pass', 'Fail', 'NotApplicable')
    ),
    constraint ck_panel_quality_report_responses_note check (
        note is null or char_length(btrim(note)) between 1 and 1000
    ),
    constraint ck_panel_quality_report_responses_value check (
        text_value is null or char_length(text_value) between 1 and 2000
    ),
    constraint ck_panel_quality_report_responses_has_value check (
        check_result is not null or text_value is not null
    )
);

create table if not exists panel_quality_report_photos (
    id uuid primary key default uuid_generate_v4(),
    report_id uuid not null references panel_quality_reports(id) on delete restrict,
    template_item_id uuid not null references panel_quality_template_items(id) on delete restrict,
    display_name text not null,
    normalized_mime text not null,
    byte_size integer not null,
    sha256 text not null,
    alt_text text not null,
    content bytea not null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    constraint ck_panel_quality_report_photos_name check (display_name ~ '^photo-[1-5]\.(jpg|png)$'),
    constraint ck_panel_quality_report_photos_mime check (normalized_mime in ('image/jpeg', 'image/png')),
    constraint ck_panel_quality_report_photos_size check (
        byte_size between 1 and 5242880 and octet_length(content) = byte_size
    ),
    constraint ck_panel_quality_report_photos_sha check (sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_panel_quality_report_photos_alt check (char_length(btrim(alt_text)) between 1 and 200)
);

create index if not exists ix_panel_quality_report_photos_report
    on panel_quality_report_photos(report_id, created_at_utc, id);

create table if not exists panel_quality_report_pdf_artifacts (
    id uuid primary key default uuid_generate_v4(),
    report_id uuid not null unique references panel_quality_reports(id) on delete restrict,
    snapshot_sha256 text not null,
    byte_size integer not null,
    sha256 text not null,
    content bytea not null,
    generator text not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_panel_quality_report_pdf_snapshot_sha check (snapshot_sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_panel_quality_report_pdf_sha check (sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_panel_quality_report_pdf_size check (byte_size > 0 and octet_length(content) = byte_size),
    constraint ck_panel_quality_report_pdf_generator check (generator = 'PDFsharp-6.2.4')
);

create table if not exists panel_manufacturing_completion_confirmations (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id),
    panel_id uuid not null unique references panel_placeholders(id),
    lqc_attempt_id uuid not null references panel_quality_inspection_attempts(id) on delete restrict,
    work_item_id uuid not null unique references work_items(id),
    confirmed_by_user_id uuid not null references qms_users(id),
    confirmed_at_utc timestamptz not null default now()
);

create table if not exists panel_quality_operations (
    operation_id uuid primary key,
    action text not null,
    project_id uuid not null references projects(id),
    panel_id uuid not null references panel_placeholders(id),
    stage_code text null,
    attempt_id uuid null references panel_quality_inspection_attempts(id) on delete restrict,
    requested_by_user_id uuid not null references qms_users(id),
    payload_fingerprint text not null,
    result_projection jsonb not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_panel_quality_operations_action check (
        action in ('Start', 'SaveResponses', 'AddPhoto', 'RemovePhoto', 'Finalize',
                   'RequestReinspection', 'ConfirmManufacturingCompleted', 'RetryPdf')
    ),
    constraint ck_panel_quality_operations_stage check (
        stage_code is null or stage_code in ('LQC', 'ManufacturingCompleted', 'OQC', 'CustomerInspection', 'FAT')
    ),
    constraint ck_panel_quality_operations_fingerprint check (payload_fingerprint ~ '^[0-9a-f]{64}$'),
    constraint ck_panel_quality_operations_projection check (jsonb_typeof(result_projection) = 'object')
);

create index if not exists ix_panel_quality_operations_panel
    on panel_quality_operations(panel_id, created_at_utc desc);

create or replace function guard_panel_quality_pdf_artifact_immutable()
returns trigger
language plpgsql
as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;
    raise exception 'panel quality PDF artifact is immutable' using errcode = 'P0001';
end;
$$;

drop trigger if exists trg_guard_panel_quality_pdf_artifact_immutable on panel_quality_report_pdf_artifacts;
create trigger trg_guard_panel_quality_pdf_artifact_immutable
before update or delete on panel_quality_report_pdf_artifacts
for each row execute function guard_panel_quality_pdf_artifact_immutable();

create or replace function guard_finalized_panel_quality_report_children()
returns trigger
language plpgsql
as $$
declare
    owner_report_id uuid;
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;
    owner_report_id := case when tg_op = 'DELETE' then old.report_id else new.report_id end;
    if exists (
        select 1 from panel_quality_reports
        where id = owner_report_id and status = 'Finalized'
    ) then
        raise exception 'finalized panel quality evidence is immutable' using errcode = 'P0001';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end;
$$;

drop trigger if exists trg_guard_finalized_panel_quality_responses on panel_quality_report_responses;
create trigger trg_guard_finalized_panel_quality_responses
before insert or update or delete on panel_quality_report_responses
for each row execute function guard_finalized_panel_quality_report_children();

drop trigger if exists trg_guard_finalized_panel_quality_photos on panel_quality_report_photos;
create trigger trg_guard_finalized_panel_quality_photos
before insert or update or delete on panel_quality_report_photos
for each row execute function guard_finalized_panel_quality_report_children();

create or replace function guard_finalized_panel_quality_report_core()
returns trigger
language plpgsql
as $$
begin
    if old.status = 'Finalized' and (
        new.attempt_id, new.template_version_id, new.status, new.version, new.result, new.reason,
        new.finalized_by_user_id, new.finalized_at_utc, new.snapshot_text, new.snapshot_sha256,
        new.created_by_user_id, new.created_at_utc
    ) is distinct from (
        old.attempt_id, old.template_version_id, old.status, old.version, old.result, old.reason,
        old.finalized_by_user_id, old.finalized_at_utc, old.snapshot_text, old.snapshot_sha256,
        old.created_by_user_id, old.created_at_utc
    ) then
        raise exception 'finalized panel quality report core is immutable' using errcode = 'P0001';
    end if;
    return new;
end;
$$;

drop trigger if exists trg_guard_finalized_panel_quality_report_core on panel_quality_reports;
create trigger trg_guard_finalized_panel_quality_report_core
before update on panel_quality_reports
for each row execute function guard_finalized_panel_quality_report_core();

insert into panel_quality_template_versions (
    id, stage_code, version_number, display_name, is_active, activated_at_utc
)
values
    ('93000000-0000-0000-0000-000000000101', 'LQC', 1, 'LQC 기본 검사', true, now()),
    ('93000000-0000-0000-0000-000000000102', 'OQC', 1, 'OQC 자체검수', true, now()),
    ('93000000-0000-0000-0000-000000000103', 'CustomerInspection', 1, '전진검수', true, now()),
    ('93000000-0000-0000-0000-000000000104', 'FAT', 1, 'FAT 시험검사', true, now())
on conflict (id) do nothing;

insert into panel_quality_template_items (
    id, template_version_id, item_code, display_order, label, guidance,
    response_type, is_required, requires_photo, max_text_length
)
values
    ('93000000-0000-0000-0000-000000000201', '93000000-0000-0000-0000-000000000101', 'DRAWING_STANDARD', 1, '도면·작업기준 일치', '도면과 작업 기준을 대조해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000202', '93000000-0000-0000-0000-000000000101', 'ASSEMBLY', 2, '조립 상태', '부품 조립과 고정 상태를 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000203', '93000000-0000-0000-0000-000000000101', 'WIRING_FASTENING', 3, '배선·체결', '배선 정리와 체결 상태를 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000204', '93000000-0000-0000-0000-000000000101', 'MARKING_FINISH', 4, '표시·마감', '명판, 표시와 마감 상태를 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000205', '93000000-0000-0000-0000-000000000101', 'NOTES', 5, '추가 메모', '특이사항이 있으면 기록해 주세요.', 'Text', false, false, 1000),

    ('93000000-0000-0000-0000-000000000211', '93000000-0000-0000-0000-000000000102', 'APPEARANCE_MARKING', 1, '외관·표시', '외관 손상과 식별 표시를 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000212', '93000000-0000-0000-0000-000000000102', 'FUNCTION_CIRCUIT', 2, '기능·회로', '회로와 주요 기능을 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000213', '93000000-0000-0000-0000-000000000102', 'CONFIG_DIMENSION', 3, '구성·치수', '구성과 주요 치수를 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000214', '93000000-0000-0000-0000-000000000102', 'SHIP_READY', 4, '출하 준비', '보호와 출하 준비 상태를 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000215', '93000000-0000-0000-0000-000000000102', 'NOTES', 5, '추가 메모', '특이사항이 있으면 기록해 주세요.', 'Text', false, false, 1000),

    ('93000000-0000-0000-0000-000000000221', '93000000-0000-0000-0000-000000000103', 'INSPECTION_SCOPE', 1, '검사 범위 확인', '고객과 합의한 검사 범위를 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000222', '93000000-0000-0000-0000-000000000103', 'PUNCH_CLEAR', 2, '지적·PUNCH 여부', '남은 지적사항이 없는지 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000223', '93000000-0000-0000-0000-000000000103', 'CUSTOMER_NOTE', 3, '고객 확인 메모', '확인 내용과 특이사항을 기록해 주세요.', 'Text', false, false, 1000),

    ('93000000-0000-0000-0000-000000000231', '93000000-0000-0000-0000-000000000104', 'TEST_SCOPE', 1, '시험 범위', '승인된 FAT 시험 범위를 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000232', '93000000-0000-0000-0000-000000000104', 'TEST_RESULT', 2, '시험 결과', '주요 시험 결과가 기준에 맞는지 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000233', '93000000-0000-0000-0000-000000000104', 'CUSTOMER_CONFIRM', 3, '고객 확인', '고객 확인 여부를 기록해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000234', '93000000-0000-0000-0000-000000000104', 'PUNCH_CLEAR', 4, 'PUNCH 여부', '남은 지적사항이 없는지 확인해 주세요.', 'Check', true, false, null),
    ('93000000-0000-0000-0000-000000000235', '93000000-0000-0000-0000-000000000104', 'NOTES', 5, '추가 메모', '시험 특이사항이 있으면 기록해 주세요.', 'Text', false, false, 1000)
on conflict (id) do nothing;
