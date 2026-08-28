alter table authorization_audit_events
    add column actual_actor_user_id uuid null references qms_users(id);

create table audit_coverage_state (
    singleton boolean primary key default true check (singleton),
    coverage_started_at_utc timestamptz not null default now()
);

insert into audit_coverage_state (singleton)
values (true)
on conflict (singleton) do nothing;

create table audit_events (
    id uuid primary key,
    occurred_at_utc timestamptz not null default now(),
    event_type text not null check (event_type in ('Login', 'Logout', 'MutationSucceeded', 'MutationFailed')),
    actor_user_id uuid not null,
    actor_display_name text not null check (char_length(actor_display_name) between 1 and 160),
    actor_department_name text null check (actor_department_name is null or char_length(actor_department_name) <= 160),
    actual_actor_user_id uuid null,
    actual_actor_display_name text null check (actual_actor_display_name is null or char_length(actual_actor_display_name) <= 160),
    actual_actor_department_name text null check (actual_actor_department_name is null or char_length(actual_actor_department_name) <= 160),
    domain text not null check (domain ~ '^[A-Za-z0-9.-]{1,80}$'),
    action text not null check (action ~ '^[A-Za-z0-9_.-]{1,120}$'),
    route_key text not null check (route_key ~ '^[A-Za-z0-9_.-]{1,160}$'),
    target_type text null check (target_type is null or target_type ~ '^[a-z0-9_]{1,120}$'),
    target_key text null check (target_key is null or char_length(target_key) <= 240),
    outcome text not null check (outcome in ('Succeeded', 'Rejected', 'Ended')),
    failure_reason text null check (failure_reason is null or failure_reason in ('Validation', 'Conflict')),
    reason_summary text null check (reason_summary is null or reason_summary in (
        '입력값 검증에서 저장이 거절되었습니다.',
        '동시 수정 또는 상태 충돌로 저장이 거절되었습니다.'
    )),
    authentication_outcome text null check (authentication_outcome is null or authentication_outcome = 'Succeeded'),
    app_access_outcome text null check (app_access_outcome is null or app_access_outcome in ('Allowed', 'ApprovalPending', 'Inactive')),
    login_correlation_id uuid null,
    request_correlation_id uuid null unique,
    client_interaction_id uuid null,
    idempotency_receipt uuid null unique,
    client_ip inet null,
    browser_family text null check (browser_family is null or browser_family in ('Chrome', 'Edge', 'Firefox', 'Safari', 'Other')),
    os_family text null check (os_family is null or os_family in ('Windows', 'macOS', 'iOS', 'Android', 'Linux', 'Other')),
    constraint ck_audit_events_login_metadata check (
        (event_type = 'Login'
            and authentication_outcome = 'Succeeded'
            and app_access_outcome is not null
            and login_correlation_id is not null
            and client_interaction_id is not null
            and idempotency_receipt is not null)
        or
        (event_type <> 'Login'
            and authentication_outcome is null
            and app_access_outcome is null
            and client_interaction_id is null
            and idempotency_receipt is null
            and client_ip is null
            and browser_family is null
            and os_family is null)
    ),
    constraint ck_audit_events_failure_metadata check (
        (event_type = 'MutationFailed' and failure_reason is not null and reason_summary is not null)
        or
        (event_type <> 'MutationFailed' and failure_reason is null and reason_summary is null)
    ),
    constraint ck_audit_events_request_correlation check (
        (event_type = 'MutationSucceeded' and request_correlation_id is not null)
        or
        (event_type <> 'MutationSucceeded' and request_correlation_id is null)
    )
);

create unique index ux_audit_events_login_interaction
    on audit_events(actor_user_id, client_interaction_id)
    where event_type = 'Login';

create unique index ux_audit_events_login_correlation
    on audit_events(login_correlation_id)
    where event_type = 'Login';

create unique index ux_audit_events_logout_once
    on audit_events(actor_user_id, login_correlation_id)
    where event_type = 'Logout';

create index ix_audit_events_time
    on audit_events(occurred_at_utc desc, id desc);

