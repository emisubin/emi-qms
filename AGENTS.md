# AGENTS.md

## 프로젝트와 지침 범위

이 Repository는 EMI 프로젝트 통합관리시스템을 개발한다. Root 지침은 Repository 전체에 적용하며, `backend/`, `frontend/`, `scripts/` 아래에서는 각 하위 `AGENTS.md`가 영역별 규칙을 추가한다.

## Source of truth

작업을 시작하기 전에 다음 순서로 실제 Repository 상태를 확인한다.

1. 현재 경로에 적용되는 `AGENTS.md` 계층
2. [Product Roadmap](docs/00-product-roadmap.md)의 제품 방향, Task 상태와 선행 의존성
3. [Task 종료 및 산출물 정책](docs/12-task-completion-policy.md)의 Finding·검수·게시 gate
4. 실제 코드, migration, 설정과 tests
5. 현재 branch, worktree, runtime과 DB 상태

대화 기록이나 기억을 canonical source로 사용하지 않는다. 문서끼리 또는 문서와 구현 사이에 의미 있는 충돌이 있으면 한쪽을 임의 선택하거나 수정하지 말고 위치, 영향과 선택지를 보고한 뒤 중단한다.

### Task 시작 instruction chain gate

- 모든 새 Task와 분리된 Codex 조사·구현·독립 검증 session은 첫 변경·runtime mutation·Git mutation 전에 현재 filesystem의 instruction chain을 다시 읽는다. 이전 Task, 대화 기억이나 요약에서 읽었다는 사실로 대체하지 않는다.
- 최소 확인 범위는 Root `AGENTS.md`, 변경 경로에 적용되는 하위 `AGENTS.md`, Product Roadmap, Task 종료 및 산출물 정책, Validation Matrix, Privacy-safe Evidence와 해당 Task의 planning·review·change·implementation report다. 적용 대상이 없으면 그 이유를 기록한다.
- instruction chain을 읽은 뒤 `instructionChainRead=true`, 선택한 `taskType`, branch/worktree 기준선과 적용되는 하위 지침을 privacy-safe projection으로 먼저 보고한다.
- 적용 지침이 없거나 읽을 수 없고, 문서·코드와 의미 있게 충돌하거나, 읽은 뒤 base branch·instruction file이 바뀌면 구현을 시작하지 않고 다시 읽거나 충돌을 보고한다.
- 같은 Task의 단순 연속 turn은 매번 전체 파일을 다시 읽지 않아도 되지만, 새 session, branch/base 변경, instruction file 변경 또는 source-of-truth drift가 있으면 gate를 다시 수행한다.

### Task Identity Gate와 Roadmap Sequence Gate

새 Task ID, branch, worktree, planning 또는 Task 파일을 만들기 전에 다음 gate를 순서대로 수행한다. Task 이름이 다르다는 이유만으로 별도 목적이라고 판단하지 않는다.

1. Product Roadmap의 실행 큐, 현재 상태, 선행 의존성, external blocker와 `Next Gate`를 읽어 현재 실행 가능한 canonical Task를 확인한다.
2. 제안된 작업의 업무 목표, root Finding, 변경 경계, 보존할 불변조건과 예상 산출물을 기준으로 purpose identity를 작성한다.
3. `tasks/`, Roadmap·Decision Log·추적 항목, local/remote branch, worktree와 open/merged PR에서 같은 purpose identity를 검색한다.
4. 결과를 [Task Identity Gate 템플릿](tasks/_templates/task-identity-gate-template.md)의 fixed projection으로 먼저 기록한다.
5. 같은 목적이 하나면 새 Task를 만들지 않고 기존 canonical Task와 다음 `change-###`를 재사용 대상으로 확정한다. 잘못 제안된 별칭은 기존 Task의 Finding으로만 기록한다.
6. 같은 목적 후보가 둘 이상이거나 canonical Task를 확정할 수 없으면 어느 것도 임의 선택하지 않고 후보와 차이를 보고한 뒤 중단한다.
7. 기존 Task 재사용과 새 Task 생성 모두 Roadmap의 현재 실행 가능 Task 또는 명시된 `Next Gate`와 일치하거나, 명시적 순서 변경 승인이 기록된 경우에만 시작할 수 있다.

