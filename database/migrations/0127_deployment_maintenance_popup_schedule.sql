-- Status changes refresh the banner; a revised expected end prompts once again.
drop index ux_deployment_maintenance_popup_release_user;
alter table deployment_maintenance add column popup_version integer not null default 0
    check(popup_version>=0);
update deployment_maintenance set popup_version=1 where release_id is not null;
