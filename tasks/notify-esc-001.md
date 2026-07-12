# TASK-NOTIFY-ESC-001 — Escalation candidate starvation 보정

## 1. 목적

고정된 첫 100건이 반복 선택돼 101번째 이후 업무가 평가되지 않는 P2와 후보 한 건의 오류가 poll 전체를 중단하는 P2를 최소 변경으로 해결한다.

## 2. 배경

기존 worker는 open work item을 `due_date`, `created_at_utc` 순으로 100건만 읽은 뒤 level 판정, 기존 처리 여부와 recipient resolution을 수행했다. 이미 평가된 선두 후보도 다음 poll의 같은 window를 점유했고 후보 처리 예외는 worker poll 경계까지 전파됐다.

## 3. Findings

- `ESCALATION_FIXED_BATCH_STARVATION`: 처리 이력 필터가 limit 뒤에 있어 고정 선두 window가 반복됐다.
- `ESCALATION_CANDIDATE_FAILURE_ABORTS_POLL`: 후보별 예외 격리가 없어 뒤쪽 후보가 평가되지 않았다.
- 심각도: P2
- 제품 데이터 손상과 actual provider 호출은 확인되지 않았다.

## 4. 선행 Task

TASK-CALENDAR-001, TASK-NOTIFY-002, TASK-NOTIFY-REL-001과 TASK-UAT-HANDOVER-003의 정책과 Persistent UAT 기준선을 유지한다.

## 5. 포함 범위

- 기존 escalation history를 재사용한 공정 순환 ordering
- 결정적인 total ordering과 stable work item key
- 후보별 오류 격리와 cancellation 전파
- 99/100/101/200/201건, 오류, 재시작과 동시 evaluator 통합 검증
- 5종 산출물과 Draft PR

## 6. 제외 범위

Migration, schema, API, frontend, runtime configuration, L0~L3 정책, recipient 정책, batch size, claim/lease 도입과 Persistent UAT 적용은 제외한다.

## 7. Root cause

기존 query는 history를 join했지만 ordering에는 사용하지 않았다. 같은 level이 이미 처리된 후보도 limit 이전에 제외되지 않았고 total order에 work item ID가 없었다. Service loop에는 후보별 예외 경계가 없었다.

## 8. Fair ordering

한 work item당 unique인 `work_item_escalations` row를 scalar watermark로 사용한다.

1. 이력이 없거나 due date가 바뀌었거나 inactive인 후보
2. active 후보 중 `updated_at_utc`가 가장 오래된 후보
3. `due_date`, `created_at_utc`, `work_item_id`

Batch size 100은 유지한다. 가짜 escalation row나 새 감사 의미를 만들지 않는다.

## 9. Watermark 의미

`UpsertActiveEscalationAsync`는 기존부터 실제 평가가 끝날 때마다 `updated_at_utc`를 갱신했다. 이번 Task는 그 기존 평가 시각을 후보 선택에 사용하며 사용자-facing 업무 수정 시각을 바꾸지 않는다.

## 10. 후보 오류 격리

각 후보는 독립 `try/catch` 경계에서 처리된다. 요청 cancellation은 즉시 다시 전파하고, 다른 오류는 stable failure code와 실패 건수만 기록한 뒤 같은 poll의 다음 후보를 계속 처리한다.

## 11. 기존 계약 보존

- BusinessDayCalculator와 L0~L3 판정 순서 유지
- due date null, Completed, Cancelled 제외
- recipient fallback 유지
- escalation, notification, recipient, delivery 중복 억제 유지
- 외부 전달은 at-least-once이며 exactly-once가 아님

## 12. 검증 결과

- 99/100건: 1 poll
- 101/200건: 2 poll 이내
- 201건: 3 poll 이내
- 처리 이력 선두 100건 뒤 tail: 다음 poll 전에 우선 평가
- 후보 오류 뒤 tail: 같은 poll에서 평가
- service 재생성: persistent watermark로 계속 진행
- 동시 evaluator: escalation·notification·delivery 중복 0
- actual provider call 0

## 13. Query plan

Isolated PostgreSQL의 synthetic 20,000건과 history 10,000건에서 LIMIT 100, top-N sort가 적용됐고 실행 시간은 약 48ms였다. 기존 index와 bounded batch로 수용 가능하여 migration을 추가하지 않았다.

## 14. Persistent UAT 보호

Persistent UAT는 read-only aggregate만 확인했다. Ledger 28/29/1, Pending/Processing 0/0, active escalation 0, provider-call-start count, PostgreSQL restart와 runtime PID가 기준선과 같았다. Escalation worker는 계속 disabled다.

## 15. 제한사항

실패 후보는 다음 poll에서 다시 평가될 수 있다. 오류 원인이 지속되면 stable failure count로 운영자가 확인해야 한다. 이 Task는 candidate claim/lease를 추가하지 않는다.

## 16. Rollback

두 backend 파일 변경을 되돌리면 기존 selection과 poll 동작으로 복귀한다. Migration과 data rollback은 필요 없다. Controlled UAT 적용 전에는 Persistent UAT runtime을 갱신하지 않는다.

## 17. 후속 Task

1. 사용자 검수와 Draft PR merge 결정
2. 별도 승인에 따른 controlled UAT 적용과 escalation worker 활성 검증
3. TASK-AUTH-HARDEN-001

## 18. 5종 산출물 상태

| 산출물 | 상태 |
| --- | --- |
| Task 정의 | 작성 완료 |
| Implementation report | 작성 완료 |
| SOP | 작성 완료 |
| User manual | 작성 완료 |
| Roadmap update | 반영 완료 |

현재 상태: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #34 병합 승인 / 미체크 항목 0.

## 19. 사용자 검수 체크리스트

- [x] 101번째 이후 후보도 유한한 poll 안에 평가됨을 이해
- [x] 99/100/101/200/201 경계 결과 확인
- [x] 후보 한 건의 오류가 나머지 후보를 중단하지 않음 확인
- [x] cancellation은 즉시 전파됨 확인
- [x] L0~L3 날짜 정책이 그대로임을 확인
- [x] recipient 정책이 그대로임을 확인
- [x] escalation·notification·delivery 중복 0 확인
- [x] 화면/API 변경 없음 확인
- [x] Persistent UAT와 기존 runtime 무변경 확인
- [x] actual provider 호출 0 확인
- [x] at-least-once 제한 이해
- [x] 다음 controlled UAT 적용은 별도 승인임을 확인