제안한 Task ID가 이미 존재하지만 purpose identity가 다르면 기존 Task에 덮어쓰지 않고 `BLOCKED_ID_COLLISION`으로 중단한다.

Roadmap 순서는 단순 번호가 아니라 status, dependencies, external blocker, 승인된 병렬 진행과 `Next Gate`를 함께 해석한다. 앞선 Task가 외부 blocker로 대기 중이라는 이유만으로 뒤 Task를 자동 선택하지 않는다. Roadmap이나 Decision Log에 병렬 진행이 명시되어 있거나 사용자가 순서 변경을 명시적으로 승인하고 그 결정을 Roadmap에 기록할 때만 우회할 수 있다.

사용자가 Roadmap과 다른 Task를 요청해도 이를 묵시적 순서 변경으로 해석하지 않는다. `roadmapSequenceMatch=false`를 먼저 보고하고, 선행조건·영향과 권장 순서를 설명한 뒤 명시적 재정렬 승인을 받는다. 확인된 P0/P1 또는 신규 P2는 현재 작업을 차단할 수 있지만, 발견 사실만으로 임의 Task를 시작하지 않고 canonical Task ID와 Roadmap 우선순위를 먼저 확정한다.

일반적인 Roadmap 단계명, 상태명 또는 “P2 Gate” 같은 설명을 Task ID로 합성하지 않는다. Base branch, Roadmap, instruction file 또는 관련 PR 상태가 바뀌면 이 gate를 다시 실행한다. `roadmapSequenceMatch=true` 또는 `explicitRoadmapOverrideApproved=true`가 아니면 `PASS_REUSE`와 `PASS_CREATE`로 판정하지 않는다. Gate 결과가 두 PASS 상태 중 하나가 아니면 Fable 호출, 조사용 mutation, branch/worktree 생성과 파일 작성을 시작하지 않는다.

이 gate는 instruction chain, Task 유형 라우터, Finding·검수·게시 gate와 영역별 `AGENTS.md`를 대체하지 않고 추가한다. 새 Codex 채팅과 분리된 session도 현재 Repository 안에서 이 모든 지침을 다시 읽은 뒤 동일한 gate를 수행한다.

## 작업 격리와 범위

- `main`에서 직접 개발하거나 push하지 않는다.
- Task별 branch는 유지하되, 일반적인 순차 작업은 하나의 canonical local clone에서 branch만 전환한다. Task마다 영구 worktree나 전체 checkout을 새로 만들지 않는다.
- 대표 local 구조는 일반 Task workspace를 겸하는 canonical clone 하나와 실제 실행에 필요한 bounded runtime worktree로 제한한다. Task 문서와 결과는 별도 폴더 복제가 아니라 Repository의 `tasks/`, Git commit과 PR에 누적한다.
- canonical clone은 새 Task 시작 전에 clean이어야 하며 최신 `origin/main`에서 승인된 Task branch를 만든다. Branch 전환 전 현재 Task 단계·남은 일·Git 게시 상태·재개 조건을 보고하고, 현재 commit이 이름 있는 local branch 또는 remote ref에서 reachable인지 확인한다. Dirty WIP, 이름 없는 stash, 보존되지 않은 detached commit 또는 source-of-truth 미기록 상태에서는 전환하지 않는다. Clean·reachable branch의 open PR과 게시 승인 대기는 그 자체로 branch 전환이나 새 worktree 생성 사유가 아니며 완료 보고의 중단·보류 Task로 추적한다.
- HTTPS 5174 Vite Development frontend는 canonical clone의 현재 branch를 그대로 반영한다. Clean branch 전환 중에는 server를 유지하고 Vite HMR 또는 full reload 뒤 root·필수 route와 화면을 확인한다. `.env*`·process env, dependency·lockfile·`node_modules`, Vite config·plugin·HTTPS certificate·port·proxy·startup command가 바뀌었거나 자동 갱신 실패·stale module·지속 오류가 있을 때만 5174를 재시작한다. 5174가 실행 중이라는 사실만으로 branch 전환을 막거나 별도 worktree를 만들지 않는다.
- 5174는 현재 branch를 따르는 Development runtime이며 latest `main` 고정·review-safe·release candidate가 아니다. Backend 5081과 다른 runtime의 source·configuration·migration·worker handover는 해당 Task의 별도 승인과 검증 규칙을 계속 따른다.
- 5174 이외 process가 canonical clone source를 고정 기준으로 사용하고 branch 전환이 source/runtime 불일치를 만들면 해당 runtime의 handover·재시작·격리 승인 전에는 전환하지 않는다. 이미 실행 중인 compiled Backend가 source 변경을 자동 반영한 것으로 간주하지 않는다.
- 병렬 구현, 5174 이외 runtime source 고정, 위험한 history/migration rehearsal처럼 물리 격리가 필요한 경우에만 임시 worktree를 추가한다. 생성 전에 purpose, owner, 기준 SHA, 예상 종료 시점과 cleanup 승인 경계를 기록한다.
- 구현·독립 검증의 Codex session 분리는 논리적 책임 분리이며 영구 worktree를 하나씩 추가해야 한다는 의미가 아니다. 동시 write가 없으면 독립 검증은 canonical clone의 같은 clean committed branch를 read-only로 사용한다.
- 임시 worktree는 Task 종료 시 clean, process 미사용, open PR 없음과 commit reachable을 확인한 뒤 승인 범위에서 `git worktree remove`로 정리한다. `rm -rf`, dirty worktree 강제 제거와 branch 자동 삭제를 사용하지 않는다.
- 임시 worktree의 `node_modules`, Backend `bin/obj`, browser report와 E2E artifact는 ownership을 확인하고 Task cleanup 범위에서 제거한다. runtime이 사용 중이거나 소유가 불명확하면 보존한다.
- 기능 개발은 `feat/<task-id>-<short-name>`, 버그 수정은 `fix/<task-id>-<short-name>`, 디자인 실험은 `experiment/<purpose>` 형식을 사용한다.
- 기존 dirty worktree, stash, branch, runtime과 사용자가 만든 WIP를 임의 수정·정리·재시작하지 않는다.
- 시작 전 동일 목적 branch/worktree/PR과 현재 diff를 확인한다.
- 승인된 포함 범위만 변경하고 범위 밖 개선은 Finding 또는 후속 Task로 분리한다.
- Commit, push, PR, merge와 branch/worktree 정리는 사용자의 명시적 요청 범위에서만 수행한다.

