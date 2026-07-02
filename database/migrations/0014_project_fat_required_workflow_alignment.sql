-- TASK-006B: align existing project/procurement pages with the 18-stage workflow.
-- Existing migration 0001~0013 remain unchanged. This migration is additive and idempotent.

alter table projects
    add column if not exists fat_required boolean not null default false;

alter table project_procurement_items
    add column if not exists supplier_name text null;

do $$
begin
    alter table project_procurement_items drop constraint if exists ck_project_procurement_items_supplier_name_not_blank;

    alter table project_procurement_items add constraint ck_project_procurement_items_supplier_name_not_blank
        check (supplier_name is null or btrim(supplier_name) <> '');
end
$$;
