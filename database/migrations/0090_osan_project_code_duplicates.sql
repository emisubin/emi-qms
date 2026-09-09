drop index if exists ux_projects_osan_project_code;

create index if not exists ix_projects_osan_project_code
    on projects(project_code)
    where project_profile = 'Osan';