## Task 유형 라우터

Codex는 작업을 시작하기 전에 실제 요청과 Repository 상태를 기준으로 `taskType`을 하나 선택한다.

- `NEW_FEATURE`: 사용자가 새로 수행할 수 있는 업무 흐름, 화면, 데이터 개념·상태 전이, 외부 연동, 알림 채널 또는 권한 능력을 추가한다.
- `APPROVED_FEATURE_IMPLEMENTATION`: 승인된 신규 기능 planning과 review resolution을 구현한다.
- `BUGFIX`: 기존 계약과 기대 동작의 결함을 수정한다.
- `P2_REMEDIATION`: 확인된 P2 Finding을 기존 정책 안에서 보정한다.
- `SECURITY_HARDENING`: 기존 보안·권한·동시성 불변조건을 강화한다.
- `UAT_RUNTIME`: migration 적용, UAT 검증, runtime handover와 운영 안전 gate를 다룬다.
- `DOCS_GOVERNANCE`: 문서, Repository 지침과 Task 상태를 실제 상태에 맞춘다.
- `HOUSEKEEPING`: 승인된 branch, worktree, candidate, backup과 임시 자원을 정리한다.
- `POLICY_DECISION`: 기존 기능 범위 안에서 사용자 정책 선택이 필요하다.

`NEW_FEATURE`만 Fable 5 신규 기능 기획 흐름으로 보낸다. 제품 source 구현은 계속 Codex-only이며 `APPROVED_FEATURE_IMPLEMENTATION`에서 Fable을 다시 호출하지 않는다. Fable은 사용자 확인이 끝난 interview를 바탕으로 primary draft 전문을 한 번 작성하고 Codex가 내용 관점에서 한 번 review하면 기획 작성 흐름을 종료한다. 별도 사용자-facing 문서가 primary draft로 승인된 경우에만 `docs/` target을 사용하며 planning과 preview를 중복 생성하지 않는다.

Codex-only 조사 중 신규 제품 능력이나 기존 확정 정책을 바꾸는 설계가 필요해지면 구현을 중단한다. 신규 능력이면 `NEW_FEATURE`, 기존 범위의 정책 선택이면 `POLICY_DECISION`으로 재분류하고 사용자 승인을 다시 받는다.

