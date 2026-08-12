alter table panel_placeholders
    add column if not exists drawing_number text;

alter table panel_placeholders
    add column if not exists panel_group_number integer;

do $$
begin
    alter table panel_placeholders add constraint ck_panel_placeholders_drawing_number_length
        check (drawing_number is null or char_length(btrim(drawing_number)) between 1 and 200);
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table panel_placeholders add constraint ck_panel_placeholders_group_number
        check (panel_group_number is null or panel_group_number > 0);
exception
    when duplicate_object then null;
end $$;

create index if not exists ix_panel_placeholders_project_group_active
    on panel_placeholders(project_id, panel_group_number, sequence_number)
    where status = 'Active' and panel_group_number is not null;
