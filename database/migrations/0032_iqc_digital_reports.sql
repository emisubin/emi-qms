alter table material_iqc_attempts
    add column if not exists decision_mode text null;

update material_iqc_attempts
set decision_mode = 'Legacy'
where decision_mode is null;

alter table material_iqc_attempts
    alter column decision_mode set default 'Detailed',
    alter column decision_mode set not null;

do $$
begin
    alter table material_iqc_attempts
        add constraint ck_material_iqc_attempts_decision_mode
        check (decision_mode in ('Legacy', 'Detailed'));
exception
    when duplicate_object then null;
end $$;

create table if not exists iqc_report_templates (
    id uuid primary key,
    template_code text not null unique,
    display_name text not null,
    is_system boolean not null default true,
    created_at_utc timestamptz not null default now(),
    constraint ck_iqc_report_templates_code check (template_code ~ '^[A-Z0-9_]{3,40}$'),
    constraint ck_iqc_report_templates_name check (char_length(btrim(display_name)) between 1 and 100)
);

create table if not exists iqc_report_template_versions (
    id uuid primary key,
    template_id uuid not null references iqc_report_templates(id) on delete restrict,
    version_number integer not null,
    is_active boolean not null default false,
    activated_at_utc timestamptz null,
    created_at_utc timestamptz not null default now(),
    constraint ux_iqc_report_template_versions unique (template_id, version_number),
    constraint ck_iqc_report_template_versions_number check (version_number >= 1),
    constraint ck_iqc_report_template_versions_activation check (
        (is_active and activated_at_utc is not null)
        or (not is_active and activated_at_utc is null)
    )
);

create unique index if not exists ux_iqc_report_template_versions_active
    on iqc_report_template_versions(template_id)
    where is_active;

create table if not exists iqc_report_template_items (
    id uuid primary key,
    template_version_id uuid not null references iqc_report_template_versions(id) on delete restrict,
    item_code text not null,
    display_order integer not null,
    label text not null,
    guidance text null,
    response_type text not null,
    is_required boolean not null default true,
    requires_photo boolean not null default false,
    max_text_length integer null,
    constraint ux_iqc_report_template_items_code unique (template_version_id, item_code),
    constraint ux_iqc_report_template_items_order unique (template_version_id, display_order),
    constraint ck_iqc_report_template_items_code check (item_code ~ '^[A-Z0-9_]{2,40}$'),
    constraint ck_iqc_report_template_items_order check (display_order >= 1),
    constraint ck_iqc_report_template_items_label check (char_length(btrim(label)) between 1 and 200),
    constraint ck_iqc_report_template_items_guidance check (guidance is null or char_length(btrim(guidance)) between 1 and 500),
    constraint ck_iqc_report_template_items_type check (response_type in ('Check', 'Text')),
    constraint ck_iqc_report_template_items_text_length check (
        (response_type = 'Check' and max_text_length is null)
        or (response_type = 'Text' and max_text_length between 1 and 2000)
    ),
    constraint ck_iqc_report_template_items_photo check (not requires_photo or response_type = 'Check')
);

