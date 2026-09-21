-- Existing panels remain uninspected. Preserve historical shipments and detached pages unchanged.
alter table busbar_products add column inspected_by uuid references qms_users(id);
alter table busbar_products add column inspected_by_display_name text;
alter table busbar_products add column inspected_at_utc timestamptz;
alter table busbar_products add constraint ck_busbar_product_inspection check (
 (inspected_by is null and inspected_by_display_name is null and inspected_at_utc is null)
 or (status='Complete' and inspected_by is not null and inspected_by_display_name is not null
     and length(trim(inspected_by_display_name))>0 and inspected_at_utc is not null)
);
