# TASK-GOV-FINDING-GATE-001 Planning — 전체 P0/P1/P2 재평가와 신규 기능 Go/No-Go 준비

## 1. 상태와 Task 분류

- Task ID: `TASK-GOV-FINDING-GATE-001`
- Task 유형: `DOCS_GOVERNANCE`
- 기획 경로: Codex-only, Fable 5 미사용
- instructionChainRead: true
- 기준 main: `197f1d0b02f0`
- branch: `fix/task-gov-history-rewrite-001-support-closure`
- planningApproved: true
- implementationApproved: true
- publishingApproved: true
- runtimeMutationApproved: false
- persistentUatWriteApproved: false
- newFeatureGoApproved: false
- 현재 실행 Gate: `READ_ONLY_REASSESSMENT_COMPLETE_USER_DECISION_PENDING`

이 Task는 남은 P0/P1/P2 Finding을 실제 main과 운영 기준선에서 다시 분류하고, 신규 기능 planning으로 넘어갈 수 있는지 사용자 결정 자료를 만드는 문서·검증 Task다. 제품 기능을 추가하지 않으므로 `NEW_FEATURE`가 아니며 Fable 5를 호출하지 않는다.

## 2. 다음 Task 선정 근거

Product Roadmap Phase 0의 `TASK-NOTIFY-004`는 PR #44로 정책 정정·사용자 검수·merge를 완료했다. 선행 순서 0.3인 `TASK-GOV-HISTORY-REWRITE-001`도 Support closure 조건을 충족했으므로 전체 P0/P1/P2 재평가를 최종 실행한다.

현재 확인된 history rewrite 상태:

- Published ref rewrite와 fresh-clone 검증: 완료
- Repository visibility: `PRIVATE`
- GitHub cached reference: `REMOVED`
- GitHub Support internal reference removal·repository GC: 완료
- History Task 문서 5종과 Roadmap 변경: 현재 bounded worktree에서 미게시 보존 중
- Public 재개와 encrypted backup 삭제: 별도 승인 대상. History·Finding 문서 게시·merge는 승인 완료
- 기존 `GIT_HISTORY_PERSONAL_DATA_REMAINS`: strict closure 조건 충족으로 Resolved

따라서 read-only Finding inventory와 운영 기준선을 처음부터 다시 확인하고, Open P0/P1/P2가 0이면 신규 기능 Gate를 사용자 결정 단계로 넘긴다. 기존 history backup·old clone·runtime과 다른 worktree는 수정·이전·정리하지 않는다.

사용자는 2026-07-15 Support 회신 확인 뒤 rewrite001을 닫고 finding001의 남은 P2를 해소하도록 승인했다. 이 승인은 read-only 조사와 두 Task 문서 작성에 한정하며 게시·merge와 신규 기능 시작 승인이 아니다.

## 3. 목표

다음을 하나의 privacy-safe closure matrix로 확정한다.

1. 현재 main과 활성 WIP에 남은 P0/P1/P2/P3 목록
2. 각 Finding의 실제 해결, risk acceptance, 외부 blocker와 후속 Task 상태
3. runtime·Persistent UAT·GitHub 운영 기준선의 변경 여부
4. 완료된 과거 Finding과 현재 Open Finding의 구분
5. 신규 기능 개발 `GO`, `CONDITIONAL_GO` 또는 `NO_GO` 권고
6. 사용자 승인 뒤 시작할 첫 신규 기능 Task와 선행 정책

본 Task가 자동으로 신규 기능 개발을 승인하지 않는다. 최종 Go/No-Go는 검증 결과를 보고한 뒤 사용자가 별도로 결정한다.

## 4. 실제 기준선

### 4.1 Git·GitHub

- local HEAD = origin/main: `197f1d0b02f0`
- Remediation PR #43·#44와 최근 PR #48·#49: origin/main 포함
- 동일 목적 canonical Task: 1, 현재 bounded closure worktree 재사용
- Repository visibility: `PRIVATE`
- 게시 시점 Open PR: PR #50 단독. GitHub app connector는 설치 범위 제한으로 unavailable이며 인증된 CLI fixed projection을 사용
- `TASK-GOV-HISTORY-REWRITE-001` 5종 산출물: 현재 worktree에서 closure 갱신

### 4.2 최근 P2 처리 상태

