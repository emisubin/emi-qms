insert into departments (id, code, name, is_active, sort_order, updated_at_utc)
values
    ('10000000-0000-0000-0000-000000000001', 'administration', '관리', true, 10, now()),
    ('10000000-0000-0000-0000-000000000002', 'sales', '영업', true, 20, now()),
    ('10000000-0000-0000-0000-000000000008', 'design', '설계', true, 30, now()),
    ('10000000-0000-0000-0000-000000000003', 'production-planning', '생산관리', true, 40, now()),
    ('10000000-0000-0000-0000-000000000009', 'procurement', '구매', true, 50, now()),
    ('10000000-0000-0000-0000-000000000010', 'materials', '자재', true, 60, now()),
    ('10000000-0000-0000-0000-000000000004', 'manufacturing', '제조', true, 70, now()),
    ('10000000-0000-0000-0000-000000000005', 'quality', '품질', true, 80, now()),
    ('10000000-0000-0000-0000-000000000006', 'logistics', '물류', true, 90, now()),
    ('10000000-0000-0000-0000-000000000007', 'readonly', '조회 전용', true, 100, now())
on conflict (code) do update
set name = excluded.name,
    sort_order = excluded.sort_order,
    updated_at_utc = excluded.updated_at_utc;

alter table qms_users
    add column if not exists is_department_head boolean not null default false;

update qms_users user_account
set is_department_head = true
where exists (
    select 1
    from form_template_manager_bindings binding
    where binding.user_id = user_account.id
      and binding.department_id = user_account.department_id
      and binding.revoked_at_utc is null
);
