alter table project_procurement_items
    add column if not exists supply_type text not null default 'Purchased';

do $$
begin
    alter table project_procurement_items
        add constraint ck_project_procurement_items_supply_type
        check (supply_type in ('Purchased', 'CustomerSupplied'));
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table project_procurement_items
        add constraint ck_project_procurement_items_customer_supply_measurement
        check (
            supply_type <> 'CustomerSupplied'
            or (
                order_quantity is not null
                and order_quantity > 0
                and order_unit is not null
                and char_length(btrim(order_unit)) between 1 and 20
            )
        );
exception
    when duplicate_object then null;
end $$;

create index if not exists ix_project_procurement_items_supply_type
    on project_procurement_items(supply_type, expected_receipt_date)
    where status = 'Active';
