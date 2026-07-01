do $$
declare
    rrp_id uuid;
    rpp_id uuid;
begin
    select id into rrp_id from production_product_types where code = 'RRP' limit 1;
    select id into rpp_id from production_product_types where code = 'RPP' limit 1;

    if rrp_id is not null and rpp_id is null then
        update production_product_types
        set code = 'RPP',
            name = 'RPP',
            is_active = true
        where id = rrp_id;
    elsif rrp_id is not null and rpp_id is not null and rrp_id <> rpp_id then
        update production_plan_templates set product_type_id = rpp_id where product_type_id = rrp_id;
        update project_production_plans set product_type_id = rpp_id where product_type_id = rrp_id;
        update production_product_types set is_active = false where id = rrp_id;
    end if;
end $$;

update projects set item = 'RPP' where item = 'RRP';
update procurement_required_item_templates
set item_code = 'RPP'
where upper(btrim(item_code)) = 'RRP'
  and not exists (
      select 1
      from procurement_required_item_templates existing
      where upper(btrim(existing.item_code)) = 'RPP'
        and existing.is_active = procurement_required_item_templates.is_active
  );

update procurement_required_item_templates
set is_active = false
where upper(btrim(item_code)) = 'RRP';

alter table project_procurement_items
    add column if not exists source_type text not null default 'Direct';

alter table project_procurement_items
    add column if not exists is_confirmed boolean not null default true;

do $$
begin
    alter table project_procurement_items drop constraint if exists ck_project_procurement_items_source_type;

    alter table project_procurement_items add constraint ck_project_procurement_items_source_type
        check (source_type in ('Direct', 'Excel', 'RequiredItemTemplate'));
exception
    when duplicate_object then null;
end $$;

update work_items
set title = case workflow_stage_code
    when 'ProductionPlanning' then '생산계획, 담당자 입력'
    when 'DesignPanelInfo' then '제품명, 사이즈 입력'
    when 'ProcurementInfo' then '구매정보 입력'
    when 'MaterialArrived' then '자재 도착 등록'
    when 'IQC' then '수입검사 입력'
    when 'ReceiptConfirmed' then '입고 확정 입력'
    when 'KittingCompleted' then '키팅 완료 입력'
    when 'ManufacturingWork' then '제조 작업 입력'
    when 'LQC' then 'LQC 입력'
    when 'ManufacturingCompleted' then '제조 완료 입력'
    when 'OQC' then '자체검수 입력'
    when 'CustomerInspection' then '전진검수 입력'
    when 'FAT' then 'FAT 입력'
    when 'PackingCompleted' then '포장 완료 입력'
    when 'DepartureProcessed' then '출발 처리 입력'
    when 'DeliveryCompleted' then '납품 완료 입력'
    when 'SalesSettlementCompleted' then '세금계산서, 완료 처리'
    else title
end
where workflow_stage_code in (
    'ProductionPlanning',
    'DesignPanelInfo',
    'ProcurementInfo',
    'MaterialArrived',
    'IQC',
    'ReceiptConfirmed',
    'KittingCompleted',
    'ManufacturingWork',
    'LQC',
    'ManufacturingCompleted',
    'OQC',
    'CustomerInspection',
    'FAT',
    'PackingCompleted',
    'DepartureProcessed',
    'DeliveryCompleted',
    'SalesSettlementCompleted'
);
