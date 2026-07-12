# TASK-NOTIFY-ESC-001 Implementation Report

## 1. 목적

Escalation 후보가 batch size를 넘을 때 발생하는 starvation과 후보 오류의 poll 중단을 보정하고 기존 업무·중복 방지 계약을 보존한다.

## 2. 기준선

구현 시작 시 main과 origin/main은 `3ba3424909a84def5c6f3236ad6b889d42492ac9`로 일치했다. Persistent UAT ledger는 canonical/live/legacy 28/29/1, Pending/Processing 0/0이었고 Development와 Review-safe는 정상 상태였다. Escalation worker는 disabled였다.

## 3. 실제 root cause

### ESCALATION_FIXED_BATCH_STARVATION

- directly observed: true
- inferred: false
- evidence count: 4
- confidence: High

Query가 due date와 created time 기준 첫 100건만 고정 선택했다. History, sent marker와 next check는 limit 이전 fairness에 사용되지 않았고 work item ID tie-breaker도 없었다. 처리 이력이 있는 선두 100건이 계속 window를 점유했다.

### ESCALATION_CANDIDATE_FAILURE_ABORTS_POLL

- directly observed: true
- inferred: false
- evidence count: 2
- confidence: High

후보 처리 loop에 후보별 예외 경계가 없어 downstream 오류가 service 전체 호출을 종료했다.

### 동시 reader

두 evaluator가 같은 window를 읽는 현상은 직접 관찰했지만 기존 unique/idempotency 계약이 중복 생성은 막았다. 공정 ordering 없이 두 reader가 tail 진행을 개선하지 못하는 것이 문제였다.

## 4. Red-before-green 증빙

수정 전 isolated PostgreSQL에서 처리 이력 선두 뒤 tail 미평가, 101건 중 100건만 평가, 후보 오류의 poll escape, 동시 evaluator 100건 고착을 재현했다. 같은 테스트는 수정 후 모두 통과했다.

## 5. 구현 범위

- `WorkItemEscalationStore.ReadOpenCandidatesAsync` ordering
- `NotificationEscalationService.EvaluateAsync` 후보별 오류 경계
- isolated PostgreSQL integration/concurrency tests

Migration, API, UI, options와 runtime registration은 변경하지 않았다.

## 6. Candidate selection

Selection은 기존 unique work-item history row를 left join한다. 미평가·due 변경·inactive 후보를 먼저, active 후보는 가장 오래 평가되지 않은 순서로 가져온다. 한 query result에서 work item은 정확히 하나다.

## 7. Ordering과 watermark

Ordering key는 fairness class, 기존 평가 시각, due date, created time, work item ID다. `updated_at_utc`는 기존 upsert가 실제 평가 때 갱신하는 필드이므로 별도 row나 사용자-facing audit 변경이 없다.

## 8. 후보 오류 격리

후보마다 독립 `try/catch`를 적용했다. Requested cancellation은 다시 throw한다. 다른 오류는 식별자나 원문 exception을 기록하지 않고 `ESCALATION_CANDIDATE_EVALUATION_FAILED`와 aggregate failure count만 남긴다.

## 9. 기존 정책 회귀

BusinessDayCalculator, L0 직전 영업일, L1 즉시 overdue, L2/L3 영업일 기준과 recipient resolver를 변경하지 않았다. Due date null, Completed와 Cancelled 제외도 유지했다.

## 10. 중복 방지

기존 work-item escalation unique, notification idempotency, recipient unique와 delivery conflict 계약을 그대로 재사용한다. 동시 evaluator에서 escalation, notification과 delivery duplicate는 모두 0이었다.

## 11. 경계 결과

| Eligible | 완료 poll | Unique evaluated | Tail evaluated |
| ---: | ---: | ---: | --- |
| 99 | 1 | 99 | true |
| 100 | 1 | 100 | true |
| 101 | 2 | 101 | true |
| 200 | 2 | 200 | true |
| 201 | 3 | 201 | true |

처리 이력이 있는 선두 100건은 미평가 tail을 막지 않았다. 같은 due date와 created time은 work item ID로 결정적으로 정렬됐다.

## 12. Recipient와 상태 혼합

Inactive recipient, resolved recipient 0명, due date null, Completed, Cancelled와 L0~L3 혼합 fixture를 검증했다. Eligible tail은 계속 진행했고 기존 recipient 정책은 바뀌지 않았다.

## 13. 재시작과 동시성

첫 poll 뒤 service를 새로 구성해도 DB watermark로 다음 후보가 진행됐다. 두 evaluator 동시 실행에서도 모든 후보가 유한한 poll에 평가됐고 중복은 0이었다.

