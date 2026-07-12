# TASK-NOTIFY-ESC-001 SOP

## 1. 문서 목적

운영자가 escalation starvation 보정 상태를 개인정보 안전 방식으로 확인하고 controlled UAT 적용 여부를 판단하는 절차다.

## 2. 적용 범위

Candidate ordering, evaluation watermark, 후보 오류 격리, 중복 억제와 worker poll 관찰에 적용한다. 업무 정책이나 실제 provider smoke 절차가 아니다.

## 3. 사전 조건

- Repository instruction chain과 validation matrix 확인
- isolated `emi_qms_e2e_*` PostgreSQL 사용
- actual provider와 `.env.notify-local` 미사용
- Persistent UAT escalation worker disabled 확인

## 4. Starvation 지표

다음 count/boolean만 기록한다.

- eligible count
- evaluated count
- unique evaluated count
- tail evaluated
- polls until tail evaluated
- candidate failure count
- duplicate escalation/notification/delivery count

실제 업무명, 사용자, recipient, 프로젝트와 식별자는 출력하지 않는다.

## 5. Fair ordering 확인

미평가·due 변경·inactive 후보가 active evaluated 후보보다 먼저 오는지 확인한다. 기존 후보는 가장 오래 평가되지 않은 순서여야 한다. Due date, created time이 같을 때 work item stable key로 결과가 결정적이어야 한다.

## 6. Evaluation watermark 확인

실제 평가가 끝난 active row의 `updated_at_utc`만 순환 기준으로 사용한다. 가짜 history row, 사용자-facing 업무 수정 시각과 별도 cursor를 만들지 않는다.

## 7. 경계 fixture

99, 100, 101, 200, 201건을 각각 독립 DB에서 검증한다. 101은 2 poll, 200은 2 poll, 201은 3 poll 이내 모든 unique 후보가 평가돼야 한다.

## 8. 상태·recipient 혼합

Due date null, Completed, Cancelled는 제외한다. Active/inactive recipient와 recipient 0명, L0~L3 혼합은 기존 정책 결과를 유지하면서 tail 진행을 막지 않아야 한다.

## 9. 후보 오류 확인

Task-owned synthetic downstream failure를 한 후보에만 주입한다. 같은 poll의 뒤쪽 후보가 계속 평가되고 failure code/count만 기록되는지 확인한다. 실제 exception 원문과 식별자는 출력하지 않는다.

## 10. Cancellation 확인

요청 cancellation은 후보 오류로 소비하지 않고 즉시 호출자에게 전파돼야 한다.

## 11. 재시작 확인

첫 poll 뒤 service를 다시 구성한다. In-memory cursor 없이 Persistent watermark만으로 다음 후보가 진행되는지 확인한다.

## 12. 동시 worker 확인

두 evaluator를 동시에 실행하고 모든 후보의 유한 진행과 escalation, notification, delivery duplicate 0을 확인한다. Actual provider는 호출하지 않는다.

## 13. Query plan 확인

Synthetic 대량 후보에서 `EXPLAIN ANALYZE`를 수행한다. LIMIT 100, bounded execution과 반환 row 100을 확인한다. Index/migration 필요성이 발견되면 본 Task 적용을 중단한다.

## 14. 기존 정책 회귀

BusinessDayCalculator 전체, L0~L3, recipient fallback, notification idempotency와 delivery conflict tests를 실행한다.

## 15. Persistent UAT 사전 점검

Read-only로 ledger, Pending/Processing, active escalation, provider-call-start, aggregate, PostgreSQL health/restart와 listener ownership을 확인한다. Worker를 활성화하거나 data를 만들지 않는다.

## 16. Controlled UAT 전 gate

- 사용자 검수와 PR merge 완료
- migration/API/config 변경 0
- Pending/Processing 0/0
- provider fake/no-op
- duplicate baseline 0
- rollback runtime 정보 확보

하나라도 충족하지 않으면 worker를 활성화하지 않는다.

## 17. Poll 관찰

Worker 활성화는 별도 승인 Task에서만 수행한다. 최소 101건 synthetic candidate로 두 poll 이내 tail 진행, failure count와 duplicate 0을 fixed projection으로 확인한다.

## 18. 장애 대응

Tail 미평가, 중복, policy regression 또는 설명되지 않는 DB 변화가 있으면 worker를 disabled로 유지한다. Data 삭제, retry, provider 호출과 Persistent restore를 임의 수행하지 않는다.

## 19. Rollback

Runtime code를 직전 승인 버전으로 되돌리고 escalation worker disabled 상태를 유지한다. Schema rollback은 없다. Persistent UAT 변경이 의심되면 read-only evidence를 보존하고 사용자 결정을 받는다.

## 20. 개인정보 안전 출력

Boolean, integer, fixed enum과 route alias만 보고한다. Raw DB row, API body, DOM, screenshot, console 원문, 실제 이름과 식별자는 금지한다.

## 21. 금지사항

- Persistent UAT synthetic row 생성
- 실제 Teams/Mail/Channel provider 호출
- migration 추가·수정
- 업무/recipient 정책 변경
- claim/lease를 exactly-once로 표현
- 사용자 승인 전 Ready 전환·merge·worker 활성화

## 22. 사용자 검수 체크리스트

- [ ] 101/200/201 tail 진행 결과 확인
- [ ] 후보 오류 뒤 같은 poll 진행 확인
- [ ] cancellation 전파 확인
- [ ] L0~L3와 recipient 정책 불변 확인
- [ ] 동시 evaluator duplicate 0 확인
- [ ] query plan이 migration 없이 수용 가능함 확인
- [ ] Persistent UAT와 runtime 무변경 확인
- [ ] controlled UAT가 별도 승인임을 확인

## 23. 변경 이력

| 일자 | 변경 |
| --- | --- |
| 2026-07-12 | TASK-NOTIFY-ESC-001 구현·자동 검증 및 사용자 검수 대기 절차 작성 |
