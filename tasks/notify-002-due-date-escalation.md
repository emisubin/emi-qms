# TASK-NOTIFY-002 예정일 기반 에스컬레이션

## 1. 목적

`work_items.due_date`를 기준으로 L0~L3 예정일 에스컬레이션을 생성한다. 인앱 알림을 원본으로 유지하고, 외부 채널은 `notification_deliveries`에 발송 이력으로 남긴다.

TASK-CALENDAR-001에서 구현된 `BusinessDayCalculator`를 재사용해 생산계획 캘린더와 알림 에스컬레이션의 영업일 기준을 통일한다.

## 2. 포함 범위

- `work_item_escalations` state table
- `DueSoonL0`, `OverdueL1`, `OverdueL2`, `OverdueL3` delivery type
- L0: 예정일의 직전 영업일
- L1: 예정일 초과 즉시
- L2: 예정일 이후 +2영업일
- L3: 예정일 이후 +3영업일
- `NotificationEscalationService`
- `NotificationEscalationWorker`
- `WorkItemEscalationStore`
- 담당자/수신자 계산
- 인앱 notification/reipient 생성
- `notification_deliveries` 생성
- System Administrator용 read-only 조회 API

## 3. 제외 범위

- Teams Activity Feed 실제 구현
- Teams DM Graph 실제 구현
- Pending List
- 개인별 알림 설정 UI
- 발송 실패 수동 재처리 UI
- 생산계획 planned_date 자동 동기화
- 구매 expected_receipt_date 자동 동기화
- 예정일 입력/수정 UI
- 부서장/경영진 에스컬레이션
- 야간 억제

## 4. 에스컬레이션 정책

| 단계 | 조건 | 수신자/채널 |
| --- | --- | --- |
| L0 | 예정일의 직전 영업일 | 정담당자 인앱 + Teams 개인 알림 dry-run |
| L1 | 예정일 초과 즉시 | 정담당자 인앱 + Mail + Teams 개인 알림 dry-run |
| L2 | 예정일 이후 +2영업일 미조치 | 부담당자 + 생산관리 담당자 인앱 + Teams 개인 알림 dry-run |
| L3 | 예정일 이후 +3영업일 미조치 | 생산관리 담당자 + 영업 담당자 인앱 + Mail |

같은 단계는 중복 생성하지 않는다. 단계 상승은 기존 24시간 dedupe와 별개로 허용한다.

## 5. BusinessDayCalculator 사용

모든 L0/L2/L3 계산은 `BusinessDayCalculator`를 사용한다.

- L0: `GetPreviousBusinessDay(due_date)`
- L2: `AddBusinessDays(due_date, 2)`
- L3: `AddBusinessDays(due_date, 3)`

기준 날짜는 `Asia/Seoul` local `DateOnly`다. Notification worker 내부에서 주말/공휴일을 하드코딩하지 않는다.

## 6. due_date 정책

이번 TASK에서 구현하는 엔진의 입력 기준은 `work_items.due_date`다.

현재 due_date 입력/동기화 정책은 미확정이다. 이번 TASK는 에스컬레이션 엔진만 구현하고, due_date가 설정된 work item만 처리한다. 생산계획/구매 예정일과 `work_items.due_date`의 자동 동기화는 후속 결정 후 구현한다.

- `due_date is null`이면 제외한다.
- `Requested`, `InProgress`만 active 대상으로 본다.
- `Completed`는 `Resolved` 처리한다.
- `Cancelled`는 `Cancelled` 처리한다.
- due_date가 변경되면 기존 level sent marker를 초기화하고 새 due_date 기준으로 재평가한다.

이번 TASK에서는 다음을 구현하지 않는다.

- 생산계획 `planned_date`의 자동 due_date 동기화
- 구매 `expected_receipt_date`의 자동 due_date 동기화
- 각 업무 입력 화면의 due_date 입력 UI

## 7. Recipient Resolver

- L0/L1: `work_items.assigned_user_id`
- L2: stage secondary 담당자 + 생산관리 담당자
- L3: 생산관리 담당자 + 영업 담당자
- 비활성 사용자는 제외한다.
- 중복 사용자는 제거한다.
- L3는 생산관리/영업 한정이며 system-administrator fallback을 사용하지 않는다.
- 수신자가 없으면 해당 level의 알림은 생성하지 않는다.

