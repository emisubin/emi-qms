# TASK-UAT-MAINTENANCE-001 — Mutation worker maintenance gate

## 1. 목적

Development 기본 동작을 유지하면서 notification delivery, escalation, administrator deletion purge worker를 명시적으로 비활성화할 수 있게 하고 TASK-UAT-HANDOVER-003 Phase A를 안전하게 재현한다.

## 2. 배경

TASK-UAT-HANDOVER-003 preflight에서 `AdminDeletionPurgeWorker`가 Development composition root에 무조건 등록되고 시작 즉시 purge query를 실행하는 P2가 발견됐다.

## 3. 발견된 P2

- failure code: `PURGE_WORKER_DISABLE_GATE_UNAVAILABLE`
- 영향: Persistent UAT migration 전에 all-workers-disabled Development Phase A를 보장할 수 없음
- 제품 데이터 손상과 actual provider 호출은 없었고 Persistent UAT 0028은 미적용 상태로 유지됐다.

## 4. TASK-UAT-HANDOVER-003 중단 이유

Delivery와 escalation에는 내부 `Enabled` check가 있었지만 DI 등록은 무조건이었다. Purge에는 option과 내부 check가 모두 없었다. 등록된 purge worker는 첫 interval을 기다리지 않고 즉시 실행하므로 due row가 0이어도 maintenance-safe로 간주할 수 없었다.

## 5. 현재 worker registration 구조

`MutationWorkerActivationPolicy`가 ReviewSafe와 세 option의 effective 값을 한 번 계산한다. Composition root와 runtime status가 같은 결과를 사용한다.

## 6. 포함 범위

- purge enable option과 strict boolean validation
- delivery, escalation, purge의 조건부 hosted-service 등록
- purge worker 내부 defense-in-depth
- worker별 runtime boolean projection
- default, explicit disabled, ReviewSafe, isolated Phase A 검증

## 7. 제외 범위

Persistent UAT 변경, migration 0028 적용, runtime handover, frontend UX, delivery claim/lease, escalation starvation, dependency 변경과 actual provider smoke는 제외한다.

## 8. Purge enable option

Canonical key는 `AdminDeletionPurge:Enabled`이고 환경 변수 override는 `AdminDeletionPurge__Enabled`다. 기본값은 `true`다. `true`/`false`가 아닌 값은 startup 전에 실패한다. 기존 delivery/escalation과 같은 운영 enable 정책이므로 Production에서도 명시적 `false`를 허용하되 runtime status로 식별한다.

## 9. Conditional DI registration

ReviewSafe에서는 모든 mutation worker를 미등록한다. Development에서는 각 effective option이 `true`인 worker만 등록한다. Phase A의 세 option이 모두 `false`이면 mutation hosted service 등록은 0이다.

## 10. Worker defense-in-depth

Purge worker는 직접 구성돼도 시작 시와 각 실행 직전에 option을 확인한다. `false`이면 purge service 호출, candidate query와 delete SQL을 실행하지 않고 종료한다.

## 11. Runtime projection

`/api/runtime-mode`에 다음 boolean을 추가했다.

- `notificationDeliveryWorkerEnabled`
- `notificationEscalationWorkerEnabled`
- `adminDeletionPurgeWorkerEnabled`
- `mutationWorkersEnabled`

PID, host, worker identity, DB 또는 대상 row 정보는 노출하지 않는다.

## 12. Default behavior

Purge 기본값 `true`에서 기존 즉시 첫 실행과 1시간 polling interval을 유지했다. Isolated synthetic holiday purge가 정상 처리됐다.

## 13. Disabled behavior

`AdminDeletionPurge__Enabled=false`이면 DI 등록 0이다. 직접 worker test도 purge service call 0이다. 잘못된 boolean은 startup 실패다.

## 14. ReviewSafe behavior

Purge option이 true 또는 false여도 delivery, escalation, purge worker와 actual provider는 기존처럼 미등록된다. DB read-only와 mutation 423 정책은 유지된다.

## 15. Maintenance Phase A

Isolated tmpfs PostgreSQL에서 due delivery 1, due escalation 1, due purge 1과 기존 Processing 2를 준비했다. 세 worker를 false로 기동한 backend는 live/ready 200과 worker projection false를 반환했다. 두 관찰 구간에서 claim, escalation, purge, attempt, provider 변화는 0이었다.

## 16. Persistent UAT 보호

Persistent UAT는 read-only snapshot만 확인했다. Migration 0028 미적용, PostgreSQL restart 0, 기존 listener 9/9와 container/volume을 유지했다.

## 17. Backup 정책

기존 secure pre-0028 backup을 열람·삭제·restore하지 않았다. HANDOVER-003 재개 시 기존 backup은 rehearsal evidence로 보존하고 migration 직전 새 backup과 restore rehearsal을 다시 수행한다.

## 18. 테스트 결과

- targeted maintenance/ReviewSafe: 14/14
- backend 전체: 331/331
- frontend unit: 61/61, lint/typecheck/build 성공
- Full-Stack E2E: 16/16, 신규 잔여 자원 0
- isolated default-enabled purge와 Phase A 검증 성공

## 19. 제한사항

Provider 성공과 DB completion 사이 전달 의미는 계속 at-least-once이며 exactly-once가 아니다. 이번 Task는 HANDOVER-003을 재개하거나 Persistent UAT 설정을 바꾸지 않는다.

## 20. 후속 Task

1. 사용자 검수와 본 Draft PR merge
2. TASK-UAT-HANDOVER-003 preflight 처음부터 재개
3. migration 직전 fresh backup/restore rehearsal
4. 이후 TASK-NOTIFY-ESC-001

## 21. 5종 산출물 상태

| 산출물 | 상태 |
| --- | --- |
| Task 정의 | 작성 완료 |
| Implementation report | 작성 완료 |
| SOP | 작성 완료 |
| User manual | 작성 완료 |
| Roadmap update | 반영 완료 |

현재 상태는 Checklist 작성됨, 자동 검증 완료, 사용자 검수 완료, PR #31 병합 승인, 미체크 항목 0이다.

## 22. 사용자 검수 체크리스트

- [x] purge worker 기본 상태가 활성임을 이해
- [x] explicit disable 시 worker 미등록 확인
- [x] explicit disable 시 purge query/delete 0 확인
- [x] ReviewSafe에서 purge worker 미등록 확인
- [x] 세 mutation worker 상태 확인 가능
- [x] Phase A에서 세 worker 모두 disabled 확인
- [x] Phase A synthetic due 후보 불변 확인
- [x] normal Development 기본 동작 회귀 없음
- [x] Persistent UAT와 migration ledger 무변경 확인
- [x] migration 0028 미적용 확인
- [x] actual provider 호출 0 확인
- [x] 기존 runtime 9/9 유지 확인
- [x] 기존 backup 보존과 fresh backup 필요성 이해
- [x] SOP 실행 가능
- [x] User manual 이해 가능
- [x] 다음 단계 TASK-UAT-HANDOVER-003 확인
- [x] 신규 기능 개발 No-Go 유지
