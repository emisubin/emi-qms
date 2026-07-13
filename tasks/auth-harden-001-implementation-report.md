# TASK-AUTH-HARDEN-001 Implementation Report

## 1. 목적과 배경

서로 다른 administrator target의 동시 감소 요청이 마지막 두 명을 각각 제거해 active count 0을 commit할 수 있는 check-then-write race를 PostgreSQL transaction serialization으로 보정한다.

## 2. 기준선

구현 시작 시 main과 origin/main은 `1142e0bfaee2ddf7837e3115e1748c1ea247e654`로 일치했다. 동일 목적 branch/worktree/PR은 0이었다. Persistent UAT ledger 28/29/1, canonical active Entra System Administrator 1, Pending/Processing 0/0, PostgreSQL restart 0이었고 Development 5174/5081과 Review-safe 5190/5092는 정상 상태였다.

## 3. Canonical active-admin predicate

공통 guard는 active EntraId user 중 deletion requested, scheduled hard delete와 purge blocked timestamp가 모두 null이고 canonical System Administrator role assignment가 존재하는 사용자를 count한다. `EXISTS`로 한 user당 한 번만 count한다. Dev persona, 승인 대기 user와 `pre_delete_is_active`는 제외한다.

## 4. 전체 감소 mutation 경로

| 경로 | 기존 transaction | 보정 |
| --- | --- | --- |
| 사용자 비활성화 | 있음 | 공통 guard |
| role 전체 교체/System Administrator 제거 | 있음 | 공통 guard |
| 삭제 예약 | 있음 | 공통 guard |
| Bulk delete | 항목별 기존 경로 재사용 | schedule/purge guard 재사용 |
| 즉시 purge | 있음 | 방어적 guard |
| background due purge | batch transaction | 방어적 guard, 거부 시 전체 rollback |

Restore, user 활성화, role 추가, JIT와 bootstrap은 count 증가 경로다. 감소 guard 대상은 아니며 lock-order 경쟁 test를 수행했다.

## 5. 기존 check-then-write race

기존 transaction은 target user row만 `FOR UPDATE`로 잠갔다. 서로 다른 두 target은 병렬로 다른 administrator를 count해 둘 다 안전하다고 판정하고 모두 감소할 수 있었다. Application pre-check와 UI 차단만으로는 commit invariant를 증명할 수 없었다.

## 6. Red-before-green 증빙

수정 전 task-owned isolated PostgreSQL 재현에서 서로 다른 admin 비활성화, 두 role 제거, 혼합 감소, 삭제 예약/role 제거와 반복 stress를 실행했다. Invariant violation은 총 35건이었고 partial update, deadlock과 serialization failure는 0이었다. 이 결과로 `CHECK_THEN_WRITE_RACE`와 `PER_TARGET_LOCK_ONLY`를 directly observed/high confidence로 확정했다.

## 7. Singleton role-row lock 선택

기존 canonical `system-administrator` role row를 transaction-scoped singleton lock으로 재사용한다. 별도 table, advisory lock key, SERIALIZABLE retry와 migration이 필요 없고 모든 감소 경로가 이미 참조하는 업무 singleton이다.

## 8. Lock ordering과 transaction boundary

순서는 target user row → canonical role row `FOR UPDATE` → lock 후 target predicate 재확인 → 다른 canonical active administrator count → mutation이다. 같은 Npgsql connection과 transaction을 사용하며 nested transaction이나 별도 connection은 만들지 않는다. Due purge 대상은 stable ID ordering을 사용한다.

증가 경로는 target row 처리 뒤 role FK/insert를 수행하므로 반대 방향의 명시적 role-first lock이 없다. 증가/감소 경쟁 test에서 unexpected deadlock은 0이었다.

## 9. 공통 guard

`ActiveSystemAdministratorInvariantGuard`가 다음을 담당한다.

- target이 감소 대상인지 사전 판정
- canonical role row 정확히 1건 lock
- lock 후 target 상태·role membership 재검증
- target을 제외한 canonical count 재계산
- not-applicable/allowed/rejected fixed result 반환