create index ix_audit_events_actor_time
    on audit_events(actor_user_id, occurred_at_utc desc);

create index ix_audit_events_domain_action_time
    on audit_events(domain, action, occurred_at_utc desc);

create table audit_event_changes (
    id bigint generated always as identity primary key,
    audit_event_id uuid not null references audit_events(id) on delete restrict,
    row_action text not null check (row_action in ('Insert', 'Update', 'Delete')),
    target_type text not null check (target_type ~ '^[a-z0-9_]{1,120}$'),
    target_key text not null check (char_length(target_key) between 1 and 240),
    field_code text not null check (field_code ~ '^[a-z0-9_.]{1,240}$'),
    projection_kind text not null check (projection_kind in ('ExactScalar', 'MetadataOnly')),
    before_value text null check (before_value is null or char_length(before_value) <= 256),
    after_value text null check (after_value is null or char_length(after_value) <= 256),
    before_length integer null check (before_length is null or before_length >= 0),
    after_length integer null check (after_length is null or after_length >= 0),
    constraint ck_audit_event_changes_projection check (
        (projection_kind = 'ExactScalar' and before_length is null and after_length is null)
        or
        (projection_kind = 'MetadataOnly' and before_value is null and after_value is null)
    )
);

create index ix_audit_event_changes_event
    on audit_event_changes(audit_event_id, id);

create or replace function qms_audit_guard_append_only()
returns trigger
language plpgsql
as $$
begin
    raise exception 'Global audit records are append-only.';
end;
$$;

create trigger trg_audit_coverage_append_only
before update or delete on audit_coverage_state
for each row execute function qms_audit_guard_append_only();

create trigger trg_audit_events_append_only
before update or delete on audit_events
for each row execute function qms_audit_guard_append_only();

create trigger trg_audit_event_changes_append_only
before update or delete on audit_event_changes
for each row execute function qms_audit_guard_append_only();

create or replace function qms_audit_identity_snapshot(
    p_user_id uuid,
    out display_name text,
    out department_name text)
language sql
stable
security definer
set search_path = public, pg_temp
as $$
    select coalesce(qms_user.display_name, '알 수 없는 사용자'), department.name
    from (select p_user_id as id) requested
    left join qms_users qms_user on qms_user.id = requested.id
    left join departments department on department.id = qms_user.department_id;
$$;

create or replace function qms_append_audit_login_event(
    p_actor_user_id uuid,
    p_client_interaction_id uuid,
    p_app_access_outcome text,
    p_client_ip inet,
    p_browser_family text,
    p_os_family text)
returns table(event_id uuid, login_correlation_id uuid, idempotency_receipt uuid)
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
    actor_name text;
    actor_department text;
begin
    if p_app_access_outcome not in ('Allowed', 'ApprovalPending', 'Inactive')
        or p_browser_family not in ('Chrome', 'Edge', 'Firefox', 'Safari', 'Other')
        or p_os_family not in ('Windows', 'macOS', 'iOS', 'Android', 'Linux', 'Other') then
        raise exception 'Invalid fixed audit login metadata.';
    end if;

    select snapshot.display_name, snapshot.department_name
    into actor_name, actor_department
    from qms_audit_identity_snapshot(p_actor_user_id) snapshot;

    insert into audit_events (
        id, event_type, actor_user_id, actor_display_name, actor_department_name,
        domain, action, route_key, outcome, authentication_outcome, app_access_outcome,
        login_correlation_id, client_interaction_id, idempotency_receipt,
        client_ip, browser_family, os_family)
    values (
        uuid_generate_v4(), 'Login', p_actor_user_id, actor_name, actor_department,
        'Identity', 'InteractiveLogin', 'RecordInteractiveLogin', 'Succeeded', 'Succeeded', p_app_access_outcome,
        uuid_generate_v4(), p_client_interaction_id, uuid_generate_v4(),
        p_client_ip, p_browser_family, p_os_family)
    on conflict (actor_user_id, client_interaction_id) where event_type = 'Login'
    do nothing;

    return query
    select event.id, event.login_correlation_id, event.idempotency_receipt
    from audit_events event
    where event.event_type = 'Login'
      and event.actor_user_id = p_actor_user_id
      and event.client_interaction_id = p_client_interaction_id;
