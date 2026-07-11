# TASK-NOTIFY-REL-001 Implementation Report

## 1. 목적

Notification delivery에 PostgreSQL 기반 claim/lease, fencing, attempt audit을 구현하고 다중 worker와 crash 경계를 재현 가능한 test로 검증한다.

## 2. 배경

UAT-VERIFY-001은 데이터·권한·Review-safe 기준선을 Go로 확정했지만 delivery worker의 select/send/update 경쟁 조건은 다음 개발 전 해결할 P2로 남겼다.

## 3. 기존 race

기존 due 조회는 row lock과 소유권이 없었다. provider 호출 후 update도 claim 조건이 없어 두 worker가 같은 row를 발송하거나 늦은 worker가 최신 상태를 덮을 수 있었다. attempt count는 delivery row에만 있어 crash 경계를 설명하기 어려웠다.

## 4. 구현 범위

0028 schema, claim store, dispatcher, channel provider boundary, retry/fencing, admin contract/store/UI, backend/frontend/concurrency/migration test, isolated candidate를 구현했다.

## 5. 제외 범위

Persistent UAT migration/runtime, actual provider smoke, escalation starvation, preferences, 기존 실패 data 정리는 제외했다.

## 6. 전체 아키텍처

`worker → atomic claim transaction → attempt Processing → optional provider-start CAS → handler → delivery/attempt completion CAS` 구조다. 관리자 조회는 delivery와 attempts를 읽되 claim token을 반환하지 않고 worker는 마스킹한다.

## 7. 상태 모델

Pending은 due claim 시 Processing이 된다. 성공·dry-run·disabled·suppressed는 terminal 상태, transient는 Pending retry, permanent/소진은 Failed다. 만료 Processing은 새 claim 또는 retry-limit Failed로 전환된다.

## 8. DB/Migration

0028은 기존 migration을 수정하지 않는 additive migration이다. Fresh isolated DB에서 canonical 28개와 latest 0028을 확인했다. claim consistency, Processing 허용, attempt FK/unique/check, 4개 index를 migration test로 확인했다.

## 9. Claim SQL

Pending due와 expired Processing을 deterministic order로 `FOR UPDATE SKIP LOCKED`한다. candidate id lock, claim update, attempt insert, claimed row read를 한 transaction에서 수행한다. 두 worker 10개 batch는 중복 없이 5개씩 분할됐다.

## 10. Lease 정책

기본 300초이며 HTTP provider ceiling 100초와 SMTP timeout 중 큰 값에 30초 safety margin을 더한 값보다 반드시 길어야 한다. 짧은 설정은 options validation과 application startup에서 fail-fast한다.

## 11. Fencing

completion과 provider-start는 claim token CAS다. stale owner의 completion은 false를 반환하고 delivery를 변경하지 않는다. attempt에는 stable claim-lost code를 기록한다.

## 12. Attempt audit

attempt 번호와 claim별로 Processing부터 terminal outcome까지 추적한다. stale 전/후 provider 경계, RetryScheduled, FailedPermanent, OwnershipLost를 구분한다. delivery+attempt와 claim token은 unique다.

## 13. Provider call boundary

handler가 실제 provider를 호출할 조건일 때만 provider-start를 먼저 기록한다. CAS 실패 시 handler는 호출되지 않는다. disabled/dry-run/suppressed는 delivery와 attempt를 완료하지만 실제 provider 기록은 만들지 않는다.

## 14. Retry/permanent failure

transient failure는 next attempt를 5분 뒤로 잡고 claim을 해제한다. 권한/설치/대상 오류와 retry 소진은 permanent failure다. cancellation은 Processing을 남겨 lease recovery가 담당한다.

## 15. Worker 변경

worker는 due row를 직접 읽지 않고 claimed batch만 처리한다. instance identity는 hostname·사용자 정보가 아닌 process별 opaque random 값이다. 정상 경쟁 test에서 provider call은 1회였다.

## 16. Admin API

Dashboard에 Processing/Sent count를 추가하고 list/detail에 lease/stale/attempt projection을 제공한다. Processing admin action은 처리 대상에서 제외돼 상태가 변하지 않는다.

## 17. Frontend UX

Processing tab·badge·lease 상태, stale 안내, attempt history와 한글 outcome을 추가했다. Processing checkbox와 bulk action을 비활성화한다. worker identity와 claim token은 표시하지 않는다.

## 18. Manual send 회귀

관리자 수동 발송은 기존 queue 접수 방식을 유지한다. 진단용 mail/Teams Activity 경로도 공통 claim/audit dispatcher를 사용해 소유권 우회를 제거했다. notification targeted suite와 Full-Stack E2E가 통과했다.

## 19. Concurrency test

- single delivery/two workers: provider 1, attempt 1
- 10 deliveries/two workers: distinct claim 10, attempt 10
- simultaneous stale recovery: new claim 1
- Processing admin action: mutation 0

## 20. Crash test

provider 전 crash는 `LeaseExpiredBeforeProviderCall`, provider 시작 후 crash는 `LeaseExpiredAfterProviderCallStarted`로 기록된다. 늦은 completion은 fencing된다. cancellation은 허위 terminal 상태를 만들지 않는다.