Role row가 없거나 중복이면 last-admin domain failure가 아니라 구성·무결성 오류를 발생시킨다. 사용자 ID, email과 role assignment ID를 log하지 않는다.

## 10. Purge 방어

정상 scheduled row는 inactive/deletion state이므로 canonical predicate 밖이며 guard가 no-op이다. 비정상 canonical active administrator가 purge 경로에 도달하면 즉시 purge는 domain failure로 거부하고 due purge는 기존 batch transaction을 rollback한다. Purge 일정과 복구 정책은 변경하지 않았다.

## 11. 오류 정규화와 API 계약

비활성화, role 제거와 삭제의 last-admin rejection은 공통 한글 domain message를 사용한다. Endpoint는 기존 HTTP 400과 `{message}` shape를 유지한다. Lock cancellation과 DB 장애는 domain error로 오인하지 않고 기존 server error 경계를 따른다. 새 공개 error code는 없다.

## 12. 동시성·failure 결과

| 검증 | 결과 |
| --- | --- |
| 서로 다른 두 admin 동시 비활성화 | 성공 1, 거부 1, final active 1 |
| 서로 다른 두 role 제거 | 성공 1, 거부 1, final active 1 |
| 비활성화 + role 제거 | 성공 1, 거부 1, final active 1 |
| 삭제 예약 + role 제거 | 성공 1, 거부 1, final active 1 |
| 동일 target 중복 | 기존 idempotent success, final active 1 |
| 신규 admin 활성화 + 기존 제거 | 모든 commit 뒤 active 1 이상 |
| lock cancellation | cancellation 전파, partial 0 |
| transaction 중간 failure | rollback, active count 2 유지 |
| 20회 순서 반전 stress | invariant violation 0, unexpected deadlock 0 |

Committed active count는 모든 성공 commit 뒤 1 이상이었다. Retry나 SERIALIZABLE transaction은 새로 도입하지 않았다.

## 13. Query·성능

Role lookup은 기존 unique `roles.code`, assignment lookup은 `user_roles` primary key와 role index를 사용한다. Canonical count는 낮은 빈도의 administrator 감소 transaction에만 실행된다. Read-only API와 일반 user update 중 감소하지 않는 요청은 singleton lock을 사용하지 않는다. Isolated lock waiter 검증에서 서로 다른 두 target이 같은 role row boundary에 직렬화됐고 index/schema 변경 필요성은 발견되지 않았다.

## 14. 기존 정책 회귀

Microsoft Entra 인증, 승인 대기, Dev persona read-only, bootstrap, restore, 일반 authorization과 deletion lifecycle을 변경하지 않았다. HTTP status와 response shape도 유지했다.

## 15. Migration·API·UI 영향

- migrationRequired: false
- schemaChangeRequired: false
- APIContractChangeRequired: false
- frontendChangeRequired: false
- newErrorCodeRequired: false
- runtimeConfigRequired: false
- controlledUatRequired: true

Backend transaction/store만 변경했다. Frontend, Excel, PDF, 첨부파일, notification workflow와 기존 업무 화면 회귀 영향은 N/A이며 해당 source diff가 0이다.

## 16. 자동 테스트

- Backend Release build: warning/error 0/0
- Last-admin/identity/API targeted: 15/15
- Backend 전체: 356/356
- Frontend lint: error 0, 기존 warning 1
- Frontend typecheck: 성공
- Frontend unit: 61/61
- Frontend build: 성공, 기존 chunk-size warning 유지
- Mock UI: 1/1
- Full-Stack E2E isolated DB: 16/16
- actionlint: 성공
- `git diff --check`: 성공

추가 `dotnet format --verify-no-changes`에서는 이번 Task changed file 위반 0을 확인했다. Repository baseline의 범위 밖 import-order 위반 9건 때문에 명령 전체 exit는 2였으며 이 Task에서 unrelated 파일을 수정하지 않았다. Controlled UAT의 실제 Entra user 동시 mutation은 미실행이며 사용자·data 변경 승인이 별도로 필요하다.