end;
$$;

create or replace function qms_resolve_audit_login_session(
    p_actor_user_id uuid,
    p_login_correlation_id uuid,
    p_idempotency_receipt uuid)
returns boolean
language sql
stable
security definer
set search_path = public, pg_temp
as $$
    select exists (
        select 1
        from audit_events login_event
        where login_event.event_type = 'Login'
          and login_event.actor_user_id = p_actor_user_id
          and login_event.login_correlation_id = p_login_correlation_id
          and login_event.idempotency_receipt = p_idempotency_receipt
          and not exists (
              select 1
              from audit_events logout_event
              where logout_event.event_type = 'Logout'
                and logout_event.actor_user_id = login_event.actor_user_id
                and logout_event.login_correlation_id = login_event.login_correlation_id
          )
    );
$$;

create or replace function qms_append_audit_logout_event(
    p_actor_user_id uuid,
    p_login_correlation_id uuid,
    p_idempotency_receipt uuid)
returns boolean
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
    actor_name text;
    actor_department text;
begin
    if not qms_resolve_audit_login_session(p_actor_user_id, p_login_correlation_id, p_idempotency_receipt) then
        return false;
    end if;

    select snapshot.display_name, snapshot.department_name
    into actor_name, actor_department
    from qms_audit_identity_snapshot(p_actor_user_id) snapshot;

    insert into audit_events (
        id, event_type, actor_user_id, actor_display_name, actor_department_name,
        domain, action, route_key, outcome, login_correlation_id)
    values (
        uuid_generate_v4(), 'Logout', p_actor_user_id, actor_name, actor_department,
        'Identity', 'ExplicitLogout', 'RecordExplicitLogout', 'Ended', p_login_correlation_id)
    on conflict (actor_user_id, login_correlation_id) where event_type = 'Logout'
    do nothing;

    return true;
end;
$$;

create or replace function qms_append_audit_failed_mutation(
    p_actor_user_id uuid,
    p_actual_actor_user_id uuid,
    p_domain text,
    p_action text,
    p_route_key text,
    p_target_type text,
    p_target_key text,
    p_failure_reason text,
    p_login_correlation_id uuid)
returns uuid
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
    new_event_id uuid := uuid_generate_v4();
    actor_name text;
    actor_department text;
    actual_actor_name text;
    actual_actor_department text;
    safe_summary text;
begin
    if p_domain !~ '^[A-Za-z0-9.-]{1,80}$'
        or p_action !~ '^[A-Za-z0-9_.-]{1,120}$'
        or p_route_key !~ '^[A-Za-z0-9_.-]{1,160}$'
        or p_failure_reason not in ('Validation', 'Conflict') then
        raise exception 'Invalid fixed audit failure metadata.';
    end if;

    safe_summary := case p_failure_reason
        when 'Validation' then '입력값 검증에서 저장이 거절되었습니다.'
        else '동시 수정 또는 상태 충돌로 저장이 거절되었습니다.'
    end;

    select snapshot.display_name, snapshot.department_name
    into actor_name, actor_department
    from qms_audit_identity_snapshot(p_actor_user_id) snapshot;

    if p_actual_actor_user_id is not null then
        select snapshot.display_name, snapshot.department_name
        into actual_actor_name, actual_actor_department
        from qms_audit_identity_snapshot(p_actual_actor_user_id) snapshot;
    end if;

    insert into audit_events (
        id, event_type, actor_user_id, actor_display_name, actor_department_name,
        actual_actor_user_id, actual_actor_display_name, actual_actor_department_name,
        domain, action, route_key, target_type, target_key, outcome,
        failure_reason, reason_summary, login_correlation_id)
    values (
        new_event_id, 'MutationFailed', p_actor_user_id, actor_name, actor_department,
        p_actual_actor_user_id, actual_actor_name, actual_actor_department,
        p_domain, p_action, p_route_key, p_target_type, left(p_target_key, 240), 'Rejected',
        p_failure_reason, safe_summary, p_login_correlation_id);

    return new_event_id;
