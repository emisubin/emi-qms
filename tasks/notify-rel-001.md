# TASK-NOTIFY-REL-001 — Notification delivery claim/lease와 attempt audit

## 1. 목적

여러 notification worker가 동시에 실행돼도 같은 delivery를 정상 경쟁 구간에서 한 번만 claim하고, claim 소유자만 결과를 반영하도록 한다. 각 시도를 별도 audit row로 남겨 재시도·lease 만료·늦은 완료를 운영자가 추적할 수 있게 한다.

## 2. 배경

기존 worker는 due delivery를 조회한 뒤 provider를 호출하고 조건 없는 update로 결과를 저장했다. 조회와 update 사이에 소유권이 없어 두 worker가 같은 row를 읽으면 provider가 중복 호출될 수 있고, 늦게 끝난 worker가 최신 결과를 덮어쓸 수 있었다.

## 3. 해결하는 P2

- due row의 원자적 소유권 부재
- 다중 worker 중복 provider 호출 위험
- 늦은 completion의 상태 overwrite 위험
- crash 뒤 장기 Pending/Processing 고착 위험
- attempt별 원인·결과·provider 경계 audit 부재
- Processing 상태의 관리자 오조작 위험

## 4. 현재 duplicate race

기존 흐름은 `SELECT due → provider call → UPDATE`였다. 두 transaction이 같은 Pending row를 읽는 것을 막지 못하고 update에도 소유권 조건이 없었다. 이번 변경은 `FOR UPDATE SKIP LOCKED`와 claim token을 사용해 `SELECT/Processing 전환/attempt 생성`을 한 transaction에서 수행한다.

## 5. 보장 수준

이 구현은 정상 worker 경쟁에서 provider call 1회를 보장하고 늦은 worker의 DB overwrite를 fencing한다. 외부 provider 성공 후 DB completion 전에 process가 종료되는 경계에서는 재claim이 provider를 다시 호출할 수 있으므로 전달 의미는 at-least-once이며 exactly-once가 아니다. 이 모호성은 attempt outcome으로 보존한다.

## 6. 포함 범위

- additive migration 0028
- Pending/Processing/Sent/Failed 상태 전환
- atomic claim, lease, opaque worker identity, fencing token
- attempt audit와 stale recovery
- transient/permanent failure 분류
- 기존 자동·수동 delivery 경로의 공통 dispatcher 사용
- 관리자 Processing count/filter/detail/attempt UI
- PostgreSQL 동시성·crash·fencing test
- 전용 tmpfs candidate 5094/5192

## 7. 제외 범위

- Persistent UAT에 0028 적용
- Development/Preview/Review-safe runtime handover
- 실제 Teams Activity, Teams Channel, SMTP 발송
- provider 자체의 idempotency key 보장
- escalation starvation, 사용자별 알림 설정, 기존 실패 row 정리
- 기존 0001~0027 migration 변경

## 8. State machine

| From | Event | To | Claim |
| --- | --- | --- | --- |
| Pending | due claim 성공 | Processing | 생성 |
| Processing | 성공/비활성/제외 | Sent/DryRunSent/Disabled/Suppressed | 해제 |
| Processing | transient failure, retry 여유 | Pending | 해제, 다음 시각 설정 |
| Processing | permanent failure 또는 retry 소진 | Failed | 해제 |
| Processing | lease 만료, retry 여유 | Processing | 새 claim으로 교체 |
| Processing | lease 만료, retry 소진 | Failed | 해제 |

## 9. Claim/lease

due query는 Pending due row와 만료된 Processing row만 선택한다. `FOR UPDATE SKIP LOCKED`, deterministic order, batch limit을 사용한다. 상태 변경, attempt count 증가, 새 UUID claim, lease expiry, attempt row 생성은 같은 transaction이다. 기본 lease는 300초다.

## 10. Fencing token

completion은 `delivery id + status=Processing + claim_token`이 모두 일치할 때만 성공한다. 이전 claim의 늦은 성공/실패는 delivery를 변경하지 않고 해당 attempt에 `NotificationDeliveryClaimLost`를 남긴다. claim token은 API와 UI에 노출하지 않는다.

## 11. Attempt audit

`notification_delivery_attempts`는 attempt 번호, claim token, opaque worker identity, claim/lease/provider-start/completion 시각, outcome, 안전한 오류 코드와 provider marker를 기록한다. UI는 worker identity를 마스킹하고 claim token을 반환하지 않는다.

## 12. Stale recovery

새 claim transaction은 만료된 Processing attempt를 provider 호출 전/후로 구분해 종료한다. retry 여유가 있으면 한 worker만 새 claim을 얻고, 한도에 도달하면 Failed로 마감한다. shutdown cancellation은 허위 Sent/Failed로 바꾸지 않고 lease recovery 대상으로 남긴다.

## 13. Provider boundary

실제 외부 호출이 예상될 때만 provider-call-start를 claim 소유권 조건으로 기록한다. 기록 실패 시 provider를 호출하지 않는다. dry-run, disabled, suppressed는 claim/audit을 거치지만 provider-call-start는 남기지 않는다.

