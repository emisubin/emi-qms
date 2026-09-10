create table if not exists g2_defect_inventory_counts (
    id uuid primary key default uuid_generate_v4(),
    count_date date not null unique,
    quantity integer not null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ck_g2_defect_inventory_counts_quantity check (quantity >= 0),
    constraint ck_g2_defect_inventory_counts_version check (version >= 1)
);

create index if not exists ix_g2_defect_inventory_counts_date
    on g2_defect_inventory_counts(count_date);
