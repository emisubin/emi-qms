# TASK-GOV-CODEX-002 Implementation Report

## 1. 해결한 업무 문제

신규 기능 기획과 기존 기능 보강이 같은 프롬프트 흐름에 섞여 불필요한 재기획, 승인 경계 누락과 역할 재귀가 발생할 수 있었다. Task 유형을 먼저 분류하고 신규 기능만 Fable 5 planning으로 보내도록 Repository 지침을 보강했다.

## 2. 실제 변경

- Root `AGENTS.md`: Task 유형, Fable 5 신규 기능 흐름, Codex-only 흐름, 수정 요청, 문서 역할과 세션 분리 추가
- `CLAUDE.md`: Fable 5의 신규 기능 기획 전용 역할, read-only 경계, source of truth와 출력 계약 추가
- Root `AGENTS.md`: 새 Task 생성 전 semantic identity와 Roadmap Sequence Gate 추가
- Task template: fixed projection과 `PASS_REUSE`·`PASS_CREATE`·blocked 상태 추가
- Task·Implementation report와 Roadmap: 종료 산출물·상태 추적

Backend, Frontend, migration, dependency, script, runtime과 Persistent UAT source diff는 없다.

## 3. 기술적 결정과 대안

### 선택안

PR #32의 66줄 Root 지침을 유지하고 Task router section만 추가했다. `CLAUDE.md`는 Fable 역할만 담고 Codex workflow 실행 책임을 갖지 않는다.

### 폐기한 대안

- 기존 root worktree의 336줄 `AGENTS.md` 전체 사용: canonical main 구조를 중복하고 과거 UAT WIP와 혼재해 폐기
- 경미한 수정의 무승인 즉시 처리: canonical 승인·종료 정책과 충돌해 폐기
- `docs/tasks/` 신규 경로: 기존 `tasks/` convention과 충돌해 폐기
- Fable output을 tracked planning 파일로 직접 redirect: 단일 작성자와 privacy-safe 검증 경계를 우회해 폐기

## 4. Fable 5 안전 경계

- model: `fable-5`
- Repository read-only 도구만 허용
- safe mode, plan permission, session persistence·slash command 비활성화, 빈 strict MCP config
- stdout/stderr는 Repository 밖 private artifact로 capture
- 호출 옵션이 지원되지 않거나 read-only를 보장할 수 없으면 fail-closed
- Fable은 planning 전문만 반환하며 Codex가 검증 후 `apply_patch`로 기록

실제 Fable 기획 호출은 이번 DOCS_GOVERNANCE Task 범위에서 수행하지 않았다.

## 5. 검증 결과

- 별도 Codex read-only session 대표 route: 9/9
- Planning/review path, Fable 재귀 차단과 승인 전 구현 금지: 통과
- Router static contract: 11/11
- Claude CLI의 Fable read-only 필수 option 지원: 8/8
- `git diff --check`: 통과
- actionlint: 통과
- Markdown local link·anchor·heading: 오류 0
- Secret/PII candidate: 0
- Changed-file allowlist: 5개 일치 / 범위 밖 0 / 삭제 0
- Backend·Frontend·migration·script·runtime 변경: 0
- Fable 실제 기획 호출, DB write, runtime restart와 provider 호출: 0

## 6. 영향

- Backend/API/DB/Migration/UI/UX: N/A — 관련 파일 변경 없음
- Excel/PDF/첨부파일/notification workflow: N/A — 관련 파일 변경 없음
- Runtime/Persistent UAT/provider: N/A — 기동·write·호출 없음
- 사용자 영향: Task 요청의 기획·승인·검증 순서만 명확해지며 제품 화면 변화 없음

## 7. 개인정보·secret

실제 identity, credential, DB/API/browser 원문을 사용하지 않는다. Dry run 결과는 fixed enum·boolean·count만 기록한다.

## 8. 시행착오 및 폐기한 접근

동일 목적의 두 초안이 서로 다른 worktree에 존재했다. 파일 수가 많은 기존 root 초안을 기준으로 삼지 않고, 최신 main에서 시작한 전용 worktree 후보를 canonical safety 구조에 최소 추가하는 방식으로 선택했다.

첫 Codex session dry run은 모호한 “purge guard 수정” 문구를 P2가 아닌 일반 bugfix로 해석해 exact match가 8/9였다. 지침 결함으로 단정하지 않고 입력을 “확인된 P2 Finding”으로 명확히 하고 route enum을 고정해 재실행했으며 9/9를 확인했다. 실제 Task 분류는 요청 문구뿐 아니라 Repository Finding 상태를 함께 사용한다.

