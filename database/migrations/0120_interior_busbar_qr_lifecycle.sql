-- Preserve printed public URLs. Assign a permanent panel number and queue the
-- pre-shipment PMS routing document as soon as a panel is created.
create function busbar_prepare_panel_qr() returns trigger language plpgsql as $$
begin
  if new.number is null then
    new.number := 'IB-' || lpad(nextval('busbar_product_number_seq')::text, 8, '0');
  end if;
  new.revision := greatest(new.revision, 1);
  new.publication_state := 'Pending';
  return new;
end;
$$;
create trigger busbar_prepare_panel_qr before insert on busbar_products
for each row execute function busbar_prepare_panel_qr();
update busbar_products set number=coalesce(number,'IB-' || lpad(nextval('busbar_product_number_seq')::text,8,'0')),
 revision=revision+1,publication_state='Pending',publication_error=null;

-- Durable recovery intent is committed separately before writing external HTML.
-- No FK: a shipment holds a product row lock while recording this intent.
create table busbar_publication_recovery (
 product_id uuid primary key,
 created_at_utc timestamptz not null default now(),
 next_attempt_at_utc timestamptz not null default now()
);
create table busbar_detached_pages (
 product_id uuid primary key references busbar_products(id),
 html bytea not null
);
