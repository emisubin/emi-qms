# TASK-ADMIN-001 구현 보고서

## 1. 목적

TASK-ADMIN-001은 시스템 관리 중심의 관리자 페이지를 제공하기 위한 작업이다. 관리자는 사용자, 부서, 휴일, 권한 조회, 기준정보 변경 이력, 업무 시작/완료 이력, 알림/에스컬레이션 상태를 한 곳에서 확인하거나 관리한다.

이번 TASK의 핵심은 각 부서가 업무 중 입력하는 기준정보를 관리자 페이지에 과도하게 통합하지 않고, 시스템 운영에 필요한 관리 기능만 안정적으로 제공하는 것이다.

## 2. 구현 범위

- 관리자 홈 대시보드
- 사용자 관리 재사용 및 삭제 lifecycle 확장
- 부서 관리
- Calendar-001 휴일 관리 재사용 및 삭제 lifecycle 확장
- 권한 매트릭스 read-only 조회
- 기준정보 변경 이력 조회
- `work_items` timestamp 기반 업무 시작/완료 이력 조회
- `notification_deliveries` 알림 발송 상태 조회
- `work_item_escalations` 에스컬레이션 상태 조회
- 사용자/부서/휴일 삭제 예정, 복구, 일괄 삭제, 일괄 복구, 즉시 삭제 시도, 삭제 보류
- 부서 code/name/sortOrder field-level validation

## 3. 제외 범위

- Item 관리
- 포장방식 관리
- 포장방식 `size_required` 관리
- 생산계획 단계 관리
- 구매 필수 항목 관리
- 권한 매트릭스 편집
- role/permission master 편집
- Pending 유형 관리
- 검사/제조 체크리스트 템플릿
- 발송 실패 수동 재처리 UI
- due_date 정책 관리
- Teams Activity Feed actual
- 전체 field-level audit 개편

## 4. DB/Migration

신규 migration은 `0020`, `0021`, `0022` 세 개다. 기존 main 반영 migration `0001~0019`는 수정하지 않았다.

- `0020_admin_master_data_management.sql`
  - `admin-history.read` permission seed
  - System Administrator에 `admin-history.read` 부여
  - `departments` active/sort/update/deletion lifecycle 컬럼 확장
  - `qms_users` deletion lifecycle 컬럼 확장
  - `system_holidays` deletion lifecycle 컬럼 확장
  - `admin_master_change_logs` 생성
  - purge 조회 index 추가
- `0021_admin_deletion_lifecycle_patch.sql`
  - UAT schema drift 보정용 patch migration
  - 사용자/부서/휴일 deletion lifecycle 컬럼과 index를 `if not exists`로 보강
- `0022_admin_deletion_restore_bulk_actions.sql`
  - 사용자/부서/휴일 `pre_delete_is_active` 추가
  - 복구 시 삭제 예약 전 활성 상태를 되돌릴 수 있게 함
  - 삭제/복구 조회 index 추가

## 5. Backend 주요 파일

- `backend/src/Emi.Qms.Api/Admin/*`
  - 관리자 dashboard, 부서 관리, 권한 매트릭스, 변경 이력, 업무 이력, purge worker/service
- `backend/src/Emi.Qms.Api/Identity/*`
  - 사용자 deletion lifecycle, restore, purge, bulk action, 마지막 System Administrator/Dev user 보호
- `backend/src/Emi.Qms.Api/Calendar/*`
  - 휴일 deletion lifecycle, restore, purge, bulk action, business-days 반영
- `backend/src/Emi.Qms.Api/Authorization/*`
  - `admin-history.read` policy
- `backend/src/Emi.Qms.Api/Program.cs`
  - Admin endpoint 및 purge worker 등록
- `backend/tests/Emi.Qms.Api.Tests/*`
  - admin, identity, calendar, migration, authorization regression tests

## 6. Frontend 주요 파일