end;
$$;

create or replace function qms_audit_target_key(p_row jsonb)
returns text
language sql
immutable
as $$
    select left(coalesce(
        nullif(p_row ->> 'id', ''),
        nullif(concat_ws('|',
            case when nullif(p_row ->> 'project_id', '') is not null then 'project_id=' || (p_row ->> 'project_id') end,
            case when nullif(p_row ->> 'panel_id', '') is not null then 'panel_id=' || (p_row ->> 'panel_id') end,
            case when nullif(p_row ->> 'user_id', '') is not null then 'user_id=' || (p_row ->> 'user_id') end,
            case when nullif(p_row ->> 'role_id', '') is not null then 'role_id=' || (p_row ->> 'role_id') end,
            case when nullif(p_row ->> 'item_id', '') is not null then 'item_id=' || (p_row ->> 'item_id') end,
            case when nullif(p_row ->> 'report_id', '') is not null then 'report_id=' || (p_row ->> 'report_id') end,
            case when nullif(p_row ->> 'attempt_id', '') is not null then 'attempt_id=' || (p_row ->> 'attempt_id') end,
            case when nullif(p_row ->> 'version_id', '') is not null then 'version_id=' || (p_row ->> 'version_id') end,
            case when nullif(p_row ->> 'work_item_id', '') is not null then 'work_item_id=' || (p_row ->> 'work_item_id') end,
            case when nullif(p_row ->> 'work_date', '') is not null then 'work_date=' || (p_row ->> 'work_date') end,
            case when nullif(p_row ->> 'date', '') is not null then 'date=' || (p_row ->> 'date') end,
            case when nullif(p_row ->> 'effective_date', '') is not null then 'effective_date=' || (p_row ->> 'effective_date') end,
            case when nullif(p_row ->> 'metric_code', '') is not null then 'metric_code=' || (p_row ->> 'metric_code') end,
            case when nullif(p_row ->> 'target_type', '') is not null then 'target_type=' || (p_row ->> 'target_type') end,
            case when nullif(p_row ->> 'code', '') is not null then 'code=' || (p_row ->> 'code') end
        ), ''),
        'row'
    ), 240);
$$;

create or replace function qms_audit_capture_row_change()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
    actor_id uuid;
    actual_actor_id uuid;
    request_id uuid;
    login_id uuid;
    domain_code text;
    action_code text;
    route_code text;
    actor_name text;
    actor_department text;
    actual_actor_name text;
    actual_actor_department text;
    old_row jsonb := case when tg_op = 'INSERT' then '{}'::jsonb else to_jsonb(old) end;
    new_row jsonb := case when tg_op = 'DELETE' then '{}'::jsonb else to_jsonb(new) end;
    target_key_value text;
    event_id uuid;
    field_name text;
    old_value jsonb;
    new_value jsonb;
    field_type text;
    projection text;
    old_text text;
    new_text text;