| Finding 또는 Gate | 현재 확인 상태 | 본 Task 처리 |
| --- | --- | --- |
| `GIT_HISTORY_PERSONAL_DATA_REMAINS` | Resolved — Support internal reference 제거·GC, cached reference `REMOVED` | Closed 근거 확인 |
| `FULL_STACK_E2E_PROCUREMENT_EDIT_ROW_RACE` | PR #43에서 수정·검증·merge 완료 | Closed 근거 확인 |
| `FAILED_RETRY_DOCUMENTATION_DRIFT` | PR #44에서 정책 정정·검증·merge 완료 | Closed 근거 확인 |
| 기존 import-order 위반 9건 | origin/main에서 정규화 완료 | Resolved 근거 확인 |
| Auth break-glass 미증명 | Persistent live auth mutation No-Go 제한 | 운영 제한과 P2 Finding 여부를 분리 판정 |

History Support 결과는 completion/follow-up/closed `1/1/1`과 old cached reference `REMOVED` fixed projection으로 재확인했다. 실제 ticket·account·commit 원문은 기록하지 않았다.

## 5. 보호할 불변조건

- P0/P1이 하나라도 Open이면 완료·게시·신규 기능 Go를 금지한다.
- P2는 해결하거나 canonical 종료 정책의 risk acceptance 필수 필드를 모두 갖춘 경우에만 닫는다.
- Historical 문서에 남은 과거 `P2` 문자열을 현재 Open Finding으로 자동 집계하지 않는다.
- 반대로 merge 완료나 Task 문서 존재만으로 Finding을 Closed 처리하지 않는다.
- Runtime·Persistent UAT·provider·GitHub setting을 이 Task에서 변경하지 않는다.
- Persistent UAT는 필요한 경우 SELECT와 안전한 metadata 조회만 수행한다.
- Encrypted backup, old clone, 다른 branch·worktree를 변경·삭제하지 않는다. History closure 문서는 사용자의 명시 승인 범위에서만 현재 worktree에 반영한다.
- 실제 개인정보, Git author·committer, raw GitHub metadata, DB row와 browser 원문을 출력하지 않는다.
- 신규 기능 Go가 나더라도 `TASK-007A` 구현을 바로 시작하지 않는다.

## 6. 실행 단계

### Phase A — 선행 history Gate 확인

다음을 모두 확인한다.

- `TASK-GOV-HISTORY-REWRITE-001`의 published ref·fresh clone 검증 결과
- GitHub cached reference 상태: `REMOVED`, `SUPPORT_PENDING`, `SUPPORT_REJECTED`, `UNKNOWN`
- Repository visibility와 public 재개 승인 상태
- old clone push quarantine와 fresh clone 사용 상태
- encrypted backup 보존·삭제 승인 상태
- history Task 5종 산출물과 사용자 검수·게시 상태
- `GIT_HISTORY_PERSONAL_DATA_REMAINS`의 Closed 또는 canonical risk acceptance 근거

확인 결과는 `REMOVED`이며 strict close 조건을 충족했다. 문서 게시·merge는 승인됐지만 public 재개와 backup 삭제는 여전히 각각 별도 승인이다.

### Phase B — Repository Finding inventory

다음을 current main 기준으로 대조한다.

- Roadmap Phase 0, 추적 목록과 Decision Log
- Task·Implementation report·SOP·User manual의 Open Finding
- merge된 PR #34~#49의 changed-file·CI·merge 상태
- `TODO`, `FIXME`, 임시 bypass, disabled validation과 known debt
- migration catalog, dependency audit와 표준 CI 상태
- 기존 P2 후속 Task가 실제로 merge됐는지 여부
- 동일 Finding이 다른 이름으로 중복 추적되는지 여부

각 후보를 `OPEN`, `RESOLVED`, `RISK_ACCEPTED`, `P3_BACKLOG`, `NOT_A_FINDING`, `UNKNOWN` 중 하나로 분류하고 근거 문서를 연결한다.

### Phase C — 운영 read-only 기준선

실행 승인을 받은 경우에만 현재 운영 상태를 read-only로 확인한다.

- Development·Review-safe health와 source relation
- PostgreSQL health·restart count와 persistent volume identity
- migration ledger canonical/live/approved legacy `28/29/1`
- Pending/Processing aggregate
- canonical active administrator aggregate
- unknown writer와 active write transaction
- provider-call-start delta
- Repository visibility와 open PR/check 상태

Runtime 종료·재시작, DB write, provider 호출과 synthetic data 생성은 수행하지 않는다. 이 Task가 제품 runtime의 장시간 안정성을 다시 인증하는 UAT Task는 아니다.

### Phase D — Finding closure matrix

각 Finding에 다음 필드를 고정한다.

- findingId
- severity
- status
- directlyObserved
- sourceOfTruth
- resolutionOrAcceptance
- riskOwner
- mitigation
- reviewTrigger
- followUpTask
- blocksNewFeature

