with reinspection_work as (
    select
        work.id as work_item_id,
        pending.issue_number,
        attempt.attempt_number,
        coalesce(item.order_item, '발주품목') as order_item,
        case
            when receipt.quantity is null then '수량 미입력'
            else trim(trailing '.' from trim(trailing '0' from receipt.quantity::text))
                || case when nullif(btrim(receipt.unit), '') is null then '' else ' ' || btrim(receipt.unit) end
        end as quantity_label
    from work_items work
    join material_iqc_attempts attempt
      on work.target_type = 'Inspection'
     and work.target_id = attempt.id
    join pending_issues pending on pending.id = attempt.pending_issue_id
    join material_receipts receipt on receipt.id = attempt.material_receipt_id
    join project_procurement_items item on item.id = receipt.procurement_item_id
)
update work_items work
set title = '재검사 · P-' || lpad(source.issue_number::text, 4, '0')
        || ' · ' || source.order_item || ' · ' || source.quantity_label
        || ' (' || source.attempt_number || '차)',
    description = 'P-' || lpad(source.issue_number::text, 4, '0')
        || ' 조치 완료 건의 ' || source.attempt_number || '차 IQC를 판정해 주세요.'
from reinspection_work source
where work.id = source.work_item_id;

update notifications notification
set title = '새 업무 · ' || work.title,
    message = work.description
from work_items work
join material_iqc_attempts attempt
  on work.target_type = 'Inspection'
 and work.target_id = attempt.id
where notification.work_item_id = work.id
  and attempt.pending_issue_id is not null
  and notification.source_kind = 'WorkAssignment';