begin
    actor_id := nullif(current_setting('qms.audit_actor_id', true), '')::uuid;
    request_id := nullif(current_setting('qms.audit_request_id', true), '')::uuid;
    domain_code := nullif(current_setting('qms.audit_domain', true), '');
    action_code := nullif(current_setting('qms.audit_action', true), '');
    route_code := nullif(current_setting('qms.audit_route_key', true), '');

    if actor_id is null or request_id is null or domain_code is null or action_code is null or route_code is null then
        if tg_op = 'DELETE' then
            return old;
        end if;
        return new;
    end if;

    actual_actor_id := nullif(current_setting('qms.audit_actual_actor_id', true), '')::uuid;
    login_id := nullif(current_setting('qms.audit_login_id', true), '')::uuid;
    target_key_value := qms_audit_target_key(case when tg_op = 'DELETE' then old_row else new_row end);

    for field_name in
        select field.key
        from (
            select jsonb_object_keys(old_row) as key
            union
            select jsonb_object_keys(new_row) as key
        ) field
        order by field.key
    loop
        old_value := old_row -> field_name;
        new_value := new_row -> field_name;

        if old_value is not distinct from new_value then
            continue;
        end if;

        if field_name ~* '(password|token|authorization|cookie|secret|payload|binary|(^|_)(request|response|exception|raw)_?body($|_)|(^|_)content($|_)|(^|_)data($|_)|sha256|(^|_)hash($|_))'
            and not (
                (field_name in ('normalized_mime', 'content_type', 'mime_type')
                    and tg_table_name in (
                        'notice_attachments', 'pending_action_photos', 'iqc_report_photos',
                        'material_iqc_scan_attachments', 'panel_quality_report_photos',
                        'logistics_evidence', 'user_profile_photos'
                    ))
                or (tg_table_name = 'sales_billing_request_batches'
                    and field_name = 'workbook_content_type')
            ) then
            continue;
        end if;

        select format_type(attribute.atttypid, attribute.atttypmod)
        into field_type
        from pg_attribute attribute
        where attribute.attrelid = tg_relid
          and attribute.attname = field_name
          and attribute.attnum > 0
          and not attribute.attisdropped;

        projection := case
            when field_type ~ '^(boolean|smallint|integer|bigint|numeric|decimal|real|double precision|date|timestamp|timestamp with time zone|timestamp without time zone|uuid)'
                then 'ExactScalar'
            when (tg_table_name || '.' || field_name) = any(array[
                'departments.code',
                'g2_daily_metrics.metric_code', 'g2_targets.target_type',
                'iqc_report_responses.check_result', 'iqc_report_template_items.item_code',
                'iqc_report_template_items.response_type', 'iqc_report_template_versions.lifecycle_status',
                'iqc_report_templates.template_code', 'iqc_reports.status', 'iqc_reports.result',
                'iqc_reports.pdf_status', 'iqc_reports.pdf_error_code',
                'logistics_batch_panels.stage_code', 'logistics_batch_units.stage_code',
                'logistics_batches.stage_code', 'logistics_batches.status',
                'logistics_evidence.owner_type', 'logistics_packing_units.status',
                'manufacturing_step_template_items.item_code',
                'manufacturing_step_template_versions.lifecycle_status',
                'manufacturing_step_templates.template_code',
                'material_categories.code', 'material_category_iqc_settings.decision_mode',
                'material_iqc_attempts.status', 'material_iqc_attempts.decision_mode',
                'material_iqc_scan_reports.status', 'material_iqc_scan_reports.result',
                'material_receipts.unit', 'material_receipts.status',
                'panel_manufacturing_executions.status', 'panel_placeholders.display_code',
                'panel_placeholders.status', 'panel_placeholders.workflow_stage',
                'panel_placeholders.component_code', 'panel_qr_codes.status',
                'notice_posts.body_format',
                'panel_quality_inspection_attempts.stage_code',
                'panel_quality_inspection_attempts.status',
                'panel_quality_inspection_attempts.decision_mode',
                'panel_quality_report_responses.check_result', 'panel_quality_reports.status',
                'panel_quality_reports.result', 'panel_quality_reports.pdf_status',
                'panel_quality_reports.pdf_error_code', 'panel_quality_template_items.item_code',
                'panel_quality_template_items.response_type',
                'panel_quality_template_versions.stage_code',
                'panel_quality_template_versions.lifecycle_status', 'pending_action_photos.status',
                'pending_issue_type_catalog.code', 'pending_issues.target_type',
                'pending_issues.issue_type', 'pending_issues.status', 'pending_issues.priority',
                'pending_issues.action_department_code',
                'procurement_required_item_templates.item_code',
                'production_control_manufacturing_versions.lifecycle_status',
                'production_control_plan_connections.source_code',
                'production_control_plan_versions.lifecycle_status', 'production_product_types.code',
                'project_assignees.responsibility_type', 'project_procurement_items.status',
                'project_procurement_items.source_type', 'project_procurement_items.order_unit',
                'project_procurement_items.supply_type',
                'project_production_plan_connections.source_code', 'projects.project_code',
                'projects.currency_code', 'projects.status', 'projects.structure_mode',
                'sales_billing_request_batches.workbook_content_type',
                'sales_billing_request_items.project_code',
                'sales_billing_request_items.currency_code', 'sales_monthly_billing_ledgers.kind',
                'sales_monthly_billing_ledgers.status',
                'sales_monthly_billing_revision_panels.panel_display_code',
                'sales_monthly_targets.currency_code', 'sales_settlements.status',
                'system_holidays.country_code', 'system_holidays.source',
                'system_holidays.holiday_type', 'ul891_recovery_cases.status',
                'ul891_set_design_slots.internal_code', 'ul891_set_design_slots.status',
                'ul891_set_instances.status', 'ul891_set_spec_components.component_code',
                'ul891_set_spec_versions.status', 'user_notification_preferences.delivery_type',
                'work_items.target_type', 'work_items.workflow_stage_code',
                'work_items.responsibility_type', 'work_items.assigned_role_code',
                'work_items.status', 'work_items.priority', 'workflow_stages.stage_code',
                'workflow_stages.department_code'
            ]) then 'ExactScalar'
            when field_name in (
                    'file_name', 'filename', 'display_name', 'original_file_name',
                    'normalized_mime', 'content_type', 'mime_type'
                )
                and tg_table_name in (
                    'notice_attachments', 'pending_action_photos', 'iqc_report_photos',
                    'material_iqc_scan_attachments', 'panel_quality_report_photos',
                    'logistics_evidence', 'user_profile_photos'
                ) then 'ExactScalar'
            else 'MetadataOnly'
        end;

        old_text := case when old_value is null or old_value = 'null'::jsonb then null else old_value #>> '{}' end;
        new_text := case when new_value is null or new_value = 'null'::jsonb then null else new_value #>> '{}' end;

        if projection = 'ExactScalar'
            and (coalesce(char_length(old_text), 0) > 256 or coalesce(char_length(new_text), 0) > 256) then
            projection := 'MetadataOnly';
        end if;

        if event_id is null then
            select snapshot.display_name, snapshot.department_name
            into actor_name, actor_department
            from qms_audit_identity_snapshot(actor_id) snapshot;

            if actual_actor_id is not null then
                select snapshot.display_name, snapshot.department_name
                into actual_actor_name, actual_actor_department
                from qms_audit_identity_snapshot(actual_actor_id) snapshot;
            end if;

            insert into audit_events (
                id, event_type, actor_user_id, actor_display_name, actor_department_name,
                actual_actor_user_id, actual_actor_display_name, actual_actor_department_name,
                domain, action, route_key, target_type, target_key, outcome,
                login_correlation_id, request_correlation_id)
            values (
                uuid_generate_v4(), 'MutationSucceeded', actor_id, actor_name, actor_department,
                actual_actor_id, actual_actor_name, actual_actor_department,
                domain_code, action_code, route_code, tg_table_name, target_key_value, 'Succeeded',
                login_id, request_id)
            on conflict (request_correlation_id) do nothing;

            select event.id into event_id
            from audit_events event
            where event.request_correlation_id = request_id;
        end if;

        insert into audit_event_changes (
            audit_event_id, row_action, target_type, target_key, field_code,
            projection_kind, before_value, after_value, before_length, after_length)
        values (
            event_id,
            initcap(lower(tg_op)),
            tg_table_name,
            target_key_value,
            tg_table_name || '.' || field_name,
            projection,
            case when projection = 'ExactScalar' then old_text else null end,
            case when projection = 'ExactScalar' then new_text else null end,
            case when projection = 'MetadataOnly' then coalesce(char_length(old_text), 0) else null end,
            case when projection = 'MetadataOnly' then coalesce(char_length(new_text), 0) else null end);
    end loop;

    if tg_op = 'DELETE' then
        return old;
    end if;
    return new;
