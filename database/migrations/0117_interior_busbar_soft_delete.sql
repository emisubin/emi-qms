-- Business rows remain addressable for names, snapshots, and audit history after removal.
-- Restores retain the most recent deletion provenance; busbar_audit keeps every transition.
alter table busbar_product_families add column is_deleted boolean not null default false,
 add column deleted_at_utc timestamptz null, add column deleted_by uuid null references qms_users(id), add column delete_reason text null,
 add column restored_at_utc timestamptz null, add column restored_by uuid null references qms_users(id), add column restore_reason text null;
alter table busbar_materials add column is_deleted boolean not null default false,
 add column deleted_at_utc timestamptz null, add column deleted_by uuid null references qms_users(id), add column delete_reason text null,
 add column restored_at_utc timestamptz null, add column restored_by uuid null references qms_users(id), add column restore_reason text null;
alter table busbar_workers add column is_deleted boolean not null default false,
 add column deleted_at_utc timestamptz null, add column deleted_by uuid null references qms_users(id), add column delete_reason text null,
 add column restored_at_utc timestamptz null, add column restored_by uuid null references qms_users(id), add column restore_reason text null;
alter table busbar_boms add column is_deleted boolean not null default false,
 add column deleted_at_utc timestamptz null, add column deleted_by uuid null references qms_users(id), add column delete_reason text null,
 add column restored_at_utc timestamptz null, add column restored_by uuid null references qms_users(id), add column restore_reason text null;
alter table busbar_projects add column is_deleted boolean not null default false,
 add column deleted_at_utc timestamptz null, add column deleted_by uuid null references qms_users(id), add column delete_reason text null,
 add column restored_at_utc timestamptz null, add column restored_by uuid null references qms_users(id), add column restore_reason text null;
alter table busbar_plans add column is_deleted boolean not null default false,
 add column deleted_at_utc timestamptz null, add column deleted_by uuid null references qms_users(id), add column delete_reason text null,
 add column restored_at_utc timestamptz null, add column restored_by uuid null references qms_users(id), add column restore_reason text null;
alter table busbar_purchases add column is_deleted boolean not null default false,
 add column deleted_at_utc timestamptz null, add column deleted_by uuid null references qms_users(id), add column delete_reason text null,
 add column restored_at_utc timestamptz null, add column restored_by uuid null references qms_users(id), add column restore_reason text null;