## 8. Channel Policy

인앱 알림은 모든 에스컬레이션의 원본이다.

- L0: `TeamsDirectMessage` dry-run delivery
- L1: `Mail` delivery + `TeamsDirectMessage` dry-run delivery
- L2: `TeamsDirectMessage` dry-run delivery
- L3: `Mail` delivery

TeamsChannel fallback은 `Notifications:Escalation:UseTeamsChannelFallback=true`일 때만 생성한다. 기본값은 false다.

## 9. Teams Activity Feed 후속 전환

현재 Teams 개인 actual 알림은 구현하지 않는다.

L0/L1/L2의 Teams 개인 알림 의도는 `TeamsDirectMessage` dry-run delivery로 남긴다. TASK-NOTIFY-003에서 `TeamsActivity` channel/handler가 추가되면 같은 에스컬레이션 정책을 유지하면서 actual provider로 교체할 수 있어야 한다.

## 10. 설정값

설정 후보:

- `Notifications:Escalation:Enabled`
- `Notifications:Escalation:WorkerIntervalSeconds`
- `Notifications:Escalation:TimeZone`
- `Notifications:Escalation:TeamsPersonalDryRun`
- `Notifications:Escalation:UseTeamsChannelFallback`
- `Notifications:Escalation:MailEnabled`
- `Notifications:Escalation:MaxBatchSize`

외부 채널 secret은 `.env` 또는 secret manager로만 주입한다.

## 11. Daily Digest 보강

TASK-NOTIFY-001에서 구현된 일일 요약 메일에 `내 담당 프로젝트 요약` section을 추가한다.

포함 정보:

- 프로젝트명
- 납기일: `projects.delivery_date`, 없으면 `미등록`
- 담당역할: `project_assignees.responsibility_type`을 한글 label로 변환

정책:

- 수신자별 개인화한다.
- `project_assignees.assigned_user_id` 기준으로 담당 프로젝트를 찾는다.
- `projects.deleted_at_utc is null`이고 `projects.status = 'Active'`인 프로젝트만 포함한다.
- 같은 프로젝트에 여러 역할이 있으면 한 줄에 comma로 묶는다.
- 담당 프로젝트 요약만 있어도 digest content로 보고 Daily Digest delivery 생성 대상에 포함한다.
- 현재 메일 renderer는 plain text 기반이므로 HTML table은 별도 구현하지 않는다.

## 12. 관리자 조회

System Administrator는 다음 API로 상태를 조회한다.

- `GET /api/admin/work-item-escalations`

응답에는 프로젝트, 업무, stage, due_date, status, current_level, last escalated, next check, assigned user, delivery summary를 포함한다.

## 13. 테스트 계획

- L0 직전 영업일 계산
- L1 예정일 초과
- L2 +2영업일
- L3 +3영업일
- due_date null 제외
- Completed/Cancelled resolved 처리
- 동일 level 중복 방지
- L2/L3 수신자 계산
- TeamsChannel fallback 기본 미생성
- Mail delivery 생성
- Teams personal dry-run delivery 생성
- 관리자 조회 API 권한
- migration 0019 적용
- 기존 Notification delivery 회귀
- Daily Digest 담당 프로젝트 요약 생성
- 담당 프로젝트만 있어도 digest 생성
- 같은 프로젝트의 여러 담당역할 grouping
- deleted/completed/cancelled project 제외

## 14. 수동 검수 항목

- UAT DB는 drop/truncate하지 않는다.
- due_date가 있는 테스트 work item으로 L0/L1/L2/L3 상태를 확인한다.
- `work_item_escalations` row가 생성되는지 확인한다.
- `notification_deliveries`에 level별 delivery가 생성되는지 확인한다.
- Mail actual 검수는 Gmail SMTP 테스트 수신자 기준으로만 수행한다.
- Teams 개인 알림은 dry-run 상태로 확인한다.

## 15. 후속 TASK 연결

- TASK-NOTIFY-003: Teams Activity Feed 개인별 알림 actual provider
- NOTIFY 후속: 에스컬레이션 관리자 UI
- ADMIN 후속: 알림 설정/재처리 UI
- 생산계획/구매 후속: 예정일을 `work_items.due_date`로 동기화할지 결정
- due_date 입력/수정 UX 후속 결정