- `frontend/src/App.tsx`
  - 관리자 홈, 사용자/부서/휴일 관리, checkbox/bulk action, lifecycle badge, 권한 매트릭스, 이력/모니터 화면
- `frontend/src/api.ts`
  - admin restore/purge/bulk endpoint client
- `frontend/src/identity.ts`
  - 사용자 lifecycle field type
- `frontend/src/projects.ts`
  - 부서/휴일 lifecycle field type
- `frontend/src/styles.css`
  - lifecycle badge, bulk action, 관리자 table/card layout
- `frontend/tests/App.test.tsx`
  - admin route, removed menu, validation, lifecycle UI regression

## 7. 삭제 lifecycle

상태는 API와 UI에서 동일하게 계산한다.

- 활성: `is_active=true`, deletion/purge 필드 없음
- 비활성: `is_active=false`, deletion/purge 필드 없음
- 삭제 예정: `deletion_requested_at_utc`와 `scheduled_hard_delete_at_utc`가 있음
- 삭제 보류: `purge_blocked_at_utc`가 있음

삭제 버튼은 즉시 hard delete하지 않고 삭제 예정 상태로 전환한다. 삭제 예정 상태에서 다시 삭제하면 즉시 완전 삭제를 시도한다. 참조 데이터가 있으면 cascade delete하지 않고 삭제 보류로 남긴다.

`AdminDeletionPurgeWorker`는 1시간 주기로 `scheduled_hard_delete_at_utc <= now`인 대상을 처리하며, API의 즉시 삭제도 같은 `AdminScheduledDeletionService` purge 로직을 재사용한다.

## 8. 일괄 action

사용자/부서/휴일 목록은 checkbox 기반 일괄 action을 제공한다.

- 선택 삭제
  - 활성/비활성: 삭제 예정 전환
  - 삭제 예정/삭제 보류: 즉시 삭제 시도
- 선택 복구
  - 삭제 예정/삭제 보류: 복구
  - 활성/비활성: skipped/no-op

bulk API는 개별 성공/실패/건너뜀 결과를 반환한다. 일부 항목 실패가 전체 작업 rollback으로 이어지지 않게 했다.

## 9. 권한 정책

- 사용자/부서/휴일 관리는 기존 관리자 사용자 관리 권한을 재사용한다.
- 조회성 관리자 화면은 `admin-history.read`를 사용한다.
- System Administrator는 관리자 페이지에 접근 가능하다.
- non-admin은 관리자 API와 화면 접근이 차단된다.
- 업무 입력 action 권한을 System Administrator 권한으로 새로 우회하지 않았다.
- 권한 매트릭스는 read-only이며 role/permission 편집 기능은 없다.

## 10. Validation

부서 추가/수정은 field-level validation 오류를 반환한다.

- `code`: 필수, 2~50자, 영문 대문자/숫자/하이픈/언더스코어만 허용, 중복 금지
- `name`: 필수, 100자 이하, 한글/영문/숫자/공백/괄호/하이픈 허용
- `sortOrder`: 0 이상 9999 이하

프론트엔드는 각 input 아래에 한글 오류를 표시한다. 삭제/복구/purge 후에는 목록에 머무르며 refresh하고, “대상을 찾을 수 없습니다.” 오류로 잘못 이동하지 않게 했다.

## 10-1. 관리자 모니터 추적성

관리자 홈의 알림/에스컬레이션 카드는 count만 표시하지 않고 상세 목록으로 연결한다.

- 발송 실패: `/admin/system/notification-deliveries?status=Failed`
- 발송 대기: `/admin/system/notification-deliveries?status=Pending`
- 진행 중 에스컬레이션: `/admin/system/work-item-escalations?status=Active`
- L0/L1/L2/L3 breakdown: `/admin/system/work-item-escalations?status=Active&level=L0` 같은 단계별 필터

