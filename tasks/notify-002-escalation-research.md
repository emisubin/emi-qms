# TASK-NOTIFY-002 사전 조사 보고서

## 1. 조사 목적

TASK-NOTIFY-002는 예정일 기반 L0~L3 에스컬레이션을 구현하기 전 단계이다. 이번 조사는 현재 코드와 UAT DB 기준으로 예정일 저장 위치, 미완료 업무 판정, 공휴일/영업일 계산 가능성, 알림 delivery 구조, 담당자/수신자 계산 방식을 확인하고 안전한 구현 범위를 제안하는 것이 목적이다.

이번 조사는 코드, migration, appsettings, env 파일을 수정하지 않았다. UAT DB는 read-only query로만 확인했다.

## 2. 현재 알림/업무/delivery 구조 요약

현재 알림 구조의 원본은 `notifications`와 `notification_recipients`이다. TASK-NOTIFY-001에서 외부 발송 이력용 `notification_deliveries`가 추가되었고, `NotificationDispatcher`, `NotificationDeliveryWorker`, channel handler가 이 테이블을 처리한다.

현재 delivery channel은 `TeamsChannel`, `TeamsDirectMessage`, `Mail`이다.

현재 delivery type은 `WorkItemCreated`, `ReferenceDigest`, `UrgentBlocking`, `DailyDigest`, `ProjectCompletion`, `ManualTest`로 제한된다. L0~L3 에스컬레이션용 delivery type은 아직 없다.

현재 delivery status는 `Pending`, `Sent`, `Failed`, `Suppressed`, `Disabled`, `DryRunSent`이다. retry는 `attempt_count`, `next_attempt_at_utc`, `last_attempt_at_utc`로 관리하고, 최종 성공/억제/비활성 상태는 더 이상 retry하지 않는다.

`DailyDigest`는 매일 07:30, `Asia/Seoul` 기준으로 처리되며, 내용이 있는 사용자만 Mail delivery 후보가 된다. 현재 digest에는 미완료 work item, 최근 생성 work item, 읽지 않은 Reference/Info 알림이 포함된다.

## 3. work_items due_date 분석

`work_items`에는 `due_date date null` 컬럼이 이미 존재한다. 따라서 예정일 기반 에스컬레이션의 공통 기준 필드로 사용할 수 있다.

UAT DB 기준 확인 결과:

| 항목 | 값 |
| --- | ---: |
| work_items 전체 | 30 |
| due_date 입력 건 | 0 |
| Requested/InProgress 중 due_date 입력 건 | 0 |

현재 UAT에 존재하는 open work item은 `ProductionPlanning`, `DesignPanelInfo`, `ProcurementInfo`, `MaterialArrived` 중심이며, 모두 `due_date`가 비어 있다.

상태값은 `Requested`, `InProgress`, `Completed`, `Cancelled`이다. 미조치 에스컬레이션 대상은 우선 `Requested`, `InProgress`로 보는 것이 적절하다. `Completed`, `Cancelled`는 resolved 처리해야 한다.

`started_at_utc`, `completed_at_utc`, `cancelled_at_utc`가 있으므로 상태 변화에 따른 에스컬레이션 종료 판정은 가능하다.

현재 `WorkflowStore.CreateWorkItemAsync`는 work item 생성 시 `due_date`를 설정하지 않는다. 즉, TASK-NOTIFY-002에서 work_items.due_date 기반 엔진만 구현하면 구조적으로 안전하지만, 실제 UAT 데이터에서는 별도 due_date 입력/동기화가 없으면 발송 대상이 생기지 않는다.

## 4. 생산계획 예정일 분석

생산계획 예정일은 `project_production_plan_items.planned_date`에 저장된다.

UAT DB 기준 확인 결과:

| 항목 | 값 |
| --- | ---: |
| project_production_plan_items 전체 | 60 |
| active planned_date 입력 건 | 24 |
| planned_date 최소 | 2026-06-02 |
| planned_date 최대 | 2026-12-03 |

생산계획 화면과 Excel import/update 흐름은 `planned_date`를 저장하고 audit event를 남긴다. 하지만 이 값은 현재 `work_items.due_date`와 동기화되지 않는다.

생산계획 item의 `planned_date`는 생산 단계별 수행 예정일 성격이고, `ProductionPlanning` work item의 완료 기한과 1:1로 동일하다고 단정하기 어렵다. 예를 들어 생산계획 업무는 여러 plan item의 예정일을 입력하는 업무이고, plan item 예정일은 이후 실무 작업의 기준일일 수 있다.