create table if not exists iqc_reports (
    id uuid primary key default uuid_generate_v4(),
    attempt_id uuid not null unique references material_iqc_attempts(id) on delete restrict,
    template_version_id uuid not null references iqc_report_template_versions(id) on delete restrict,
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
    constraint ck_iqc_reports_status check (status in ('Draft', 'Finalized')),
    constraint ck_iqc_reports_version check (version >= 1),
    constraint ck_iqc_reports_result check (result is null or result in ('Passed', 'Failed')),
    constraint ck_iqc_reports_pdf_status check (pdf_status is null or pdf_status in ('Pending', 'Ready', 'Failed')),
    constraint ck_iqc_reports_pdf_error check (pdf_error_code is null or pdf_error_code ~ '^[a-z0-9_]{3,60}$'),
    constraint ck_iqc_reports_snapshot_hash check (snapshot_sha256 is null or snapshot_sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_iqc_reports_lifecycle check (
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

create index if not exists ix_iqc_reports_status
    on iqc_reports(status, updated_at_utc desc);

create table if not exists iqc_report_responses (
    report_id uuid not null references iqc_reports(id) on delete restrict,
    template_item_id uuid not null references iqc_report_template_items(id) on delete restrict,
    check_result text null,
    text_value text null,
    note text null,
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    primary key (report_id, template_item_id),
    constraint ck_iqc_report_responses_check_result check (check_result is null or check_result in ('Pass', 'Fail', 'NotApplicable')),
    constraint ck_iqc_report_responses_note check (note is null or char_length(btrim(note)) between 1 and 1000),
    constraint ck_iqc_report_responses_value check (text_value is null or char_length(text_value) between 1 and 2000),
    constraint ck_iqc_report_responses_has_value check (check_result is not null or text_value is not null)
);

create table if not exists iqc_report_photos (
    id uuid primary key default uuid_generate_v4(),
    report_id uuid not null references iqc_reports(id) on delete restrict,
    template_item_id uuid not null references iqc_report_template_items(id) on delete restrict,
    display_name text not null,
    normalized_mime text not null,
    byte_size integer not null,
    sha256 text not null,
    alt_text text not null,
    content bytea not null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    constraint ck_iqc_report_photos_name check (display_name ~ '^photo-[1-5]\.(jpg|png)$'),
    constraint ck_iqc_report_photos_mime check (normalized_mime in ('image/jpeg', 'image/png')),
    constraint ck_iqc_report_photos_size check (byte_size between 1 and 5242880 and octet_length(content) = byte_size),
    constraint ck_iqc_report_photos_sha check (sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_iqc_report_photos_alt check (char_length(btrim(alt_text)) between 1 and 200)
);

create index if not exists ix_iqc_report_photos_report
    on iqc_report_photos(report_id, created_at_utc, id);

create table if not exists iqc_report_pdf_artifacts (
    id uuid primary key default uuid_generate_v4(),
    report_id uuid not null unique references iqc_reports(id) on delete restrict,
    snapshot_sha256 text not null,
    byte_size integer not null,
    sha256 text not null,
    content bytea not null,
    generator text not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_iqc_report_pdf_snapshot_sha check (snapshot_sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_iqc_report_pdf_sha check (sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_iqc_report_pdf_size check (byte_size > 0 and octet_length(content) = byte_size),
    constraint ck_iqc_report_pdf_generator check (generator = 'PDFsharp-6.2.4')
);

create or replace function guard_iqc_report_pdf_artifact_immutable()
returns trigger
language plpgsql
as $$
begin
    raise exception 'IQC report PDF artifact is immutable' using errcode = 'P0001';
end;
$$;

drop trigger if exists trg_guard_iqc_report_pdf_artifact_immutable on iqc_report_pdf_artifacts;
create trigger trg_guard_iqc_report_pdf_artifact_immutable
before update or delete on iqc_report_pdf_artifacts
for each row execute function guard_iqc_report_pdf_artifact_immutable();

create or replace function guard_finalized_iqc_report_children()
returns trigger
language plpgsql
as $$
declare
    owner_report_id uuid;
begin
    owner_report_id := case when tg_op = 'DELETE' then old.report_id else new.report_id end;
    if exists (select 1 from iqc_reports where id = owner_report_id and status = 'Finalized') then
        raise exception 'finalized IQC report evidence is immutable' using errcode = 'P0001';
    end if;
    if tg_op = 'DELETE' then
        return old;
    end if;
    return new;
end;
$$;

drop trigger if exists trg_guard_finalized_iqc_report_responses on iqc_report_responses;
create trigger trg_guard_finalized_iqc_report_responses
before insert or update or delete on iqc_report_responses
for each row execute function guard_finalized_iqc_report_children();

drop trigger if exists trg_guard_finalized_iqc_report_photos on iqc_report_photos;
create trigger trg_guard_finalized_iqc_report_photos
before insert or update or delete on iqc_report_photos
for each row execute function guard_finalized_iqc_report_children();

create or replace function guard_finalized_iqc_report_core()
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
        raise exception 'finalized IQC report core is immutable' using errcode = 'P0001';
    end if;
    return new;
end;
$$;

drop trigger if exists trg_guard_finalized_iqc_report_core on iqc_reports;
create trigger trg_guard_finalized_iqc_report_core
before update on iqc_reports
for each row execute function guard_finalized_iqc_report_core();

insert into iqc_report_templates (id, template_code, display_name, is_system)
values ('92000000-0000-0000-0000-000000000001', 'MATERIAL_IQC', '자재 수입검사 기본 양식', true)
on conflict (id) do nothing;

insert into iqc_report_template_versions (id, template_id, version_number, is_active, activated_at_utc)
values (
    '92000000-0000-0000-0000-000000000101',
    '92000000-0000-0000-0000-000000000001',
    1,
    true,
    now()
)
on conflict (id) do nothing;

insert into iqc_report_template_items (
    id, template_version_id, item_code, display_order, label, guidance,
    response_type, is_required, requires_photo, max_text_length
)
values
    ('92000000-0000-0000-0000-000000000201', '92000000-0000-0000-0000-000000000101', 'ITEM_SPEC', 1,
        '품명·규격이 발주 정보와 일치', '발주 품명과 입고품 표시를 대조해 주세요.', 'Check', true, false, null),
    ('92000000-0000-0000-0000-000000000202', '92000000-0000-0000-0000-000000000101', 'ARRIVAL_QUANTITY', 2,
        '도착 수량이 등록 수량과 일치', '포장 단위와 등록 수량을 함께 확인해 주세요.', 'Check', true, false, null),
    ('92000000-0000-0000-0000-000000000203', '92000000-0000-0000-0000-000000000101', 'APPEARANCE', 3,
        '외관 손상·오염 없음', '찍힘, 긁힘, 오염과 포장 손상을 확인해 주세요.', 'Check', true, false, null),
    ('92000000-0000-0000-0000-000000000204', '92000000-0000-0000-0000-000000000101', 'IDENTIFICATION', 4,
        '식별 표시(라벨·명판) 확인', '라벨, 명판과 lot 식별 가능 여부를 확인해 주세요.', 'Check', true, false, null),
    ('92000000-0000-0000-0000-000000000205', '92000000-0000-0000-0000-000000000101', 'ENCLOSURE', 5,
        '외함 상태 확인', '외함 전체 상태를 확인하고 증빙 사진을 등록해 주세요.', 'Check', true, true, null),
    ('92000000-0000-0000-0000-000000000206', '92000000-0000-0000-0000-000000000101', 'NOTES', 6,
        '측정값·특이사항', '측정값이나 추가로 남길 내용을 입력해 주세요.', 'Text', false, false, 1000)
on conflict (id) do nothing;
