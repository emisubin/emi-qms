# TASK-GOV-CODEX-002 — Fable 5 신규 기능 기획과 Codex-only 작업 라우터

## 1. 상태

- Task 유형: `DOCS_GOVERNANCE`
- 구현: 완료
- 자동 검증: 완료
- 사용자 검수: 완료
- 게시·merge: PR #38 squash merge 승인
- Fable 5 호출: 초기 Task 0 / Change 007 USER-FLOW interview Round 1·2 실제 호출 2 / Change 008 Round 3 검증 호출 2
- Change 001: Fable 5 deep-interview+planning Gate 보정·자동 검증·사용자 검수 완료 / merge 승인
- Change 002: Task Identity+Roadmap Sequence Gate 보정·자동 검증·사용자 검수 완료 / merge 승인
- Change 003: 단일 canonical clone lifecycle 구현·local clean worktree 정리·자동 검증·사용자 검수 완료 / merge 승인
- Change 004: merged temporary worktree `5→2` 정리·canonical root 정규화·5174 Entra frontend-only handover·자동·독립 재검증·사용자 검수 완료 / 게시·merge 승인
- Change 005: public main required-PR 최소 ruleset 적용·운영 문서 P2 보정·독립 검증·사용자 검수 완료 / P2·P3 Resolved / 게시·merge 승인
- Change 006: GitHub 최상위 폴더 `6→3→2` 보존 통합·exact audit·controlled maintenance·최종 삭제·자동·독립 검증·사용자 검수 완료 / PR #52 squash merge 완료
- Change 007: Fable 5 `fable` CLI alias·읽기 전용 runner·runner 전용 project rule 구현 / 대표 branch 선별 통합·자동 검증·사용자 검수 완료 / 독립 재검증 뒤 merge 승인
- Change 008: Task-scoped private session·drift guard·질문 최대 5개·exact cleanup 구현 / USER-FLOW Round 3와 planning true-resume 성능 검증·대표 branch 통합·사용자 검수 완료 / session cleanup은 USER-FLOW closure로 이관
- Change 009: Fable interview 원문·승인된 기획 전문 direct write와 GPT-5.6 SOL 사후 review 계약 / USER-FLOW 전문·review 실제 검증·대표 branch 통합·사용자 검수 완료 / 독립 재검증 뒤 merge 승인
- Change 010: Fable primary draft 1회·Codex 내용 review 1회·자동 revise 금지 계약 / USER-FLOW 내용 review·자동·독립 검증·대표 branch 통합·사용자 검수 완료 / 독립 재검증 뒤 merge 승인
- Change 011: 대표 5174 branch-following 운영 보정 / 구현·자동 검증·대표 branch 통합·사용자 검수 완료 / 독립 재검증 뒤 merge 승인
- Change 012: Fable 정책·USER-FLOW WIP 선별 이식과 대표·디자인 2-worktree 정규화 / 로컬 보존·결과 커밋·일반 worktree 제거·자동 검증·사용자 검수 완료 / 독립 재검증 뒤 merge 승인
- Change 013: Generic primary draft와 USER-FLOW compatibility redraft 분리·exact target 승인 gate·Reporting 상태 충돌 보정 / 구현·자동 검증·사용자 검수 완료 / 독립 재검증 뒤 merge 승인

## 2. 목표

신규 기능만 Fable 5 기획 단계로 보내고, 기존 기능의 수정·보강·UAT·문서·정리·정책 결정은 Codex-only 조사와 승인 흐름으로 처리한다. 기획, 검토, 구현, 독립 검증과 사용자 승인 경계를 Repository 지침으로 고정한다.

## 3. 기준선

- PR #32가 Root·Backend·Frontend·Scripts 지침, 종료 정책, validation matrix, privacy-safe evidence와 project-local Rules를 main에 이관했다.
- Root `AGENTS.md`에는 Task 유형별 Fable/Codex 라우터가 없었다.
- Fable 전용 `CLAUDE.md`는 main에 없었다.
- 기존 root worktree에 다른 장문 초안이 있으나 과거 UAT WIP와 혼재되어 있고 canonical main 지침을 통째로 대체하므로 본 Task 범위에서 수정·정리하지 않는다.

## 4. 확정 라우팅