## 17. Persistent UAT·runtime 보호

Persistent UAT에는 read-only aggregate만 수행했다. 사용자·role·deletion write와 provider call은 0이다. Ledger 28/29/1, Pending/Processing 0/0과 PostgreSQL restart 0을 유지했고 Development·Review-safe listener/PID는 변경하지 않았다.

## 18. 보안·PII

테스트는 isolated synthetic user만 사용했다. Tracked 문서와 출력에 실제 사용자, email/UPN, role assignment ID, raw DB/API body와 credential을 포함하지 않았다. 게시 전 secret/PII·generated artifact scan을 수행한다.

## 19. Rollback

공통 guard, UserAdministrationStore와 AdminScheduledDeletionService 변경을 함께 revert한다. Schema/data rollback은 없다. Controlled UAT 이상 시 runtime을 교체하지 않고 mutation을 중단한 뒤 별도 결정을 받는다.

## 20. 제한사항

지원되는 application mutation 경로만 보호한다. DBA direct SQL을 DB constraint/trigger로 막지 않는다. Direct SQL user/role 변경은 운영 금지다. Lock timeout은 환경 설정을 새로 추가하지 않았고 cancellation path로 rollback 경계를 검증했다.

## 21. 해결한 업무 문제

두 관리자가 서로를 동시에 비활성화하거나 역할 제거·삭제해 관리 기능을 사용할 administrator가 0명이 되는 장애를 commit boundary에서 방지한다.

## 22. 기술적 결정과 검토한 대안

- PostgreSQL advisory lock: key 관리와 관측성이 role row보다 약해 폐기
- singleton invariant table: migration과 별도 lifecycle이 필요해 폐기
- SERIALIZABLE + retry: 전체 transaction retry와 오류 정책이 과도해 폐기
- table lock: 일반 user mutation 영향 범위가 커 폐기
- process-local mutex: multi-instance를 보호하지 못해 폐기

기존 canonical role row lock이 schema 변경 없이 서로 다른 target을 직렬화하는 최소안이다.

## 23. 시행착오 및 폐기한 접근

Target row lock과 pre-count만 유지하는 접근은 서로 다른 target race를 해결하지 못했다. 단순 sleep 기반 test 대신 coordinator transaction이 role row를 보유하고 PostgreSQL lock waiter를 확인하는 결정적 barrier를 사용했다. Test compile에서 namespace import와 xUnit collection assertion convention 2건을 바로잡았다.

## 24. 사용자 검수 결과와 남은 항목

Checklist, 자동 검증과 사용자 검수를 완료했고 PR #36 squash merge 승인을 확인했다. 미체크 항목은 0이다. 사용자는 서로 다른 target 경쟁의 성공 1·거부 1·active count 1, rollback, 기존 HTTP 400·Entra 정책, Migration/API/Frontend 변경 0, direct SQL 금지 제한과 기존 범위 밖 import-order 위반 9건을 승인했다. Persistent UAT 적용은 별도 `TASK-UAT-AUTH-HARDEN-001` 승인 대상이며 신규 기능 개발 No-Go를 유지한다.

## 25. 주요 파일

- `backend/src/Emi.Qms.Api/Identity/ActiveSystemAdministratorInvariantGuard.cs`
- `backend/src/Emi.Qms.Api/Identity/UserAdministrationStore.cs`
- `backend/src/Emi.Qms.Api/Admin/AdminScheduledDeletionService.cs`
- `backend/tests/Emi.Qms.Api.Tests/IdentityInfrastructureTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/ProjectRegistrationApiTests.cs`

## 26. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Task·검수 checklist | `tasks/auth-harden-001.md` | 작성 완료 |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | `tasks/auth-harden-001-sop.md` | 작성 완료 |
| User manual | `tasks/auth-harden-001-user-manual.md` | 작성 완료 |
| Roadmap | `docs/00-product-roadmap.md` | 반영 완료 |
