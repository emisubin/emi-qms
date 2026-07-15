# TASK-GOV-CODEX-002 Change 007 — Fable 5 읽기 전용 실행기

## 1. 사용자 발화 기준 문제

`TASK-USER-FLOW-001`의 첫 Fable 5 deep-interview 호출에서 CLI stdout/stderr를 private 임시 파일로 분리하는 compound shell command가 project-local generic shell-wrapper prompt 규칙과 일치했다. 현재 Codex approval policy가 `never`여서 승인 prompt를 열 수 없었고 `FABLE_CLI_EXECUTION_POLICY_BLOCKED`로 중단됐다. 사용자가 같은 명령을 직접 terminal에서 실행하면 outer Codex execpolicy를 거치지 않지만, 모든 신규 기능 round마다 사용자가 수동 실행해야 하는 비정상 운영이 된다.

추가로 기존 지침의 `--model fable-5`는 현재 CLI가 받는 alias와 달랐다. 실제 `claude --help`는 `fable`을 최신 Fable alias로, full model 예시를 `claude-fable-5`로 표시한다.

## 2. 기대 동작

- Codex가 사용자 terminal 실행 없이 Fable 5 interview와 planning을 호출한다.
- Fable model은 CLI가 Fable 5로 표시하는 `fable` alias로 고정한다.
- Read·Glob·Grep, safe mode, plan permission, strict empty MCP, no persistence와 slash command disable을 항상 함께 적용한다.
- 임의 prompt나 임의 Repository 파일을 받지 않고 `tasks/<task-id>-interview.md`만 입력으로 허용한다.
- Planning은 confirmed interview, user confirmation과 blocking decision 0을 script가 먼저 확인한다.
- stdout/stderr는 process 생성 시점부터 Repository 밖 mode `0700/0600` private artifact에 저장한다.
- 일반 `bash -lc`, `zsh -c`, `sh -c` prompt와 다른 project safety rule은 유지한다.

## 3. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 007`
- taskType: `BUGFIX`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapExpectedTaskId: `TASK-USER-FLOW-001`
- roadmapNextGate: `TASK-USER-FLOW-001 Fable Round 1`
- roadmapSequenceMatch: false
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: true
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 승인된 Fable 5 신규 기능 workflow를 사용자 수동 terminal 없이 Codex가 안전하게 실행한다.
- Root Finding: compound redirect가 generic shell-wrapper prompt와 일치하고 `never` approval policy에서 거절됐다.
- 변경·검증 경계: Fable 전용 Bash runner, exact project rule, CLI alias·운영 문서와 실제 USER-FLOW Round 1 검증이다.
- 보존할 불변조건: Fable read-only·private capture·no persistence·planning Gate, 일반 shell 보호, 제품 source·runtime·DB·provider 불변을 유지한다.
- 예상 산출물: fail-closed runner, exact allow rule, Change·Task·Implementation report·Roadmap 상태와 실제 Fable 결과다.

## 4. 승인 범위

- 최신 `origin/main` 기반 bounded Change 007 worktree와 bugfix branch
- `scripts/run-fable-readonly.sh` 추가
- `.codex/rules/project-safety.rules`의 runner 전용 allow prefix
- Root Fable model alias와 호출 SOP 정규화
- 기존 Task·Implementation report·Roadmap 동기화
- shell syntax, negative path/gate, execpolicy와 실제 Fable Round 1 검증

## 5. 제외 범위

- generic shell-wrapper prompt 제거 또는 broad shell allow
- Claude/Fable global configuration, subscription, credential 또는 account 변경
- Backend·Frontend·API·DB·migration·dependency·runtime configuration 변경
- 5174·5176·5081·5092·5190·5432 중단·재시작
- Persistent UAT write 또는 provider 발송
- commit·push·PR·merge와 branch·worktree 정리

## 6. 기술적 결정

### 선택안

Project rule에는 `bash scripts/run-fable-readonly.sh` prefix만 허용한다. Script가 mode, 단일 interview 경로 형식, interview round, planning 선행 상태, 필수 CLI option, `fable`/`claude-fable-5` help projection, 고정 prompt와 Fable 상태 contract를 다시 검증한다. CLI 원문은 terminal에 출력하지 않고 private artifact의 path·byte count와 stable status만 반환한다.

### 폐기한 대안

- 모든 `bash -lc`·`zsh -c`를 allow: unrelated compound mutation까지 prompt 없이 실행될 수 있어 폐기한다.
- approval policy를 전역 `on-request`로 변경: 매 Task prompt를 만들고 project safety 의미를 약화해 폐기한다.
- 사용자가 매 Fable round를 terminal에서 직접 실행: 반복 수동 단계와 output 전달 오류가 생겨 폐기한다.
- arbitrary prompt runner: allow prefix 뒤에 다른 목적 명령을 넣을 수 있어 폐기한다.

## 7. 검증 계획과 상태

- Bash syntax: PASS
- invalid mode·extra argument·path traversal·missing interview·incomplete planning gate: stable failure 5/5 PASS
- execpolicy runner `allow`·generic zsh/bash wrapper `prompt` 유지: 3/3 PASS
- CLI 필수 option 9개와 `fable`·`claude-fable-5` help projection: PASS
- private artifact directory/file mode `0700/0600`, Repository 미추적과 exact cleanup: PASS
- 실제 `TASK-USER-FLOW-001` Fable interview Round 1·2: `READY` 2/2, 누적 질문 6건, `QUESTIONS_REQUIRED`, planning 0, stderr 0
- Fable output absolute path·email·UUID·credential·HTML candidate: `0/0/0/0/0`
- `git diff --check`, Markdown link·duplicate heading, PII/secret와 changed allowlist: PASS
- Backend·Frontend·infrastructure·migration diff: 0
- 독립 검증: 대기

첫 incomplete-planning negative test는 문서 전체의 상태 문자열을 검색해 하단의 "사용자 확인 후 값" 예시를 실제 front matter로 오인했다. Read-only Fable process가 시작됐으나 output은 사용하지 않았고 private artifact 검사를 거쳐 exact cleanup했다. Planning Gate를 실제 front matter와 같은 exact line 3개로 변경한 뒤 같은 입력이 CLI 호출 전에 `FABLE_READONLY_PLANNING_GATE_INCOMPLETE`로 중단됨을 확인했다.

## 8. 승인 상태

- implementationApproved: true
- genericShellPolicyRelaxationApproved: false
- globalCodexConfigurationMutationApproved: false
- runtimeMutationApproved: false
- publishingApproved: false
- mergeApproved: false
- userValidationComplete: false

## 9. Finding과 게시 Gate

- P0/P1/P2/P3: `0/0/0/0`
- resolved implementation Finding: `FABLE_PLANNING_GATE_EXAMPLE_FALSE_POSITIVE`
- automaticValidationComplete: true
- actualFableRound1And2Complete: true
- independentVerificationComplete: false
- publishGate: `NO_GO_INDEPENDENT_VERIFICATION_AND_USER_APPROVAL_PENDING`
