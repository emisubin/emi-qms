-- Keep legacy project-wide sales for audit; never backfill historical shipments.
alter table busbar_ecount_jobs add column shipment_id uuid null;
alter table busbar_shipments add constraint busbar_shipments_project_id_id_key unique(project_id,id);
alter table busbar_ecount_jobs add constraint busbar_ecount_shipment_project_fk
 foreign key(project_id,shipment_id) references busbar_shipments(project_id,id);
alter table busbar_ecount_jobs add constraint busbar_ecount_shipment_sale_check check(shipment_id is null or kind='Sale');
alter table busbar_ecount_jobs drop constraint busbar_ecount_jobs_project_id_kind_key;
create unique index busbar_ecount_project_kind_legacy on busbar_ecount_jobs(project_id,kind) where shipment_id is null;
create unique index busbar_ecount_shipment_once on busbar_ecount_jobs(shipment_id) where shipment_id is not null;
update busbar_ecount_jobs set state=case when state in ('Pending','Held','Failed') then 'Held' else state end,
 needs_review=true,message='이전 프로젝트 단위 판매 · 출하별 전송 전 전표 확인 필요',updated_at_utc=now()
 where kind='Sale';
