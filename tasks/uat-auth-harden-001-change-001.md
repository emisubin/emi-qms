# TASK-UAT-AUTH-HARDEN-001 Change 001 — Purge guard predicate 정합성

## 0. 상태

- Task 유형: `P2_REMEDIATION` + `POLICY_DECISION`
- Finding: `PURGE_GUARD_PREDICATE_UNREACHABLE`
- Finding 심각도: P2
- 조사 판정: `DIRECTLY_OBSERVED`
- Confidence: `HIGH`
- Fable 5 호출: false
- 사용자 승인 상태: true
- 구현 승인 상태: true
- 사용자 검수 완료: true
- Merge 승인 상태: true

사용자는 REDESIGN, malformed lifecycle defense-in-depth와 due purge 방어 거부 시 batch 전체 rollback을 승인했다. 이 문서는 승인된 구현 지시 계약이다.

## 1. 증상

TASK-AUTH-HARDEN-001 문서와 controlled-UAT 검증 계획은 비정상적인 active System Administrator가 immediate 또는 due purge에 도달하면 기존 last-admin guard가 거부한다고 설명한다. 실제 production selection 경로에서는 purge 대상 lifecycle과 canonical active-admin predicate가 상호 배타적이므로 해당 거부 결과에 도달할 수 없다.

## 2. 확인된 코드 사실

### 2.1 Canonical active-admin predicate

`ActiveSystemAdministratorInvariantGuard.CheckRemovalAsync`는 target이 다음 조건을 모두 만족할 때만 canonical role row를 잠그고 다른 administrator 수를 계산한다.

- active EntraId user
- deletion requested 없음
- scheduled hard deletion 없음
- purge blocked 없음
- canonical System Administrator role assignment 존재

위 조건을 만족하지 않으면 role lock 전에 `NotApplicable`을 반환한다.

### 2.2 삭제 예약의 authoritative boundary

`UserAdministrationStore.ScheduleEntraUserDeletionAsync`는 target user row를 잠근 transaction 안에서 공통 last-admin guard를 실행한다. 허용된 경우에만 같은 transaction에서 user를 inactive로 바꾸고 deletion requested와 scheduled hard deletion을 설정한다.

`UpdateEntraUserAsync`의 비활성화와 role 전체 교체도 같은 guard를 사용한다. Bulk delete는 lifecycle이 없는 user에 대해 삭제 예약 경로를 재사용한다.

### 2.3 Immediate purge selection

`AdminScheduledDeletionService.PurgeUserNowAsync`는 deletion requested 또는 purge blocked marker가 있는 row만 `FOR UPDATE`로 읽는다. 따라서 읽힌 target은 canonical predicate의 lifecycle-null 조건을 만족하지 못하며 현재 `CheckRemovalAsync`는 항상 `NotApplicable`이다.

### 2.4 Due purge selection

`PurgeDueAsync`의 user query는 deletion requested가 있고 scheduled hard deletion이 due인 row만 stable ID 순서로 잠근다. 모든 due target이 canonical predicate 밖이므로 현재 guard의 `Rejected`와 그에 따른 batch rollback 분기는 도달 불가능하다.

### 2.5 지원 mutation 경로 감사

- 사용자 비활성화·role 제거: `UpdateEntraUserAsync`의 공통 guard 사용
- 삭제 예약: `ScheduleEntraUserDeletionAsync`의 공통 guard 사용
- Bulk delete: 삭제 예약 또는 lifecycle row의 immediate purge 재사용
- Restore: lifecycle marker를 지우고 active 상태를 복구하는 증가 경로
- JIT/bootstrap role assignment: 증가 경로
- Background purge: due lifecycle row만 선택

삭제 예약 외의 지원 감소 mutation이 canonical invariant를 우회하는 경로는 발견되지 않았다. Direct SQL과 손상된 외부 데이터 주입은 기존 application 보호 범위 밖이며 운영상 금지된다.

## 3. Root cause

`CheckRemovalAsync`의 질문은 “target이 현재 canonical active administrator인가?”다. Purge가 필요한 질문은 “lifecycle marker가 이미 있는 target이라도 물리 삭제 직전에 방어해야 할 active Entra administrator인가?”다. 서로 다른 질문에 동일 predicate를 재사용하면서 dead branch와 검증 불가능한 문서 계약이 생겼다.

Root cause 분류:

