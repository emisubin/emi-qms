drop index if exists ux_panel_quality_inspection_attempts_linked_pending;

create index if not exists ix_panel_quality_inspection_attempts_linked_pending
    on panel_quality_inspection_attempts(linked_pending_issue_id, attempt_number desc)
    where linked_pending_issue_id is not null;
