create table panel_qr_codes (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    panel_id uuid not null references panel_placeholders(id) on delete cascade,
    token text not null unique,
    status text not null,
    issued_by_user_id uuid not null references qms_users(id),
    issued_at_utc timestamptz not null default now(),
    revoked_by_user_id uuid null references qms_users(id),
    revoked_at_utc timestamptz null,
    revoke_reason text null,
    constraint ck_panel_qr_token check (token ~ '^[A-Za-z0-9_-]{43}$'),
    constraint ck_panel_qr_status check (status in ('Active', 'Revoked')),
    constraint ck_panel_qr_revocation check (
        (status = 'Active' and revoked_by_user_id is null and revoked_at_utc is null and revoke_reason is null)
        or
        (status = 'Revoked' and revoked_by_user_id is not null and revoked_at_utc is not null and char_length(btrim(revoke_reason)) between 2 and 500)
    )
);

create unique index ux_panel_qr_codes_active_panel
    on panel_qr_codes(panel_id)
    where status = 'Active';

create index ix_panel_qr_codes_project
    on panel_qr_codes(project_id, panel_id, issued_at_utc desc);

create table panel_qr_events (
    id uuid primary key,
    qr_code_id uuid not null references panel_qr_codes(id) on delete cascade,
    project_id uuid not null references projects(id) on delete cascade,
    panel_id uuid not null references panel_placeholders(id) on delete cascade,
    event_type text not null,
    outcome_status text null,
    item_count integer null,
    actor_user_id uuid not null references qms_users(id),
    correlation_id text null,
    occurred_at_utc timestamptz not null default now(),
    constraint ck_panel_qr_event_type check (event_type in (
        'Issued', 'Rotated', 'ImageRendered', 'PrintSheetRendered', 'ResolveSucceeded', 'ResolveStateViewed'
    )),
    constraint ck_panel_qr_event_outcome check (
        outcome_status is null or outcome_status in (
            'Ok', 'OkCompletedProject', 'PanelInactiveOrProjectHold', 'Revoked', 'ProjectDeleted'
        )
    ),
    constraint ck_panel_qr_event_item_count check (item_count is null or item_count between 1 and 50),
    constraint ck_panel_qr_event_correlation check (correlation_id is null or char_length(correlation_id) between 1 and 200)
);

create index ix_panel_qr_events_project
    on panel_qr_events(project_id, occurred_at_utc desc);

create index ix_panel_qr_events_qr
    on panel_qr_events(qr_code_id, occurred_at_utc desc);

create or replace function guard_panel_qr_event_append_only()
returns trigger language plpgsql as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' and tg_op = 'DELETE' then
        return old;
    end if;
    raise exception 'Panel QR events are append-only.';
end $$;

create trigger trg_guard_panel_qr_event
before update or delete on panel_qr_events
for each row execute function guard_panel_qr_event_append_only();