따라서 TASK-NOTIFY-002 MVP에서는 생산계획 item별 planned_date를 곧바로 work item due_date로 자동 동기화하기보다, work_items.due_date 기반 엔진을 먼저 구현하고 planned_date 동기화 정책은 별도 결정 후 제한적으로 추가하는 것이 안전하다.

## 5. 구매 입고예정일 분석

구매 입고예정일은 `project_procurement_items.expected_receipt_date`에 저장된다.

UAT DB 기준 확인 결과:

| 항목 | 값 |
| --- | ---: |
| project_procurement_items 전체 | 27 |
| expected_receipt_date 입력 건 | 12 |
| expected_receipt_date 최소 | 2026-06-11 |
| expected_receipt_date 최대 | 2026-07-29 |
| expected_receipt_date 입력 건 중 receipt_completed=true | 2 |
| expected_receipt_date 입력 건 중 receipt_completed=false | 10 |

현재 구매 도메인은 입고예정일 경과 품목 수와 최근 입고예정일을 계산한다. D-Day 표시도 존재하지만 calendar day 기준이며 영업일 계산은 아니다.

`expected_receipt_date`는 `ProcurementInfo` 업무보다 `MaterialArrived` 또는 입고 확인 성격의 업무와 더 가깝다. 현재 work item의 `target_type`은 `ProcurementItem`을 허용하지만 실제 자동 생성 work item은 project 단위 중심이다.

따라서 구매 입고예정일을 에스컬레이션에 연결하려면 다음 중 하나를 결정해야 한다.

- `MaterialArrived` project-level work item의 due_date를 해당 프로젝트의 미완료 구매 품목 중 가장 빠른 expected_receipt_date로 동기화한다.
- 품목별 `target_type=ProcurementItem` work item을 생성/관리한다.
- NOTIFY-002에서는 구매 품목별 에스컬레이션을 제외하고 후속 TASK로 분리한다.

안전한 MVP 관점에서는 품목별 에스컬레이션은 제외하고, work_items.due_date 엔진을 먼저 구현하는 것이 적절하다.

## 6. 미구현 stage 예정일 처리

workflow stage는 1~18단계가 master data로 존재한다. 현재 UAT work item은 2~5단계 중심으로 생성되어 있고, stage 6~18은 일부 화면/업무 흐름이 아직 미구현 또는 제한적이다.

미구현 stage에 별도 화면별 예정일 필드가 없는 경우에도 `work_items.due_date`가 입력되어 있다면 공통 에스컬레이션 엔진은 적용 가능하다. 반대로 due_date가 없으면 대상에서 제외해야 한다.

TASK-NOTIFY-002에서는 stage별 화면 구현 여부와 관계없이 다음 기준을 권장한다.

- `work_items.status in ('Requested', 'InProgress')`
- `work_items.due_date is not null`
- `completed_at_utc is null`
- `cancelled_at_utc is null`

이 기준이면 미구현 stage를 억지로 해석하지 않고도 due_date가 명확한 업무만 처리할 수 있다.

## 7. 공휴일/영업일 계산 구조

공휴일/국경일 데이터는 `system_holidays`에 저장된다.

주요 컬럼:

- `holiday_date`
- `name`
- `country_code`
- `source`
- `source_key`
- `is_active`
- `synced_at_utc`

UAT DB 기준 active holiday는 4건이다.

`SystemHolidayStore`는 공휴일 조회와 한국 공휴일/국경일 동기화를 제공한다. 단, 현재 코드에는 `BusinessDayCalculator` 같은 영업일 계산 helper가 없다.

TASK-NOTIFY-002에서는 신규 helper가 필요하다.

권장 구조:

- `BusinessDayCalendar` 또는 `BusinessDayCalculator`
- 기준 timezone: `Asia/Seoul`
- 주말 제외
- `system_holidays where country_code='KR' and is_active=true` 제외
- L0: due_date 기준 D-1 calendar day 또는 business day 여부 결정 필요
- L1: due_date 초과 첫 실행일
- L2: due_date 초과 후 +2영업일
- L3: due_date 초과 후 +3영업일

