alter table g2_daily_metrics
    drop constraint if exists ck_g2_daily_metrics_code;

alter table g2_daily_metrics
    add constraint ck_g2_daily_metrics_code
    check (metric_code in (
        'MorningProduction',
        'AfternoonProduction',
        'Delivery',
        'Defect',
        'MorningEmiAttendance',
        'MorningContractorAttendance',
        'AfternoonEmiAttendance',
        'AfternoonContractorAttendance'
    ));

alter table g2_targets
    drop constraint if exists ck_g2_targets_type;

alter table g2_targets
    add constraint ck_g2_targets_type
    check (target_type in ('DailyProduction', 'Delivery', 'Inventory'));
