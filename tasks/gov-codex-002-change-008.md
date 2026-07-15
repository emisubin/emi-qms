# TASK-GOV-CODEX-002 Change 008 — Fable Task session과 Round 성능 최적화

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 008`
- taskType: `POLICY_DECISION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-USER-FLOW-001`
- roadmapNextGate: `FABLE_ROUND_3`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: Fable 5가 같은 `NEW_FEATURE` Task에서 첫 기준선을 반복 조사하지 않도록 Task-scoped private session을 사용하고, interview canonical source·drift guard·종료 cleanup을 보존한다.
- Root Finding: Round 1·2마다 전체 기준선을 다시 읽는 비영구 호출은 질문 품질과 무관한 대기 시간을 반복시켰다.
- 변경 경계: Root Fable 지침, Fable 기획자 계약, fail-closed runner, runner 전용 Project Rule, governance 산출물과 현재 USER-FLOW interview 정책 projection.
- 보존할 불변조건: read-only 도구, safe mode, private stdout/stderr, 사용자 답변 비추정, planning gate, 제품 source·runtime·DB·provider·게시 경계.
- 예상 산출물: 이 Change, 갱신된 runner·지침·SOP·Implementation report·Roadmap과 USER-FLOW Round 3 성능 projection.

## 2. 사용자 승인 계약

- Task 첫 Fable 호출은 Repository 전체 기준선을 조사한다.
- 같은 Task의 후속 round와 planning은 Task-scoped session을 재개해 기준선 cache를 활용한다.
- Interview 파일은 계속 canonical source이며 session memory는 가속 cache일 뿐이다.
- Base HEAD, instruction·runner contract 또는 예상 밖 제품 source 변경 시 session을 폐기하고 기준선을 새로 만든다.
- 한 round에는 서로 관련된 질문을 최대 5개까지 묶되 질문 품질·구체성과 필수 coverage를 낮추지 않는다.
- Task 종료 시 해당 Task 소유 private session state와 transcript만 제거한다.
- Change 008을 구현한 뒤 `TASK-USER-FLOW-001` Round 3 성능을 실측한다.

## 3. 구현 범위

### 포함

- `interview|planning|cleanup` mode의 fail-closed runner
- 새 session UUID 발급, 검증된 `--resume`, Task별 private state·transcript 소유권 추적
- same HEAD·instruction contract digest·예상 dirty path 기반 drift guard
- 첫 호출 전체 기준선, 기존 interview에서 1회성 bootstrap, 후속 interview resume, planning 재검증 prompt 분리
- 질문 1~5개 또는 확인용 요약 output contract
- preflight·model·postflight 시간과 session mode의 privacy-safe projection
- 정확한 Task session cleanup
- Root 지침·Fable 계약·Project Rule·Task 문서 동기화

### 제외

- Fable 질문 effort·품질 하향, 임의 token 제한과 prompt caching 비용을 0으로 가정하는 표현
- global Claude/Codex 설정, credential·subscription 변경
- 제품 Frontend·Backend·API·DB·migration·dependency·runtime·provider 변경
- 현재 runtime 중단·재시작
- commit·push·PR·merge와 branch·worktree 정리

## 4. Session·drift 계약

1. State는 Repository 밖 사용자 private state root 아래 Repository key·Task key로 분리한다.
2. 새 session은 runner가 UUID를 발급해 `--session-id`로 시작하고 exact marker를 소유권 증빙으로 남긴다.
3. 후속 호출은 transcript 존재, same HEAD, 동일 contract digest와 예상 밖 dirty path 0일 때만 `--resume`을 사용한다.
4. Interview 재개는 최신 interview 전문을 다시 읽고 필요한 근거만 표적 확인한다.
5. Planning은 session을 재개해도 최신 interview·Roadmap·관련 code·tests를 다시 확인한다.
6. Drift 또는 transcript 누락 시 기존 session을 재사용하지 않고 새 전체 기준선을 만든다.
7. Cleanup은 marker의 UUID 형식, transcript scope·type·owner를 검증한 뒤 exact Task 소유 파일만 제거한다.
8. Session ID, transcript와 raw tool output은 tracked 문서나 사용자 보고에 기록하지 않는다.

## 5. Round 3 성능 검증

- 이전 참조: Round 1·2 실제 호출은 각각 대략 2~3분 범위였다. 당시 runner는 세션을 보존하지 않아 세부 단계별 시간은 기록하지 않았다.
- Round 3 session mode: `BOOTSTRAPPED_FROM_INTERVIEW`
- 해석: Change 008 이전 Round의 transcript가 없으므로 true resume이 아니라 최신 canonical interview를 기준선으로 만드는 1회성 경량 bootstrap이다.
- drift: `INTERVIEW_BASELINE`
- preflight/model/postflight: `1/129/0초`
- 상태: `SUMMARY_CONFIRMATION_REQUIRED`
- 질문·planning: `0/0`
- stderr: `0 bytes`
- private artifact mode: directory `0700`, files `0600`
- output privacy candidate: absolute local path·email·UUID·credential assignment `0/0/0/0`
- Cleanup의 선검증 순서를 보강하면서 runner contract가 바뀌어 최종 코드 기준 drift refresh를 추가 수행했다. 결과는 `REFRESHED_AFTER_DRIFT`, preflight/model/postflight `0/135/0초`, 동일한 확인용 요약, stderr 0이다.
- Persistent Task session: marker/transcript `2/2`, current marker/transcript `1/1`, directory/file `0700/0600`, owner mismatch 0. 두 session은 Task 종료 exact cleanup 대상으로 보존한다.
- 판정: 실행기 전후 overhead는 0~1초로 제한됐지만 총 모델 시간 129~135초는 이전 2~3분 범위 안이다. Round 3은 bootstrap과 contract refresh이므로 session resume에 의한 총시간 개선을 아직 입증하지 않는다. 사용자 요약 답변 뒤 첫 true-resume 호출에서 같은 지표로 비교한다.
- 첫 true resume: 사용자 Round 3 확인 뒤 planning에서 `RESUMED_PLANNING_PREFLIGHT`, baseline reused, drift `UNCHANGED`, preflight/model/postflight `1/264/0초`, stderr 0과 21,804-byte planning 초안을 반환했다.
- 최종 성능 판정: Session 재개·drift guard와 반복 preflight overhead 1초는 입증했다. 그러나 planning은 최신 code·tests 재검증과 장문 output 생성이 필요해 model 시간이 더 길었으므로 전체 대기 시간 단축은 입증하지 못했다. 최적화는 기준선 재조사 중복을 줄이지만 모델 생성 시간을 제거하지 않는다.

## 6. 검증 계획과 상태

- Bash syntax: PASS
- ShellCheck: literal Markdown backtick regex `SC2016`만 의도적으로 제외하고 PASS
- invalid mode·path traversal·missing interview·extra argument·incomplete planning gate: stable failure `5/5` PASS
- Project Rule: runner cleanup `allow`, generic zsh/bash wrapper `prompt`, unrelated script 무일치 `3/3` PASS
- 이전 session cleanup baseline: session/transcript/missing `0/0/0`
- 실제 Round 3 output contract: PASS
- private artifact permissions·stderr·privacy projection: PASS
- persistent session state·transcript exact ownership: PASS
- runner cleanup validation-before-delete static contract: PASS
- 실제 Task session exact cleanup: Task 진행 중이므로 종료 시 검증
- true-resume planning output contract·performance projection: PASS — 기능 재개 성공, 총시간 개선 미입증
- `git diff --check`: governance·USER-FLOW PASS
- Markdown local link·duplicate heading: governance `0/0`, USER-FLOW `0/0`
- Privacy: email·UUID·private key·credential assignment·absolute user path 모두 0
- Changed allowlist: governance 9, USER-FLOW 4, staged·deleted·제품 source diff `0/0/0`
- independent Codex verification: 별도 session 대기

## 7. 승인 상태

- implementationApproved: true
- runtimeMutationApproved: false
- publishingApproved: false
- mergeApproved: false
- userValidationComplete: false

## 8. Finding과 게시 Gate

- Open P0/P1/P2/P3: `0/0/0/0`
- automaticValidationComplete: true
- independentVerificationComplete: false
- publishGate: `NO_GO_VALIDATION_AND_USER_APPROVAL_PENDING`