- `NEW_FEATURE`: Fable 5 deep-interview → 사용자 요약 확인 → Fable 5 primary draft 전문 1회 → Codex 내용·제품 방향 review 1회 → 사용자 승인 → 새 Codex 구현 → 분리된 Codex 검증 → 사용자 게시·merge 승인
- `APPROVED_FEATURE_IMPLEMENTATION`: Fable 재호출 없이 Codex-only 구현
- `BUGFIX`, `P2_REMEDIATION`, `SECURITY_HARDENING`, `UAT_RUNTIME`, `DOCS_GOVERNANCE`, `HOUSEKEEPING`, `POLICY_DECISION`: Codex 조사 → 사용자 승인 → 새 Codex 구현 → 분리된 Codex 검증 → 사용자 게시·merge 승인
- Codex-only 조사에서 신규 제품 능력이 필요해지면 `NEW_FEATURE`, 기존 범위의 정책 선택이면 `POLICY_DECISION`으로 재분류하고 중단한다.

## 5. 포함 범위

- Root `AGENTS.md` Task 유형 라우터
- 신규 기능, Codex-only, 수정 요청과 세션 분리 규칙
- Fable 5 read-only 호출 경계
- Fable 5 deep-interview와 사용자 확인 Gate
- 신규 기능 interview template와 artifact 경로
- Fable 전용 `CLAUDE.md`
- planning·review·change·implementation report 역할 구분
- Task 종료 산출물 상태와 Roadmap 추적
- 대표 instruction-chain dry run
- 같은 목적 Task semantic identity와 Roadmap Sequence Gate
- Fixed projection template과 기존 canonical Task 재사용
- 단일 canonical clone 재사용, bounded runtime·temporary worktree와 cleanup gate
- 대표·디자인 두 폴더의 local 최상위 구조와 불명확한 checkout의 선감사·후삭제 경계
- Fable 5 전용 fail-closed runner와 runner prefix만 허용하는 project-local Rule
- Fable 질문 원문 artifact, 승인된 전문 `draft|revise`와 Codex 사후 review 책임 분리
- Codex review 뒤 자동 Fable revise를 금지하고 사용자 명시적 redraft 요청만 허용하는 단일-pass 계약
- 대표 clone 현재 branch를 따르는 HTTPS 5174 Vite 유지·조건부 재시작 경계

## 6. 제외 범위

- Backend·Frontend·migration·dependency·runtime·Persistent UAT 변경
- 실제 신규 기능 기획 또는 구현
- Change 008 Round 3 검증을 넘는 실제 Fable 5 기획 호출
- 기존 dirty root worktree, 실행 중 runtime worktree와 historical branch 정리
- Fable runner 전용 allow 이외 project-local Rules 완화
- 사용자 승인 전 Ready 전환과 merge

## 7. 보존할 불변조건

- main 직접 작업·push 금지, Task별 branch와 필요한 runtime 격리
- Persistent UAT, migration, provider와 runtime 승인 경계
- P0/P1/P2 Finding gate
- 개인정보·secret과 fixed projection
- 명시적 allowlist staging
- 자동 검증과 사용자 검수 상태 분리
- Task 종료 5종 산출물 추적

## 8. 운영 SOP