## 14. Retry/failure

일시 오류는 Pending과 `RetryScheduled`로 전환하고 5분 뒤 재시도한다. 명시된 권한·대상·설치 오류와 retry 소진은 Failed/`FailedPermanent`로 끝낸다. attempt count와 retry limit은 claim 시점 기준으로 일관되게 적용한다.

## 15. Admin API/UI

- Dashboard: Pending, Processing, Failed, Sent count 분리
- 목록: Processing tab/filter, claim 시각/expiry/stale 표시
- 상세: attempt 번호, 한글 outcome, provider 경계·완료 시각
- Processing: checkbox와 acknowledge/dismiss/retry 차단
- raw worker/claim token: 미노출

## 16. Migration 0028

`0028_notification_delivery_claim_lease.sql`은 delivery claim 4개 column, Processing 상태·claim consistency constraint, attempt table과 unique/FK/check, due/owner/attempt/stale index를 추가한다. 기존 row는 non-Processing/claim null 조건을 만족하며 보존된다.

## 17. Concurrency test matrix

| Case | 결과 |
| --- | --- |
| 단일 delivery, worker 2개 | claim/provider 1회 |
| delivery 10개, worker 2개 | SKIP LOCKED로 중복 없이 10개 분할 |
| provider 전 crash | lease 만료 후 재claim, 명확한 audit |
| provider 시작 후 crash | 결과 불확실 audit, at-least-once 경계 |
| 늦은 completion | fencing으로 overwrite 0 |
| transient/permanent | Pending retry / Failed 종료 |
| stale 동시 회수 | 새 claim 1개 |
| cancellation | Processing 유지 후 recovery |
| Processing admin action | 상태 변경 0 |

## 18. Candidate 환경

- alias: notification-rel-candidate
- backend/frontend: 5094/5192 HTTPS
- DB: 전용 `emi_qms_e2e_*` PostgreSQL, tmpfs, Persistent UAT와 물리 분리
- data: synthetic Pending/Processing/Sent/Failed와 attempt history만 사용
- provider/dispatch: 비활성, 실제 호출 0
- desktop/390px: blank/target-not-found/console error/page overflow 0

## 19. Persistent UAT 보호

작업 전후 16개 aggregate field가 정확히 일치했다. PostgreSQL container, volume, restart count 0, live migration 28개와 기존 runtime PID가 유지됐다. Persistent UAT에는 0028을 적용하지 않았다.

## 20. 제한사항

- exactly-once가 아니다.
- provider가 idempotency key를 지원하지 않으면 provider 성공/DB 실패 경계의 중복 가능성이 남는다.
- 기존 Persistent UAT는 0028 이전 schema이므로 이 branch runtime을 연결할 수 없다.
- unrelated procurement test의 고정 날짜 fixture는 현재 날짜에 따라 실패하는 기존 P3였고 상대 날짜로 test-only 보정했다.

## 21. 후속 Task

1. 사용자 검수와 본 PR merge
2. TASK-UAT-HANDOVER-003: Persistent UAT 0028 controlled migration과 runtime handover
3. TASK-NOTIFY-ESC-001
4. TASK-AUTH-HARDEN-001
5. TASK-GOV-002

## 22. 5종 산출물 상태

| 산출물 | 상태 |
| --- | --- |
| Task 정의 | 작성 완료 |
| Implementation report | 작성 완료 |
| SOP | 작성 완료 |
| User manual | 작성 완료 |
| Roadmap update | 반영 완료 |

현재 상태는 Checklist 작성됨, 자동 검증 완료, 사용자 검수 완료, PR #30 병합 승인이다. 미체크 사용자 항목은 0개다. claim/lease/fencing/attempt audit과 정상 경쟁 provider call 1회, at-least-once 제한 및 exactly-once 미보장, Persistent UAT 0028 미적용, actual provider 호출 0을 확인했다. 다음 Task는 TASK-UAT-HANDOVER-003이며 전체 신규 기능 개발 No-Go를 유지한다.

## 23. 사용자 검수 체크리스트

- [x] Candidate 환경 접속 정상
- [x] 알림 발송 상태에 발송 처리 중 표시
- [x] Pending과 Processing이 구분됨
- [x] Processing count/filter 정상
- [x] Processing row action disabled
- [x] Attempt 이력과 번호/outcome 이해 가능
- [x] Stale lease 안내 이해 가능
- [x] Pending/Sent/Failed 기존 표시 회귀 없음
- [x] 정상 worker 경쟁 provider call 1회 증빙 확인
- [x] 늦은 worker overwrite 0 증빙 확인
- [x] exactly-once가 아님을 이해
- [x] Persistent UAT에 0028 미적용 확인
- [x] 실제 외부 발송 0 확인
- [x] Console 오류와 390px overflow 0 확인
- [x] SOP 실행 가능
- [x] User manual 이해 가능
- [x] 다음 단계 TASK-UAT-HANDOVER-003 확인
- [x] 전체 신규 기능 개발 No-Go 유지 확인
