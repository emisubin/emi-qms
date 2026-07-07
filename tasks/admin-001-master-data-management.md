# TASK-ADMIN-001 관리자 시스템 관리 1차 구현

## 1. 목적

ADMIN-001은 관리자 페이지를 시스템 관리 중심으로 정리한다. 관리자는 사용자, 부서, 휴일, 권한 조회, 이력 조회, 알림/에스컬레이션 상태를 관리하거나 확인한다. 각 부서가 업무 과정에서 입력하는 Item, 포장방식, 생산계획 단계, 구매 필수 항목은 이번 관리자 페이지 관리 대상에서 제외한다.

## 2. 포함 범위

- 관리자 홈 대시보드
- 기존 사용자 관리 화면/API 재사용
- 부서 관리
- 기존 휴일 관리 화면/API 재사용
- 권한 매트릭스 읽기 전용
- 기준정보 변경 이력 조회
- work_items timestamp 기반 업무 시작/완료 이력 조회
- notification_deliveries 발송 상태 조회
- work_item_escalations 에스컬레이션 상태 조회
- 관리자 메뉴/route 정리
- 관리 가능한 데이터의 삭제 버튼 제공

## 3. 제외 범위

- Item 관리
- 포장방식 관리
- 포장방식 `size_required` 관리
- 생산계획 단계 설정의 관리자 메뉴 통합
- 구매 필수 항목 설정의 관리자 메뉴 통합
- 권한 매트릭스 편집
- role/permission master 편집
- 신규 Item 추가
- Pending 유형 관리
- 검사/제조 체크리스트 템플릿
- 알림 설정 UI
- 발송 실패 수동 재처리 UI
- due_date 정책 관리
- Teams Activity Feed actual
- 전체 field-level audit 개편

## 4. 관리자 메뉴 구조

- 관리자 홈
- 운영
  - 사용자 관리
  - 알림 발송 상태
  - 에스컬레이션 상태
- 시스템 관리
  - 부서
  - 공휴일
- 조회
  - 권한 매트릭스
  - 기준정보 변경 이력
  - 업무 시작/완료 이력

제거 대상 메뉴는 관리자 메뉴에서 표시하지 않는다.

## 5. 삭제 정책

화면 버튼 문구는 `삭제`로 표시한다. 삭제 시점에는 hard delete하지 않고 `is_active=false`와 삭제 예약 컬럼을 함께 기록한다. 예약 후 7일이 지나면 purge worker가 완전 삭제 가능 여부를 확인한다.

- 활성: `is_active=true`, 삭제 예약 컬럼 없음
- 비활성: `is_active=false`, 삭제 예약 컬럼 없음
- 삭제 예정: `is_active=false`, `deletion_requested_at_utc`와 `scheduled_hard_delete_at_utc` 있음
- 삭제 완료: 참조 무결성 확인 후 DB에서 hard delete 완료

대상별 정책:

- 사용자: 삭제 버튼 → 비활성화 + 7일 후 완전 삭제 예약. 마지막 active System Administrator와 Dev user는 삭제 예약 불가.
- 부서: 삭제 버튼 → 비활성화 + 7일 후 완전 삭제 예약. 참조 사용자가 있으면 purge 시 완전 삭제를 보류한다.
- 휴일: 삭제 버튼 → 비활성화 + 7일 후 완전 삭제 예약. BusinessDayCalculator에서는 즉시 휴일로 계산하지 않는다.
- 단순 비활성화: 삭제 예약 컬럼 없이 `is_active=false`만 유지하며 7일 후 purge 대상이 아니다.

조회성 페이지에는 삭제 버튼을 만들지 않는다.

- 권한 매트릭스
- 기준정보 변경 이력
- 업무 시작/완료 이력
- 알림 발송 상태
- 에스컬레이션 상태

## 6. 권한 정책

- 사용자/부서/휴일 관리는 기존 `users.manage` 기반 관리자 정책을 재사용한다.
- 조회성 관리자 화면은 `admin-history.read`를 사용한다.
- 불필요한 `master-data.manage` 신규 권한은 만들지 않는다.
- UI 숨김은 보조 수단이며 서버 Policy가 권한을 강제한다.
- System Administrator는 모든 페이지 조회/접근이 가능해야 한다. 단, 업무 입력 action 권한을 관리자 권한으로 우회하지 않는다.