1. Instruction chain과 Roadmap 실행 큐를 읽고 현재 `Next Gate`를 확인한다.
2. 요청의 goal·Finding·변경 경계·불변조건·산출물로 purpose identity를 만들고 기존 Task·branch·worktree·PR을 검색한다.
3. Task Identity Gate가 `PASS_REUSE`면 기존 Task의 다음 change를 사용하고, `PASS_CREATE`일 때만 새 Task ID와 자원을 만든다.
4. Roadmap 순서가 다르거나 같은 목적 후보가 모호하면 사용자 재정렬 결정 전 중단한다.
5. 요청의 실제 의미와 Repository 상태를 읽고 `taskType`을 선택한다.
6. `NEW_FEATURE`면 Fable 5가 서로 관련된 질문을 1~5개씩 작성하고 선택지·영향·권장안을 설명한다. 질문 수를 채우기 위한 비차단·무관 질문은 추가하지 않는다.
7. Codex는 질문을 사용자에게 전달하고 답변을 `tasks/<task-id>-interview.md`에 의미 변경 없이 기록한다.
8. Fable 5가 누적 답변을 읽어 추가 질문 또는 확인용 요약을 작성한다.
9. 사용자가 Fable 요약을 확인하고 blocking decision이 0일 때만 Fable 5 planning을 시작한다.
10. Fable 호출은 `bash scripts/run-fable-readonly.sh`만 사용한다. Script가 `fable` Fable 5 alias, read-only option, fixed Task path, 승인 상태, Task session 소유권·drift, private output과 상태 contract를 보장할 수 없으면 중단한다.
11. Runner가 interview 질문 원문을 round artifact에 byte-for-byte로 기록하고 Codex는 이를 변경 없이 사용자에게 전달한다.
12. Fable은 사용자 확인이 끝난 interview를 바탕으로 primary draft 전문을 한 번 작성한다. 기본 target은 Task planning이며 사용자가 별도 개인·사용자-facing 문서를 primary draft로 승인하면 최신 change에 사용자 요청과 exact target을 기록하고 그 target 하나만 사용한다. 이 경로는 planning·preview를 중복 생성하지 않는다.
13. Codex는 별도 파일에서 개발 방향·사용자 가치·기능 필요성·누락·우선순위·과도한 범위와 trade-off를 중심으로 내용 review를 한 번 작성한다. Code 대조는 구현 가능성과 기존 계약 충돌을 확인하는 보조 근거다.
14. Codex review로 Fable revise나 추가 review를 자동 실행하지 않는다. 사용자가 새 전문을 명시적으로 요청한 경우에만 별도 change의 승인 marker와 exact target을 확인하고 Fable `revise`로 전문 전체를 교체한다. Runner는 approval change digest를 private receipt로 한 번만 소비하고 Task session cleanup 뒤에도 같은 승인 재사용을 차단한다. Generic 문서에는 업무별 H1·section·diagram·journey를 하드코딩하지 않는다.
15. 사용자 승인 전에 제품 구현으로 넘어가지 않는다.
16. 승인된 기능 구현과 모든 비신규 작업은 Codex-only 흐름을 사용한다.
17. 실질적 수정 요청은 `tasks/<task-id>-change-###.md`로 계약을 고정한다.
18. 구현과 독립 검증은 별도 Codex 세션을 기본으로 한다.
19. 사용자 검수와 게시·merge 승인을 분리한다.
20. 일반 Task는 clean한 canonical clone 하나에서 최신 `origin/main` 기준 branch를 만들고 Task별 영구 source 폴더를 추가하지 않는다. Open PR 또는 게시 승인 대기는 clean·reachable branch라면 전환을 막지 않고 중단·보류 상태로 추적한다.
21. HTTPS 5174는 canonical clone의 현재 branch를 따르게 두고 clean branch 전환 중 Vite를 유지한다. 전환 뒤 HMR 또는 full reload와 필수 route를 확인한다.
22. Env·dependency·Vite startup 설정 변경이나 자동 갱신 실패가 있을 때만 5174를 재시작한다.
23. 별도 worktree가 필요하면 purpose·process ownership·종료 시점·cleanup 경계를 먼저 기록한다. 5174가 실행 중이라는 사실만으로는 추가하지 않는다.
24. merge 뒤 clean·process 미사용·open PR 없음·commit reachable을 확인한 임시 worktree만 승인 범위에서 정리한다.

## 9. 사용자 안내

