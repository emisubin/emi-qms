alter table g2_daily_metrics
    add column if not exists is_forecast boolean not null default false;

update g2_daily_metrics
set is_forecast = true
where work_date > timezone('Asia/Seoul', now())::date
  and quantity is not null;

alter table g2_daily_metrics
    drop constraint if exists ck_g2_daily_metrics_forecast_quantity;

alter table g2_daily_metrics
    add constraint ck_g2_daily_metrics_forecast_quantity
    check (not is_forecast or quantity is not null);

create index if not exists ix_g2_daily_metrics_forecast_expiry
    on g2_daily_metrics(work_date)
    where is_forecast;