## 9. Rollback

Root Task router section, `CLAUDE.md`, Task 문서와 Roadmap entry를 함께 revert한다. Runtime·DB rollback은 없다.

## 10. 사용자 검수 결과와 남은 항목

- 사용자 검수: 완료
- 게시·merge: PR #38 squash merge 승인
- 기존 root WIP와 historical branch 정리: 별도 HOUSEKEEPING 승인 대상
- Change 003 사용자 검수·게시·merge: 승인

## 10.1 Change 001 — Deep Interview Gate

사용자 정정에 따라 신규 기능 workflow 앞에 Fable 5 deep-interview를 추가했다. Fable 5가 질문을 1~3개씩 진행하고 선택지의 영향과 권장안을 설명한다. Codex는 질문·답변 relay와 privacy-safe 기록만 담당한다. 사용자 확인이 끝난 interview artifact와 blocking decision 0이 있을 때만 Fable 5가 planning을 시작한다.

- Interview 위치: `tasks/<task-id>-interview.md`
- Template: `tasks/_templates/new-feature-interview-template.md`
- Fable interview 상태: `QUESTIONS_REQUIRED` → `SUMMARY_CONFIRMATION_REQUIRED` → `COMPLETED_CONFIRMED`
- Session persistence 없이 interview 문서를 round별 source of truth로 재사용
- Interview 완료 시에도 `planningApproved=false`, `implementationApproved=false`
- 실제 신규 기능 interview와 Fable 호출: 0
- 제품 코드·runtime·Persistent UAT 영향: 0
- 사용자 검수: 완료
- 게시·merge 승인: 완료

Change 001 자동 검증 결과:

- 변경 파일 8, allowlist 위반 0, staged 0
- `git diff --check` 통과
- Markdown missing link/anchor·duplicate heading `0/0/0`
- Added secret/PII candidate `0/0`
- Task type enum 9개 누락 0, `NEW_FEATURE` 전용 Fable rule 1
- Fable interview-before-planning ordering: true
- Fable interview state case 4/4 통과
- Recursive workflow block 확인, `docs/tasks/` 생성 0
- Backend·Frontend·migration·dependency·script·runtime diff 0
- 실제 Fable 호출, runtime·Persistent UAT mutation 0
- 독립 Codex 검증: 별도 세션 대기

## 10.2 Change 002 — Task Identity와 Roadmap Sequence Gate

동일 목적의 Task를 다른 이름으로 중복 생성하거나 Roadmap 순서를 건너뛰는 문제를 막기 위해 새 Task 자원 생성 전 fail-closed gate를 추가했다.

- Purpose identity: 업무 목표, root Finding, 변경·검증 경계, 보존 불변조건과 예상 산출물
- 검색 범위: Task 산출물, Roadmap·Decision Log·추적 항목, branch, worktree와 open/merged PR
- 같은 목적 하나+Roadmap 일치 또는 승인된 override: 기존 canonical Task와 다음 `change-###` 재사용
- 같은 목적 둘 이상: `BLOCKED_AMBIGUOUS`
- 같은 ID·다른 목적: `BLOCKED_ID_COLLISION`
- 같은 목적 없음+Roadmap 일치: `PASS_CREATE`
- 재사용·신규 생성 모두 Roadmap 불일치: 명시적 재정렬 승인과 Roadmap 기록 전 `BLOCKED_SEQUENCE`
- 일반 queue label에서 Task ID 합성 금지
- Base·Roadmap·instruction·PR drift 시 gate 재실행

이번 문제에서는 `TASK-GOV-P2-GATE-001`이 물리 Task로 생성되지는 않았고 기존 `TASK-GOV-FINDING-GATE-001`의 목적을 잘못 축약한 별칭이었다. 기존 canonical Task를 재사용하는 것으로 정정한다.

Fable 5 질문 제한은 전체 질문 수 제한이 아니라 round당 1~3개 제한이다. 제한 해제는 왕복을 줄일 수 있으나 누락·모순·피로·과수집과 잘못된 완료 판정 위험이 커 현재 규칙을 유지했다. Adaptive 최대 5개는 별도 승인 후보로 남긴다.

Change 002 자동 검증 결과:

