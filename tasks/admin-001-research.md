# TASK-ADMIN-001 사전 조사 보고서

## 1. 조사 목적

관리자 기준정보 페이지를 구현하기 전에 현재 main 기준 관리자/기준정보 구조를 확인하고, 사용자 결정에 따라 ADMIN-001 범위를 시스템 관리 중심으로 축소한다.

## 2. 기준 문서 요약

- AGENTS.md: main 직접 작업 금지, 기존 UAT DB drop/truncate 금지, 기존 migration 수정 금지, secret commit 금지.
- roadmap: TASK-INFRA-001, CALENDAR-001, NOTIFY-001/002 구현이 완료되어 사용자 관리, 휴일 관리, 알림 발송 상태, 에스컬레이션 상태 조회 기반이 존재한다.
- Calendar-001: 휴일 관리는 System Administrator 전용 API/UI와 Excel 등록 기능이 이미 구현되어 있으므로 ADMIN-001에서 중복 구현하지 않는다.
- Notify-001/002: `notification_deliveries`, `work_item_escalations` 조회 구조가 이미 존재하므로 관리자 모니터는 이를 재사용한다.

## 3. 현재 관리자/기준정보 구현 현황

- 사용자 관리: INFRA-001 사용자 관리 API/UI가 존재한다.
- 휴일 관리: CALENDAR-001 휴일 관리 API/UI가 존재한다.
- 알림 상태: admin notification delivery 조회 API가 존재한다.
- 에스컬레이션 상태: admin work item escalation 조회 API가 존재한다.
- 업무 이력: `work_items.started_at_utc`, `completed_at_utc`, `cancelled_at_utc` 기준 조회가 가능하다.
- 권한: role/permission seed와 permission claim 기반 Policy 구조가 있다.

## 4. 기준정보별 저장 방식

- Item: `production_product_types`와 코드/seed 기반으로 쓰이며 이번 ADMIN-001 관리 대상에서 제외한다.
- 생산계획 단계 템플릿: 생산관리 영역 설정으로 유지하고 관리자 메뉴에 통합하지 않는다.
- 구매 필수 항목 템플릿: 구매 영역 설정으로 유지하고 관리자 메뉴에 통합하지 않는다.
- 포장방식: 현재 프로젝트 입력/검증의 기존 고정 기준을 유지하고 ADMIN-001에서 DB 기준정보화하지 않는다.
- 공휴일: `system_holidays`와 Calendar-001 관리자 휴일 기능을 재사용한다.
- 부서: `departments`를 확장해 활성/정렬/변경시각을 관리할 수 있다.

## 5. 참조 테이블/참조 검사 대상

- 사용자: `qms_users`는 업무, 알림, 이력, 역할, 담당자 구조에서 참조될 수 있다. 삭제 버튼은 즉시 hard delete가 아니라 비활성화 + 7일 후 완전 삭제 예약으로 처리한다. purge 시 참조가 남아 있으면 완전 삭제를 보류한다.
- 부서: 기존 사용자 소속은 보존하고 신규 선택에서 제외한다. 삭제 버튼은 비활성화 + 7일 후 완전 삭제 예약으로 처리하며, 참조 사용자가 있으면 purge를 보류한다.
- 휴일: 삭제 버튼은 비활성화 + 7일 후 완전 삭제 예약으로 처리한다. 비활성/삭제 예정 휴일은 BusinessDayCalculator 계산에서 즉시 제외한다.
- 알림/에스컬레이션/이력: 조회성 데이터이므로 삭제 버튼을 제공하지 않는다.
- 관리자 대시보드의 발송 실패/대기/진행 중 에스컬레이션 count는 상세 추적 화면으로 연결되어야 한다.
- 발송 실패/대기 상세는 status query filter를 지원하고, 관리자 조치 안내를 함께 제공해야 한다.
- 진행 중 에스컬레이션은 active total만 표시하지 않고 L0 예정일 임박과 L1~L3 초과 단계를 구분해야 한다.
- 관리자 표는 header/body 정렬이 일관되어야 하며, checkbox/status/date/action 컬럼은 공통 alignment 원칙을 적용한다.

## 6. 기존 사용자 관리 재사용 방안