- 새 업무 흐름·화면·데이터 개념·외부 연동·권한 능력을 요청하면 Fable 5가 업무 맥락을 먼저 interview한다. 선택 사항은 쉬운 비교와 권장안을 제공한다.
- Codex는 질문·답변을 전달·기록하며 사용자가 interview 요약을 확인한 뒤 Fable planning과 Codex 검토 결과를 받는다.
- 버그, P2, 보안 보강, UAT, 문서, 정리와 기존 정책 선택은 Fable 없이 Codex가 조사한다.
- “승인된 기능을 구현하라”는 요청은 다시 기획하지 않는다.
- 조사·기획 결과를 승인하기 전에는 source 구현이 시작되지 않는다.
- 구현 뒤에도 독립 검증과 사용자 검수·merge 승인이 별도로 필요하다.
- 새 Task를 시작하기 전 기존 같은 목적의 Task와 Roadmap의 현재 순서를 대조한다. 같은 목적이면 기존 Task를 이어가고, 순서가 다르면 이유와 선택지를 먼저 안내한다.
- 일반 Task는 하나의 canonical clone을 재사용하므로 Task가 늘어도 source 폴더가 계속 늘어나지 않는다. Runtime 격리용 폴더는 실제 process가 사용하는 동안만 유지한다.
- Fable 5 질문은 Codex가 전용 읽기 전용 runner로 실행하므로 사용자가 round마다 terminal 명령을 대신 실행할 필요가 없다. Runner가 지원되지 않으면 안전 옵션을 완화하지 않고 stable failure를 보고한다.
- 같은 신규 기능 Task의 첫 Fable 호출은 전체 기준선을 읽고 후속 round는 변경이 없음을 runner가 확인한 private Task session을 재개한다. 질문·답변의 기준은 계속 interview 문서이며, Task가 끝나면 해당 Task session만 cleanup한다.
- Fable 질문은 원문 artifact 그대로 전달한다. Codex가 이해를 돕는 설명을 붙일 때도 원문과 분리하며 질문·선택지·권장안을 고쳐 쓰지 않는다.
- 승인된 Fable primary draft는 Fable stdout과 파일이 byte-identical한 원문이다. Runner는 existing target·symlink를 atomic no-overwrite로 보존한다. Codex는 원문을 고치지 않고 내용 review를 별도 작성하며 그 review로 기본 기획 작성 흐름을 끝낸다. 새 전문은 사용자가 명시적으로 요청한 경우에만 Fable이 한 번 다시 작성한다.
- 5174는 대표 폴더의 현재 branch 화면을 자동 반영한다. 일반 코드·문서 branch 전환마다 중단하지 않으며 env·dependency·Vite 기동 계약 변경 또는 자동 갱신 실패 때만 재시작한다.
- 미커밋 WIP는 자동 stash하거나 덮어쓰지 않는다. 다른 Task로 전환하려면 먼저 승인된 commit·push 또는 이름 있는 보존 경계와 재개 조건을 확정한다.

## 10. Rollback

`AGENTS.md`의 Task 라우터 section, `CLAUDE.md`, Fable runner와 runner 전용 project rule을 함께 revert한다. Code, DB, migration와 runtime rollback은 없다. 기존 PR #32 지침 구조는 변경하지 않는다.

## 11. Findings

