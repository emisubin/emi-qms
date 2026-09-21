-- Existing labels cannot be inferred from earlier QR generation/downloads.
alter table busbar_products add column label_state text not null default 'LegacyUnknown'
 check (label_state in ('LegacyUnknown','Unprinted','Printed','Attached'));
alter table busbar_products alter column label_state set default 'Unprinted';
alter table busbar_products add column label_printed_at_utc timestamptz;
alter table busbar_products add column label_printed_by uuid references qms_users(id);
alter table busbar_products add column label_attached_at_utc timestamptz;
alter table busbar_products add column label_attached_by uuid references qms_users(id);
create table busbar_label_requests (
 id uuid primary key, actor_id uuid not null references qms_users(id),
 action text not null check(action in ('Printed','Attached')), fingerprint text not null,
 created_at_utc timestamptz not null default now()
);
create table busbar_label_events (
 id uuid primary key, request_id uuid not null references busbar_label_requests(id),
 product_id uuid not null references busbar_products(id),
 action text not null check(action in ('Printed','Attached')),
 actor_id uuid not null references qms_users(id), actor_display_name text not null,
 created_at_utc timestamptz not null default now(), unique(request_id,product_id)
);
create index busbar_label_pending_actor on busbar_label_events(actor_id,product_id) where action='Printed';
