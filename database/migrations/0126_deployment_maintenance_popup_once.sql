-- An account acknowledges a public deployment once, even when its schedule changes.
create unique index ux_deployment_maintenance_popup_release_user
    on deployment_maintenance_popup_receipts(release_id,user_id);
