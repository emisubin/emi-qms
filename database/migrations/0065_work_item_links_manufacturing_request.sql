alter table work_items
    add column if not exists link_url text null;

update work_items work_item
set link_url = (
    select candidate.link_url
    from notifications candidate
    where candidate.work_item_id = work_item.id
      and candidate.link_url is not null
    order by candidate.created_at_utc desc, candidate.id desc
    limit 1
)
where work_item.link_url is null
  and exists (
      select 1
      from notifications candidate
      where candidate.work_item_id = work_item.id
        and candidate.link_url is not null
  );

update work_items
set link_url = '/quality/iqc?request=' || target_id::text
where link_url is null
  and workflow_stage_code = 'IQC'
  and target_type = 'Inspection'
  and target_id is not null;

update work_items
set link_url = '/materials/receipts?receipt='
    || substring(idempotency_key from 'materials:receipt:([0-9a-fA-F-]{36}):confirm')
where link_url is null
  and workflow_stage_code = 'ReceiptConfirmed'
  and idempotency_key ~ '^materials:receipt:[0-9a-fA-F-]{36}:confirm';

update work_items work_item
set link_url = '/materials/receipts?project=' || project.project_code
from projects project
where work_item.project_id = project.id
  and work_item.link_url is null
  and work_item.workflow_stage_code = 'MaterialArrived';

update work_items work_item
set priority = case
        when exists (
            select 1
            from material_iqc_attempts attempt
            where attempt.id = work_item.target_id
              and attempt.pending_issue_id is not null
        ) then 'Blocking'
        else 'Normal'
    end
where work_item.workflow_stage_code = 'IQC'
  and work_item.target_type = 'Inspection';

update workflow_stages
set department_code = 'production-planning',
    stage_name = '제조 요청',
    is_optional = false
where stage_code = 'KittingCompleted';