- Task Identity decision table: 8/8
- Static contract: 9/9
- `git diff --check`: 통과
- Markdown 10개 파일 local link·anchor·duplicate heading: `0/0/0`
- Secret/PII candidate: email·UUID·token `0/0/0`
- Changed-file allowlist: 10개 일치 / 범위 밖 0 / staged 0
- Backend·Frontend·migration·dependency·script·runtime 변경: 0
- Noncanonical `TASK-GOV-P2-GATE-001` Task file·branch·worktree: `0/0/0`
- Round당 Fable 질문 수 규칙: AGENTS·CLAUDE 각 1개 / adaptive 최대 5개 적용 0
- 제품 코드·runtime·Persistent UAT 영향: 0
- 실제 Fable 호출: 0
- 사용자 승인: B안과 Roadmap Sequence Gate 승인
- 사용자 검수: 완료
- 게시·merge 승인: 완료

## 10.3 Change 003 — 재사용 worktree lifecycle

Task별 영구 worktree와 반복 dependency/build artifact 누적을 막기 위해 일반 작업 workspace를 fresh canonical clone 하나로 통합하는 lifecycle을 확정했다. 세션 분리는 유지하지만 동시 write가 없는 독립 검증은 같은 clean committed branch를 read-only로 사용할 수 있다.

실제 local cleanup 결과:

- linked worktree: 30 → 9
- 제거: clean·process 미사용·open PR 0·commit reachable 21개
- 보존: `current` 1개, dirty 3개, process 사용 runtime source 5개
- worktree root: 약 6.04GB → 약 2.01GB
- 회수: 약 4.03GB
- branch 삭제, 강제 remove와 removal failure: 0
- Development·Review-safe·Candidate process 종료·재시작: 0
- Persistent UAT·provider·product source 변경: 0

남은 9개는 사용자 WIP 또는 실행 중 process ownership 때문에 자동 정리하지 않았다. History rewrite의 raw artifact·backup·Support 상태와 dirty patch 이전 승인 경계도 변경하지 않았다.

Change 003 검증 결과:

- cleanup 대상 safety gate: 21/21
- removal failure: 0
- dirty/runtime 보존 위반: 0
- cleanup·게시용 임시 `current`와 latest-main branch 기준: PASS
- 문서·diff·secret/PII 검증: PASS
- 승인 파일 외 변경, 삭제, staged 파일과 stale worktree 등록: 0
- Markdown duplicate heading, missing local link·anchor와 secret/PII 후보: 0
- 단일 canonical clone 전환: merge 후 fresh clone 검증 단계에서 수행
- 사용자 검수·게시·merge: 승인

## 10.4 Change 004 — Repository worktree cleanup과 canonical root 정규화

PR #48·#49·#50 게시를 위해 남아 있던 clean inactive worktree 3개를 safety gate 뒤 정상 제거했다. Linked worktree는 5개에서 canonical root와 5176 디자인 실험용 2개로 줄었고 local·remote branch는 모두 보존했다.

원본 root는 5174 runtime과 수정 9·미추적 2 상태 때문에 최신 main으로 전환되지 못하고 있었다. 14개 WIP path 중 13개는 최신 `origin/main`과 byte-identical이었고 Roadmap에는 main에 없는 과거 승인 Decision Log 2행이 있었다. 5174 frontend만 승인된 범위에서 중단하고 전체 WIP를 이름 있는 stash로 보존한 뒤, root를 `origin/main` 기반 cleanup branch로 전환했다. 고유 Decision Log, 실제 `PUBLIC` visibility와 cleanup 결과만 문서화한다.

- worktree: `5 → 2`
- 제거 대상 clean/process/Open PR: `3/3`, `0`, `0`
- local·remote branch 삭제: 0
- root WIP stash: 1, 삭제 0
- 5174 frontend restart: 완료, Entra login shell·HTTPS root·health·Teams Activity 정상, HTTP 실패
- Backend·Review-safe·PostgreSQL restart: 0
- product source·migration·dependency·Persistent UAT 변경: 0
- 문서 게시: 미승인

자동 검증에서 5081 live/ready, 5176, 5190과 5092는 모두 200이고 PostgreSQL은 healthy/restart 0을 유지했다. 문서 6개 link·anchor·duplicate heading은 `0/0/0`, `git diff --check`와 privacy/secret 5종은 통과했다. Stash 14개 path 중 13개는 최신 main과 동일하고 Roadmap의 고유 Decision Log 2행은 현재 문서에 보존했다. Stash는 사용자 검수 전 삭제하지 않는다.

