# TASK-NOTIFY-004 Planning — Terminal Failed delivery 재처리 범위 결정

## 1. 상태와 Task 분류

- Task ID: `TASK-NOTIFY-004`
- Task 유형: `POLICY_DECISION`
- 기획 경로: Codex-only, Fable 5 미사용
- instructionChainRead: true
- 기준 main: `ae3d9289d3ee`
- branch: `fix/task-notify-004-failed-reprocessing-policy`
- worktree alias: `notify-004`
- 적용 지침: Root `AGENTS.md`; 후속 대안의 영향 검토에는 Backend·Frontend `AGENTS.md` 적용
- planningApproved: true
- implementationApproved: true
- publishingApproved: true
- userValidationCompleted: true
- mergeApproved: true
- 권장 정책: `POLICY_CORRECTION_AND_DEFER`
- approvedPolicy: `POLICY_CORRECTION_AND_DEFER`
- approvalDate: `2026-07-14`
- Persistent UAT·runtime 변경 승인: false

이 문서는 terminal `Failed` delivery 수동 재처리가 기존 P2의 필수 보정인지, 별도 운영 편의 기능인지 결정하기 위한 계획이다. 사용자 승인 전 Backend, Frontend, migration, 기존 Task 산출물, runtime과 Persistent UAT를 변경하지 않는다.

## 2. 다음 Task 선정 근거

Product Roadmap의 Phase 0에서 `TASK-GOV-HISTORY-REWRITE-001`이 `TASK-NOTIFY-004`보다 앞선다. 다만 history rewrite는 동일 목적의 보존 WIP가 이미 존재하고 외부 cache 확인이 남아 있어 이번 planning worktree에서 이어받거나 수정할 수 없다.

따라서 현재 독립 실행 가능한 다음 항목은 `TASK-NOTIFY-004` 잔여 범위다. 이 선택은 history rewrite Task를 완료 또는 폐기한 것으로 간주하지 않으며, 해당 WIP·branch·worktree와 외부 blocker를 그대로 보존한다.

## 3. 확인된 제품 계약과 코드 사실

### 3.1 이미 완료된 계약

- claim/lease, fencing과 `Processing` 소유권 보호
- automatic retry와 retry limit
- retryable/non-retryable failure 분류
- attempt lineage와 provider-call-start audit
- stale lease recovery
- 관리자 Pending/Processing/Failed/Sent 조회
- Pending의 다음 시도 시각 앞당기기
- 실패·대기 확인 및 목록 제외
- escalation fair ordering과 후보 오류 격리

외부 전달은 at-least-once이며 exactly-once가 아니다. Provider 성공 후 DB completion 전 process가 중단되면 다음 시도에서 중복 발송될 가능성이 남는다.

### 3.2 현재 retry 동작

- 관리자 retry endpoint와 store는 `Pending`만 허용한다.
- retry action은 `next_attempt_at_utc`를 현재 시각으로 앞당기며 `attempt_count`를 재설정하지 않는다.
- worker claim은 `Pending` 또는 stale `Processing` 중 `attempt_count < retry limit`인 delivery만 선택한다.
- attempt 번호는 delivery 안에서 unique이고 기존 attempt는 보존된다.
- Frontend의 선택 재발송 action은 선택 항목이 모두 `Pending`일 때만 활성화된다.
- `Failed`는 permanent failure 또는 retry limit 소진 후 next attempt가 없는 terminal 상태다.

### 3.3 단순 Failed → Pending 변경이 안전하지 않은 이유

Retry limit을 소진한 `Failed`를 상태만 `Pending`으로 바꾸면 `attempt_count < retry limit` 조건을 만족하지 않아 worker가 claim하지 않는다. `attempt_count`를 0으로 초기화하면 다음 claim에서 기존 attempt 번호 unique 계약과 충돌하고 누적 시도 의미도 훼손한다.

기존 admin handling column은 최신 처리 상태와 note를 보존하지만 append-only action history가 아니다. 따라서 terminal Failed 재처리를 반복 허용하면 누가 어떤 위험을 확인하고 몇 번째 수동 cycle을 시작했는지 기존 schema만으로 완전하게 추적할 수 없다.