end;
$$;

do $$
declare
    relation_name text;
begin
    foreach relation_name in array array[
        'departments', 'form_template_manager_bindings',
        'g2_daily_metrics', 'g2_inventory_counts', 'g2_targets',
        'iqc_report_photos', 'iqc_report_responses', 'iqc_report_template_items',
        'iqc_report_template_versions', 'iqc_report_templates', 'iqc_reports',
        'lqc_item_settings', 'manufacturing_step_template_items',
        'manufacturing_step_template_versions', 'manufacturing_step_templates',
        'material_categories', 'material_category_iqc_settings', 'material_iqc_attempts',
        'material_iqc_scan_attachments', 'material_iqc_scan_reports', 'material_receipts',
        'notice_attachments', 'notice_posts',
        'logistics_batch_panels', 'logistics_batch_units', 'logistics_batches',
        'logistics_delivery_results', 'logistics_evidence',
        'logistics_packing_unit_panels', 'logistics_packing_units',
        'panel_kitting_completions', 'panel_manufacturing_completion_confirmations',
        'panel_manufacturing_execution_steps', 'panel_manufacturing_executions',
        'panel_placeholders', 'panel_qr_codes', 'panel_quality_inspection_attempts',
        'panel_quality_report_photos', 'panel_quality_report_responses',
        'panel_quality_reports', 'panel_quality_template_items',
        'panel_quality_template_versions',
        'pending_action_photos', 'pending_comments', 'pending_issue_type_catalog', 'pending_issues',
        'procurement_required_item_template_rows', 'procurement_required_item_templates',
        'production_control_manufacturing_items', 'production_control_manufacturing_templates',
        'production_control_manufacturing_versions', 'production_control_plan_connections',
        'production_control_plan_items', 'production_control_plan_templates',
        'production_control_plan_versions', 'production_plan_template_steps',
        'production_plan_templates', 'production_product_types',
        'project_assignees', 'project_manufacturing_step_snapshots', 'project_procurement_items',
        'project_production_plan_connections', 'project_production_plan_items',
        'project_production_plan_set_default_values', 'project_production_plan_set_defaults',
        'project_production_plan_set_item_values', 'project_production_plan_set_scopes',
        'project_production_plans', 'projects', 'qms_users', 'role_permissions',
        'sales_billing_request_batches', 'sales_billing_request_items',
        'sales_monthly_billing_confirmations', 'sales_monthly_billing_ledgers',
        'sales_monthly_billing_revision_cases', 'sales_monthly_billing_revision_panels',
        'sales_monthly_billing_revisions', 'sales_monthly_targets', 'sales_settlements',
        'system_holidays', 'ul891_recovery_cases', 'ul891_set_design_slots',
        'ul891_set_instances', 'ul891_set_spec_components', 'ul891_set_spec_versions',
        'ul891_set_specs', 'user_notification_preference_profiles',
        'user_notification_preferences', 'user_profile_photos', 'user_project_access',
        'user_roles', 'work_items', 'workflow_stages'
    ]
    loop
        if to_regclass('public.' || relation_name) is null then
            raise exception 'Audit tracked relation is missing: %', relation_name;
        end if;

        execute format(
            'create trigger %I after insert or update or delete on %I for each row execute function qms_audit_capture_row_change()',
            'trg_qms_global_audit_' || relation_name,
            relation_name);
    end loop;
end;
$$;

alter table data_export_events
    drop constraint if exists ck_data_export_events_kind;

alter table data_export_events
    add constraint ck_data_export_events_kind
    check (export_kind in (
        'Projects', 'ProjectsSelected', 'ProcurementDashboard', 'MyWork',
        'MyWorkSelected', 'ProductionPlanningSelected', 'ProcurementDashboardSelected',
        'MaterialReceiptsSelected', 'PanelKittingSelected', 'ManufacturingSelected',
        'QualityIqcSelected', 'QualityInspectionsSelected', 'LogisticsSelected',
        'PendingSelected', 'NotificationsSelected', 'AdminUsersSelected',
        'AdminDepartmentsSelected', 'AdminCalendarHolidaysSelected',
        'AdminPermissionMatrixSelected', 'AdminMasterChangeLogsSelected',
        'AdminWorkHistorySelected', 'AdminNotificationDeliveriesSelected',
        'AdminNotificationPreferenceAuditSelected', 'AdminWorkItemEscalationsSelected',
        'AuditLedgerSelected'
    ));
