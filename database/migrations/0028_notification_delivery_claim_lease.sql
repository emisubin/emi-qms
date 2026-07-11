alter table notification_deliveries
    add column if not exists claim_token uuid null,
    add column if not exists claimed_at_utc timestamptz null,
    add column if not exists claim_expires_at_utc timestamptz null,
    add column if not exists claimed_by_instance_id text null;

do $$
begin
    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_status;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_status
        check (status in ('Pending', 'Processing', 'Sent', 'Failed', 'Suppressed', 'Disabled', 'DryRunSent'));

    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_claim_state;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_claim_state
        check (
            (
                status = 'Processing'
                and claim_token is not null
                and claimed_at_utc is not null
                and claim_expires_at_utc is not null
                and claimed_by_instance_id is not null
                and btrim(claimed_by_instance_id) <> ''
            )
            or
            (
                status <> 'Processing'
                and claim_token is null
                and claimed_at_utc is null
                and claim_expires_at_utc is null
                and claimed_by_instance_id is null
            )
        );

    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_claim_expiry;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_claim_expiry
        check (
            claim_expires_at_utc is null
            or claimed_at_utc is null
            or claim_expires_at_utc > claimed_at_utc
        );
end $$;

create table if not exists notification_delivery_attempts (
    id uuid primary key default uuid_generate_v4(),
    delivery_id uuid not null references notification_deliveries(id) on delete cascade,
    attempt_no integer not null,
    claim_token uuid not null,
    worker_instance_id text not null,
    claimed_at_utc timestamptz not null,
    lease_expires_at_utc timestamptz not null,
    provider_call_started_at_utc timestamptz null,
    completed_at_utc timestamptz null,
    outcome text not null default 'Processing',
    error_code text null,
    error_message text null,
    provider_message_id text null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ux_notification_delivery_attempts_delivery_attempt unique (delivery_id, attempt_no),
    constraint ux_notification_delivery_attempts_claim_token unique (claim_token),
    constraint ck_notification_delivery_attempts_attempt_no check (attempt_no > 0),
    constraint ck_notification_delivery_attempts_worker_instance check (btrim(worker_instance_id) <> ''),
    constraint ck_notification_delivery_attempts_lease check (lease_expires_at_utc > claimed_at_utc),
    constraint ck_notification_delivery_attempts_outcome check (
        outcome in (
            'Processing',
            'Sent',
            'DryRunSent',
            'Disabled',
            'Suppressed',
            'RetryScheduled',
            'FailedPermanent',
            'LeaseExpiredBeforeProviderCall',
            'LeaseExpiredAfterProviderCallStarted',
            'OwnershipLost'
        )
    ),
    constraint ck_notification_delivery_attempts_completion check (
        (outcome = 'Processing' and completed_at_utc is null)
        or (outcome <> 'Processing' and completed_at_utc is not null)
    )
);

create index if not exists ix_notification_deliveries_claim_due
    on notification_deliveries(status, next_attempt_at_utc, claim_expires_at_utc, created_at_utc);

create index if not exists ix_notification_deliveries_claim_owner
    on notification_deliveries(claimed_by_instance_id, claim_expires_at_utc)
    where status = 'Processing';

create index if not exists ix_notification_delivery_attempts_delivery
    on notification_delivery_attempts(delivery_id, attempt_no desc);

create index if not exists ix_notification_delivery_attempts_processing_lease
    on notification_delivery_attempts(lease_expires_at_utc)
    where outcome = 'Processing';
