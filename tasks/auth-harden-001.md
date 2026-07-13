# TASK-AUTH-HARDEN-001 — Last active System Administrator concurrency guard

## 1. 목적

서로 다른 관리자에 대한 동시 비활성화·System Administrator 역할 제거·삭제 요청이 실행돼도 모든 성공한 commit 뒤 canonical active System Administrator가 최소 1명 남도록 PostgreSQL transaction 수준에서 보호한다.

## 2. 배경

기존 구현은 target user row만 잠근 뒤 다른 active administrator 수를 검사했다. 서로 다른 target은 서로를 잠그지 않아 각 transaction이 상대를 active로 본 뒤 모두 감소를 commit할 수 있었다.

## 3. Findings

- `CHECK_THEN_WRITE_RACE`: count와 write가 서로 다른 target 요청 사이에서 직렬화되지 않았다.
- `PER_TARGET_LOCK_ONLY`: target user row lock만으로 다른 관리자 요청을 보호할 수 없었다.
- `MISSING_MUTATION_PATH_GUARD`: purge 경로에는 방어적인 last-admin guard가 없었다.
- `PURGE_GUARD_PREDICATE_UNREACHABLE`: canonical lifecycle-null predicate를 purge target에 재사용해 defensive rejection이 도달 불가능했다. Change 001에서 purge 전용 predicate로 보정했다.
- 심각도: P2

## 4. Canonical predicate

Canonical active System Administrator는 다음을 모두 만족한다.

- `qms_users.is_active = true`
- `auth_provider = EntraId`
- deletion requested/scheduled/purge blocked timestamp가 모두 null
- canonical `system-administrator` role assignment 존재

Dev persona, 승인 대기 사용자와 `pre_delete_is_active` 복구 snapshot은 count에서 제외한다. `EXISTS`를 사용해 한 사용자가 join으로 중복 count되지 않게 한다.

## 5. 포함 범위

- 공통 transaction guard
- target user row → canonical role row 순서의 lock
- lock 뒤 predicate와 count 재검산
- 사용자 비활성화·role 제거·삭제 예약·bulk delete·즉시 purge·due purge 방어
- PostgreSQL 동시성·취소·rollback·API 회귀
- 5종 종료 산출물과 Draft PR

## 6. 제외 범위

Migration, schema/index, frontend, public API shape, runtime configuration, Entra 정책, Dev persona/bootstrap 정책, Persistent UAT 적용과 direct SQL 차단은 제외한다.

## 7. Serialization boundary

감소 가능 요청은 기존 connection/transaction을 유지한다. Target row를 잠근 뒤 canonical role row를 singleton invariant lock으로 잠그고, target의 canonical membership과 다른 administrator 수를 다시 확인한다. 다른 administrator가 없으면 mutation 전에 기존 domain failure로 종료한다.

## 8. Lock ordering

지원 경로는 target user row → canonical role row → count → mutation 순서를 사용한다. 여러 due row는 stable user ID 순서로 잠근다. Lock cancellation과 DB 장애는 last-admin domain failure로 바꾸지 않는다.

## 9. Mutation 경로

- `UpdateEntraUserAsync`: 비활성화와 role 전체 교체에 따른 role 제거
- `ScheduleEntraUserDeletionAsync`: 삭제 예약
- Bulk delete: 위 삭제 예약 또는 즉시 purge 경로 재사용
- `PurgeUserNowAsync`: lifecycle marker를 제외한 purge 전용 active-admin predicate로 비정상 상태를 방어
- `PurgeDueAsync`: 같은 purge 전용 predicate와 batch transaction 전체 rollback 유지

정상 삭제 예정 row는 inactive이므로 purge guard가 no-op이고 기존 reference 정책으로 진행한다. Lifecycle marker와 active Entra administrator role이 비정상적으로 공존하면 purge 전용 predicate가 canonical role row를 잠근 뒤 다른 canonical administrator를 재계산한다.

## 10. 오류 계약

