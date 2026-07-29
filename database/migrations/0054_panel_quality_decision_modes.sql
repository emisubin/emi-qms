alter table panel_quality_inspection_attempts
    add column decision_mode text;

update panel_quality_inspection_attempts
set decision_mode = 'Checklist';

update panel_quality_inspection_attempts attempt
set decision_mode = 'Aggregate'
where attempt.stage_code in ('CustomerInspection', 'FAT')
  and attempt.status in ('Requested', 'InProgress')
  and not exists (
      select 1
      from panel_quality_reports report
      join panel_quality_report_responses response on response.report_id = report.id
      where report.attempt_id = attempt.id
  );

alter table panel_quality_inspection_attempts
    alter column decision_mode set default 'Checklist',
    alter column decision_mode set not null,
    add constraint ck_panel_quality_inspection_attempts_decision_mode
        check (decision_mode in ('Checklist', 'Aggregate'));

comment on column panel_quality_inspection_attempts.decision_mode is
    '검사 회차 생성 시 고정한 판정 단위. Checklist=LQC/OQC 항목별, Aggregate=전진검수/FAT 패널 통합 판정.';