- 기존 `/admin/users` 화면과 `/api/admin/users` API를 재사용한다.
- Dev user read-only 정책과 마지막 System Administrator 보호 정책은 유지한다.
- 화면에는 삭제 버튼을 추가하되 내부 처리는 삭제 예약 API로 처리한다.
- 마지막 active System Administrator 삭제/비활성화 시 “마지막 System Administrator는 삭제할 수 없습니다.” 문구를 표시한다.

## 7. 기존 휴일 관리 재사용 방안

- 기존 `/admin/calendar/holidays` 화면과 Calendar-001 API를 재사용한다.
- 버튼 문구는 `삭제`로 표시하고 내부는 Calendar API의 삭제 예약/deactivate 경로를 호출한다.
- Excel 양식/preview/apply는 그대로 유지한다.

## 8. 알림 발송 상태/작업 모니터 가능성

- 관리자 홈 카드:
  - 승인 대기 사용자 수
  - notification delivery 실패/대기 건수
  - 마지막 Daily Digest 발송 시각
  - active escalation 건수
  - 최근 기준정보 변경 건수
- 상세 조회:
  - `/api/admin/notification-deliveries`
  - `/api/admin/work-item-escalations`

## 9. 변경 이력 구조 조사

전체 field-level audit는 이번 범위가 아니다. ADMIN-001에서 관리하는 부서 변경만 `admin_master_change_logs`에 기록한다.

후보 컬럼:

- entity_type
- entity_id
- action
- before_json
- after_json
- reason
- changed_by_user_id
- changed_at_utc

## 10. 업무 시작/완료 이력 조회 가능성

별도 event table 없이 `work_items` timestamp로 1차 조회가 가능하다.

- 시작: `started_at_utc`
- 완료: `completed_at_utc`
- 취소: `cancelled_at_utc`
- 필터 후보: 프로젝트, 사용자, 단계, 상태, 기간

## 11. 권한 체계와 신규 권한 제안

- 신규 `admin-history.read`만 추가한다.
- `master-data.manage`는 이번 축소 범위에서 추가하지 않는다.
- 사용자/부서/휴일 관리는 기존 `users.manage` 계열 관리자 권한을 재사용한다.
- System Administrator 역할에만 `admin-history.read`를 부여한다.

## 12. 포장방식 사이즈 필수 속성화 조사

포장방식 `size_required` 속성화는 이번 ADMIN-001에서 제외한다.

이 기능은 패널 완료 판정, 프로젝트 입력 validation, Excel import/export, 기존 포장방식 표시와 연결되어 회귀 범위가 크다. 사용자 결정에 따라 관리자 페이지는 시스템 관리 중심으로 축소하고, 포장방식은 각 업무 기준정보 영역의 후속 TASK로 분리한다.

## 13. 비활성화 + 참조 검사 정책 제안

- 사용자: 삭제 버튼 → `is_active=false`, `deletion_requested_at_utc=now`, `scheduled_hard_delete_at_utc=now+7일`. Dev user read-only와 마지막 System Administrator 보호를 유지한다.
- 부서: 삭제 버튼 → 삭제 예정 상태. 기존 사용자 소속은 유지하고 신규 선택에서는 제외한다. 참조 사용자가 있으면 7일 후 purge를 보류한다.
- 휴일: 삭제 버튼 → 삭제 예정 상태. BusinessDayCalculator 계산에서는 즉시 제외하고, 7일 후 hard delete 가능하면 삭제한다.
- 단순 비활성: 삭제 예약 컬럼 없이 `is_active=false`만 유지하며 7일 후 purge 대상이 아니다.
- 조회성 페이지: 삭제 버튼 없음
- 7일 유예기간의 목적은 삭제 예정/삭제 보류 데이터를 복구할 수 있게 하는 것이다.
- 삭제 예약 시 `pre_delete_is_active`에 이전 활성 상태를 저장하고, 복구 시 원래 활성/비활성 상태를 되돌린다.
- 삭제 예정/삭제 보류 상태에서 삭제를 다시 누르면 즉시 완전 삭제를 시도한다.
- 참조 때문에 즉시 hard delete가 불가능하면 cascade delete하지 않고 삭제 보류 상태로 남긴다.
- 사용자/부서/휴일 목록은 체크박스 기반 선택 삭제와 선택 복구를 제공한다.

API와 UI는 다음 lifecycle 상태를 공통으로 사용한다.

