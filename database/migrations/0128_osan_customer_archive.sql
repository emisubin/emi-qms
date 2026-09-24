-- Retain customer identity, project history and existing access assignments after removal.
alter table osan_customers add column archived_at_utc timestamptz;

-- New local users receive only customers still available for selection.
create or replace function osan_assign_existing_customers_to_new_user() returns trigger language plpgsql as $$
begin
    insert into osan_customer_assignment_versions(user_id) values(new.id);
    insert into osan_customer_assignments(user_id,customer_id)
    select new.id,id from osan_customers where archived_at_utc is null;
    return new;
end $$;