알림 발송 상태 화면은 실패/대기 상태의 의미와 관리자 조치 안내를 표시한다. 발송 실패 수동 재처리 버튼은 이번 범위에 포함하지 않았다.

에스컬레이션 상태 화면은 active escalation을 “예정일 임박 또는 초과 후 아직 완료/취소되지 않은 업무”로 정의하고, L0 예정일 임박과 L1~L3 초과 단계를 구분해서 표시한다.

관리자 화면 전반의 표는 공통 table alignment CSS를 사용한다. 텍스트는 좌측, 상태/날짜/checkbox는 중앙, 숫자/작업 영역은 화면별 일관된 정렬을 적용한다.

## 11. 사용자 검수 체크리스트

- [ ] `/admin` 관리자 홈 정상 표시
- [ ] “대상을 찾을 수 없습니다.” 오류 없음
- [ ] 관리자 홈 모든 카드/버튼 정상 이동
- [ ] 발송 실패 카드에서 실패 건수 확인 가능
- [ ] 발송 실패 카드를 누르면 실패 상세 목록으로 이동
- [ ] 실패 상세 목록에서 어떤 알림이 실패했는지 확인 가능
- [ ] 실패 상세 목록에서 오류 코드/조치 안내 확인 가능
- [ ] 발송 대기 카드에서 대기 건수 확인 가능
- [ ] 발송 대기 카드를 누르면 대기 상세 목록으로 이동
- [ ] 대기 상세 목록에서 어떤 알림이 대기 중인지 확인 가능
- [ ] 대기 상세 목록에서 다음 시도 시각/대기 사유 확인 가능
- [ ] 진행 중 에스컬레이션 카드에서 L0/L1/L2/L3 breakdown 확인 가능
- [ ] 진행 중 에스컬레이션 카드를 누르면 상세 목록으로 이동
- [ ] 에스컬레이션 상세에서 프로젝트/업무/담당자/due_date/current_level 확인 가능
- [ ] 에스컬레이션 안내 문구가 표시됨
- [ ] System Administrator가 모든 페이지에 접근 가능
- [ ] non-admin은 관리자 페이지 접근 차단
- [ ] Item/포장방식/생산계획 단계/구매 필수 항목 메뉴 없음
- [ ] 사용자/부서/휴일 목록에 선택 체크박스 표시
- [ ] 전체 선택 체크박스가 동작함
- [ ] 선택 삭제가 동작함
- [ ] 선택 복구가 동작함
- [ ] 사용자 삭제 후 “삭제 예정” 표시
- [ ] 사용자 삭제 후 “완전 삭제 예정일” 표시
- [ ] 삭제 예정 사용자를 복구할 수 있음
- [ ] 삭제 예정 사용자를 다시 삭제하면 즉시 삭제 시도됨
- [ ] 참조 때문에 즉시 삭제가 불가하면 “삭제 보류” 표시
- [ ] 마지막 System Administrator 삭제 시 “마지막 System Administrator는 삭제할 수 없습니다.” 문구 표시
- [ ] Dev user는 삭제 불가/read-only 유지
- [ ] 부서 추가 시 잘못된 칸 아래에 상세 오류 사유 표시
- [ ] 부서 코드 입력 조건이 명확히 표시
- [ ] 부서명 입력 조건이 명확히 표시
- [ ] 정렬 순서 입력 조건이 명확히 표시
- [ ] 부서 삭제 후 “삭제 예정” 표시
- [ ] 부서 복구 가능
- [ ] 부서 즉시 삭제 시 참조가 있으면 삭제 보류 표시
- [ ] 휴일 삭제 후 “삭제 예정” 표시
- [ ] 휴일 복구 가능
- [ ] 휴일 즉시 삭제 가능
- [ ] 삭제 예정 휴일은 business-days 계산에서 제외됨
- [ ] 복구된 휴일은 business-days 계산에 다시 포함됨
- [ ] 비활성/삭제 예정/삭제 보류가 서로 다른 badge/문구로 구분됨
- [ ] 권한 매트릭스 헤더와 데이터 정렬이 맞음
- [ ] 사용자/부서/휴일/변경 이력/업무 이력/알림/에스컬레이션 표 header와 데이터 정렬이 맞음
- [ ] 권한 매트릭스는 읽기 전용임
- [ ] 업무 시작/완료 이력은 조회 전용임
- [ ] 알림 발송 상태는 조회 전용임
- [ ] 에스컬레이션 상태는 조회 전용임
- [ ] 모바일에서 page-level horizontal overflow 없음
- [ ] Console 오류 없음