- `Active` / `활성`
- `Inactive` / `비활성`
- `DeletionScheduled` / `삭제 예정`
- `PurgeBlocked` / `삭제 보류`

사용자, 부서, 휴일 API 응답에는 `deletionRequestedAtUtc`, `scheduledHardDeleteAtUtc`, `purgeBlockedAtUtc`, `purgeBlockedReason`, `preDeleteIsActive`, `lifecycleStatus`, `lifecycleStatusLabel`, `scheduledHardDeleteLabel`을 포함한다. 화면은 삭제 예정/삭제 보류 상태에서 완전 삭제 예정일을 한국 기준 `yyyy-MM-dd HH:mm` 형태로 표시한다.

UAT DB에 과거 WIP 0020 migration이 이미 적용되어 현재 작업 브랜치의 deletion 컬럼이 없는 경우, 현재 0020은 다시 실행되지 않는다. 따라서 `0021_admin_deletion_lifecycle_patch.sql`을 추가해 누락 컬럼을 `if not exists`로 보강한다. UAT DB를 수동 조작하거나 drop/truncate하지 않는다.

복구와 일괄 처리에는 `0022_admin_deletion_restore_bulk_actions.sql`을 추가한다. 이 patch migration은 사용자, 부서, 휴일에 `pre_delete_is_active`를 `if not exists`로 추가하고, clean DB와 drift UAT DB 모두에서 기존 데이터를 보존한다.

## 14. D-1~D-5 결정 추천

- D-1 벤치마크 6종: 관리자 홈, 비활성화 원칙, 변경 이력, 권한 매트릭스 읽기 전용, 업무 이력, 경량 작업 모니터는 승인 추천
- D-2 권한 분리: `admin-history.read`만 승인 추천, `master-data.manage`는 제외 추천
- D-3 삭제 정책: 화면 삭제 + 내부 삭제 예정 + 7일 후 purge 승인 추천
- D-4 공휴일 Excel: Calendar-001 구현 재사용 승인 추천
- D-5 포장방식 size_required: ADMIN-001 제외 추천

## 15. ADMIN-001 포함 범위 추천

- 관리자 홈 대시보드
- 사용자 관리 메뉴 통합
- 부서 관리
- 휴일 관리 메뉴 통합
- 권한 매트릭스 읽기 전용
- 기준정보 변경 이력
- 업무 시작/완료 이력
- 알림 발송 상태 조회
- 에스컬레이션 상태 조회

## 16. ADMIN-001 제외 범위 추천

- Item 관리
- 포장방식 관리
- 포장방식 `size_required`
- 생산계획 단계 설정 관리자 메뉴 통합
- 구매 필수 항목 설정 관리자 메뉴 통합
- role/permission 편집
- 알림 설정/수동 재처리
- due_date 정책 관리
- Teams Activity actual

## 17. 필요한 migration 후보

- `0020_admin_master_data_management.sql`
  - `admin-history.read`
- `0021_admin_deletion_lifecycle_patch.sql`
  - UAT drift 보정용 deletion lifecycle 컬럼/index patch
- `0022_admin_deletion_restore_bulk_actions.sql`
  - 삭제 예정 복구와 일괄 처리용 `pre_delete_is_active`
- `departments.is_active`
- `departments.sort_order`
- `departments.updated_at_utc`
- 사용자/부서/휴일 삭제 예약 컬럼
  - `deletion_requested_at_utc`
  - `scheduled_hard_delete_at_utc`
  - `purge_blocked_at_utc`
  - `purge_blocked_reason`
- `admin_master_change_logs`

## 18. 필요한 backend service/API 후보

- `GET /api/admin/dashboard`
- `GET/POST/PUT/PATCH /api/admin/departments`
- `PATCH /api/admin/users/{id}/schedule-deletion`
- `POST /api/admin/users/{id}/restore`
- `DELETE /api/admin/users/{id}/purge`
- `POST /api/admin/users/bulk-delete`
- `POST /api/admin/users/bulk-restore`
- `POST /api/admin/departments/{id}/restore`
- `DELETE /api/admin/departments/{id}/purge`
- `POST /api/admin/departments/bulk-delete`
- `POST /api/admin/departments/bulk-restore`
- `POST /api/admin/calendar/holidays/{id}/restore`
- `DELETE /api/admin/calendar/holidays/{id}/purge`
- `POST /api/admin/calendar/holidays/bulk-delete`
- `POST /api/admin/calendar/holidays/bulk-restore`
- `GET /api/admin/permissions/matrix`
- `GET /api/admin/master-data/change-logs`
- `GET /api/admin/work-items/history`
- `AdminScheduledDeletionService`
- `AdminDeletionPurgeWorker`
- 기존 notification/escalation admin endpoint 재사용