## 7. Migration 범위

신규 migration `0020_admin_master_data_management.sql`은 다음으로 제한한다.

- `admin-history.read` permission seed
- System Administrator 역할에 `admin-history.read` 부여
- `departments.is_active`
- `departments.sort_order`
- `departments.updated_at_utc`
- `departments.deletion_requested_at_utc`
- `departments.scheduled_hard_delete_at_utc`
- `departments.purge_blocked_at_utc`
- `departments.purge_blocked_reason`
- `qms_users.deletion_requested_at_utc`
- `qms_users.scheduled_hard_delete_at_utc`
- `qms_users.purge_blocked_at_utc`
- `qms_users.purge_blocked_reason`
- `system_holidays.deletion_requested_at_utc`
- `system_holidays.scheduled_hard_delete_at_utc`
- `system_holidays.purge_blocked_at_utc`
- `system_holidays.purge_blocked_reason`
- `admin_master_change_logs`

UAT DB에는 과거 WIP `0020_admin_master_data_management`가 이미 기록된 상태에서 deletion lifecycle 컬럼이 누락될 수 있다. 이 drift는 기존 `schema_migrations`를 수동 조작하지 않고 `0021_admin_deletion_lifecycle_patch.sql`로 보정한다.

`0021_admin_deletion_lifecycle_patch.sql`:

- `qms_users` deletion lifecycle 컬럼을 `if not exists`로 보강
- `departments` deletion lifecycle 컬럼을 `if not exists`로 보강
- `system_holidays` deletion lifecycle 컬럼을 `if not exists`로 보강
- purge 조회용 scheduled hard delete index를 `if not exists`로 보강
- clean DB에서는 0020에 같은 컬럼이 이미 있어 no-op에 가깝게 동작
- drift UAT DB에서는 누락된 컬럼만 추가하고 기존 데이터는 보존

`0022_admin_deletion_restore_bulk_actions.sql`:

- 삭제 예정 데이터를 7일 안에 원상 복구하기 위한 `pre_delete_is_active`를 사용자, 부서, 휴일에 `if not exists`로 추가한다.
- 삭제 예약 시 기존 `is_active` 값을 `pre_delete_is_active`에 저장한다.
- 복구 시 `is_active = coalesce(pre_delete_is_active, true)`로 복원한 뒤 deletion/purge 필드와 `pre_delete_is_active`를 비운다.
- clean DB와 drift UAT DB 모두에서 안전하게 적용되도록 기존 데이터 삭제 없이 no-op 가능한 patch migration으로 유지한다.

다음 항목은 migration에서 제외한다.

- `production_product_types` 확장
- `packaging_methods` 신규 테이블
- 포장방식 `size_required`
- 패널 완료 판정 함수 변경

## 8. 기존 기능 재사용

- 사용자 관리는 INFRA-001 구현을 재사용한다.
- 휴일 관리는 CALENDAR-001 구현을 재사용한다.
- 알림 발송 상태는 NOTIFY-001/002의 `notification_deliveries` 조회를 재사용한다.
- 에스컬레이션 상태는 NOTIFY-002의 `work_item_escalations` 조회를 재사용한다.

## 8-1. 삭제 예약 / purge 구조

- `AdminScheduledDeletionService`는 삭제 예정 사용자, 부서, 휴일 중 `scheduled_hard_delete_at_utc <= now`인 대상을 처리한다.
- 휴일은 참조 위험이 낮으므로 완전 삭제 가능하면 hard delete한다.
- 사용자와 부서는 참조 테이블을 확인한 뒤 안전하지 않으면 hard delete하지 않고 `purge_blocked_at_utc`, `purge_blocked_reason`을 기록한다.
- purge worker는 TimeProvider 기반으로 테스트 가능해야 한다.
- purge 보류 사유는 관리자 UI에서 `삭제 보류` 상태와 함께 표시한다.
- 삭제 예정 또는 삭제 보류 상태에서 삭제 버튼을 다시 누르면 즉시 완전 삭제를 시도한다.
- 즉시 완전 삭제도 `AdminScheduledDeletionService`의 purge 로직을 재사용한다.
- 사용자와 부서는 참조가 있으면 cascade delete하지 않고 삭제 보류로 남긴다.