## 신규 기능: Fable 5 기획과 Codex 검토

`NEW_FEATURE`는 다음 순서를 지킨다.

1. Codex가 Task 유형과 read-only 안전 경계를 확인하고 Fable 5 deep-interview를 시작한다.
2. Fable 5가 Repository를 읽고 사용자에게 필요한 관련 질문 1~5개, 선택지 비교와 권장안을 작성한다.
3. Codex는 질문을 임의로 답하거나 바꾸지 않고 사용자에게 전달하고, 사용자 답변을 interview source에 기록한다.
4. Fable 5가 누적 interview source를 다시 읽어 추가 질문 또는 확인용 요약을 작성한다. 필수 결정이 채워질 때까지 2~4를 반복한다.
5. 사용자가 Fable 5의 interview 요약을 명시적으로 확인한다.
6. Fable 5가 확인된 interview를 source of truth로 primary draft Markdown 전문을 한 번 작성한다. 기본 target은 `tasks/<task-id>-planning.md`이며, 사용자가 개인 검토용 또는 사용자-facing `docs/` 전문을 primary draft로 명시하면 그 승인된 target 하나만 사용한다.
7. Codex가 Fable 원문을 수정하지 않고 내용·제품 방향 review를 한 번 작성한다.
8. Codex가 유지·추가·보류·제거 권고, 개발 순서, 사용자 가치, 누락 기능, 과도한 범위와 결정 필요 항목을 보고하고 기획 작성 흐름을 종료한다. Code 대조는 구현 가능성과 충돌 확인의 보조 근거다.
9. Codex review가 Fable 재작성을 자동 실행하지 않는다. 사용자가 review를 본 뒤 새 전문 재작성을 명시적으로 요청한 경우에만 별도 change와 승인 상태를 기록하고 Fable redraft를 한 번 실행한다.
10. 사용자가 primary draft와 review resolution을 승인한다.
11. 새 Codex 구현 세션이 승인된 계약만 구현하고 검증한다.
12. 구현 세션과 분리된 Codex 검증 세션이 계약, diff와 검증 결과를 read-only로 재검토한다.
13. 사용자 검수와 별도 게시·merge 승인을 받는다.

### Codex 내용 Review 계약

- Review의 첫 목적은 문법·코드 결함 탐지가 아니라 기획 품질 판단이다.
- 반드시 사용자 문제와 기대 결과, 제품 방향·Roadmap 정합성, 기능별 필요성·사용자 가치, 우선순위와 의존성, 과도한 범위·운영 부담, 누락 기능, 대안과 trade-off를 검토한다.
- 기능은 가능한 경우 `유지`, `추가`, `보류`, `제거`로 분류하고 근거와 권장 개발 순서를 제시한다.
- 현재 코드·API·DB·권한 대조는 “지금 가능한가”, “기존 계약과 충돌하는가”, “예상 비용이 큰가”를 판단하는 보조 근거로만 사용한다. Code와 정확히 일치한다는 사실만으로 좋은 기획이라고 판정하지 않는다.
- Review는 Fable 원문과 별도 파일에 작성하고 원문을 patch하지 않는다.
- 기본 review는 한 번으로 끝낸다. Review Finding을 이유로 Codex와 Fable이 자동 수정·재검토 round를 반복하지 않는다.

### Fable 5 deep-interview Gate