확정 문서에는 L2/L3가 영업일 기준으로 명시되어 있다. L0 D-1도 영업일 기준인지 calendar day 기준인지 사용자 결정이 필요하다. 실무상 D-1 역시 휴일을 피하려면 영업일 기준이 일관적이다.

## 8. 담당자/수신자 계산 구조

현재 담당자 정보는 `project_assignees`에 저장된다. `responsibility_type`은 sales/design/production/procurement/materials/manufacturing/logistics/quality의 정/부 담당자와 일부 legacy alias를 포함한다.

`WorkflowStore`에는 stage별 primary/secondary responsibility mapping이 이미 있다.

대표 매핑:

| stage | primary | secondary |
| --- | --- | --- |
| ProductionPlanning | ProductionPlanningPrimary | ProductionPlanningSecondary |
| DesignPanelInfo | DesignPrimary | DesignSecondary |
| ProcurementInfo | ProcurementPrimary | ProcurementSecondary |
| MaterialArrived | MaterialsPrimary | MaterialsSecondary |
| IQC | QualityIQC | QualityIQCSecondary |
| ManufacturingWork | ManufacturingPrimary | ManufacturingSecondary |
| LQC | QualityLQC | QualityLQCSecondary |
| OQC | QualityOQC | QualityOQCSecondary |
| CustomerInspection/FAT | QualityCustomerInspection | QualityCustomerInspectionSecondary |
| Packing/Departure/Delivery | LogisticsPrimary | LogisticsSecondary |
| SalesSettlementCompleted | SalesPrimary | SalesSecondary |

UAT DB 기준 project_assignees에는 primary/legacy 담당자가 주로 있고 secondary 담당자는 제한적으로만 존재한다. 예를 들어 `SalesSecondary`는 1건이 있으나, 다수 secondary responsibility는 아직 없다.

수신자 계산 권장:

- L0/L1 정담당자: `work_items.assigned_user_id`를 기준으로 한다. work item 생성 시 이미 fallback까지 반영된 실제 담당자이기 때문이다.
- L2 부담당자: `work_items.workflow_stage_code`와 stage responsibility mapping으로 secondary responsibility를 찾고, `project_assignees`에서 active user를 조회한다.
- L2 생산관리 담당자: `ProductionPlanningPrimary`, `ProductionPlanningSecondary`, legacy `ProductionPlanning` 중 active user를 distinct로 조회한다.
- L3 생산관리 + 영업: `ProductionPlanningPrimary`, `ProductionPlanningSecondary`, `ProductionPlanning`, `SalesPrimary`, `SalesSecondary` 중 active user를 distinct로 조회한다.
- 중복 수신자는 user id 기준 제거한다.
- inactive user는 제외한다.
- email 없는 사용자는 Mail handler에서 `Suppressed`로 기록한다.

L3는 생산관리와 영업으로 한정되어 있으므로 system-administrator fallback은 사용하지 않는 것이 맞다. 담당자가 누락된 경우에는 외부 발송을 억제하거나 admin 조회에서 확인 가능하게 기록하는 편이 안전하다.

## 9. escalation state 관리 선택지

### 선택지 A. notification_deliveries만 사용

구조 예:

- `delivery_type=DueSoonL0`
- `delivery_type=OverdueL1`
- `delivery_type=OverdueL2`
- `delivery_type=OverdueL3`
- `dedupe_key=work-item:{work_item_id}:due:{due_date}:level:{level}:recipient:{user_id}:channel:{channel}`

장점:

- 테이블 추가가 적다.
- 기존 retry/dedupe/status 구조를 재사용한다.
- 실제 발송 이력은 한 곳에서 확인 가능하다.

단점:

- 현재 escalation level, resolved 여부, due_date 변경 이력, next check를 매번 재계산해야 한다.
- 단계 상승이 중복 억제 예외라는 정책을 query만으로 표현하기 복잡하다.
- due_date가 변경되거나 업무가 완료/취소될 때 기존 pending delivery 정리가 어렵다.
- 관리자 조회나 사후 분석에서 "현재 이 업무가 몇 단계인지"를 바로 알기 어렵다.

### 선택지 B. work_item_escalations 테이블 추가

구조 예:

- `id`
- `work_item_id`
- `due_date`
- `current_level`
- `status`
- `last_escalated_at_utc`
- `next_check_at_utc`
- `resolved_at_utc`
- `resolved_reason`
- `created_at_utc`
- `updated_at_utc`