## 8-1-1. 복구 / 일괄 처리

- 삭제 예정과 삭제 보류 상태의 사용자, 부서, 휴일은 7일 유예기간 안에 복구할 수 있다.
- 사용자, 부서, 휴일 목록 앞에는 선택 체크박스를 제공한다.
- 선택 삭제는 활성/비활성 항목은 삭제 예정으로 전환하고, 삭제 예정/삭제 보류 항목은 즉시 완전 삭제를 시도한다.
- 선택 복구는 삭제 예정/삭제 보류 항목을 복구하고, 활성/비활성 항목은 건너뛴다.
- bulk API는 개별 성공/실패/건너뜀 결과를 반환하고, 일부 실패가 전체 작업을 rollback하지 않는다.

## 8-2. Lifecycle 상태/API 응답

사용자, 부서, 휴일 관리자 API는 다음 필드를 내려준다.

- `isActive`
- `deletionRequestedAtUtc`
- `scheduledHardDeleteAtUtc`
- `purgeBlockedAtUtc`
- `purgeBlockedReason`
- `preDeleteIsActive`
- `lifecycleStatus`
- `lifecycleStatusLabel`
- `scheduledHardDeleteLabel`

상태 계산 기준:

- `PurgeBlocked` / `삭제 보류`: `purgeBlockedAtUtc`가 있음
- `DeletionScheduled` / `삭제 예정`: `deletionRequestedAtUtc`와 `scheduledHardDeleteAtUtc`가 있음
- `Inactive` / `비활성`: `isActive=false`이고 삭제 예약/보류 필드가 없음
- `Active` / `활성`: `isActive=true`이고 삭제 예약/보류 필드가 없음

UI는 `lifecycleStatusLabel`을 우선 표시하고, 삭제 예정/삭제 보류 상태에서는 `scheduledHardDeleteLabel` 또는 raw UTC 값을 한국 기준으로 변환한 `완전 삭제 예정일 yyyy-MM-dd HH:mm`을 함께 표시한다.

## 8-3. UAT schema drift 주의

현재 ADMIN-001은 작업 브랜치의 WIP migration `0020_admin_master_data_management.sql`을 수정 중이다. UAT DB에 과거 WIP 0020이 이미 기록된 경우 현재 migration의 deletion 컬럼이 자동 적용되지 않는다. 이를 위해 `0021_admin_deletion_lifecycle_patch.sql`을 추가해 누락 컬럼을 안전하게 보강한다. UAT DB `schema_migrations` 수동 조작, drop/truncate, Docker volume 삭제는 금지한다.

## 8-4. 부서 validation 상세 오류

부서 추가/수정 API는 `message`와 `fieldErrors`를 함께 반환한다.

- `code`: 필수, 2~50자, 영문 대문자/숫자/하이픈/언더스코어만 허용, 중복 금지
- `name`: 필수, 100자 이하, 한글/영문/숫자/공백/괄호/하이픈만 허용
- `sortOrder`: 0 이상 9999 이하 숫자

프론트엔드는 각 input 아래에 해당 field error를 한글로 표시한다. 전체 오류 메시지로만 “입력값을 확인해주세요.”를 노출하지 않는다.

## 8-5. target-not-found 방지

사용자 삭제 예약 성공 후에는 현재 사용자 관리 화면에 머무르고 목록을 갱신한다. 삭제 문맥에서 404가 발생하면 “대상을 찾을 수 없습니다.”를 그대로 노출하지 않고 삭제 예약 실패 문맥의 한글 메시지를 표시한다. 마지막 System Administrator와 Dev user 차단 메시지는 서버 메시지를 그대로 표시한다.

## 9. 사용자 검수 체크리스트