마지막 administrator 감소는 HTTP 400과 단일 `message` response shape를 유지한다. Lock cancellation·timeout·DB 장애는 기술 오류 경계를 유지하며 SQLSTATE, query와 lock 정보는 사용자에게 노출하지 않는다.

## 11. 검증 결과

- 수정 전 isolated 재현: invariant violation 35
- 수정 후 서로 다른 target 감소 조합: active count 1, 경쟁 loser 1
- 동일 target 중복 요청: 기존 idempotent 성공 계약 유지
- 신규 admin 증가와 기존 admin 감소 경쟁: 모든 commit 뒤 active count 1 이상
- 20회 stress: violation 0, partial update 0, unexpected deadlock 0
- cancellation·transaction 중간 실패: rollback, active count 유지
- API: HTTP 400, response property 1개 유지
- Change 001 purge defense targeted PostgreSQL tests: 5/5
- Immediate last-admin rejection, due batch rollback, role-removal concurrency와 purge cancellation: 통과
- Backend Release build: warning/error 0/0
- Backend 전체: 361/361
- Frontend lint/typecheck/unit/build: error 0 / 성공 / 61/61 / 성공
- Mock UI와 isolated Full-Stack E2E: 1/1, 16/16
- actionlint, Markdown link·anchor·heading, secret/PII와 changed-file allowlist: 통과
- Persistent UAT read-only 전후: ledger 28/29/1, canonical active administrator 1, Pending/Processing 0/0, runtime·PostgreSQL identity 불변

## 12. Persistent UAT 보호

Persistent UAT는 read-only aggregate만 확인했다. 사용자·role write, runtime restart와 provider 호출은 0이며 ledger 28/29/1을 유지한다.

## 13. 제한사항

Application의 지원 mutation 경로를 보호한다. DBA direct SQL을 DB trigger/constraint로 차단하지는 않는다. 직접 SQL user/role 변경은 금지한다.

## 14. Rollback

공통 guard와 두 store 변경을 되돌리면 기존 동작으로 복귀한다. Migration/data rollback은 없다. Controlled UAT에서 이상이 있으면 기존 runtime을 유지하고 사용자·role 데이터를 임의 보정하지 않는다.

## 15. 후속 Task

1. 사용자 검수와 PR merge 결정
2. 별도 승인에 따른 `TASK-UAT-AUTH-HARDEN-001` controlled UAT
3. `TASK-GOV-002`

## 16. 5종 산출물 상태

| 산출물 | 상태 |
| --- | --- |
| Task 정의·검수 checklist | 작성 완료 |
| Implementation report | 작성 완료 |
| SOP | 작성 완료 |
| User manual | 작성 완료 |
| Roadmap update | 반영 완료 |

현재 상태: 원 구현 사용자 검수·PR #36 merge 완료 / Change 001 REDESIGN 구현·자동 검증·사용자 검수 완료 / Change 001 merge 승인.

## 17. 사용자 검수 체크리스트

- [x] 마지막 active System Administrator 보호 목적을 이해
- [x] 서로 다른 두 administrator 동시 감소 시 하나가 거부됨을 확인
- [x] 비활성화·role 제거·삭제 예약이 같은 guard를 사용함을 확인
- [x] 동일 target의 기존 idempotent 결과가 유지됨을 확인
- [x] HTTP 400과 화면/API shape가 유지됨을 확인
- [x] Entra·승인 대기·Dev persona·bootstrap 정책이 바뀌지 않음을 확인
- [x] Migration·frontend·runtime configuration 변경이 없음을 확인
- [x] Persistent UAT와 기존 runtime이 변경되지 않음을 확인
- [x] direct SQL 우회는 보호 범위 밖이며 금지됨을 이해
- [x] controlled UAT가 별도 승인임을 확인
- [x] Change 001 purge 전용 predicate와 malformed lifecycle defense-in-depth 결과 확인
- [x] Due purge 방어 거부의 batch 전체 rollback 결과 확인