- `NEW_FEATURE`의 업무 interview owner는 Fable 5다. Codex는 Task 분류, read-only 실행 경계, 질문·답변 전달, privacy-safe 기록과 이후 Repository 대조만 담당하며 제품 질문에 대신 답하거나 사용자 선택을 추정하지 않는다.
- Fable 5는 현재 업무, 문제, 대상 사용자와 역할, 정상·예외·복구 흐름, 권한, data/state lifecycle, audit, UX·접근성·좁은 화면, integration·attachment·notification, migration·UAT·rollout·rollback과 성공 기준을 Repository 기준선에 맞춰 질문한다.
- Fable 5는 한 round에 서로 관련된 질문 1~5개만 제시한다. 왕복 횟수를 줄이기 위해 최대 5개를 사용할 수 있지만 질문 수를 채우기 위한 비차단·무관 질문은 추가하지 않는다. 정책 선택이 있으면 2~3개의 상호 배타적 선택지를 쉬운 말로 비교하고 Repository 근거에 따른 권장안을 함께 제시한다.
- Codex는 [신규 기능 deep-interview 템플릿](tasks/_templates/new-feature-interview-template.md)으로 `tasks/<task-id>-interview.md`를 유지한다. Fable 질문과 사용자 답변을 의미 변경 없이 기록하고, 다음 Fable round가 이 파일을 다시 읽게 한다.
- Runner는 각 interview round의 Fable stdout 전문을 `tasks/<task-id>-interview-round-<n>-fable.md`에 byte-for-byte로 먼저 기록한다. Codex는 사용자에게 이 원문을 순서·표현·선택지·권장안 변경 없이 전달하며, 설명이 꼭 필요하면 Fable 원문과 명확히 분리한다. Canonical interview에는 원문 artifact link와 사용자 답변만 기록하고 raw Fable artifact를 편집하지 않는다.
- Fable Task session은 같은 Task의 interview와 planning 호출에 한해 Repository 밖 private state로 유지한다. 첫 호출은 전체 기준선을 읽고, 이후 호출은 runner가 같은 base HEAD·instruction contract와 예상한 interview·Roadmap 변경만 있음을 확인한 경우에만 session을 재개한다.
- Session memory는 반복 기준선 조사를 줄이는 가속 cache일 뿐이며 interview 파일과 현재 Repository를 canonical source로 대체하지 않는다. 각 interview round는 최신 interview 파일을 다시 읽고, planning은 최신 interview·Roadmap·관련 구현과 tests를 다시 확인한다.
- Base·instruction·runner contract 또는 예상 밖 제품 source가 바뀌었거나 session transcript를 확인할 수 없으면 기존 session을 재사용하지 않고 전체 기준선을 새 session으로 갱신한다. Task 종료 시 runner의 `cleanup` mode로 해당 Task가 소유한 private session state와 transcript만 제거한다.
- Fable은 `QUESTIONS_REQUIRED`, `SUMMARY_CONFIRMATION_REQUIRED`, `COMPLETED_CONFIRMED` 중 하나로 interview 상태를 반환한다. 사용자가 요약을 확인하기 전에는 `userConfirmed: false`다.
- Planning은 `interviewStatus: COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`일 때만 Fable 5가 시작한다. 명시적으로 deferred된 비차단 결정은 planning의 사용자 결정 항목으로 전달한다.
- Interview 완료는 planning 승인이나 구현 승인이 아니다. `planningApproved`와 `implementationApproved`는 계속 `false`다.

### Fable 5 호출 경계

- Claude CLI model ID는 `fable`을 사용한다. 현재 CLI가 이 alias를 Fable 5(`claude-fable-5`)로 표시하는지 `claude --help`에서 확인하고, alias가 없으면 호출하지 않는다.
- Fable에는 `Read`, `Glob`, `Grep`과 동등한 Repository 읽기 기능만 허용한다.
- shell, edit/write, Git mutation, MCP 쓰기, browser/computer control과 재귀 agent 호출을 허용하지 않는다.
- safe mode, plan permission, 빈 MCP 설정과 slash command 비활성화를 함께 적용한다. Task session ID와 transcript는 Repository 밖 private 경로에만 저장하고 다른 Task와 공유하지 않는다.
- Fable stdout/stderr는 먼저 Repository 밖 private 임시 파일에 저장한다. Interview 원문과 명시적으로 승인된 기획 전문은 runner가 기계적 contract·privacy guard를 통과한 stdout과 대상 파일의 byte equality를 확인한 뒤 fixed path에 원문 그대로 기록할 수 있다.
- Codex는 Fable 원문을 다듬거나 `apply_patch`로 대신 작성하지 않는다. Contract·privacy guard는 위반 시 저장을 거부할 뿐 문장을 수정하지 않는다. Codex Finding은 별도 review 파일에 기록하고 기본 흐름은 그 review에서 종료한다. Fable `revise`는 Codex가 자동 호출하지 않으며 사용자의 명시적 redraft 요청이 기록된 경우에만 허용한다.
- 위 읽기 전용 경계를 현재 실행 환경에서 보장할 수 없으면 Fable을 호출하지 않고 중단한다.
- Fable은 [CLAUDE.md](CLAUDE.md)의 기획자 규칙만 수행하며 Task 라우팅, Codex 검토·구현·검증, 사용자 승인과 Git workflow를 재귀 실행하지 않는다.
- 각 Fable prompt에는 최신 `tasks/<task-id>-interview.md`를 source of truth로 포함한다. Fable은 deep-interview와 planning을 담당하지만 사용자를 대신해 답변을 만들거나 미확인 결정을 확정하지 않는다.