- 신규 P0/P1/P2: 없음
- 기존 root 장문 초안: Change 006 exact audit에서 현재 canonical 지침으로 대체된 비채택 초안임을 확인하고 승인 범위에서 삭제
- `docs/task-close-process-guidelines`: remote에는 있으나 open PR 0인 historical branch다. Change 006에서 local preservation checkout만 삭제했고 remote branch는 변경하지 않았다.
- Change 003 cleanup: worktree 30→9, 약 4.03GB 회수. Dirty 3개와 process 사용 5개는 보존했다.
- Change 004 cleanup: PR #48·#49·#50의 clean inactive worktree를 제거해 `5→2`로 정리했다. Canonical root와 5176 디자인 실험 worktree만 보존하고 local·remote branch와 이름 있는 WIP stash는 삭제하지 않았다.
- Change 005 P3: Public default branch `main`에 active required-pull-request ruleset을 적용했다. 승인·CI·최신화·review 해결을 강제하지 않아 기존 1인 개발 속도를 유지하면서 direct main push 금지만 서버 측에서 강제한다.
- Change 005 P2: 독립 검증에서 발견한 History Rewrite SOP·User manual의 과거 private 상태 표기를 실제 public·required-PR 상태로 동기화해 Resolved했다.
- Change 006 cleanup: 먼저 GitHub 최상위 폴더를 대표·디자인·보존 3개로 통합하고 linked worktree 3개를 repair했다. 이후 dirty checkout 6개·local branch 32개·local 설정·artifact를 exact audit해 canonical 자료가 없음을 확인했다. Docker/PostgreSQL controlled maintenance로 stale handle `4→0`을 만든 뒤 보존 폴더를 영구 삭제해 최종 `6→3→2`로 정리했다. 동일 PostgreSQL container·persistent volume과 DB aggregate, 대표·디자인 runtime은 보존했고 사용자 검수와 PR #52 squash merge를 완료했다.
- Change 007 root Finding: private stdout/stderr redirect를 포함한 compound command가 generic shell-wrapper prompt와 일치했고 `never` approval policy에서 실행 전 거절됐다. Broad shell allow 없이 fail-closed runner prefix만 허용해 해소했다.
- Change 008 performance Finding: Round마다 전체 Repository 기준선을 다시 읽는 비영구 호출을 Task-scoped session·drift guard로 대체했다. Round 3 bootstrap·contract refresh model 시간은 129초·135초였고 첫 true-resume planning은 264초였다. Session 재개와 preflight 1초는 입증했지만 장문 planning 생성 때문에 총시간 단축은 입증하지 못했다.
- Change 009 authorship Finding: Fable stdout을 Codex가 검증·반영하는 기존 계약은 Fable이 질문과 전문의 실제 작성자여야 한다는 사용자 의도와 달랐다. Runner는 contract 위반 시 거부하거나 stdout을 그대로 운반할 뿐이며 Codex는 원문을 편집하지 않는다.
- Change 010 content-review Finding: 자동 review·revise 반복을 Fable primary draft 1회와 Codex 내용 review 1회로 종료하도록 바꾸고, revise는 별도 사용자 명시 요청이 있을 때만 허용한다.
- Change 011 P3 `CANONICAL_VITE_PROCESS_BRANCH_SWITCH_CONFLICT`: 대표 clone 재사용과 실행 중 5174 process 소유 금지 규칙의 충돌을 5174 branch-following·조건부 재시작 정책으로 해소했다.
- Change 011 P3 `PAUSED_WORKTREE_INTEGRATION_PENDING`: `RESOLVED`. Governance·USER-FLOW source WIP를 local preservation commit으로 고정하고 각각 결과 commit을 만든 뒤 두 임시 worktree를 force 없이 제거했다.
- Change 011 P3 `USER_FLOW_P3_001_IDENTITY_TRACE_GAP`: `RESOLVED`. USER-FLOW Change 003·Implementation report·Roadmap에서 과거 문서 작성 승인과 제품 구현·Fable redraft·게시 미승인을 stable identity로 분리했다.
- Change 012 Finding `WORKTREE_PROCESS_HANDLE_ACTIVE`: `RESOLVED`. USER-FLOW worktree의 terminal/Fable handle 때문에 첫 제거를 중단했고 사용자 terminal 종료 뒤 handle 0을 재확인한 후 일반 제거했다.
- Change 012: Governance preservation/result `4058849`/`a6232b2`, USER-FLOW preservation/result `1cc66fe`/`c4b2858`. 최종 worktree는 대표·디자인 `2/2`이며 강제 제거·runtime restart·branch 삭제·push·PR·merge는 실행하지 않았다.
- Change 013 P2 `FABLE_PRIMARY_DRAFT_MODE_CONTRACT_CONFLICT`: Generic `docs/` primary draft를 planning·review 구현 승인과 분리하고 latest change의 사용자 요청·exact target으로 gate한다. USER-FLOW 전용 H1·metadata·review path는 exact historical redraft 조합으로 한정하고 generic 계약에서 업무별 구조를 제거했다. 독립 재검증 대기다.
- Change 013 P2 `REPORTING_CHANGE001_COMPLETION_STATE_CONFLICT`: Reporting Implementation report의 최초 Task 완료와 Change 001 현재 상태를 분리하고 사용자 검수·조건부 merge 승인 상태를 Task·Roadmap과 정렬했다. 독립 재검증 대기다.
- Change 013 P2 `ROADMAP_CURRENT_STATE_CONFLICT`: 실행 큐·Decision Log와 충돌하던 USER-FLOW 상세·추적 87·88의 과거 미승인 current state를 Governance merge 선행·redraft/문서 merge 승인·제품 구현 미승인으로 정렬했다. 독립 재검증 대기다.

## 12. 5종 산출물 상태

