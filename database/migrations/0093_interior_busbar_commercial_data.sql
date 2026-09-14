-- Prices belong only to product families; do not create project-level prices.
alter table busbar_product_families add column ecount_product_code text null;
alter table busbar_product_families add column standard_unit_price numeric(18,4) null check(standard_unit_price >= 0);
alter table busbar_settings add column ecount_customer_code text not null default '';
alter table busbar_settings add column ecount_warehouse_code text not null default '';
