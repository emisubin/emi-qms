# TASK-NOTIFY-REPROCESS-001 — Terminal Failed 알림 수동 재처리 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 현재 `experiment/*` branch에서 terminal `Failed` notification delivery를 관리자가 안전하게 새 수동 retry cycle로 재처리하는 기능의 interview source of truth다. 사용자-facing interview와 중간 승인은 생략하고 Fable 2-pass 권장안을 자동 채택한다. 실제 provider·Persistent UAT·대표 repo·GitHub `main`·push·PR·merge는 제외한다.

## Task Identity Gate

- proposedTaskId: `TASK-NOTIFY-REPROCESS-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-NOTIFY-AUDIT-001`
- roadmapNextGate: `TASK-NOTIFY-AUDIT-001`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-NOTIFY-REPROCESS-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `terminal Failed delivery 수동 재처리`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 자동 retry를 모두 소진하거나 영구 실패한 delivery를 System Administrator가 중복 위험을 확인하고 사유를 남긴 뒤 새 수동 retry generation으로 재처리한다.
- Root Finding 또는 정책 결정: `TASK-NOTIFY-004`는 `Failed`를 terminal로 유지하고 단순 Failed→Pending 전이를 금지했으며, generation·append-only action·원본 lineage·duplicate-risk 확인을 갖춘 별도 NEW_FEATURE로 분리했다.
- 변경·검증 경계: additive generation/audit schema, terminal eligibility·CAS/lock, admin API·단일/선택 UX, 기존 worker claim/attempt lineage의 generation 지원, fake/dry-run provider 동시성·crash ambiguity·desktop/390px 검증을 포함한다.
- 보존할 불변조건: 기존 attempt·terminal failure 불변, 자동 retry limit 의미 보존, Processing/Sent/Suppressed/Disabled/DryRunSent 재처리 금지, actual provider exactly-once 표현 금지, 관리자·ReviewSafe server fence, 실제 provider call 0, `main`·Persistent UAT 불변.
- 예상 산출물: Fable 2-pass planning, Codex review, additive migration/API/UI/worker/tests, desktop/mobile screenshot, Implementation report와 5종 종료 산출물, local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR·Issue

PR #44와 `TASK-NOTIFY-004`는 이 기능을 구현하지 않고 별도 NEW_FEATURE로 보류한 정책 source다. 현재 Backend의 retry endpoint는 `Pending`만 다루며 같은 목적 구현·branch·PR·Issue는 0건이다.

## 사용자 실행 지시

- 요청일: 2026-07-20
- 요청: 남은 작업 1번과 3번을 한꺼번에 진행한다.
- 순서 override: 감사 UI 다음의 첨부 storage보다 terminal Failed 재처리를 먼저 같은 배치에 포함한다.
- 게시 경계: local experiment commit만 승인. main merge 승인 `0/3`.

## 1. 업무 문제와 기대 결과

- 현재 자동 retry가 끝난 `Failed`는 원인·attempt를 확인하고 acknowledge/dismiss할 수 있지만 다시 처리할 수 없다.
- 단순 status reset은 exhausted attempt count와 unique attempt 번호를 깨고 provider 성공 후 DB 실패 ambiguity에서 중복 발송을 숨긴다.
- 성공 결과는 관리자가 실패 원인·기존 attempt·중복 가능성을 보고 명시 확인과 사유를 남긴 뒤 새 generation을 시작하며, 원본과 모든 generation을 한 상세 화면에서 추적하는 것이다.

## 2. 확정된 Repository 계약

- external delivery는 at-least-once이며 provider 성공 후 DB completion 전 crash는 중복 가능성이 있다.
- 기존 automatic retry, retryable/permanent 분류, claim/lease, fencing, stale recovery와 attempt outcome은 유지한다.
- 기존 `/retry`는 Pending의 `next_attempt_at_utc`만 앞당기며 Failed를 받지 않는다.
- terminal `Failed`만 수동 재처리 후보이고 Processing·Sent·Suppressed·Disabled·DryRunSent·Pending은 제외한다.
- 재처리는 기존 attempt를 삭제·번호 초기화하지 않고 generation lineage와 append-only 관리자 action을 남겨야 한다.
- System Administrator와 기존 ReviewSafe mutation 차단을 최종 서버 경계로 사용한다.
- 실제 provider는 호출하지 않고 isolated fake/dry-run으로 worker 결과를 검증한다.

## 3. Fable이 권장할 비차단 선택

