# TASK-NOTIFY-REL-001 SOP

## 1. 문서 목적

Notification delivery claim/lease 상태를 운영자가 안전하게 확인하고, worker 수평 확장·중단·stale recovery·장애 대응을 반복 수행할 수 있게 한다.

## 2. Pending/Processing/Sent/Failed 의미

| 상태 | 의미 | 운영 action |
| --- | --- | --- |
| Pending | claim 대기 또는 transient retry 예약 | due 시각과 open handling 확인 |
| Processing | 한 worker가 lease로 소유 | 임의 retry/ack/dismiss 금지 |
| Sent | provider 또는 dry-run 성공 결과 반영 | attempt history 확인 |
| Failed | permanent 또는 retry 소진 | error code와 handling 정책 확인 |

## 3. Claim과 lease 의미

Claim은 한 delivery의 현재 처리 소유권이다. Lease는 그 소유권의 유효기간이다. DB는 Processing row에 claim token, claim/expiry 시각, opaque worker 값을 요구한다. token은 운영 화면이나 보고서에 복사하지 않는다.

## 4. Attempt audit 확인

관리자 알림 발송 상세에서 발송 시도 이력을 연다. attempt 번호, outcome, claim/provider-start/completion 시각, safe error code를 확인한다. raw worker identity와 claim token이 보이지 않는 것이 정상이다.

## 5. 정상 worker 확인

1. worker instance 수와 각 process health를 확인한다.
2. Pending due가 Processing으로 한 번만 이동하는지 확인한다.
3. 같은 delivery의 active Processing attempt가 1개인지 확인한다.
4. 정상 경쟁 test에서 provider call count가 1인지 확인한다.
5. 실제 provider payload는 로그에 출력하지 않는다.

## 6. Processing 장기 체류 확인

현재 시각이 lease expiry 이전이면 정상 처리 중일 수 있다. expiry 이후면 stale이다. worker가 활성인데도 다음 poll 뒤 stale가 회수되지 않으면 dispatch enabled, retry limit, DB connectivity를 확인한다.

## 7. Stale lease 의미

Stale은 이전 worker의 lease가 만료됐다는 뜻이다. provider 시작 전 만료와 시작 후 만료를 구분한다. 후자는 외부 성공 여부를 확정할 수 없어 중복 가능 경계다.

## 8. Ownership lost 의미

이전 worker가 새 claim 이후 완료를 제출하면 fencing이 거절한다. delivery 상태는 최신 owner 결과를 유지하고 이전 attempt에는 claim-lost code가 남는다. DB를 수동 update하지 않는다.

## 9. Transient retry 확인

`RetryScheduled` outcome, Pending 상태, next attempt 시각, attempt count 증가를 함께 확인한다. retry 시각 전 수동 실행하지 않는다.

## 10. Permanent failure 확인

`FailedPermanent`, Failed 상태, next attempt null을 확인한다. 대상/권한/설정 오류를 운영 경계에서 수정하고 delivery는 acknowledge 또는 dismiss로 처리한다. 현재 terminal Failed 수동 retry 기능은 없으며 Processing과 Failed는 retry 대상이 아니다. 수동 재처리가 필요하면 [TASK-NOTIFY-004 정책](notify-004.md)에 따라 별도 신규 기능으로 요청한다.

## 11. Duplicate 가능 경계

Provider call 시작 후 응답 성공, DB completion 전 process 종료가 발생하면 결과가 불확실하다. attempt audit의 provider-start와 lease-expired-after-provider outcome을 근거로 운영 판단한다.

## 12. Exactly-once가 아닌 이유

PostgreSQL transaction과 외부 provider transaction을 원자적으로 묶을 수 없다. claim/fencing은 정상 경쟁과 DB overwrite를 막지만 provider 성공 직후 crash의 재발송 가능성은 제거하지 못한다.

## 13. 관리자 화면 확인

1. Dashboard의 Pending/Processing/Failed/Sent count를 확인한다.
2. Processing filter를 연다.
3. lease 유효/만료 표시를 확인한다.
4. Processing checkbox와 bulk action이 disabled인지 확인한다.
5. 상세 attempt history의 outcome을 확인한다.

## 14. Worker 수평 확장 절차