- [ ] `/admin` 접속 시 관리자 홈이 정상 표시됨
- [ ] “대상을 찾을 수 없습니다.” 오류가 표시되지 않음
- [ ] 관리자 홈의 모든 카드/버튼이 정상 route로 이동함
- [ ] 제거 대상 메뉴가 보이지 않음
  - Item 관리
  - 포장방식 관리
  - 생산계획 단계 설정
  - 구매 필수 항목 설정
- [ ] 사용자 관리 화면에서 삭제 버튼이 보임
- [ ] 사용자/부서/휴일 목록에 선택 체크박스가 표시됨
- [ ] 전체 선택 체크박스가 동작함
- [ ] 선택 삭제가 동작함
- [ ] 선택 복구가 동작함
- [ ] 사용자 삭제 클릭 시 확인창이 표시됨
- [ ] 사용자 삭제 후 상태가 “삭제 예정”으로 표시됨
- [ ] 사용자 삭제 후 완전 삭제 예정일이 표시됨
- [ ] 삭제 예정 사용자를 복구할 수 있음
- [ ] 삭제 예정 사용자를 다시 삭제하면 즉시 삭제가 시도됨
- [ ] 참조 때문에 즉시 삭제가 불가하면 “삭제 보류”가 표시됨
- [ ] 마지막 System Administrator 삭제 시 “마지막 System Administrator는 삭제할 수 없습니다.” 문구가 표시됨
- [ ] Dev user는 기존 read-only 정책을 유지함
- [ ] 부서 관리 화면에서 추가/수정/삭제가 가능함
- [ ] 부서 추가 시 잘못된 칸 아래에 상세 오류 사유가 표시됨
- [ ] 부서 코드 입력 조건이 명확히 표시됨
- [ ] 부서명 입력 조건이 명확히 표시됨
- [ ] 정렬 순서 입력 조건이 명확히 표시됨
- [ ] 부서 삭제 후 상태가 “삭제 예정”으로 표시됨
- [ ] 부서 복구가 가능함
- [ ] 부서 즉시 삭제 시 참조가 있으면 삭제 보류가 표시됨
- [ ] 부서 삭제 후 기존 사용자 소속 정보가 깨지지 않음
- [ ] 휴일 관리 화면에서 기존 Calendar 기능이 정상 동작함
- [ ] 휴일 삭제 버튼이 보이고 삭제 후 상태가 “삭제 예정”으로 표시됨
- [ ] 휴일 복구가 가능함
- [ ] 휴일 즉시 삭제가 가능함
- [ ] 휴일 삭제 후 business-days 계산에서 제외됨
- [ ] 복구된 휴일은 business-days 계산에 다시 포함됨
- [ ] 비활성 상태와 삭제 예정 상태가 화면에서 구분됨
- [ ] System Administrator가 모든 페이지에 접근 가능함
- [ ] 권한 매트릭스는 읽기 전용임
- [ ] 권한 매트릭스 헤더와 데이터 정렬이 맞음
- [ ] 업무 시작/완료 이력은 조회 전용임
- [ ] 알림 발송 상태는 조회 전용임
- [ ] 에스컬레이션 상태는 조회 전용임
- [ ] non-admin 사용자는 관리자 기능에 접근할 수 없음
- [ ] 모바일에서 page-level horizontal overflow가 없음
- [ ] Console 오류 없음

## 10. 테스트 계획

- backend: admin dashboard, department create/update/delete, change logs, work item history, permission matrix, notification/escalation monitor, non-admin 403
- frontend: admin dashboard render, route buttons, removed menu hidden, user/department/holiday delete button, read-only pages, non-admin block
- regression: project create/edit, panel validation, production planning, procurement
- validation: git diff check, actionlint, backend build/tests, frontend lint/typecheck/unit/build, Full-Stack E2E, UAT smoke, secret/PII scan

## 11. 남은 후속 범위

- 포장방식 기준정보와 `size_required` 속성화는 별도 사용자 결정 후 진행
- Item 신규 추가/관리 정책은 별도 TASK에서 결정
- 생산계획/구매 기준정보는 각 업무 영역 정책 확정 후 처리
- role/permission 편집 UI는 후속 범위
- 발송 실패 수동 재처리 UI는 후속 범위