| 번호 | 결정 대상 | 비교 경계 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- |
| 1 | generation 모델 | 동일 delivery generation vs replacement delivery lineage | Fable 권장안 자동 채택 | No |
| 2 | 중복 위험 확인 | 항상 확인 vs provider-call-started/ambiguity 상태별 확인 | Fable 권장안 자동 채택 | No |
| 3 | 반복 제한 | 무제한+감사, generation 상한, 기간별 제한 | Fable 권장안 자동 채택 | No |
| 4 | 단일/선택 처리 | detail 단일, 선택 batch, 원자적 전체 실패 vs 항목별 결과 | Fable 권장안 자동 채택 | No |
| 5 | reason·note | 필수 사유 길이·표준 사유+자유 note | Fable 권장안 자동 채택 | No |
| 6 | admin handling | 재처리 때 Open reset·이전 처리 snapshot·현재 handling 관계 | Fable 권장안 자동 채택 | No |
| 7 | 실패 원인 eligibility | permanent 설정 오류·수신자 오류도 허용할지와 안내 | Fable 권장안 자동 채택 | No |

## 4. 정상·예외·복구 흐름

- 정상: 실패 목록/상세 → 기존 attempt·최종 원인·중복 위험 확인 → 실패 건 선택 → 확인 checkbox+필수 사유 → 새 generation 생성 → Pending → 기존 worker가 fake/dry-run 처리 → 상세에서 generation별 결과 확인.
- 동시성: 같은 Failed generation을 두 관리자가 동시에 재처리하면 하나만 성공하고 다른 요청은 stable conflict로 종료한다.
- stale selection: 선택 뒤 다른 관리자가 재처리하거나 상태가 바뀌면 전체 또는 항목 결과 정책에 따라 명확히 실패하고 silent skip하지 않는다.
- 복구: 새 generation도 Failed가 되면 기존 generation과 event를 보존하고 허용 반복 정책 안에서 다시 명시 action을 수행한다.
- 금지 상태: Processing·Sent·Pending·Suppressed·Disabled·DryRunSent는 수동 재처리하지 않는다.

## 5. Data·동시성·감사

- candidate: delivery의 current generation, attempt row generation, append-only reprocess event(actor/time/reason/prior status/prior final error code/duplicate-risk acknowledgement/result generation).
- attempt의 기존 provider-call-start·outcome·error·provider message id는 변경하지 않는다.
- transaction에서 delivery row를 lock하고 expected generation·Failed 상태·claim null을 재검증한 뒤 새 generation을 만든다.
- retry budget은 generation별로 적용하되 전체 과거 시도 수는 attempt 원장에서 계산 가능해야 한다.
- event와 상태 전이는 한 transaction이며 provider 호출은 transaction 밖 기존 worker만 수행한다.
- actual provider 결과를 exactly-once로 표현하거나 duplicate-safe라고 오도하지 않는다.

## 6. API·권한·UX

- 별도 reprocess endpoint를 사용하고 기존 Pending retry endpoint 의미를 넓히지 않는다.
- 요청은 선택 delivery와 각 expected generation, 필수 reason, duplicate-risk acknowledgement를 받는다.
- 일반 역할·익명·비활성 사용자는 서버에서 차단하고 UI 숨김만 신뢰하지 않는다.
- Desktop 실패 tab과 상세에 action을 제공하고 attempt table을 generation 단위로 읽기 쉽게 묶는다.
- 모바일은 실패 원인·마지막 attempt·generation·중복 위험·단일 재처리 중심으로 단순화하며 대량 action은 PC 우선 권장안을 검토한다.
- loading·validation·conflict·부분/전체 결과·중복 submit·focus·`aria-live`·390px overflow 0을 포함한다.

## 7. 포함·제외

### 포함

- terminal Failed의 안전한 수동 retry generation
- append-only reprocess audit와 generation별 attempt lineage
- 관리자 단일/선택 UI와 상세 projection
- concurrency·stale·retry limit·fake provider·ReviewSafe·desktop/390px 검증

### 제외

- Sent 재발송, notification 내용 편집·수신자 변경, provider별 취소/조회 API
- 실제 메일/Teams 발송, exactly-once 보장, 운영 queue 일괄 replay
- 기존 자동 retry 정책·채널 matrix 변경
- 대표 repo·`main`·Persistent UAT·push·PR·merge

## 8. 성공 기준

- 기존 Failed와 attempt history를 보존하며 새 generation이 원자적으로 Pending이 된다.
- 같은 generation 동시 재처리는 한 번만 성공하고 stale 요청은 conflict다.
- 관리자 action은 actor·사유·중복 위험 확인·원본/새 generation lineage로 추적된다.
- 기존 automatic retry·claim/lease/fencing과 상태별 관리자 action 회귀가 없다.
- 실제 provider 호출 0인 isolated E2E에서 새 generation의 dry-run/fake 결과가 상세에 표시된다.
- desktop/390px UX, Backend/Frontend/migration/전체 회귀와 screenshot이 통과한다.

## 9. Fable 확인용 요약

- 단순 Failed→Pending이 아니라 generation 기반 신규 능력이다.
- append-only history, duplicate-risk acknowledgement, admin-only, worker 재사용이 핵심이다.
- actual provider·Persistent UAT는 제외한다.
- `interviewStatus: COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`.
