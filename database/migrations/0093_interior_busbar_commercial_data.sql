-- Existing projects keep unknown prices; never fabricate historical commercial data.
alter table busbar_product_families add column ecount_product_code text null;
alter table busbar_product_families add column standard_unit_price numeric(18,4) null check(standard_unit_price >= 0);
alter table busbar_projects add column unit_price numeric(18,4) null check(unit_price >= 0);
alter table busbar_settings add column ecount_customer_code text not null default '';
alter table busbar_settings add column ecount_warehouse_code text not null default '';