분리된 Codex 독립 검증은 allowlist, worktree·stash·branch 보존, 5174 HTTPS-only와 다른 runtime 보존을 다시 확인해 `PASS`로 판정했다. Open P0/P1/P2는 `0/0/0`이며 Change 004 사용자 검수는 완료됐다. 당시 추적한 P3 1건은 Change 005에서 해소했다.

첫 handoff 뒤 사용자가 5174의 server·runtime mode 연결 실패를 보고했다. Root cause는 frontend-only 재시작 명령이 HTTPS와 proxy만 설정하고 same-origin API 값과 Repository root의 기존 Entra local 설정 로드를 누락한 것이었다. 5174 frontend만 다시 정규화해 기존 local Entra 설정을 privacy-safe하게 로드하고 `EntraId`, `https://localhost:5174` redirect와 same-origin API를 명시했다. Browser에서 승인된 로그인 shell을 확인했고 5174 runtime API·health, 5081·5176·5190·5092와 PostgreSQL 보존 검증을 다시 통과했다.

분리된 Codex 독립 재검증도 root cause, Entra runtime 계약, runtime 보존, 문서 일관성과 allowlist를 모두 `PASS`로 판정했다. Open P0/P1/P2는 `0/0/0`이고 사용자 검수를 완료했다.

Public Repository 설정의 최초 read-only fixed projection에서 default branch main, classic branch protection 0, Repository ruleset 0을 확인해 P3 `PUBLIC_MAIN_SERVER_SIDE_PROTECTION_ABSENT`로 추적했다. Change 005에서 사용자가 승인한 required-PR 최소 ruleset을 적용해 해소했다.

## 10.5 Change 005 — Public main 최소 PR 강제

1인 개발 속도를 유지하기 위해 Repository 지침의 direct main push 금지만 GitHub 서버 측 ruleset으로 강제했다. Public default branch `main`에 active `pull_request` rule 1개를 적용했으며 approving review, required status check, strict base update, review thread resolution, code owner와 last-push approval은 강제하지 않는다. 기존 merge·squash·rebase 방식도 제한하지 않았다.

- Repository ruleset: `0 → 1`
- main effective rule: `pull_request` 1
- required approving review/status check: `0/0`
- optional review·latest-base gate: 전부 false
- bypass actor: 0
- Backend·Frontend·DB·migration·runtime·provider 변경: 0
- commit·push·PR·merge: 미수행

GitHub의 ruleset 목록과 main effective rules API를 각각 재조회해 active/default-branch와 required pull request를 확인했다. Direct push 차단을 검증하기 위한 실제 원격 mutation은 수행하지 않았다. P3 `PUBLIC_MAIN_SERVER_SIDE_PROTECTION_ABSENT`는 Resolved다.

첫 독립 검증은 GitHub effective rules read-only 조회가 실행 정책에 막혀 `INCOMPLETE`로 종료했다. 다른 독립 검증 세션이 public/default main, active required-PR 1, required status check·approving review·bypass actor 0과 optional review gate false를 확인했다. 이 과정에서 History Rewrite SOP·User manual이 과거 `PRIVATE`·public 재개 대기 상태를 유지하는 P2 `HISTORY_REWRITE_OPERATIONAL_DOC_STATE_DRIFT`를 발견했다. 두 운영 문서를 실제 `PUBLIC`·required-PR 상태로 동기화해 P2를 해소했다.

최종 독립 재검증은 GitHub rules, 정확한 9개 문서 allowlist, staged·deleted·제품 source·runtime configuration diff 0, diff check, added local link·anchor 0, duplicate heading 0과 Roadmap 85·P3 closure 일관성을 확인했다. 별도 privacy aggregate 세션도 9개 파일의 email·UUID·credential assignment 후보를 `0/0/0`으로 확인했다. 최종 Open P0/P1/P2/P3는 `0/0/0/0`이고 품질 게시 gate는 GO지만 commit·push·PR·merge 승인을 대신하지 않는다.

사용자는 Change 005 적용 결과 검수를 완료하고 Change 004·005 문서 묶음의 commit·push·PR·merge를 승인했다.

## 10.6 Change 006 — Local GitHub 폴더 보존 통합과 최종 삭제

1차 정리에서는 GitHub 폴더 바로 아래의 과거 clone과 worktree 상위 폴더 네 묶음을 mode `0700`의 단일 보존 폴더로 이동하고 legacy repository 2개의 linked worktree 3개를 repair했다. 최상위 구조는 `6→3`이 됐다.

