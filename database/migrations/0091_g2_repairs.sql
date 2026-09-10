alter table g2_daily_metrics
    drop constraint if exists ck_g2_daily_metrics_code;

alter table g2_daily_metrics
    add constraint ck_g2_daily_metrics_code
    check (metric_code in (
        'MorningProduction',
        'AfternoonProduction',
        'MorningRepair',
        'AfternoonRepair',
        'Delivery',
        'Defect',
        'MorningEmiAttendance',
        'MorningContractorAttendance',
        'AfternoonEmiAttendance',
        'AfternoonContractorAttendance'
    ));
