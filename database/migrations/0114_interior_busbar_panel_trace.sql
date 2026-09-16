-- Historical quantity-only shipments remain explicitly unlinked; never infer panel identity.
alter table busbar_shipments add column project_name_snapshot text null;
alter table busbar_shipments add column destination_snapshot text null;
alter table busbar_shipments add column task_number_snapshot text null;
create table busbar_shipment_products (
 shipment_id uuid not null references busbar_shipments(id),
 product_id uuid not null references busbar_products(id),
 released_at_utc timestamptz null,
 primary key(shipment_id,product_id)
);
create unique index busbar_panel_active_shipment on busbar_shipment_products(product_id) where released_at_utc is null;
-- Final photographs are immutable even if a future write path bypasses Store.Photo.
create function busbar_guard_final_photo() returns trigger language plpgsql as $$
begin
 perform id from busbar_products where id=case when TG_OP='DELETE' then OLD.product_id else NEW.product_id end for update;
 if exists(select 1 from busbar_products where id=case when TG_OP='DELETE' then OLD.product_id else NEW.product_id end and status<>'Draft')
    or (TG_OP='UPDATE' and exists(select 1 from busbar_products where id=OLD.product_id and status<>'Draft')) then
   raise exception 'Completed production photos are immutable' using errcode='23514';
 end if;
 if TG_OP='DELETE' then return OLD; end if;
 return NEW;
end $$;
create trigger busbar_final_photo before insert or update or delete on busbar_photos for each row execute function busbar_guard_final_photo();
