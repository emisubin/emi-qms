# TASK-NOTIFY-004 — Terminal Failed delivery 재처리 정책 정정

## 1. 상태

- Task 유형: `POLICY_DECISION`
- 기획: Codex-only
- 승인 정책: `POLICY_CORRECTION_AND_DEFER`
- planningApproved: true
- implementationApproved: true
- publishingApproved: true
- 자동 검증: 완료
- 독립 Codex 검증: PASS
- 사용자 검수: 대기 / Draft PR 게시 승인
- Persistent UAT·runtime 적용: 대상 아님

## 2. 목적

Notification delivery의 automatic retry, terminal `Failed`와 관리자 action 계약을 실제 코드에 맞춘다. 존재하지 않는 Failed 수동 retry 절차를 운영 문서에서 제거하고, 수동 재처리를 현재 P2의 필수 보정이 아닌 별도 신규 기능 후보로 분리한다.

## 3. 확인된 코드 사실

- 관리자 retry API와 UI는 `Pending`만 허용한다.
- retry action은 다음 시도 시각만 앞당기고 attempt count를 초기화하지 않는다.
- worker는 retry limit 미만인 Pending 또는 stale Processing만 claim한다.
- Failed는 permanent failure 또는 retry limit 소진 후 next attempt가 없는 terminal 상태다.
- attempt 번호는 delivery 안에서 unique이고 claim/provider/completion lineage를 보존한다.
- 기존 admin handling field는 최신 acknowledge/dismiss 상태를 저장하지만 append-only manual retry audit가 아니다.
- Provider 성공 후 DB completion 전 중단은 중복 가능 경계이므로 전달 보장은 at-least-once다.

## 4. 결정

Terminal Failed는 현재 상태 모델에서 수동 retry 대상이 아니다. 운영자는 오류 코드와 attempt history를 확인하고 acknowledge 또는 dismiss할 수 있다. Automatic retry 범위와 횟수는 기존 worker 정책을 따른다.

Failed 수동 재처리는 다음 이유로 현재 P2 범위에서 제외한다.

1. 기존 canonical 제품 계약은 자동 retry와 최종 실패 가시성까지 요구하며 수동 Failed retry를 완료 조건으로 두지 않았다.
2. Retry limit을 소진한 row는 상태만 Pending으로 바꿔도 claim되지 않는다.
3. Attempt count 초기화는 unique lineage와 누적 의미를 훼손한다.
4. 안전한 전체 재처리에는 manual retry generation, append-only admin action과 duplicate-risk acknowledgement가 필요하다.

향후 업무 필요성이 확인되면 `NEW_FEATURE`로 재분류하고 Fable 5 planning, Codex review와 사용자 승인을 새로 거친다.

## 5. 보호할 불변조건

- Failed/attempt history를 삭제하거나 덮어쓰지 않는다.
- Processing row에 관리자 retry, acknowledge 또는 dismiss를 수행하지 않는다.
- Pending retry의 attempt count와 retry limit 의미를 바꾸지 않는다.
- 같은 notification·recipient·channel·type delivery를 중복 생성하지 않는다.
- 외부 발송을 exactly-once라고 표현하지 않는다.
- 실제 provider payload, recipient와 notification 원문을 증빙에 사용하지 않는다.
- 기존 migration 0001~0028을 수정하지 않는다.

## 6. 포함 범위

- `FAILED_RETRY_DOCUMENTATION_DRIFT` 정정
- Terminal Failed와 Pending retry 운영 의미 정리
- TASK-NOTIFY-004 Roadmap·tracking·Decision Log 상태 동기화
- Task 종료 5종 산출물과 사용자 검수 checklist
- 문서 전용 validation

## 7. 제외 범위

- Backend·Frontend source와 tests
- Failed→Pending 또는 replacement delivery 상태 전이
- API·schema·migration·dependency·runtime configuration
- Actual provider call과 synthetic/Persistent delivery 생성
- Persistent UAT write와 runtime handover
- TASK-NOTIFY-005 또는 다른 신규 기능 시작

## 8. Finding

| Finding | Severity | 판정 | 처리 |
| --- | --- | --- | --- |
| `FAILED_RETRY_DOCUMENTATION_DRIFT` | P2 | SOP가 구현되지 않은 Failed retry 절차를 가리킴 | 이번 Task에서 정책 정정 |

신규 runtime·data integrity Finding은 없다.

## 9. 완료 기준

- 존재하지 않는 Failed retry 절차 표현 0
- Automatic retry와 terminal Failed 설명이 실제 코드와 일치
- Backend·Frontend·migration·runtime diff 0
- Persistent UAT write와 actual provider call 0
- 5종 산출물 상태·위치 추적 가능
- 독립 Codex 검증 PASS와 사용자 검수 대기 상태를 분리

## 10. 5종 산출물 상태

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | [Implementation report](notify-004-implementation-report.md) | 작성 완료 |
| SOP | [SOP](notify-004-sop.md) | 작성 완료 |
| User manual | [User manual](notify-004-user-manual.md) | 작성 완료 |
| Roadmap update | [Product Roadmap](../docs/00-product-roadmap.md) | 반영 완료 |
| User validation checklist | 이 문서 11장 | 사용자 검수 대기 |

## 11. 사용자 검수 체크리스트

- [ ] Terminal Failed가 automatic retry 종료 상태임을 확인
- [ ] Pending 재발송과 Failed 수동 재처리가 다름을 확인
- [ ] Failed는 현재 acknowledge/dismiss만 지원함을 확인
- [ ] 수동 Failed 재처리는 현재 P2가 아닌 별도 신규 기능 후보임을 확인
- [ ] Attempt history와 at-least-once 제한이 유지됨을 확인
- [ ] Backend·Frontend·migration·runtime 변경 0을 확인
- [ ] Persistent UAT write와 actual provider call 0을 확인
- [ ] Implementation report, SOP와 User manual을 검수

사용자 검수 전 Ready 전환, merge와 완료 판정을 수행하지 않는다.