새 replacement delivery를 만들면 notification/channel/type unique 계약과 dedupe 계약을 다시 설계해야 한다. 같은 delivery를 재사용하면 별도 manual retry generation 또는 retry budget이 필요하다. 어느 방식도 현재 Pending retry endpoint를 단순 확장하는 최소 수정이 아니다.

## 4. 확인된 문서 drift

Roadmap의 기존 제품 정책은 자동 retry 후 최종 실패를 관리자 페이지에서 확인할 수 있어야 한다고 정의한다. 현재 구현은 이 계약을 충족한다.

반면 `TASK-NOTIFY-REL-001` SOP의 permanent failure 절차에는 “승인된 관리자 retry 절차”가 있다고 적혀 있지만 실제 Backend와 UI에는 Failed retry가 없다. 이는 제품 결함의 증명이 아니라 운영 문서가 구현된 계약보다 앞서간 표현이다.

Finding:

- ID: `FAILED_RETRY_DOCUMENTATION_DRIFT`
- Severity: `P2`
- directlyObserved: true
- productRuntimeDefect: false
- dataIntegrityDefect: false
- recommendedResolution: `POLICY_CORRECTION_AND_DEFER`

## 5. 보호할 불변조건

- Automatic retry와 retry limit 의미를 변경하지 않는다.
- `FailedPermanent`와 retry 소진 이력을 덮어쓰거나 삭제하지 않는다.
- Attempt 번호, outcome과 provider-call-start audit를 append-only로 보존한다.
- Processing row는 관리자 action 대상이 아니다.
- 관리자 권한과 ReviewSafe mutation 차단을 유지한다.
- 동일 notification·recipient·channel·delivery type의 중복 row를 만들지 않는다.
- Provider 성공 여부가 불확실한 attempt를 안전한 미발송으로 간주하지 않는다.
- 실제 외부 발송을 exactly-once라고 표현하지 않는다.
- 관리자 수동 action은 actor, 시각, 이유와 결과를 추적할 수 있어야 한다.
- 실제 provider 호출 없이 isolated fake/no-op 환경에서 검증할 수 있어야 한다.
- 기존 migration 0001~0028을 수정하지 않는다.

## 6. 정책 대안 비교

| 대안 | 의미 | P2 종료 | 코드·schema 영향 | 중복 위험 | 운영 효용 | 판정 |
| --- | --- | --- | --- | --- | --- | --- |
| A. `POLICY_CORRECTION_AND_DEFER` | Failed를 terminal로 유지하고 문서 drift를 정정한다. 수동 재처리는 별도 신규 기능으로만 재검토한다. | 가능 | 문서만 | 현재보다 증가 없음 | 자동 retry·확인/제외 유지 | **권장** |
| B. `RESTRICTED_SAFE_RETRY` | provider ambiguity가 없고 retry budget이 남은 일부 Failed만 같은 delivery로 재처리한다. | 부분 | Backend·Frontend·tests, audit 보강 검토 | 분류 오류 시 증가 | 제한적 | 별도 NEW_FEATURE planning 필요 |
| C. `FULL_MANUAL_REPROCESSING` | retry 소진·permanent failure를 포함해 관리자 승인으로 새 manual cycle을 시작한다. | 기능 구현 후 가능 | additive migration 가능성 높음, API·UI·UAT 필요 | 명시적 확인·audit 없으면 높음 | 높음 | 별도 NEW_FEATURE planning 필요 |
| D. `BROAD_IN_PLACE_REQUEUE` | 모든 Failed를 기존 Pending endpoint로 바로 되돌린다. | 불가 | 겉보기에는 작지만 계약 파손 | 높음 | 불안정 | 폐기 |

### 6.1 Option A — Policy correction and defer

유지하는 계약:

- 자동 retry가 허용 범위 안에서 수행된다.
- terminal Failed는 관리자가 원인과 attempt를 확인하고 acknowledge/dismiss한다.
- 원본 Failed와 attempt audit는 변경되지 않는다.

정정하는 계약:

