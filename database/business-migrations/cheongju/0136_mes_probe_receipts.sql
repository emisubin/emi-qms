-- Cheongju only. No MES or production payloads: a shared one-use receipt across replicas.
create table mes_probe_receipts (
    device_id text not null check (device_id ~ '^[0-9a-f]{64}$'),
    request_id uuid not null,
    expires_at timestamptz not null,
    primary key (device_id, request_id)
);
create index mes_probe_receipts_expiry on mes_probe_receipts(expires_at);
