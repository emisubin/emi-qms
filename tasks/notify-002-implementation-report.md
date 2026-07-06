# TASK-NOTIFY-002 구현 보고서

## 1. 목적

TASK-NOTIFY-002는 `work_items.due_date`를 기준으로 예정일 임박/초과 업무에 대한 L0~L3 에스컬레이션 엔진을 추가하는 작업이다.

목표는 다음과 같다.

- TASK-CALENDAR-001의 BusinessDayCalculator를 재사용해 영업일 기준을 통일한다.
- 인앱 알림을 원본으로 유지하고, 외부 채널은 `notification_deliveries` 발송 이력으로 관리한다.
- L1/L3는 Mail delivery를 생성하고, L0/L1/L2의 Teams 개인 알림 의도는 Activity Feed 후속 전환을 고려해 dry-run delivery로 기록한다.
- Daily Digest에 담당 프로젝트 요약을 추가해 담당자가 매일 프로젝트명, 납기일, 담당역할을 함께 확인할 수 있게 한다.

## 2. 구현 범위

- `work_item_escalations` state table 추가
- `DueSoonL0`, `OverdueL1`, `OverdueL2`, `OverdueL3` delivery type 추가
- `NotificationEscalationService`
- `NotificationEscalationWorker`
- `WorkItemEscalationStore`
- L0/L1/L2/L3 recipient resolver
- 에스컬레이션 인앱 notification / notification_recipient 생성
- Mail / TeamsDirectMessage / optional TeamsChannel fallback delivery 생성
- System Administrator 전용 에스컬레이션 조회 API
- Daily Digest 담당 프로젝트 요약
- 관련 backend/migration/E2E 안정화 테스트

## 3. 제외 범위

- Teams Activity Feed actual provider
- Teams DM actual provider
- 생산계획 `planned_date` 자동 due_date 동기화
- 구매 `expected_receipt_date` 자동 due_date 동기화
- 업무 입력 화면 due_date 입력/수정 UI
- Pending List
- 알림/에스컬레이션 설정 UI
- 발송 실패 수동 재처리 UI
- 부서장/경영진 수신

## 4. DB/Migration

신규 migration:

- `database/migrations/0019_work_item_escalations.sql`

주요 내용:

- `work_item_escalations` table 추가
- `work_item_id` unique constraint
- `status`: `Active`, `Resolved`, `Cancelled`
- `current_level`: `None`, `L0`, `L1`, `L2`, `L3`
- `due_date` snapshot
- `l0_sent_at_utc`, `l1_sent_at_utc`, `l2_sent_at_utc`, `l3_sent_at_utc`
- `next_check_at_utc`, `last_escalated_at_utc`, `resolved_at_utc`
- status/next_check, due_date, project, assigned user indexes
- `notification_deliveries.delivery_type` check constraint 확장

기존 `0001~0018` migration은 수정하지 않았다.

## 5. Backend 주요 파일

- `backend/src/Emi.Qms.Api/Notifications/NotificationEscalationContracts.cs`
  - escalation level/status/response contract 정의
- `backend/src/Emi.Qms.Api/Notifications/NotificationEscalationService.cs`
  - BusinessDayCalculator 기반 L0/L1/L2/L3 판단
- `backend/src/Emi.Qms.Api/Notifications/NotificationEscalationWorker.cs`
  - BackgroundService 기반 주기 평가
- `backend/src/Emi.Qms.Api/Notifications/WorkItemEscalationStore.cs`
  - DB 조회, state upsert, recipient resolver, notification/delivery 생성, 관리자 조회
- `backend/src/Emi.Qms.Api/Notifications/NotificationEscalationEndpointExtensions.cs`
  - `GET /api/admin/work-item-escalations`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryContracts.cs`
  - L0~L3 delivery type 추가
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryStore.cs`
  - Daily Digest 담당 프로젝트 요약 추가
- `backend/src/Emi.Qms.Api/Notifications/NotificationOptions.cs`
  - Escalation 설정 구조 추가
- `backend/src/Emi.Qms.Api/Program.cs`
  - service/worker/endpoint 등록
- `backend/tests/Emi.Qms.Api.Tests/NotificationDeliveryTests.cs`
  - escalation, delivery, Daily Digest, 관리자 API 테스트
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
  - 0019 migration 및 constraint 테스트

## 6. Escalation 정책