## 19. 필요한 frontend 화면 후보

- `/admin`
- `/admin/users`
- `/admin/departments`
- `/admin/calendar/holidays`
- `/admin/permissions`
- `/admin/history/master-data`
- `/admin/history/work-items`
- `/admin/system/notification-deliveries`
- `/admin/system/work-item-escalations`

## 20. 필요한 tests

- admin dashboard
- department create/update/delete
- user delete/deactivate button
- holiday delete/deactivate button
- user/department/holiday restore
- user/department/holiday bulk delete
- user/department/holiday bulk restore
- deletion scheduled item immediate purge
- deletion scheduled status and scheduled hard delete date display
- 7-day purge and purge blocked policy
- lifecycleStatus/lifecycleStatusLabel API response
- scheduledHardDeleteLabel API response
- department validation fieldErrors
- last System Administrator Korean error message
- System Administrator all page access, without business action bypass
- non-admin 403
- permission matrix read-only
- permission matrix header/body alignment
- change logs
- work item history
- notification/escalation monitor
- removed menu hidden
- project/panel/production/procurement regression

## 21. 위험 요소

- 관리자 메뉴의 제거 대상 링크가 남아 있으면 “대상을 찾을 수 없습니다.” 오류가 발생한다.
- 부서 삭제가 실제 사용자 소속을 지우면 안 된다.
- 사용자 삭제는 마지막 System Administrator 보호를 깨면 안 된다.
- 사용자/부서 완전 삭제는 참조 무결성을 깨면 안 되므로 purge 보류 상태가 필요하다.
- Calendar-001 휴일 관리와 중복 구현하면 route/API 충돌 가능성이 있다.

## 22. 사용자 결정 필요 항목

- 포장방식 기준정보/`size_required`를 별도 TASK로 진행할지
- Item 관리 정책을 별도 TASK로 진행할지
- role/permission 편집 UI를 추후 구현할지
- 업무 기준정보를 각 부서 업무 화면에서만 관리할지

## 23. 구현 프롬프트 작성 전 체크리스트

- [x] Item/포장방식/생산계획 단계/구매 필수 항목을 ADMIN-001에서 제외
- [x] 사용자/부서/휴일 삭제 버튼은 삭제 예정 + 7일 후 purge 구조로 처리
- [x] 삭제 예정/삭제 보류 데이터 복구와 선택 삭제/선택 복구를 구현 범위에 포함
- [x] 조회성 페이지에는 삭제 버튼 없음
- [x] `admin-history.read`만 신규 권한으로 유지
- [x] Calendar-001 휴일 관리는 재사용
- [x] UAT 0020 drift는 0021 patch migration으로 보정

## 24. 사용자 검수 체크리스트

- [ ] `/admin` 접속 시 관리자 홈이 정상 표시됨
- [ ] “대상을 찾을 수 없습니다.” 오류가 표시되지 않음
- [ ] 관리자 홈의 모든 카드/버튼이 정상 route로 이동함
- [ ] 발송 실패 카드를 누르면 실패 상세 목록으로 이동함
- [ ] 실패 상세 목록에서 오류 코드와 조치 안내를 확인할 수 있음
- [ ] 발송 대기 카드를 누르면 대기 상세 목록으로 이동함
- [ ] 대기 상세 목록에서 다음 시도 시각과 대기 사유를 확인할 수 있음
- [ ] 진행 중 에스컬레이션 카드에서 L0/L1/L2/L3 breakdown을 확인할 수 있음
- [ ] 진행 중 에스컬레이션 상세에서 프로젝트/업무/담당자/due_date/current_level을 확인할 수 있음
- [ ] 관리자 표 header와 데이터 정렬이 맞음
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
- [ ] 부서 추가 시 잘못된 칸 아래에 상세 오류 사유 표시
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