Risk acceptance에서 risk owner, 근거, 영향, 완화책, 재검토 시점 또는 후속 Task가 하나라도 빠지면 P2는 Open으로 유지한다.

### Phase E — 신규 기능 Go/No-Go 결정 자료

권고 enum은 다음으로 제한한다.

- `GO`: Open P0/P1/P2 0, 운영 기준선 정상, 선행 Gate 완료
- `CONDITIONAL_GO`: P2가 canonical risk acceptance로 닫혔지만 재검토 조건이 존재
- `NO_GO`: Open P0/P1/P2, UNKNOWN 근거, 운영 이상 또는 선행 Gate 미완료

`GO` 또는 `CONDITIONAL_GO`도 사용자 승인을 대신하지 않는다. 사용자가 승인하면 다음 신규 기능 후보는 Roadmap의 `TASK-007A Pending List`다. 향후 `NEW_FEATURE`는 별도 deep-interview 완료와 사용자 요약 확인을 먼저 거친 뒤 Fable 5 planning과 Codex review를 시작한다.

## 7. History Support 결과별 선택지

| 선택 | 조건 | 장점 | 위험·제한 | 권고 |
| --- | --- | --- | --- | --- |
| A. 제거 확인 뒤 strict close | Cached reference `REMOVED`, fresh 검증 PASS | 가장 명확한 P2 종료 | Support 처리 대기 가능 | **권장** |
| B. Residual risk acceptance | Support 거절·부분 처리, 필수 acceptance 필드와 private/public 정책 승인 | 외부 한계 속에서 Gate 진행 가능 | 과거 cache 잔존과 재검토 의무 | 별도 `POLICY_DECISION` 필요 |
| C. Private 유지·Gate 보류 | Support 상태 불명확 또는 검증 불충분 | 노출 표면 최소화 | 신규 기능 No-Go 지속 | 근거 부족 시 기본값 |

본 planning의 기본 권장안은 A다. B를 선택하려면 본 Task 구현 승인과 별도로 risk owner의 명시적 정책 승인이 필요하다.

## 8. 포함 범위

- 실제 main·PR·Task·Roadmap·Finding 상태 read-only 재조사
- History Support/cache 상태의 privacy-safe projection
- 완료된 P2와 Open P2의 closure matrix
- Runtime·Persistent UAT read-only aggregate 확인
- 전체 신규 기능 Go/No-Go 권고
- Roadmap Phase 0과 추적 상태 동기화
- Task 종료 5종 산출물과 사용자 검수 checklist
- 독립 Codex read-only 검증

## 9. 제외 범위

- 승인 범위 밖 `TASK-GOV-HISTORY-REWRITE-001` 실행 자원·backup·old clone 수정
- Repository public/private 전환
- Git history rewrite·force push·Support 요청
- Encrypted backup restore·삭제
- Branch·worktree·candidate·old clone 정리
- Runtime 종료·재시작·handover
- Persistent UAT write·migration·provider 호출
- P2 source code 수정
- `TASK-007A` 또는 다른 신규 기능 planning·구현

새 P2 source 결함이 발견되면 이 문서 Task 안에서 수정하지 않고 별도 Codex-only remediation Task를 계획한다.

## 10. 예상 영향 파일

이번 연속 closure의 fixed allowlist:

- `tasks/gov-finding-gate-001-planning.md`
- `tasks/gov-finding-gate-001.md`
- `tasks/gov-finding-gate-001-implementation-report.md`
- `tasks/gov-finding-gate-001-sop.md`
- `tasks/gov-finding-gate-001-user-manual.md`
- `docs/00-product-roadmap.md`

선행 history closure 5개 파일은 같은 사용자 승인 묶음에서 별도 allowlist로 추적한다.

Backend, Frontend, migration, dependency, lockfile, script와 runtime 설정은 허용하지 않는다.

## 11. 검증 계획

- task worktree HEAD = origin/main과 생성 시점 기준선 일치
- 동일 목적 canonical Task 1과 current bounded worktree
- History backup·old clone·다른 worktree 상태 불변
- PR #34~#49 origin/main 포함과 merge-time fixed-field projection
- Finding source 문서와 실제 merge 결과 대조
- P0/P1/P2/P3 severity·status 일관성 검사
- Risk acceptance 필수 필드 completeness 검사
- `git diff --check`
- Markdown local link·anchor·중복 heading 검사
- Secret·PII·raw metadata 후보 검사
- Changed-file allowlist와 삭제 파일 검사
- Backend·Frontend·migration·dependency·script diff 0
- Persistent UAT before/after read-only aggregate 불변
- Development·Review-safe process ownership·health 불변
- PostgreSQL restart delta 0
- 독립 Codex read-only 검증