| 단계 | 조건 | 수신자/채널 |
| --- | --- | --- |
| L0 | 예정일의 직전 영업일 | 정담당자 인앱 + TeamsDirectMessage dry-run |
| L1 | 예정일 초과 즉시 | 정담당자 인앱 + Mail + TeamsDirectMessage dry-run |
| L2 | 예정일 이후 +2영업일 미조치 | 부담당자 + 생산관리 담당자 인앱 + TeamsDirectMessage dry-run |
| L3 | 예정일 이후 +3영업일 미조치 | 생산관리 담당자 + 영업 담당자 인앱 + Mail |

업무 상태 처리:

- `Requested`, `InProgress`: active 평가 대상
- `due_date is null`: 제외
- `Completed`: `Resolved`
- `Cancelled`: `Cancelled`
- due_date 변경: 기존 level marker 초기화 후 새 due_date 기준 재평가

due_date 정책:

- 현재 due_date 입력/동기화 정책은 미확정이다.
- 이번 TASK는 `work_items.due_date` 기반 엔진만 구현한다.
- 생산계획/구매 예정일 자동 동기화와 due_date 입력 UI는 후속 결정 후 구현한다.

## 7. BusinessDayCalculator 사용

`NotificationEscalationService`는 TASK-CALENDAR-001의 BusinessDayCalculator를 사용한다.

- L0: `GetPreviousBusinessDay(due_date)`
- L2: `AddBusinessDays(due_date, 2)`
- L3: `AddBusinessDays(due_date, 3)`
- 기준 날짜: `Asia/Seoul` local `DateOnly`

Notification worker 내부에 주말/공휴일 계산을 별도 하드코딩하지 않았다.

## 8. Recipient Resolver

L0/L1:

- `work_items.assigned_user_id`
- 비활성 사용자는 제외

L2:

- stage별 secondary responsibility
- `ProductionPlanningPrimary`, `ProductionPlanningSecondary`, legacy `ProductionPlanning`
- 중복 사용자 제거

L3:

- `ProductionPlanningPrimary`, `ProductionPlanningSecondary`, legacy `ProductionPlanning`
- `SalesPrimary`, `SalesSecondary`
- system-administrator fallback 없음
- 부서장/경영진 미포함

품질 단계:

- `QualityIQC` / `QualityIQCSecondary`
- `QualityLQC` / `QualityLQCSecondary`
- `QualityOQC` / `QualityOQCSecondary`
- `QualityCustomerInspection` / `QualityCustomerInspectionSecondary`

## 9. Channel Policy

인앱 notification은 모든 에스컬레이션의 원본이다.

- L0: TeamsDirectMessage dry-run delivery
- L1: Mail delivery + TeamsDirectMessage dry-run delivery
- L2: TeamsDirectMessage dry-run delivery
- L3: Mail delivery

TeamsChannel fallback:

- `Notifications:Escalation:UseTeamsChannelFallback=true`일 때 L2에만 생성한다.
- 기본값은 false다.
- TeamsChannel을 개인 알림 대체로 하드코딩하지 않았다.

Mail:

- `notification_deliveries`에 `Pending`으로 생성한다.
- `Notifications:Dispatch:Enabled=true`인 환경에서 기존 NotificationDeliveryWorker가 Mail handler를 통해 처리한다.
- 외부 발송 실패는 에스컬레이션 state 생성을 중단하지 않는다.

## 10. Daily Digest 담당 프로젝트 요약

Daily Digest에 `내 담당 프로젝트 요약` section을 추가했다.

조회 기준:

- `project_assignees.assigned_user_id = recipient user`
- `projects.deleted_at_utc is null`
- `projects.status = 'Active'`

표시 항목:

- 프로젝트명
- 납기일: `projects.delivery_date`, 없으면 `미등록`
- 담당역할: responsibility type을 한글 label로 변환

정책:

- 같은 프로젝트에 여러 담당역할이 있으면 한 줄에 comma로 묶는다.
- 납기일 빠른 순, 납기일 없는 프로젝트는 하단으로 정렬한다.
- 담당 프로젝트가 없으면 section을 생략한다.
- 담당 프로젝트 요약만 있어도 digest content로 보고 Daily Digest delivery 생성 대상에 포함한다.
- 현재 mail renderer는 plain text 기반이다. HTML table 개선은 후속 가능하다.

## 11. Admin Escalation API

