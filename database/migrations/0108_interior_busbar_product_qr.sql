-- QR artifacts are generated once on manufacturing completion. Keep binary content out of product lists.
create table busbar_product_qr (
    product_id uuid primary key references busbar_products(id),
    url text not null,
    png bytea not null check(octet_length(png) > 0),
    created_at_utc timestamptz not null
);