## 14. Query plan

Task-owned PostgreSQL 16 tmpfs DB에 후보 20,000건과 history 10,000건을 구성했다. Plan은 bounded LIMIT 100과 top-N heapsort를 사용했고 약 48ms에 100건을 반환했다. 전체 eligible set의 순환 정렬 특성상 candidate scan은 있었지만 현재 규모와 poll interval에서 수용 가능했고 신규 index/migration stop condition은 발생하지 않았다.

## 15. 검토한 대안

- 이미 처리된 level을 query에서 제외: 다음 level/next-check 평가를 누락할 수 있어 폐기
- keyset cursor/watermark schema: 공정성은 높지만 migration과 crash cursor 관리가 필요해 과도함
- level별 batch: 공유 정책과 수신자 흐름을 분기해 복잡도가 증가함
- claim/lease: 중복 경쟁에는 강하지만 현재 unique/idempotency 계약에 비해 과도함
- 한 poll multi-page: 한 poll 작업량 bound를 약화시켜 폐기

기존 evaluation timestamp 재사용이 schema 변경 없이 starvation과 restart continuation을 함께 해결하는 최소안이었다.

## 16. 자동 테스트

- 신규 targeted 15/15
- 기존 escalation과 BusinessDay 회귀 통과
- backend Release build warning/error 0/0
- backend 전체 suite 통과
- frontend lint/typecheck/unit 61/61/build 통과
- Full-Stack E2E 16/16
- actionlint와 `git diff --check` 통과

Frontend lint의 Fast Refresh 경고 1건과 production chunk-size 경고는 기존 경고이며 신규 경고는 0이다.

## 17. Provider와 전달 계약

모든 integration fixture는 fake/no-op provider를 사용했다. Provider call start와 actual provider call은 0이다. 외부 전달은 기존 claim/lease worker의 at-least-once 계약이며 exactly-once로 확대하지 않았다.

## 18. Persistent UAT 보호

Persistent UAT에는 read-only query만 수행했다. Ledger 28/29/1, risk count, core aggregate, container health/restart와 listener PID를 전후 비교했다. Escalation worker는 활성화하지 않았다.

## 19. 보안·개인정보

Synthetic fixture만 사용했다. 로그와 보고에는 실제 업무, 사용자, recipient, 프로젝트, 알림 내용과 row ID를 포함하지 않았다. Secret/PII scan과 generated artifact 검사를 게시 전에 수행한다.

## 20. Rollback

Store ordering과 service error boundary를 되돌리면 된다. Migration/data rollback은 없다. UAT 적용 중 이상이 있으면 worker를 disabled로 유지하고 기존 runtime을 재시작하지 않은 채 별도 결정을 받는다.

## 21. 제한사항

지속적으로 실패하는 후보는 다음 poll에서 재평가되며 stable failure count가 반복될 수 있다. 이번 Task는 실패 원인을 자동 격리 큐로 이동하거나 escalation claim을 추가하지 않는다.

## 22. 해결한 업무 문제

대량 업무에서도 뒤쪽 예정일 후보가 유한하게 평가되고 한 건의 데이터·recipient 오류가 다른 업무의 기한 알림 평가를 막지 않는다.

## 23. 기술적 결정

Schema를 늘리지 않고 이미 안전하게 갱신되던 evaluation timestamp를 fairness watermark로 재사용했다. Total order와 bounded batch를 유지해 동작을 결정적으로 만들었다.

## 24. 시행착오 및 폐기한 접근

단순 ID pagination은 due date 변경과 level 재평가를 자연스럽게 포함하지 못한다. History row를 limit 뒤 메모리에서 건너뛰는 접근은 기존 starvation을 해결하지 못한다. 실패를 worker 바깥에서만 잡는 접근도 같은 poll 진행을 보장하지 못해 폐기했다.

## 25. 주요 파일

- `backend/src/Emi.Qms.Api/Notifications/WorkItemEscalationStore.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationEscalationService.cs`
- `backend/tests/Emi.Qms.Api.Tests/NotificationDeliveryTests.cs`

## 26. 후속 Task

사용자 검수와 merge 뒤 별도 controlled UAT 절차에서 worker 활성 전 read-only 기준선, fake/no-op provider와 duplicate metric을 다시 확인한다. 다음 코드 P2는 TASK-AUTH-HARDEN-001이다.

## 27. 사용자 검수 결과와 남은 항목

Checklist와 자동 검증은 완료됐다. 사용자 검수와 Persistent UAT controlled 적용은 대기 상태다. 신규 기능 개발 No-Go를 유지한다.
