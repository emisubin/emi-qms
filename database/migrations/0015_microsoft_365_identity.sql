alter table qms_users add column if not exists entra_object_id text null;
alter table qms_users add column if not exists email text null;
alter table qms_users add column if not exists auth_provider text not null default 'Dev';

update qms_users
set auth_provider = 'Dev'
where auth_provider is null;

alter table qms_users alter column department_id drop not null;

do $$
begin
    alter table qms_users add constraint ck_qms_users_auth_provider
        check (auth_provider in ('Dev', 'EntraId'));
exception
    when duplicate_object then null;
end $$;

create unique index if not exists ux_qms_users_entra_object_id
    on qms_users(entra_object_id)
    where entra_object_id is not null;

create index if not exists ix_qms_users_auth_provider
    on qms_users(auth_provider);

create index if not exists ix_qms_users_email
    on qms_users(email)
    where email is not null;