Backend·Frontend·Full-Stack E2E 재실행은 문서-only 변경 기준 `N/A`가 원칙이다. 다만 현재 CI 상태와 최근 P2 remediation의 최신 head CI는 fixed projection으로 확인한다. 코드 또는 runtime 영향이 발견되면 문서 Task를 중단하고 별도 Task로 분리한다.

## 12. 완료 기준

- 모든 현재 Finding 후보가 closure matrix에 한 번만 존재
- Open P0/P1/P2 count가 실제 근거와 일치
- Historical P2 언급과 현재 Open P2 오분류 0
- `FULL_STACK_E2E_PROCUREMENT_EDIT_ROW_RACE`와 `FAILED_RETRY_DOCUMENTATION_DRIFT`의 Closed 근거 확인
- `GIT_HISTORY_PERSONAL_DATA_REMAINS`의 실제 상태를 과장 없이 판정
- Runtime·Persistent UAT mutation 0
- 5종 산출물 위치·상태 추적 가능
- 독립 Codex 검증 PASS
- 사용자 검수 완료 상태 반영
- 신규 기능 Go/No-Go는 사용자 별도 승인 대기

## 13. 중단 조건

- History rewrite WIP 또는 Support 상태를 privacy-safe하게 확인할 수 없음
- `GIT_HISTORY_PERSONAL_DATA_REMAINS`가 Open인데 GO 판정이 필요함
- P0/P1 또는 새로운 P2 발견
- Finding 해결에 source·migration·runtime 변경이 필요함
- Persistent UAT write 또는 실제 provider 호출이 필요함
- Runtime·PostgreSQL 이상 또는 unknown writer 발견
- Risk acceptance 필수 필드를 확정할 수 없음
- 실제 개인정보·secret·raw GitHub/DB/browser metadata 출력 필요
- 기존 history WIP를 수정해야만 본 Task가 진행 가능함

중단 시 현재 근거와 blocker만 보고하고 신규 기능 planning, Git 게시와 환경 정리를 수행하지 않는다.

## 14. 사용자 승인 필요 항목

본 planning 검수 뒤 다음을 별도로 승인해야 한다.

1. History P2가 닫힌 뒤 Phase A~E read-only 재평가 실행
2. Runtime·Persistent UAT read-only aggregate 조회
3. 5종 산출물과 Roadmap 작성
4. 독립 Codex 검증
5. 성공 후 commit·push·PR·squash merge
6. 최종 신규 기능 `GO`, `CONDITIONAL_GO` 또는 `NO_GO` 결정

사용자는 2026-07-15에 1~5의 조사·문서·게시·squash merge 범위를 승인했다. 신규 기능 Go 결정은 별도 승인해야 한다.

## 15. 현재 판정과 다음 순서

- Planning 작성: 완료
- Planning 사용자 승인: 완료
- `TASK-GOV-HISTORY-REWRITE-001`: Support strict closure 조건 충족, history P2 Resolved
- 본 Task 실행: read-only 재평가 완료 / Open P0/P1/P2 `0/0/0`
- 신규 기능 개발: `GO_FOR_USER_DECISION` — 아직 시작 승인 아님
- 독립 Codex 검증: PASS, blockers 0, merge Gate GO
- 사용자 checklist: 완료 / 문서 게시·squash merge 승인
- 다음 즉시 작업: 승인된 문서 commit·push·PR·squash merge
- 그 다음 작업: 신규 기능 Go/No-Go 별도 결정
- 첫 신규 기능 후보: 사용자 Go 승인 뒤 `TASK-007A` deep-interview → 사용자 요약 확인 → Fable 5 planning

## 16. Task 이름 정합성

- Canonical Task ID: `TASK-GOV-FINDING-GATE-001`
- Non-canonical shorthand: `TASK-GOV-P2-GATE-001`
- 동일 목적 여부: true
- 중복 Task 생성: false

`TASK-GOV-P2-GATE-001`은 Roadmap의 “전체 P0/P1/P2 재평가”라는 단계명을 Task ID처럼 임시 조합하면서 나온 잘못된 shorthand다. 실제 Repository에는 이미 canonical branch, worktree와 planning이 `TASK-GOV-FINDING-GATE-001`로 존재했다. 동일 목적 자원을 먼저 확인했어야 했으므로 이후 문서·보고·branch에서는 canonical ID만 사용한다.