현재 Claude CLI에서는 최소한 `--safe-mode`, `--model fable`, `--permission-mode plan`, `--tools "Read,Glob,Grep"`, `--disable-slash-commands`, `--strict-mcp-config`와 빈 `--mcp-config`를 함께 사용한다. 새 Task session에는 runner가 생성한 UUID를 `--session-id`로 전달하고, 검증된 후속 호출은 같은 UUID를 `--resume`으로 재개한다. Safe mode에서는 `CLAUDE.md`가 자동 로드되지 않으므로 첫 기준선 prompt가 `CLAUDE.md`와 적용되는 `AGENTS.md`를 먼저 읽도록 명시한다. subprocess를 생성할 때부터 stdout/stderr를 private 임시 파일로 redirect하며, 이 옵션 중 하나라도 지원되지 않으면 실행하지 않는다.

Codex는 임의 shell wrapper와 redirect를 조합하지 않고 `bash scripts/run-fable-readonly.sh interview tasks/<task-id>-interview.md <round>`, `planning ...`, `draft ... docs/<approved-target>.md`, 명시적 사용자 redraft용 `revise ... docs/<approved-target>.md` 또는 `cleanup ...`만 사용한다. 이 script는 고정 prompt, Task interview·승인·target 경로, CLI option, session 소유권·drift, private artifact, direct-write byte equality와 출력 상태 계약을 fail-closed로 검증한다. `planning`과 `draft`는 atomic exclusive create로 기존 target·symlink를 덮어쓰지 않는다. `revise`는 사용자 redraft 승인을 담은 Task change와 기존 target·review가 모두 있을 때만 전문을 교체하고, approval change identity·digest별 private receipt를 atomic claim·consume해 같은 승인을 두 번 사용할 수 없게 한다. Task session cleanup은 이 one-time receipt를 삭제하지 않는다. Project Rules의 허용 범위는 이 script prefix에만 적용하며 일반 `bash -lc`, `zsh -c`와 `sh -c` prompt 규칙은 유지한다.

기본 interview 위치는 `tasks/<task-id>-interview.md`, planning 위치는 `tasks/<task-id>-planning.md`, Codex review 위치는 `tasks/<task-id>-review.md`다. `docs/tasks/`를 새로 만들지 않는다. Fable 도구에는 Repository 쓰기 권한을 주지 않지만, 질문·요약·planning·승인된 기획 전문의 작성자는 Fable이다. Runner는 Fable stdout을 fixed target에 byte-for-byte로 운반할 뿐 내용을 편집하지 않으며 Codex는 Fable 작성 파일을 수정하지 않는다.

## Codex-only 조사·구현·검증

`APPROVED_FEATURE_IMPLEMENTATION`, `BUGFIX`, `P2_REMEDIATION`, `SECURITY_HARDENING`, `UAT_RUNTIME`, `DOCS_GOVERNANCE`, `HOUSEKEEPING`, `POLICY_DECISION`은 다음 순서를 지킨다.

1. Codex 조사 세션이 Repository와 실제 상태를 read-only로 확인하고 가능한 경우 문제를 재현한다.
2. Root cause, 불변조건, 대안, 권장 최소안, 영향 범위와 검증 계획을 사용자에게 제시한다.
3. 사용자가 구현 범위와 필요한 mutation·게시 경계를 승인한다.
4. 새 Codex 구현 세션이 승인 범위만 변경하고 검증한다.
5. 구현 세션과 분리된 Codex 검증 세션이 승인 계약, Git diff, 테스트와 Finding gate를 read-only로 확인한다.
6. 사용자 검수와 별도 게시·merge 승인을 받는다.

조사 요청은 구현 승인이 아니다. 사용자가 같은 요청에서 구체적인 변경 범위와 실행을 명시적으로 승인한 경우에는 그 승인을 별도 조사 승인으로 다시 묻지 않되, 승인 범위를 넓혀 해석하지 않는다.

## 수정 요청과 문서 역할

