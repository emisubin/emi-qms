alter table projects
    add column if not exists lse_task_number varchar(100) null;

alter table projects
    drop constraint if exists ck_projects_lse_task_number;

alter table projects
    add constraint ck_projects_lse_task_number
    check (
        lse_task_number is null
        or (
            lse_task_number = btrim(lse_task_number)
            and char_length(lse_task_number) between 1 and 100
        )
    );
