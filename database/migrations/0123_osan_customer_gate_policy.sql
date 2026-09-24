-- Customer identities are exact legacy names. Similar spellings remain separate.
create table osan_customers (
    id uuid primary key default uuid_generate_v4(),
    name text not null unique check (length(btrim(name)) between 1 and 200),
    version bigint not null default 1,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now()
);

insert into osan_customers(name)
select distinct customer_name from projects
where project_profile = 'Osan' and customer_name is not null and customer_name <> '';

alter table projects add column osan_customer_id uuid references osan_customers(id);
update projects p set osan_customer_id = c.id
from osan_customers c
where p.project_profile = 'Osan' and p.customer_name = c.name;
create index ix_projects_osan_customer_id on projects(osan_customer_id)
where project_profile = 'Osan';

create table osan_customer_assignments (
    user_id uuid not null references qms_users(id) on delete cascade,
    customer_id uuid not null references osan_customers(id) on delete restrict,
    primary key(user_id, customer_id)
);
create index ix_osan_customer_assignments_customer_id on osan_customer_assignments(customer_id);
create table osan_customer_assignment_versions (
    user_id uuid primary key references qms_users(id) on delete cascade,
    version bigint not null default 1
);

insert into osan_customer_assignment_versions(user_id)
select id from qms_users;
insert into osan_customer_assignments(user_id,customer_id)
select u.id,c.id from qms_users u cross join osan_customers c;

-- A newly provisioned local account receives only the catalog present at insertion.
-- Later customer registration deliberately does not add assignment rows.
create function osan_assign_existing_customers_to_new_user() returns trigger language plpgsql as $$
begin
    insert into osan_customer_assignment_versions(user_id) values(new.id);
    insert into osan_customer_assignments(user_id,customer_id)
    select new.id,id from osan_customers;
    return new;
end $$;
create trigger trg_osan_assign_existing_customers_to_new_user
after insert on qms_users for each row execute function osan_assign_existing_customers_to_new_user();

create table osan_gate_configuration (
    id smallint primary key check (id = 1),
    version bigint not null default 1,
    updated_at_utc timestamptz not null default now()
);
insert into osan_gate_configuration(id) values(1);
create table osan_gate_departments (
    stage_sequence smallint not null check(stage_sequence between 1 and 7),
    department_id uuid not null references departments(id) on delete restrict,
    primary key(stage_sequence,department_id)
);
-- Preserve the pre-existing manufacturing/quality completion rule at upgrade.
insert into osan_gate_departments(stage_sequence,department_id)
select stage.stage_sequence,d.id
from generate_series(1,7) as stage(stage_sequence) cross join departments d
where d.code in ('manufacturing','quality');