구현 결과에 대한 수정 요청은 먼저 실제 코드와 기존 승인 계약에 대조한다. Codex는 증상, 기대 동작, 확인된 원인, 포함·제외 범위, 영향 파일, 보존할 불변조건과 검증 방법을 정리하고 사용자 승인을 받은 뒤 수정한다.

승인된 planning 범위 안의 실질적 수정은 `tasks/<task-id>-change-###.md`에 순번대로 기록한다. 신규 사용자 능력, 신규 상태 전이, 신규 외부 연동 또는 권한 확대가 필요하면 change로 처리하지 않고 `NEW_FEATURE`로 재분류한다. 의미를 바꾸지 않는 단순 오탈자도 사용자가 수정 실행을 요청한 범위 안에서만 처리한다.

문서 역할은 다음과 같이 분리한다.

- Planning: 구현 전 업무 문제, 정책, 범위, 대안과 완료 기준을 정의한다.
- Review: Codex가 planning을 실제 Repository와 대조한 Finding과 resolution을 기록한다.
- Change: 승인된 planning 또는 Task 계약 안에서 후속 수정 지시를 고정한다.
- Implementation report: 실제 구현, 결정, 변경 파일, 검증, 미실행 항목, Finding, rollback과 planning 대비 차이를 기록한다.

Planning, review와 change는 구현 완료 증빙을 대신하지 않는다. Implementation report는 사용자 승인이나 planning 정책을 사후에 새로 만들어내지 않는다. Task 마감 산출물은 별도 파일 수를 임의로 강제하지 않고 [Task 종료 및 산출물 정책](docs/12-task-completion-policy.md)의 5종 상태·위치 추적 규칙을 따른다.

## Codex 세션 분리

- 신규 기능의 기획·검토, 승인 후 구현, 구현 후 독립 검증은 서로 다른 Codex 세션을 기본으로 한다.
- Codex-only 작업도 조사, 승인 후 구현, 독립 검증 세션을 분리한다.
- 독립 검증 세션은 구현 세션의 결론을 신뢰하지 않고 승인 계약, Repository 상태, diff와 실행 가능한 검증을 직접 확인한다.
- 세션 사이 source of truth는 대화 기억이 아니라 planning, review와 resolution, Task, change, Implementation report, Git diff와 PR이다.
- 사용자 승인 전 구현 세션으로 넘어가지 않고, merge 승인 전 Ready 전환이나 merge를 수행하지 않는다.

## 데이터와 migration 안전

- Persistent UAT DB를 drop, truncate, reset하지 않고 persistent volume을 삭제하지 않는다.
- 이미 `main`에 반영된 migration은 수정하거나 번호를 재사용하지 않는다.
- 신규 migration은 feature branch에서 additive·forward-fix 원칙으로 작성하고 기존 DB와 fresh DB를 모두 검증한다.
- Persistent UAT write, migration 적용, 실제 provider 발송과 runtime 교체는 Task 범위와 사용자 승인이 명확할 때만 수행한다.
- E2E는 Persistent UAT와 분리된 전용 DB·container·storage를 사용한다.

## Finding과 완료 판정

- P0/P1은 미해결 상태에서 완료·게시·merge할 수 없다.
- P2는 먼저 해결한다. Risk acceptance는 canonical 종료 정책의 필수 기록과 사용자 승인이 모두 있을 때만 허용한다.
- P3는 후속 Task 또는 backlog에 연결한다.
- 확인하지 않았거나 실행하지 않은 결과를 성공으로 기록하지 않는다.
- 자동 검증 완료와 사용자 검수 완료를 별도 상태로 관리한다.
- Task 종료 시 5종 산출물의 위치와 상태를 [Task 종료 및 산출물 정책](docs/12-task-completion-policy.md)에 따라 추적한다.
- Implementation report는 실제 변경, 결정, 검증, 미실행 항목과 rollback을 기록하는 기술 원장이다.

## 개인정보와 secret

- 실제 사용자·회사 계정·고객·프로젝트·업무·알림 원문, tenant/client/object ID, credential과 secret을 tracked 파일이나 보고에 기록하지 않는다.
- raw DOM, API/DB response body, console 원문과 Git/GitHub 개인 metadata를 검증 증빙으로 출력하지 않는다.
- 검증 결과는 가능한 경우 boolean, integer, fixed enum, aggregate와 익명 역할명으로 기록한다.
- 상세 규칙과 output guard는 [Privacy-safe Evidence](docs/development/privacy-safe-evidence.md)를 따른다.