사용자의 재감사 요청에 따라 보존 폴더 약 684MB의 dirty checkout 6개, local branch 32개, tag, local 설정, history 문서와 생성 artifact를 모두 확인했다. Permission alignment 코드는 main에 반영 또는 대체됐고, History Rewrite·Finding Gate 문서는 pre-closure 상태로 현재 종료 문서에 의해 대체됐다. 과거 instruction 초안은 비채택 상태였고 UAT 초안은 현재 Phase A~D 산출물로 실현됐다. Local env·certificate는 canonical 설정과 중복이고 build·test·dependency artifact는 재생성 가능했다. Branch는 main reachable 5, tree-equivalent 25, 개별 검토 2였으며 나머지 두 branch도 main 반영 또는 remote copy·현재 문서 대체를 확인했다. 보존 폴더 내부 encrypted backup과 open PR은 0이었다.

사용자는 보존 폴더 완전 삭제, Docker/PostgreSQL controlled maintenance와 stale handle 해제를 승인했다. Checkout 6개를 강제 reset 없이 local stash로 clean 상태로 전환했다. Docker Desktop VM이 연 과거 read-only bind-mount path 4개는 PostgreSQL container stop만으로 해제되지 않아 container와 설정을 보존하는 Docker Desktop restart를 실행했고 `4→0`을 확인했다. Data cleanup·factory reset·volume 삭제는 수행하지 않았다.

보존 폴더 전체는 Finder의 exact-path 영구 삭제로 제거했다. Git worktree remove 명령은 실행 환경 정책에서 차단됐지만 linked worktree와 그 소유 repository가 모두 같은 삭제 대상 내부에 있었고, 먼저 모든 checkout을 clean 상태로 만든 뒤 parent repository와 함께 삭제했다. 대표와 디자인 폴더는 대상에서 제외했다.

- GitHub 최상위 폴더: `6 → 3 → 2`
- 최종 유지: canonical root 1, 5176 디자인 experiment 1
- 삭제: 보존 폴더 1, 약 684MB
- exact audit: checkout `6/6`, local branch `32/32`, tag 0
- stale Docker handle: `4 → 0`
- PostgreSQL container ID·persistent volume: 전후 동일
- PostgreSQL state/health/restart: `running/healthy/0`
- DB aggregate: `29/14/10/39/98/101` 전후 동일
- listener PID: 5174·5176·5081·5092·5190·5432 전후 동일
- canonical worktree registry: 대표·디자인 `2/2`
- remote branch·encrypted backup·제품 source·migration·runtime configuration 변경: 0
- commit·push·PR·merge: 미수행

자동 검증에서 5174 root·live, 5081 live·ready, 5176 root, 5190 root와 5092 live는 모두 200이다. Changed allowlist는 문서 4개이며 staged·제품 source·migration·runtime configuration diff는 `0/0/0/0`이다.

분리된 read-only 독립 검증도 최상위 exact 2개, preservation absent, canonical worktree 2개, 동일 PostgreSQL container·volume·DB aggregate, URL 7/7, listener 6/6, diff·Markdown·privacy와 문서 일관성을 모두 `PASS`로 확인했다. 첫 projection의 staged 1은 porcelain leading space를 제거한 파싱 오류였고 exact cached 명령으로 staged 0·allowlist exact 4를 재현해 false positive로 해소했다. Open P0/P1/P2/P3는 `0/0/0/0`이다.

사용자는 최종 삭제 결과 검수와 merge까지 승인했다. Commit `3476112`를 push해 Ready PR #52를 만들었고 Backend·Frontend·Full-Stack E2E 3개 CI가 모두 성공한 뒤 squash merge `e5507a8`로 main에 반영했다. Local·remote branch 삭제와 worktree cleanup은 수행하지 않았다.

## 11. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | Change 006 최종 삭제 반영 / 자동·독립 검증 완료 |
| SOP | `tasks/gov-codex-002.md` 8장 | 작성됨 |
| User manual | `tasks/gov-codex-002.md` 9장 | 작성됨 |
| Roadmap update | `docs/00-product-roadmap.md` | Change 006 최종 local 구조 반영 |
| User validation checklist | `tasks/gov-codex-002.md` 13장 | Change 001~006 완료 / Change 006 PR #52 squash merge 완료 |
