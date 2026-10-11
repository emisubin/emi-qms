-- Private approved catalog is provisioned separately; never seed actual inventory.
create table g2_material_items (
 id integer primary key check (id between 1 and 92),
 source_row integer not null unique,
 metadata jsonb not null check (jsonb_typeof(metadata) = 'object')
);
create table g2_material_counts (
 count_date date not null,
 item_id integer not null references g2_material_items(id),
 working numeric(16,4) check (working >= 0),
 kitting numeric(16,4) check (kitting >= 0),
 rack numeric(16,4) check (rack >= 0),
 materials numeric(16,4) check (materials >= 0),
 erp numeric(16,4) check (erp >= 0),
 transit numeric(16,4) check (transit >= 0),
 updated_by_user_id uuid not null references qms_users(id),
 updated_at_utc timestamptz not null default now(),
 primary key (count_date, item_id)
);