## 검증과 게시

- 변경 유형별 최소·영향·전체 테스트는 [Validation Matrix](docs/development/validation-matrix.md)를 따른다.
- Task allowlist의 개별 경로만 stage한다. `git add .`와 `git add -A`를 사용하지 않는다.
- stage 후 cached file 목록, 삭제, migration, dependency, env/certificate, generated artifact와 secret/PII 포함 여부를 재검증한다.
- 사용자 검수 대기 상태에서 PR이 필요하면 Draft로 유지한다.
- CI 실패, Finding gate 위반, 범위 밖 변경 또는 secret/PII가 있으면 게시·merge를 중단한다.

## Task 종료 고정 10개 항목 완료 보고

Task를 완료·중단하거나 사용자 검수 handoff로 종료할 때 최종 응답은 먼저 다음 고정 필드의 `작업 현황 요약` 표를 표시한다.

- 현재 Task와 현재 단계
- 현재 Task에 남은 일
- Git 게시 상태: Commit·Push·PR·Merge 각각 `완료`, `미완료`, `승인 대기` 또는 `적용 없음`
- 중단·보류 Task: Task ID, 중단 단계, 사유와 재개 조건. 없으면 `없음`
- 재개 우선순위: 중단·보류 Task가 있으면 어느 Task부터 재개할지
- 모든 활성·중단·보류 작업이 끝난 뒤 Product Roadmap 기준 다음 canonical Task와 `Next Gate`

요약 표 뒤에는 다음 10개 항목을 순서와 제목을 유지해 모두 포함한다.

1. 수정 요약
2. 수정한 파일
3. 실행한 테스트
4. 테스트 결과
5. Frontend URL
6. Backend URL
7. 수동 검수 체크리스트
8. 미커밋 변경사항
9. 남은 문제
10. 게시 가능 여부

- 적용 대상이 없는 항목도 생략하지 않고 `N/A`와 구체적인 이유를 기록한다.
- `실행한 테스트`와 `테스트 결과`를 분리하고, 미실행 검증과 이유를 성공 결과에 섞지 않는다.
- URL은 실제 확인한 환경만 기록한다. Runtime을 확인하지 않은 문서·조사 Task는 `N/A — runtime 검증 대상 아님`처럼 적고 과거 URL을 추정하지 않는다.
- 수동 검수 체크리스트는 자동 검증과 사용자 검수 상태를 구분하고 미체크 항목을 그대로 표시한다.
- 미커밋 변경사항에는 changed/staged 상태와 Commit·Push·PR·Merge 각각의 완료 여부, 남은 Git 작업과 필요한 사용자 승인을 기록한다.
- 남은 문제에는 현재 Task의 잔여 단계, 중단·보류 Task와 재개 조건, P0~P3 Finding, 미검증 항목, 외부 blocker, 별도 승인 필요 작업과 Roadmap 다음 Gate를 포함한다.
- Finding은 count만 쓰지 않고 각 ID 또는 stable label, severity, 상태(`OPEN`, `RESOLVED`, `RISK_ACCEPTED`, `BACKLOG`), 원인·영향과 해소 또는 후속 위치를 기록한다. 과거 Finding을 해소했더라도 무엇이 발생했는지 식별할 수 없는 상태로 축약하지 않는다.
- 현재 Task와 Git 게시가 모두 끝났더라도 `남은 문제`를 단순히 `없음`으로 끝내지 않고, 중단·보류 Task가 없다는 사실과 Product Roadmap 기준 다음 Task·Next Gate를 기록한다.
- 게시 가능 여부는 `GO`, `NO_GO` 또는 `N/A`와 근거를 기록한다. `GO`도 commit·push·PR·merge의 사용자 승인을 대신하지 않는다.
- 이 10개 항목은 대화의 완료 보고 형식이며 Implementation report와 5종 종료 산출물을 대체하지 않는다.

## 영역별 지침

- Backend: [backend/AGENTS.md](backend/AGENTS.md)
- Frontend: [frontend/AGENTS.md](frontend/AGENTS.md)
- Scripts: [scripts/AGENTS.md](scripts/AGENTS.md)