장점:

- 현재 escalation state와 resolved 상태가 명확하다.
- due_date 변경, 업무 완료/취소, 단계 상승 처리가 안정적이다.
- 단계 상승을 중복 억제 예외로 처리하기 쉽다.
- 후속 관리자 조회/운영 진단이 쉽다.

단점:

- 신규 migration이 필요하다.
- state table과 delivery table 간 정합성 관리가 필요하다.

추천은 선택지 B이다. `notification_deliveries`는 발송 이력과 retry 상태에 집중시키고, `work_item_escalations`는 업무별 escalation state를 담당하게 분리하는 것이 안전하다.

## 10. 추천 설계

TASK-NOTIFY-002의 권장 설계는 다음과 같다.

1. `work_items.due_date`를 공통 escalation 기준으로 사용한다.
2. `work_item_escalations` 신규 테이블로 현재 escalation state를 관리한다.
3. `notification_deliveries`는 각 level/channel/recipient별 발송 이력으로 사용한다.
4. 영업일 계산은 `system_holidays`와 주말을 제외하는 신규 helper로 구현한다.
5. L0~L3 판단은 `Asia/Seoul` local date 기준으로 수행한다.
6. 업무가 `Completed` 또는 `Cancelled`가 되면 escalation state를 resolved 처리한다.
7. due_date가 null이면 대상에서 제외한다.
8. due_date가 변경되면 기존 escalation state를 due_date 기준으로 재평가한다.
9. 외부 채널 발송 실패는 업무 흐름을 중단하지 않는다.
10. 단계 상승은 동일 대상/동일 유형 24시간 dedupe의 예외로 처리한다.

추천 delivery_type 후보:

- `DueSoonL0`
- `OverdueL1`
- `OverdueL2`
- `OverdueL3`

현재 `notification_deliveries.delivery_type` check constraint가 고정값을 사용하므로 TASK-NOTIFY-002 migration에서 해당 값을 추가해야 한다.

## 11. 채널 정책 제안

확정 매트릭스는 Teams 개인 알림을 중심으로 정의되어 있지만, 현재 actual 개인 Teams 알림은 구현되어 있지 않다. TASK-NOTIFY-001에서 가능한 actual channel은 Teams channel webhook과 Gmail SMTP Mail이다. Teams Activity Feed 개인별 알림은 TASK-NOTIFY-003으로 분리되어 있다.

선택지:

| 선택지 | 내용 | 장점 | 단점 |
| --- | --- | --- | --- |
| 1. TeamsDirectMessage dry-run + Mail/InApp actual 보완 | L0/L1/L2 개인 Teams 의도는 delivery row로 남기고, actual은 Mail/InApp/필요 시 TeamsChannel로 보완 | 현재 구조와 후속 Activity Feed 전환이 쉽다 | L0/L2 actual 개인 알림이 아직 없다 |
| 2. TeamsChannel actual로 임시 대체 | TeamsChannel에 escalation card를 게시 | 실제 Teams 화면에 보인다 | 개인별 알림 보장이 없고 채널 소음이 커질 수 있다 |
| 3. Activity Feed 선행 | NOTIFY-003을 먼저 구현 | 확정 매트릭스와 가장 잘 맞다 | NOTIFY-002가 지연된다 |

추천은 선택지 1이다. 단, L1/L2처럼 업무 지연 영향이 큰 단계는 사용자가 허용하면 TeamsChannel actual도 병행할 수 있다.

권장 초안:

| 단계 | InApp | TeamsDirectMessage | TeamsChannel | Mail |
| --- | --- | --- | --- | --- |
| L0 | 생성 | DryRunSent 또는 Disabled | 기본 보류 | 보류 |
| L1 | 생성 | DryRunSent 또는 Disabled | 선택적 actual | 정담당자 actual |
| L2 | 생성 | DryRunSent 또는 Disabled | 선택적 actual | 보류 |
| L3 | 생성 | 보류 | 보류 | 생산관리+영업 actual |

사용자가 TeamsChannel 소음을 허용하지 않으면 L1/L2 TeamsChannel actual도 제외한다.

## 12. 포함 범위 제안

TASK-NOTIFY-002 MVP 추천 범위:

- `work_items.due_date` 기반 L0~L3 escalation engine
- `work_item_escalations` state table
- L0/L1/L2/L3 delivery_type 추가
- `BusinessDayCalculator`
- `WorkItemEscalationService`
- `EscalationRecipientResolver`
- `EscalationNotificationRenderer`
- BackgroundService 또는 기존 NotificationDeliveryWorker와 연계되는 scheduler
- `notifications`/`notification_recipients` 인앱 원본 생성
- `notification_deliveries` 외부 발송 row 생성
- 관리자 read-only 조회 확장, 필요 시 escalation state 조회 API
- 테스트 fixture 기반 due_date 업무 검증

실제 생산계획 planned_date, 구매 expected_receipt_date의 자동 동기화는 MVP에서 제외하거나, 사용자가 명확히 승인한 제한 정책만 포함하는 것을 권장한다.

## 13. 제외 범위 제안

TASK-NOTIFY-002에서 제외할 범위:

- Teams Activity Feed 실제 구현
- Teams DM Graph 실제 구현
- Teams Bot 구현
- 개인별 알림 설정 UI
- 발송 실패 수동 재처리 UI
- Pending List 구현
- 예정일 UI 대규모 변경
- 생산계획 item별 escalation
- 구매 item별 escalation
- stage 6~18 화면별 별도 예정일 정책
- 부서장/경영진 escalation
- 야간 억제
- 외부 발송 실패로 workflow transaction 실패시키는 구조

## 14. 필요한 migration 후보

예상 migration 번호:

- `database/migrations/0019_work_item_escalations.sql`

후보 내용:

- `work_item_escalations` 신규 테이블
- `notification_deliveries.delivery_type` check constraint 확장
- `work_items(due_date, status)` 또는 `work_items(status, due_date)` index 추가
- `work_item_escalations(work_item_id, status)` index
- `work_item_escalations(next_check_at_utc, status)` index
- active/open escalation 중복 방지 unique constraint

`notification_deliveries` 기존 row와 TASK-NOTIFY-001 migration은 수정하지 말고 신규 migration에서 constraint를 재정의해야 한다.

## 15. 필요한 backend service 후보

후보 서비스:

- `WorkItemEscalationWorker`
- `WorkItemEscalationService`
- `WorkItemEscalationStore`
- `BusinessDayCalculator`
- `EscalationRecipientResolver`
- `EscalationNotificationRenderer`
- `EscalationDeliveryPlanner`

기존 `NotificationDeliveryWorker`에 escalation row 생성을 직접 넣기보다, escalation 계산 service가 인앱 notification과 delivery row를 생성하고, 기존 dispatcher/worker가 외부 delivery 발송을 처리하게 분리하는 것이 좋다.

설정 후보:

- `Notifications:Escalation:Enabled`
- `Notifications:Escalation:WorkerIntervalSeconds`
- `Notifications:Escalation:TimeZone`
- `Notifications:Escalation:CountryCode`
- `Notifications:Escalation:UseTeamsChannelFallback`
- `Notifications:Escalation:L0UseBusinessDay`
- `Notifications:Escalation:MaxBatchSize`

## 16. 필요한 tests

Backend tests:

- due_date null work item 제외
- Completed/Cancelled work item 제외
- L0 D-1 생성
- L1 due_date 초과 즉시 생성
- L2 +2영업일 생성
- L3 +3영업일 생성
- 주말 제외
- system_holidays active 공휴일 제외
- inactive holiday 미제외
- `Asia/Seoul` local date 경계
- 단계 상승 시 dedupe 예외
- 동일 단계/동일 recipient/channel/due_date 24시간 중복 억제
- work item 완료 시 escalation resolved
- due_date 변경 시 state 재평가
- L0/L1 primary recipient = `work_items.assigned_user_id`
- L2 secondary + production planning recipient 계산
- L3 production planning + sales recipient 계산
- 중복 수신자 제거
- inactive user 제외
- email 없는 Mail recipient suppressed
- TeamsDirectMessage dry-run/disabled 처리
- Mail actual/dry-run 기존 provider 회귀
- notification_deliveries retry와 escalation state 충돌 없음
- Migration 0017 기존 DB 적용 test
- 신규 DB 적용 test

E2E/Smoke:

- 기존 Full-Stack E2E 회귀
- mock UI smoke
- UAT DB persistence
- 필요 시 dev-admin으로 escalation admin 조회 smoke

## 17. 위험 요소