- 존재하지 않는 “승인된 Failed retry 절차” 표현을 제거한다.
- terminal Failed 수동 재처리는 현재 P2 필수 범위가 아니라 별도 운영 편의 기능 후보로 분리한다.
- 필요성이 확인되면 `NEW_FEATURE`로 재분류하고 Fable 5 planning → Codex review → 사용자 승인 순서를 새로 거친다.

### 6.2 Option B — Restricted safe retry

가능한 후보는 최신 terminal attempt가 확정 실패이고, provider 성공 ambiguity가 없으며, 현재 retry budget이 남은 경우다. 하지만 non-retryable 분류를 관리자가 임의로 뒤집는 정책, append-only admin action audit와 configuration 수정 완료 확인이 필요하다.

Retry limit 소진 건은 처리하지 못하므로 “terminal Failed 재처리” 전체를 해결하지 않는다. 구현한다면 현재 endpoint의 의미를 넓히지 않고 별도 API와 명시적 UI 안내를 사용하는 편이 안전하다.

### 6.3 Option C — Full manual reprocessing

Retry cycle generation, 원본과 수동 cycle lineage, append-only admin action, duplicate-risk acknowledgement, per-channel eligibility와 반복 제한이 필요하다. 기존 attempt 번호를 초기화하거나 삭제하지 않는다.

Additive migration과 API/UI 상태 전이가 예상되므로 기존 P2 보정이 아니라 새로운 사용자 능력이다. 선택 시 현재 planning을 구현 계약으로 사용하지 않고 `NEW_FEATURE` 절차로 다시 기획한다.

### 6.4 Option D — Broad in-place requeue

Retry-limit claim 조건, attempt unique, terminal 의미와 provider ambiguity를 동시에 위반할 수 있어 채택하지 않는다.

## 7. 권장안과 결정 근거

권장안은 **Option A — `POLICY_CORRECTION_AND_DEFER`**다.

근거:

1. Canonical 제품 정책은 최종 실패의 관리자 가시성까지 요구하며 Failed 수동 재처리를 완료 조건으로 두지 않았다.
2. Automatic retry, claim/lease와 attempt lineage는 이미 구현·UAT 완료됐다.
3. 현재 결함은 runtime failure가 아니라 SOP가 존재하지 않는 retry 절차를 가리키는 문서 drift다.
4. 모든 terminal Failed를 안전하게 다시 처리하려면 retry generation·append-only audit·duplicate confirmation이라는 신규 계약이 필요하다.
5. 실제 provider의 중복 가능성을 단순 UI action으로 숨기는 것보다 terminal 이력을 보존하는 편이 안전하다.

Option A 승인 시 `TASK-NOTIFY-004`는 문서 정책 정정 Task로 끝내고 Phase 0 P2 gate에서 제거한다. 수동 Failed 재처리는 운영 필요성과 사용자 승인이 생길 때 별도 `NEW_FEATURE` 후보로 Deferred한다.

## 8. Option A 승인 시 포함 범위

- `FAILED_RETRY_DOCUMENTATION_DRIFT` 정정
- terminal Failed가 현재는 수동 retry 대상이 아님을 Task·SOP·User manual에 명시
- automatic retry, retry limit, acknowledge/dismiss와 at-least-once 계약 재확인
- Roadmap의 TASK-NOTIFY-004 상태를 정책 결정 완료로 갱신
- tracking item 50·74의 상태와 후속 신규 기능 경계 정정
- 기존 Decision Log 행을 수정·삭제하지 않고 정책 결정 행 추가
- Task 종료 5종 산출물과 사용자 검수 체크리스트 작성
- 독립 Codex read-only 검증

## 9. Option A 승인 시 제외 범위

- Backend·Frontend source와 test 변경
- Failed→Pending 또는 새 delivery 상태 전이
- API endpoint·response contract 변경
- migration·schema·index 변경
- retry count·worker·provider configuration 변경
- synthetic 또는 Persistent notification/delivery 생성
- Persistent UAT write와 actual provider 호출
- runtime 종료·재시작·handover
- `TASK-NOTIFY-005` 또는 다른 신규 기능 시작
- history rewrite WIP·branch·worktree 변경

## 10. 영향 파일 계획

Option A의 예상 변경 파일:

- `docs/00-product-roadmap.md`
- `tasks/notify-004-planning.md`
- `tasks/notify-004.md`
- `tasks/notify-004-implementation-report.md`
- `tasks/notify-004-sop.md`
- `tasks/notify-004-user-manual.md`
- `tasks/notify-rel-001-sop.md`

5종 종료 산출물은 implementation report, SOP, User manual, Roadmap update와 Task 문서의 user validation checklist로 추적한다. 파일 수를 5개로 억지로 제한하지 않고, 실제 drift가 있는 기존 SOP를 함께 정정한다.

Option B 또는 C를 선택하면 위 문서 목록은 구현 allowlist가 아니다. 별도 NEW_FEATURE planning과 Codex review에서 Backend, Frontend, migration과 test 영향 파일을 다시 확정한다.

## 11. 검증 계획

### 11.1 Option A 문서 정책 정정

- instruction chain 재확인
- `git diff --check`
- Markdown heading duplicate, local link와 anchor 검사
- secret/PII scan
- changed-file allowlist 확인
- Backend·Frontend·migration·dependency·script diff 0
- 기존 Decision Log 행 수정·삭제 0
- terminal Failed, Pending retry와 at-least-once 표현 일관성 검사
- 사용자 검수 미체크 상태 분리
- 독립 Codex read-only 검증

문서 전용이므로 Backend, Frontend와 Full-Stack E2E는 `N/A`로 둘 수 있다. 실제 코드 diff가 생기면 Option A 범위를 위반한 것이므로 중단한다.

### 11.2 Option B 또는 C 후속 기능

- isolated PostgreSQL과 fake/no-op provider
- permanent·retry-exhausted·ambiguous failure 분리
- provider-start 전/후 crash와 timeout
- 동일 row·서로 다른 관리자 동시 action
- retry generation과 attempt 번호 연속성
- duplicate notification/delivery/provider call 검사
- authorization 403과 ReviewSafe 423
- Backend 전체, Frontend 전체와 Full-Stack E2E
- migration이 있으면 existing/fresh apply와 forward-fix
- Persistent UAT controlled handover 별도 승인

## 12. 성공 기준

Option A 성공 기준:

- Failed 수동 재처리가 현재 구현된 기능처럼 표현된 문서 0
- automatic retry와 terminal Failed 의미가 코드와 일치
- runtime product defect로 잘못 분류된 P2 0
- Backend·Frontend·migration·runtime 변경 0
- Persistent UAT write와 actual provider call 0
- 5종 산출물 상태와 위치 추적 가능
- 신규 P0/P1/P2 0
- 사용자 검수 완료 전 Draft 상태 유지

## 13. 중단 조건

- 실제 코드에 이미 Failed retry 경로가 추가돼 있음
- Failed row가 현재 worker에 의해 retry limit 밖에서 claim될 수 있음
- 자동 retry 또는 attempt lineage 자체의 결함 발견
- provider error classification이 기존 확정 정책과 불일치
- Option A를 위해 source·schema 변경이 필요함
- history rewrite WIP 또는 다른 dirty worktree 변경 필요
- Persistent UAT write 또는 actual provider 호출 필요
- secret/PII/raw notification metadata 노출 필요
- 신규 P0/P1/P2 발견

## 14. 사용자 결정 항목

다음 중 하나를 승인해야 한다.

1. **Option A — `POLICY_CORRECTION_AND_DEFER` (권장)**: 현재 terminal Failed를 유지하고 문서 drift만 정정한다. P2 gate를 닫고 수동 재처리는 별도 신규 기능으로 미룬다.
2. Option B — `RESTRICTED_SAFE_RETRY`: 일부 확정 실패만 재처리하는 신규 기능 planning을 시작한다. 모든 Failed를 해결하지는 않는다.
3. Option C — `FULL_MANUAL_REPROCESSING`: migration 가능성을 포함한 전체 신규 기능 planning을 시작한다.

사용자는 2026-07-14 Option A와 Draft 게시를 승인한 뒤 5종 산출물 검수를 완료하고 PR #44의 Ready 전환과 squash merge를 승인했다. Backend·Frontend·migration·runtime 변경은 승인 범위에 포함되지 않는다.
