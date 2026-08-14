with revoked as (
    update form_template_manager_bindings binding
    set revoked_by_user_id = binding.assigned_by_user_id,
        revoked_at_utc = now()
    where binding.domain = 'Manufacturing'
      and binding.revoked_at_utc is null
    returning binding.id, binding.user_id, binding.assigned_by_user_id
)
insert into form_template_audit_events (
    action, domain, family, template_key, binding_id, actor_user_id, detail
)
select
    'ManagerRevoked', 'Administration', 'Administration', 'Manufacturing',
    revoked.id, revoked.assigned_by_user_id,
    jsonb_build_object(
        'userId', revoked.user_id,
        'reason', 'DepartmentHeadFormScopeChanged'
    )
from revoked;

with desired as (
    select
        user_account.id as user_id,
        department.id as department_id,
        case department.code
            when 'quality' then 'Quality'
            when 'production-planning' then 'ProductionPlanning'
        end as domain,
        coalesce((
            select administrator.id
            from qms_users administrator
            join user_roles user_role on user_role.user_id = administrator.id
            join roles role on role.id = user_role.role_id
            where administrator.is_active
              and role.code = 'system-administrator'
            order by administrator.id
            limit 1
        ), user_account.id) as actor_user_id
    from qms_users user_account
    join departments department on department.id = user_account.department_id
    where user_account.is_active
      and user_account.is_department_head
      and department.code in ('quality', 'production-planning')
), inserted as (
    insert into form_template_manager_bindings (
        id, user_id, department_id, domain, assigned_by_user_id
    )
    select
        uuid_generate_v4(), desired.user_id, desired.department_id,
        desired.domain, desired.actor_user_id
    from desired
    on conflict (user_id, department_id, domain)
        where revoked_at_utc is null
        do nothing
    returning id, user_id, domain, assigned_by_user_id
)
insert into form_template_audit_events (
    action, domain, family, template_key, binding_id, actor_user_id, detail
)
select
    'ManagerAssigned', 'Administration', 'Administration', inserted.domain,
    inserted.id, inserted.assigned_by_user_id,
    jsonb_build_object(
        'userId', inserted.user_id,
        'reason', 'DepartmentHeadFormScopeChanged'
    )
from inserted;