## 21. At-least-once 제한

provider가 성공했지만 DB completion 전에 process가 종료되면 다음 worker는 결과를 알 수 없다. 안전하게 audit을 남기고 재시도할 수 있으나 provider 중복 가능성은 남는다. 따라서 exactly-once라고 문서화하지 않는다.

## 22. Persistent UAT 보호

Persistent UAT는 read-only aggregate만 조회했다. 전후 snapshot 16/16 field 동일, restart 0, volume/container/runtime PID 동일, live migration count 유지, 0028 미적용이다.

## 23. Candidate 검증

- backend/frontend: 5094/5192, live/ready/root 200
- DB: dedicated tmpfs, migration 28, latest 0028
- synthetic delivery: Pending/Processing/Sent/Failed와 stale Processing
- actual provider call log: 0
- desktop/390px 7개 route: blank/target-not-found/console error/overflow 0
- Processing row 2개 checkbox disabled, bulk action disabled
- attempt history 표시, raw worker identity 미표시
- output guard synthetic negative sample 5/5 차단

## 24. 자동 테스트

| 검증 | 결과 |
| --- | --- |
| git diff check / actionlint | 통과 |
| Backend Release build | warning 0, error 0 |
| Claim/lease + migration targeted | 14/14 |
| Notification/migration/authorization | 151/151 |
| Backend 전체 | 325/325 |
| Frontend lint | error 0, 기존 warning 1 |
| Frontend typecheck/unit/build | 통과, 61/61 |
| Mock UI | 1/1 |
| Full-Stack E2E | 16/16, 잔여 resource 0 |
| pnpm audit | advisory 0 |
| Candidate/browser/output guard | 통과 |

기존 main의 procurement dashboard test는 절대 날짜 fixture가 현재 날짜를 지나면서 실패했다. 제품 회귀가 아닌 P3 test stability 문제로 확인하고 해당 fixture만 상대 날짜로 보정했다.

## 25. 보안/PII

실제 UAT 원문, 사용자명, 이메일, provider payload/response, token, credential을 출력하거나 문서화하지 않았다. Browser 결과는 boolean/integer/fixed alias만 반환했고 screenshot을 생성하지 않았다. Candidate는 synthetic placeholder만 사용한다.

## 26. Rollback/forward-fix

병합 전 rollback은 task branch와 전용 candidate만 제거하면 된다. Persistent UAT는 변하지 않는다. 병합 후 schema/runtime 적용은 TASK-UAT-HANDOVER-003에서 backup, migration, ledger 29개, fake/dry-run, rollback/forward-fix를 통제한다. 0028은 이미 적용된 운영 DB에서 down migration 대신 forward-fix를 사용한다.

## 27. 후속 Task

1. TASK-UAT-HANDOVER-003
2. TASK-NOTIFY-ESC-001
3. TASK-AUTH-HARDEN-001
4. TASK-GOV-002

## 28. 해결한 업무 문제

운영자는 같은 알림이 왜 두 번 시도됐는지, 현재 처리 중인지, lease가 만료됐는지, 어느 결과가 DB에 반영됐는지 구분할 수 있다. 수평 확장 시 정상 경쟁의 중복 발송과 늦은 overwrite를 차단한다.

## 29. 기술적 결정과 검토한 대안

- advisory lock 대신 row-level `SKIP LOCKED`: batch 확장과 DB 상태 가시성이 좋다.
- 단일 attempt_count 대신 attempt table: crash 계보를 보존한다.
- worker hostname 대신 opaque identity: 개인정보·인프라 정보 노출을 줄인다.
- claim token을 API에 노출하는 방식은 보안상 폐기했다.
- exactly-once 표기는 provider transaction 부재로 채택하지 않았다.

## 30. 시행착오 및 폐기한 접근

- 초기 frontend deep-link test는 관리자 상태 확정 전 route를 평가해 실패했다. 관리자 선택 후 popstate를 발생시키는 기존 test 관례로 수정했다.
- candidate synthetic recipient kind를 허용 enum과 다르게 넣은 첫 insert는 transaction 전체가 rollback됐다. canonical constraint를 확인한 뒤 허용 값으로 다시 구성했다.
- 기존 procurement fixture의 절대 날짜를 그대로 두고 full suite 실패를 warning으로 숨기는 접근은 폐기하고 상대 날짜로 보정했다.

## 31. 사용자 검수 결과와 남은 항목

Checklist 작성과 자동 검증은 완료됐다. 사용자 검수는 대기 중이며 완료로 표시하지 않는다. Candidate 화면, attempt 의미, at-least-once 제한, Persistent UAT 미적용, 후속 handover를 확인해야 한다.

## 32. 주요 파일 목록

- `database/migrations/0028_notification_delivery_claim_lease.sql`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryStore.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDispatcher.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryLeasePolicy.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryContracts.cs`
- `backend/src/Emi.Qms.Api/Admin/AdminMasterDataStore.cs`
- `frontend/src/App.tsx`
- `frontend/src/projects.ts`
- 관련 backend/frontend tests와 5종 산출물