- 현재 work_items.due_date가 비어 있어 engine만 구현하면 실제 발송 대상이 없을 수 있다.
- 생산계획 planned_date와 구매 expected_receipt_date를 어떤 work item due_date로 동기화할지 업무 의미가 아직 불명확하다.
- Teams 개인 actual 알림이 아직 없으므로 L0/L2의 Teams 요구사항을 actual로 완전히 만족하지 못한다.
- L2/L3 수신자 계산에서 secondary 담당자가 비어 있을 가능성이 높다.
- L3에서 system administrator fallback을 쓰면 "생산관리와 영업 한정" 기준을 훼손할 수 있다.
- 영업일 계산 기준 timezone과 D-1 기준이 명확하지 않으면 경계일 테스트가 흔들릴 수 있다.
- due_date 변경 후 기존 pending delivery를 어떻게 처리할지 정책이 필요하다.

## 18. 사용자 결정 필요 항목

1. 생산계획 planned_date를 work_items.due_date로 자동 동기화할지 결정해야 한다.
2. 구매 expected_receipt_date를 `MaterialArrived` work item due_date로 동기화할지, 품목별 work item을 만들지 결정해야 한다.
3. 사용자에게 due_date 입력/수정 UI를 제공할지 결정해야 한다.
4. Teams 개인 알림 actual이 없는 동안 L0/L1/L2를 TeamsDirectMessage dry-run으로 둘지, TeamsChannel actual로 임시 대체할지 결정해야 한다.
5. L2에서 secondary 담당자가 없을 때 해당 수신자를 생략할지, 별도 fallback을 둘지 결정해야 한다.
6. L3에서 생산관리/영업 담당자가 없을 때 system-administrator fallback 없이 suppressed/diagnostic 처리할지 결정해야 한다.

## 19. 구현 착수 시 반영된 결정

이번 TASK-NOTIFY-002 구현 착수에서는 다음 결정을 반영했다.

- L0 기준은 예정일의 직전 영업일로 확정한다.
- 이번 구현은 `work_items.due_date`가 있는 업무만 대상으로 하는 에스컬레이션 엔진까지로 제한한다.
- 현재 due_date 입력/동기화 정책은 미확정이다. 생산계획/구매 예정일과 `work_items.due_date`의 자동 동기화는 후속 결정 후 구현한다.
- 생산계획 planned_date와 구매 expected_receipt_date의 자동 동기화는 이번 구현에서 제외한다.
- 각 업무 입력 화면의 due_date 입력 UI는 이번 구현에서 제외한다.
- 에스컬레이션 상태는 `work_item_escalations` 신규 테이블로 관리한다.
- delivery type은 `DueSoonL0`, `OverdueL1`, `OverdueL2`, `OverdueL3`를 추가한다.
- L0/L1/L2의 Teams 개인 알림 의도는 `TeamsDirectMessage` dry-run delivery로 기록한다.
- TeamsChannel actual fallback은 설정값으로만 허용하고 기본값은 false로 둔다.
- L1/L3 Mail은 기존 Gmail SMTP Mail provider가 처리할 수 있도록 Mail delivery row를 생성한다.
- L3 알림은 기존 긴급 알림 dispatcher가 `UrgentBlocking`을 추가 생성하지 않도록 별도 `OverdueL3` delivery로만 처리한다.
- L2/L3 수신자는 담당자 누락 시 존재하는 수신자만 distinct로 처리하고, system-administrator fallback은 사용하지 않는다.
- System Administrator용 에스컬레이션 조회 API를 최소 read-only로 제공한다.

## 20. 사용자 추가 결정 반영

최종 리뷰 전 사용자 결정으로 due_date 상세 정책은 아직 확정하지 않기로 했다. 따라서 TASK-NOTIFY-002는 due_date 자동 동기화나 입력 UX를 추가하지 않고, due_date가 이미 설정된 work item을 처리하는 엔진만 제공한다.

또한 Daily Digest에는 `내 담당 프로젝트 요약` section을 추가한다.

- 기준: `project_assignees.assigned_user_id`
- 대상 프로젝트: 삭제되지 않았고 상태가 `Active`인 프로젝트
- 표시 항목: 프로젝트명, 납기일(`projects.delivery_date`), 담당역할
- 같은 프로젝트의 여러 담당역할은 comma로 묶는다.
- 담당 프로젝트 요약만 있어도 digest content로 보고 일일 요약 delivery 생성 대상에 포함한다.