| 산출물 | Canonical 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/gov-codex-002-implementation-report.md` | Change 007~013 통합·P2 보정·자동 검증 완료 / 독립 재검증 대기 |
| SOP | 이 문서 8장 | 작성됨 |
| User manual | 이 문서 9장 | 작성됨 |
| Roadmap update | `docs/00-product-roadmap.md` | Change 007~013와 대표·디자인 2-worktree 운영 반영 완료 / 독립 재검증 뒤 게시 승인 |
| User validation checklist | 이 문서 13장 | Change 001~013 사용자 검수·merge 승인 / Change 013 독립 재검증 대기 |

## 13. 사용자 검수 체크리스트

- [x] `NEW_FEATURE`만 Fable 5로 라우팅되는지 확인
- [x] 승인된 기능 구현과 BUGFIX/P2/SECURITY/UAT/DOCS/HOUSEKEEPING/POLICY가 Codex-only인지 확인
- [x] Fable이 Repository 파일과 Codex workflow를 재귀 실행하지 않는지 확인
- [x] planning·review·change·implementation report 역할이 구분되는지 확인
- [x] 사용자 승인 전 구현, Ready 전환과 merge가 금지되는지 확인
- [x] 기존 Repository 안전·Finding·5종 산출물 규칙이 유지되는지 확인
- [x] `NEW_FEATURE`에서 Fable 5 deep-interview와 사용자 요약 확인 후에만 Fable 5 planning이 시작되는지 확인
- [x] Interview 완료가 planning·implementation 승인으로 오인되지 않는지 확인
- [x] 같은 목적의 다른 Task 이름이 제안돼도 기존 canonical Task를 재사용하는지 확인
- [x] Roadmap의 현재 Next Gate와 다른 Task가 사용자 재정렬 승인 없이 시작되지 않는지 확인
- [x] 새 채팅에서도 instruction chain과 Task Identity Gate가 첫 변경 전에 다시 실행되는지 확인
- [x] 일반 Task를 단일 canonical clone에서 수행하고 Task별 영구 worktree를 만들지 않는 운영 모델 승인
- [x] Dirty·runtime worktree가 보존되고 clean inactive worktree만 정리됐는지 확인
- [x] Change 003 게시·merge 승인
- [x] Change 004 worktree cleanup과 5174 중단·재시작·root 정규화 승인
- [x] Change 004 자동·독립 재검증
- [x] Change 004 사용자 검수
- [x] Change 005 required-PR 최소 ruleset 정책·적용 승인
- [x] Change 005 active main effective rule 확인
- [x] Change 005 사용자 검수
- [x] Change 004·005 commit·push·PR·merge 승인
- [x] Change 006 GitHub 최상위 폴더를 대표·디자인·보존 3개로 통합
- [x] Change 006 dirty checkout 보존과 legacy linked-worktree repair
- [x] Change 006 보존 폴더 exact audit와 완전 삭제 승인
- [x] Change 006 Docker/PostgreSQL controlled maintenance와 stale handle 해제 승인
- [x] Change 006 최상위 폴더 `6→3→2`, 동일 persistent volume·DB aggregate·runtime 자동 검증
- [x] Change 006 독립 검증
- [x] Change 006 사용자 검수
- [x] Change 006 commit·push·PR·merge 승인·PR #52 squash merge
- [x] Change 007 `fable`이 CLI에서 Fable 5로 표시되는지 사용자 확인
- [x] Change 007 전용 runner로 사용자 terminal 실행 없이 Fable interview가 생성되는지 확인
- [x] Change 007 일반 shell wrapper 보호가 유지되는지 확인
- [x] Change 008 Task-scoped session·drift guard·질문 최대 5개 정책 확인
- [x] Change 008 session cleanup은 남은 USER-FLOW redraft·merge 뒤 그 Task closure에서 실행하도록 이관
- [x] Change 009 Fable 원문 직접 작성·Codex 사후 review 정책 승인
- [x] Change 009 preview direct-write byte equality와 GPT-5.6 SOL review 확인
- [x] Change 010 Fable primary draft 1회·Codex 내용 review 1회 정책 승인
- [x] Change 010 USER-FLOW 내용·제품 방향 review 작성과 governance 독립 검증
- [x] Change 011에서 5174가 대표 폴더의 현재 branch를 따르도록 운영 정책 승인
- [x] Branch 전환마다 5174를 중단하지 않고 조건부 재시작만 허용하는 정책 승인
- [x] Change 011 branch 전환 중 5174 유지와 root·Teams Activity·live·ready route 확인
- [x] Change 011 대표 branch 전환 뒤 5174 현재 branch 반영과 필수 route 확인
- [ ] Change 011 분리된 Codex 독립 검증
- [x] Change 011 작업 현황·Finding·게시 경계 사용자 검수
- [x] Change 011 독립 재검증 PASS 뒤 commit·push·PR·merge 승인
- [x] Change 012 Roadmap 순서 변경과 기존 canonical Task 재사용 승인
- [x] Change 012 Fable 정책·USER-FLOW 선별 이식과 로컬 보존·결과 커밋 승인
- [x] Change 012 5174·5176·Backend·DB 보존과 push·PR·merge·branch 삭제 제외 승인
- [x] Change 012 대표·디자인 2-worktree exact projection 확인
- [x] Change 012 자동 검증
- [ ] Change 012 분리된 Codex 독립 검증
- [x] Change 012 사용자 검수와 별도 Git 게시 승인
- [x] Change 013 generic primary draft·USER-FLOW compatibility 분리와 exact target gate 확인
- [x] Change 013 Governance 독립 재검증 뒤 merge 승인
- [ ] Change 013 분리된 Codex 독립 재검증