Endpoint:

- `GET /api/admin/work-item-escalations`

권한:

- System Administrator only
- non-admin은 403

응답 주요 항목:

- 프로젝트명/코드
- work item title
- workflow stage
- due date
- status/current level
- level별 sent timestamp
- last escalated / next check
- assigned user display name
- delivery status summary

## 12. Tests

실행한 검증:

- `git diff --check`
- `actionlint .github/workflows/ci.yml`
- backend Release build
- backend 전체 test
- Notification/Escalation targeted tests
- Migration tests
- Authorization tests
- BusinessDay tests
- frontend lint/typecheck/unit/build
- mock UI smoke
- Full-Stack E2E
- seed 격리 A/B/C/D 관련 DevelopmentDataSeeder tests
- Docker Compose config
- UAT health
- UAT browser smoke
- UAT L0 dry-run smoke
- secret/PII scan

결과:

- Backend 전체 test: 259 passed
- Notification/Escalation targeted tests: 31 passed
- Migration tests: 16 passed
- Authorization tests: 48 passed
- BusinessDay tests: 4 passed
- Frontend unit: 52 passed
- Mock UI smoke: 1 passed
- Full-Stack E2E: 16 passed
- Seed 격리: 7 passed

Frontend lint는 기존 Fast Refresh warning 1건이 있으나 error는 없다. Frontend build는 기존 chunk size warning이 있으나 성공했다.

## 13. UAT 검수 결과

UAT 상태:

- Backend `/health/live`: 200
- Backend `/health/ready`: 200
- Frontend HTTP: 200
- PostgreSQL: healthy
- latest migration: `0019_work_item_escalations`

Escalation smoke:

- UAT에서 테스트 work item 1건에 due_date를 설정해 L0 dry-run smoke를 수행했다.
- `DueSoonL0 / TeamsDirectMessage / DryRunSent` delivery 생성 확인.
- 테스트 work item 완료 처리 후 `work_item_escalations.status = Resolved` 확인.
- active escalation 0건 확인.

Daily Digest:

- 담당 프로젝트 요약은 backend tests로 renderer output을 검증했다.
- UAT DB에는 담당 프로젝트 후보 사용자가 존재함을 read-only query로 확인했다.
- 실제 메일 발송 smoke는 수행하지 않았다.

## 14. 보안/Secret

- `.env`, `.env.*`, `.env.notify-local` 미포함
- SMTP password, Gmail app password, Teams Webhook URL 미포함
- token, Authorization header, client secret 원문 미포함
- appsettings에는 빈 placeholder만 유지
- 테스트에는 `example.test`와 `placeholder-secret`만 사용
- raw stack trace 사용자 노출 없음

## 15. 후속 TASK 연결

- TASK-NOTIFY-003: Teams Activity Feed 개인별 알림 actual provider
- due_date 정책 후속: 생산계획 `planned_date`와 `work_items.due_date` 동기화 여부 결정
- due_date 정책 후속: 구매 `expected_receipt_date`와 `work_items.due_date` 동기화 여부 결정
- 각 업무 입력 화면 due_date 입력/수정 UX
- Pending List
- 에스컬레이션 설정 관리자 UI
- 에스컬레이션 수동 재처리/감사 UI

## 16. 알려진 제한사항

- 기존 UAT work item 대부분은 due_date가 비어 있어 실제 에스컬레이션 대상이 제한적이다.
- 생산계획/구매 예정일 자동 동기화는 의도적으로 제외했다.
- Teams 개인 actual 알림은 아직 없다.
- L0/L1/L2 Teams 개인 알림은 dry-run delivery다.
- Mail delivery는 Dispatch worker가 활성화된 환경에서 처리된다.
- Daily Digest 담당 프로젝트 요약은 plain text renderer 기준이다.

## 17. 운영 적용 전 체크리스트

- 각 업무 due_date 입력 정책 확정
- 생산계획 `planned_date`와 due_date 동기화 여부 결정
- 구매 `expected_receipt_date`와 due_date 동기화 여부 결정
- 기존 업무 due_date 보강 기준 결정
- `Notifications:Dispatch:Enabled` 운영 설정 확인
- Gmail SMTP actual 설정 확인
- Teams Activity Feed 후속 구현 여부 결정
- 관리자 에스컬레이션 조회 검수
- 영업일/휴일 데이터 검수