- `PREDICATE_SCOPE_MISMATCH`
- `UNREACHABLE_DEFENSIVE_BRANCH`
- `DOCUMENTATION_OVERSTATES_PURGE_GUARD`

## 4. REDESIGN

기존 canonical predicate와 삭제 예약 guard를 변경하지 않고 물리 삭제 전용 방어 predicate를 별도로 둔다.

권장 계약:

1. Purge target row를 현재 순서대로 먼저 잠근다.
2. Target이 active EntraId user이며 canonical System Administrator role assignment를 갖는지 확인한다.
3. Purge 전용 target 판정에서는 deletion requested, scheduled hard deletion과 purge blocked marker를 의도적으로 제외한다.
4. 해당 target이면 canonical role row를 잠근다.
5. Lock 뒤 target의 active/provider/role 상태를 다시 읽는다.
6. Target을 제외한 기존 canonical active administrator 수를 계산한다.
7. 다른 canonical administrator가 0이면 기존 last-admin domain error로 거부한다.
8. Immediate purge는 기존 HTTP 400 shape를 유지한다.
9. Due purge는 현재 계약대로 exception을 통해 batch transaction 전체를 rollback한다.
10. 정상 scheduled row는 inactive이므로 purge 전용 guard가 `NotApplicable`이고 기존 purge를 계속 수행한다.

Guard가 허용하거나 `NotApplicable`을 반환하면 기존 reference scan으로 계속 진행한다. 현재 System Administrator role assignment는 user reference이므로 해당 row는 기존 정책에 따라 `PurgeBlocked`가 될 수 있다.

이 guard는 이미 손상된 canonical count를 복구하지 않는다. Lifecycle marker와 active administrator 상태가 함께 존재하는 비정상 row를 last-admin domain error로 먼저 차단해 복구 가능성을 보존하는 defense-in-depth다.

## 5. POLICY_CORRECTION

삭제 예약을 canonical invariant의 유일한 authoritative 감소 boundary로 확정하고 purge의 last-admin rejection 계약을 제거한다.

수정 방향:

1. Immediate/due purge의 현재 unreachable guard 호출을 제거한다.
2. 문서에서 purge rejection과 role-row serialization 주장을 제거한다.
3. Purge는 이미 inactive·scheduled 상태가 된 row의 physical cleanup 단계로만 정의한다.
4. Corrupt lifecycle row와 Direct SQL은 application invariant 범위 밖의 data-integrity incident로 분류한다.
5. 테스트는 삭제 예약 거부, 정상 lifecycle 전환, purge transaction rollback과 aggregate 불변만 검증한다.

이 대안은 코드와 문서의 정합성을 가장 작게 회복하지만, last-admin 안전이 generic reference scan에 간접 의존한다. Reference 정책이 바뀌어도 유지되는 명시적 purge invariant가 없다는 잔여 위험을 수용해야 한다.

## 6. 대안별 불변조건·위험·테스트 영향

| 항목 | REDESIGN | POLICY_CORRECTION |
| --- | --- | --- |
| Canonical predicate | 변경 없음 | 변경 없음 |
| 삭제 예약 authoritative boundary | 유지 | 유일한 purge 선행 boundary로 명시 |
| 비정상 active-admin purge | 다른 canonical admin이 없으면 domain 차단 | generic reference scan에 간접 의존 |
| 정상 scheduled purge | 유지 | 유지 |
| Immediate HTTP contract | 기존 400 shape 유지 | active row는 lifecycle filter로 purge 대상 아님 |
| Due batch contract | 방어 거부 시 전체 rollback | DB failure 시 rollback만 보장 |
| Concurrent role-row serialization | 비정상 active-admin purge에서 유지 | purge에서는 제거 |
| Predicate 복잡도 | purge 전용 predicate 추가 | dead guard 제거로 감소 |
| 문서 정정 폭 | predicate와 defense-in-depth 의미 정정 | purge guard 주장을 삭제 |
| 테스트 영향 | malformed-state guard·concurrency test 추가 | lifecycle boundary·rollback test 중심 |
| Migration/schema | 없음 | 없음 |
| Runtime/API/Frontend | 변경 없음 | 변경 없음 |

공통 불변조건:

- 지원되는 모든 canonical 감소 mutation은 기존 transaction guard를 유지한다.
- 모든 성공 commit 뒤 canonical active administrator 수는 1 이상이어야 한다.
- 정상 scheduled row purge와 기존 참조 차단 정책은 회귀하지 않는다.
- Cancellation·DB failure를 last-admin domain error로 오인하지 않는다.
- Direct SQL은 보호 범위 밖이며 운영상 금지한다.

