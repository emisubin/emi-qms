alter table busbar_projects add column registered_by uuid null references qms_users(id),
 add column registered_by_name text null,
 add column registered_by_source text null;
create table busbar_ecount_employees(
 user_id uuid primary key references qms_users(id),
 employee_code varchar(30) not null check(length(trim(employee_code))>0)
);

-- Recover only an unambiguous creation audit. Historical display names were not recorded.
with proven as (
 select entity_id, (array_agg(changed_by))[1] actor
 from busbar_audit
 where entity_kind='Project' and before_value='{}'::jsonb
   and after_value->'Id'='null'::jsonb
 group by entity_id having count(*)=1
)
update busbar_projects p set registered_by=a.actor, registered_by_name=u.display_name,
 registered_by_source='CreationAuditCurrentName'
from proven a join qms_users u on u.id=a.actor where p.id=a.entity_id and p.registered_by is null;
