create table if not exists procurement_required_item_templates (
    id uuid primary key default uuid_generate_v4(),
    item_code text not null,
    version integer not null default 1,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now(),
    created_by_user_id uuid null references qms_users(id),
    constraint ck_procurement_required_item_templates_item_not_blank check (btrim(item_code) <> ''),
    constraint ck_procurement_required_item_templates_version_positive check (version >= 1)
);

create unique index if not exists ux_procurement_required_item_templates_active_item
    on procurement_required_item_templates(upper(btrim(item_code)))
    where is_active = true;

create table if not exists procurement_required_item_template_rows (
    id uuid primary key default uuid_generate_v4(),
    template_id uuid not null references procurement_required_item_templates(id) on delete cascade,
    sequence_number integer not null,
    item_name text not null,
    normalized_item_name text not null,
    is_required boolean not null default true,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_procurement_required_item_template_rows_sequence_positive check (sequence_number >= 1),
    constraint ck_procurement_required_item_template_rows_item_not_blank check (btrim(item_name) <> ''),
    constraint ck_procurement_required_item_template_rows_normalized_not_blank check (btrim(normalized_item_name) <> ''),
    constraint ux_procurement_required_item_template_rows_sequence unique (template_id, sequence_number)
);

create unique index if not exists ux_procurement_required_item_template_rows_active_name
    on procurement_required_item_template_rows(template_id, normalized_item_name)
    where is_active = true;