## 12. Tests

실행 및 통과:

- `git diff --check`
- `actionlint .github/workflows/ci.yml`
- backend Release build
- backend 전체 test
- Admin/Migration/Authorization/Calendar/Holiday/User/Identity targeted tests
- frontend lint/typecheck/unit/build
- mock UI smoke
- Full-Stack E2E
- Docker Compose config
- PostgreSQL health
- UAT health
- UAT admin browser desktop/mobile smoke
- UAT deletion/restore/bulk synthetic smoke
- secret/PII scan

frontend lint는 기존 `react-refresh/only-export-components` warning 1건이 남아 있으나 error는 없다. frontend build는 기존 chunk size warning이 남아 있으나 build는 성공했다.

## 13. UAT 검수 결과

UAT DB는 drop/truncate하지 않았고 Docker volume도 삭제하지 않았다.

- latest migration: `0022_admin_deletion_restore_bulk_actions`
- 사용자/부서/휴일 deletion lifecycle 컬럼과 `pre_delete_is_active` 존재 확인
- synthetic 사용자/부서/휴일로 bulk delete, bulk restore, 즉시 purge smoke 수행
- synthetic 휴일 삭제 후 business-days에서 제외, 복구 후 다시 포함 확인
- `/admin`, 사용자, 부서, 휴일, 권한, 이력, 모니터 화면 desktop/mobile smoke 확인
- target-not-found 문구와 console error 없음 확인

UAT에는 과거 검수에서 남은 synthetic 삭제 예정 데이터가 일부 남아 있다. 운영성 데이터는 삭제하지 않았고, 이번 검수에서 생성한 synthetic 데이터는 purge 가능한 범위에서 즉시 삭제했다.

## 14. 보안/Secret

- `.env`, `.env.entra-local`, `.env.notify-local`은 stage 대상이 아니다.
- Teams manifest/icon 파일은 포함하지 않는다.
- SMTP/Gmail/Teams/Graph secret 원문은 문서와 코드 diff에 포함하지 않는다.
- raw stack trace를 사용자 화면에 노출하지 않는다.

## 15. 후속 TASK 연결

- Item 관리 여부
- 포장방식 기준정보화와 `size_required`
- 생산계획 단계/구매 필수 항목 관리자 통합 여부
- role/permission 편집 UI
- Pending/검사/제조 템플릿
- 발송 실패 수동 재처리 UI
- 전체 field-level audit 확장

## 16. 알려진 제한사항

- role/permission 편집 기능은 없다.
- 사용자/부서 hard delete는 참조가 있으면 삭제 보류된다.
- UAT에는 과거 synthetic 삭제 예정 데이터가 일부 남아 있다.
- 관리자 모바일 UX는 page-level overflow 방지 기준으로 검수했으며, 표 UX 고도화 여지는 남아 있다.

## 17. 운영 적용 전 체크리스트

- System Administrator 계정 접근 검수
- 사용자/부서/휴일 삭제 예정/복구 정책 운영 안내
- 7일 purge worker 운영 주기 확인
- 삭제 보류 데이터 처리 절차 수립
- 권한 매트릭스 read-only 검수
- 기준정보 변경 이력/업무 이력 조회 검수
- UAT synthetic 데이터 정리 필요 여부 결정