1. 모든 instance가 같은 0028 schema와 설정을 사용함을 확인한다.
2. lease가 provider timeout + 30초보다 긴지 확인한다.
3. dispatch batch/retry 설정을 동일하게 한다.
4. fake provider isolated DB에서 worker 2개 경쟁 test를 통과한다.
5. 한 instance씩 추가하고 Processing/stale/provider count를 관찰한다.

## 15. Worker 중단·재시작

Graceful shutdown을 우선한다. 처리 중 cancellation은 Processing을 유지하며 lease 만료 후 다른 worker가 회수한다. 종료 직후 DB 상태를 terminal로 수동 변경하지 않는다.

## 16. Lease 설정 변경

SMTP timeout과 HTTP ceiling을 확인한다. `ClaimLeaseSeconds`는 최대 provider timeout + 30초보다 커야 한다. 짧은 값은 startup을 실패시키는 것이 정상이다. 변경은 코드 리뷰와 crash/concurrency test를 거친다.

## 17. Candidate 실행

1. 5094/5192와 candidate session이 비어 있는지 확인한다.
2. 전용 `emi_qms_e2e_*` database와 tmpfs container/network를 생성한다.
3. actual provider와 dispatch를 비활성화한다.
4. 0001~0028 migration과 synthetic seed만 적용한다.
5. backend 5094, HTTPS frontend 5192를 strict port로 시작한다.
6. health, migration 28/latest 0028, UI, browser output guard를 확인한다.
7. 사용자 검수 전 candidate 전용 자원만 유지할 수 있다.

## 18. UAT migration handover 전 주의

Persistent UAT는 현재 0028 이전 schema다. 본 branch runtime을 연결하지 않는다. TASK-UAT-HANDOVER-003 승인 전 migration 실행, runtime 교체, worker 추가를 금지한다.

## 19. 장애 대응

- claim 0: due/handling/retry limit 확인
- stale 증가: worker health/DB/lease 설정 확인
- claim-lost 증가: instance clock과 긴 provider latency 확인
- ambiguous attempt: provider 운영 기록과 audit를 함께 확인
- duplicate 의심: attempt 번호와 provider-start 시각만 비식별 aggregate로 보고

## 20. Rollback/forward-fix

병합 전 candidate 중지와 task branch 폐기로 rollback한다. Persistent UAT는 변경되지 않는다. 0028 적용 후 문제는 schema row 삭제나 down migration 대신 worker 중단, 이전 호환 runtime 판단, 승인된 forward-fix를 사용한다.

## 21. 보안·개인정보

실제 사용자명, 이메일, 알림 본문, provider payload/response, claim token, worker raw value를 출력하지 않는다. Browser는 screenshot/DOM 원문 없이 boolean/count/fixed enum만 기록한다.

## 22. 금지사항

- Persistent UAT에서 임의 migration/DDL/DML
- Processing row의 수동 상태 변경
- live claim token 복사·공유
- 실제 provider를 candidate에서 활성화
- retry로 exactly-once를 가정
- 기존 migration 수정 또는 0028 번호 변경

## 23. 사용자 검수 체크리스트

- [x] Candidate 5192 접속
- [x] Pending/Processing/Sent/Failed 구분
- [x] Processing count/filter와 disabled action
- [x] attempt 번호/outcome/history
- [x] stale lease 안내
- [x] normal concurrency provider 1회
- [x] late completion fencing
- [x] at-least-once 제한과 exactly-once 미보장
- [x] Persistent UAT 0028 미적용
- [x] actual provider call 0
- [x] desktop/390px 오류·overflow 0
- [x] TASK-UAT-HANDOVER-003 선행 확인
- [x] 신규 기능 No-Go 확인

사용자 검수 완료, PR #30 squash merge 승인, 미체크 항목 0이다. Persistent UAT 적용은 다음 TASK-UAT-HANDOVER-003에서만 수행한다.

## 24. 변경 이력

| 일자 | 변경 |
| --- | --- |
| 2026-07-11 | TASK-NOTIFY-REL-001 최초 작성, claim/lease·attempt audit·candidate 절차 반영 |
| 2026-07-11 | 사용자 검수 완료와 PR #30 병합 승인 반영, 다음 절차를 TASK-UAT-HANDOVER-003으로 확정 |