## 7. 권장안

`REDESIGN`을 권장한다.

근거:

- 사용자 승인된 TASK-AUTH-HARDEN-001의 “purge 방어” 의도를 유지한다.
- 기존 canonical predicate, public API와 runtime configuration을 변경하지 않는다.
- Schema·migration 없이 두 predicate의 질문을 명확히 분리할 수 있다.
- 정상 scheduled row는 추가 last-admin 차단 없이 기존 reference 정책으로 계속 진행된다.
- 비정상 lifecycle state에서 마지막 복구 가능 administrator의 물리 삭제를 fail-closed한다.
- 문서만 축소하는 것보다 기존 P2 safety contract를 보존한다.

Due purge의 방어 거부는 기존 문서 계약과 transaction 구조를 따라 batch 전체 rollback을 유지하는 것을 함께 권장한다.

## 8. 포함 범위

- Purge 전용 defensive predicate와 공통 role-lock/count helper 정리
- Immediate purge의 reachable defensive rejection
- Due purge의 reachable defensive rejection과 batch rollback
- 정상 scheduled immediate/due purge 회귀
- Malformed active-admin lifecycle fixture의 isolated PostgreSQL 검증
- Purge와 role 제거의 deterministic concurrency 검증
- TASK-AUTH-HARDEN-001 문서와 Roadmap의 정확한 표현 정정
- 변경 후 TASK-UAT-AUTH-HARDEN-001 Phase A/B 재검증 계획 갱신

## 9. 제외 범위

- Canonical active-admin predicate 변경
- 삭제 예약, restore, retention과 purge 일정 정책 변경
- Schema, migration와 index 변경
- Public API response shape와 frontend 변경
- Runtime configuration과 worker enable 정책 변경
- Persistent UAT user/role/deletion mutation
- Direct SQL 차단용 trigger/constraint
- Backup restore와 runtime handover
- Commit, push, PR과 merge

## 10. 영향 파일

REDESIGN 승인 시 예상 파일:

- `backend/src/Emi.Qms.Api/Identity/ActiveSystemAdministratorInvariantGuard.cs`
- `backend/src/Emi.Qms.Api/Admin/AdminScheduledDeletionService.cs`
- `backend/tests/Emi.Qms.Api.Tests/IdentityInfrastructureTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/ProjectRegistrationApiTests.cs`
- `tasks/auth-harden-001.md`
- `tasks/auth-harden-001-implementation-report.md`
- `tasks/auth-harden-001-sop.md`
- `tasks/auth-harden-001-user-manual.md`
- `docs/00-product-roadmap.md`
- 이 change 문서

`UserAdministrationStore`, migration, frontend, dependency, lockfile와 script 변경은 예상하지 않는다.

POLICY_CORRECTION 승인 시 guard source는 변경하지 않고 deletion service의 unreachable 호출, 관련 tests와 위 문서 표현만 정리한다.

## 11. 검증 계획

공통:

- 기존 유일 administrator 비활성화·role 제거·삭제 예약 거부
- 서로 다른 administrator 감소 경쟁과 20회 stress
- 기존 HTTP 400 단일 message shape
- 정상 scheduled row immediate purge
- 정상 due purge와 stable ordering
- Immediate/due purge transaction failure rollback
- Cancellation·lock failure 정규화 회귀
- Backend Release build와 전체 tests
- Migration regression
- Frontend 영향 0 확인과 표준 전체 validation
- `git diff --check`, Markdown link/heading, secret/PII scan
- Persistent UAT read-only before/after 불변
- Development·Review-safe와 PostgreSQL 불변

REDESIGN 추가:

- Lifecycle marker가 있으면서 active Entra administrator role을 가진 malformed fixture
- 다른 canonical administrator 0일 때 immediate purge HTTP 400과 row delta 0
- 다른 canonical administrator 0일 때 due purge batch 전체 rollback
- 다른 canonical administrator 1명 이상일 때 guard 허용 후 기존 `PurgeBlocked` 정책으로 진행하는지 검증
- Due purge와 다른 administrator role 제거 경쟁의 role-row serialization
- 정상 inactive scheduled administrator는 방어 predicate `NotApplicable`

POLICY_CORRECTION 추가:

