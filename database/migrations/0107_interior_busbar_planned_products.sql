-- Planning now prepares identifiable work items; these are not manufactured products yet.
-- Existing production records remain unlinked rather than guessing their originating plan.
alter table busbar_products
    alter column worker_id drop not null,
    alter column worker_name drop not null,
    add column plan_id uuid null references busbar_plans(id),
    add column plan_sequence integer null,
    add constraint busbar_products_plan_sequence_check
        check ((plan_id is null and plan_sequence is null) or (plan_id is not null and plan_sequence is not null and plan_sequence > 0)),
    add constraint busbar_products_complete_worker_check
        check (status <> 'Complete' or (worker_id is not null and worker_name is not null)),
    add constraint busbar_products_plan_sequence_unique unique(plan_id, plan_sequence);

-- Legacy plans are prepared on their next explicit save, with that authenticated user as creator.
alter table busbar_plans add column products_initialized boolean not null default false;
create index ix_busbar_products_plan on busbar_products(plan_id, plan_sequence);