- Active non-lifecycle user가 immediate/due selection에 포함되지 않음
- 삭제 예약 commit 뒤 target이 canonical set 밖이고 inactive임
- Purge에서는 last-admin rejection을 기대하지 않음
- Corrupt-state risk acceptance와 운영 대응 문서화

## 12. 기존 문서에서 정정할 표현

REDESIGN 선택 시:

- “canonical active administrator가 purge 경로에 도달”을 “lifecycle marker와 active administrator 상태가 비정상 공존하는 target”으로 변경한다.
- Purge 전용 predicate가 canonical predicate와 다름을 명시한다.
- Purge guard는 invariant 복구가 아니라 physical deletion 방어임을 명시한다.
- Existing purge 검증 결과가 없었던 항목은 구현 후 실제 실행 결과로 교체한다.

POLICY_CORRECTION 선택 시:

- Task의 immediate/due purge 방어 포함 범위를 삭제 예약 authoritative boundary 설명으로 바꾼다.
- Implementation report의 defensive guard와 due rejection rollback 주장을 제거한다.
- SOP의 active-admin purge rejection과 due guard rejection 절차를 data-integrity incident 대응으로 바꾼다.
- User manual의 “즉시·자동 purge 방어” 표현을 삭제 예약 단계 보호로 바꾼다.
- Roadmap의 purge 방어 표현을 실제 지원 mutation boundary에 맞춘다.

어느 대안이든 확인하지 않은 purge guard 결과를 과거 완료 증빙으로 유지하지 않는다.

## 13. Persistent UAT·Runtime 영향

- 이번 조사와 change 초안 작성의 Persistent UAT write: 0
- 이번 조사와 change 초안 작성의 runtime restart: 0
- 구현 예상 migration/schema 영향: 없음
- 구현 예상 public API/frontend 영향: 없음
- 구현 후 Persistent live user/role mutation: 별도 승인 없이는 금지
- 구현 후 controlled UAT: isolated PostgreSQL 우선, Persistent runtime 적용은 별도 승인

## 14. 사용자 승인 상태

- `userApproval: true`
- `implementationApproved: true`
- `selectedAlternative: REDESIGN`
- `recommendedAlternative: REDESIGN`
- `selectedDuePurgeFailurePolicy: ROLLBACK_ENTIRE_BATCH`
- `malformedLifecyclePolicy: DEFENSE_IN_DEPTH`

승인된 사용자 결정:

1. `REDESIGN` 선택
2. Due purge 방어 거부의 batch 전체 rollback 유지
3. Malformed lifecycle state를 defense-in-depth 대상으로 유지

게시, Persistent UAT mutation과 runtime handover는 별도 승인 전 수행하지 않는다.

## 15. 구현 결과

- Purge 전용 `CheckPurgeRemovalAsync` predicate 추가
- Immediate와 due purge가 purge 전용 predicate를 사용
- Target row → canonical role row → 재검산 순서 유지
- Immediate last-admin rejection과 기존 HTTP 400 shape 확인
- Due purge rejection이 앞선 delete를 포함한 batch 전체 rollback함을 확인
- 다른 canonical administrator가 있으면 guard 통과 후 기존 reference 정책의 `PurgeBlocked`로 진행함을 확인
- Due purge와 role 제거가 동일 canonical role row에서 직렬화됨을 확인
- Purge role-lock cancellation에서 target과 assignment가 보존됨을 확인
- Targeted PostgreSQL tests: 5/5
- Backend Release build: warning/error 0/0
- Backend 전체 tests: 361/361
- Frontend lint/typecheck/unit/build: error 0 / 성공 / 61/61 / 성공
- Mock UI와 isolated Full-Stack E2E: 1/1, 16/16
- actionlint, `git diff --check`, Markdown link·anchor·heading, secret/PII와 changed-file allowlist: 통과
- Persistent UAT read-only 전후: ledger 28/29/1, canonical active administrator 1, Pending/Processing 0/0, Development·Review-safe·PostgreSQL 불변
- Runtime·migration·API·frontend configuration 변경: 0
- 게시 작업: 0

## 16. 사용자 검수 결과

- purge 전용 predicate와 malformed lifecycle defense-in-depth: 승인
- due purge 방어 거부의 전체 batch rollback: 승인
- 기존 generic reference 정책의 `PurgeBlocked` 가능성: 확인
- Migration·API response shape·Frontend·runtime configuration 변경 0: 확인
- Commit·Push·PR·Merge: 승인
