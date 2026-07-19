# TASK-GOV-CODEX-002 Change 016 — 실험 다음 작업 재승인 방지

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 016`
- taskType: `DOCS_GOVERNANCE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `Pending 유형 관리자 화면 (ID 미정)`
- roadmapNextGate: `EXPERIMENT_LEDGER_PRIORITY_1`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 실험 branch에서 사용자가 이미 승인한 인터뷰·중간 승인 생략과 Fable 권장안 자동 채택 규칙이 “다음 작업”마다 다시 승인 질문으로 바뀌지 않게 한다.
- Root Finding: Change 014 fast-track보다 나중에 Roadmap과 완료 원장에 추가된 “정책 입력 전 다음 작업 불가” 문장이 standing instruction보다 강한 gate처럼 해석돼, 이름 있는 다음 Task와 비차단 정책 입력이 있는데도 재승인 질문이 발생했다.
- 변경·검증 경계: Root 지침, 종료 정책, Product Roadmap, experiment 완료 원장, Task identity template와 governance 종료 산출물만 변경한다.
- 보존할 불변조건: 완료 기능 재선택 금지, Repository 충돌·보안 불변조건의 fail-closed, 대표 repo·`main`·Persistent UAT·실제 provider·destructive operation 제외, main merge 승인 `0/3`.
- 예상 산출물: “다음 작업” 선택 규칙, 비차단 정책 자동 채택 규칙, 실제 차단 경계, source-of-truth 일관성 검증과 local experiment commit.

## 2. 사용자 결정

- 이 experiment branch와 대화에서는 새 Task의 인터뷰·중간 승인·채택·확인을 생략한다.
- “다음 작업 시작”은 완료 원장에서 우선순위가 가장 높은 이름 있는 미완료 제품 Task를 즉시 시작하라는 실행 지시다.
- 남은 일반 제품 정책은 Fable 1차 권장안, Codex review, Fable 2차 기획으로 확정하고 blocking decision이 0이면 Codex가 구현·검증·screenshot·local commit까지 이어간다.
- 대표 repo·`main`·Persistent UAT·실제 provider·destructive operation은 자동 진행하지 않는다.
- `main` merge는 서로 분리된 승인 3회 전까지 금지한다.

## 3. 원인과 재발 방지

### 확인된 원인

1. Change 014가 experiment 권장안 자동 채택을 허용했다.
2. Change 015 뒤 Roadmap과 원장에는 Pending 유형 관리자 화면을 `DEFERRED / POLICY_INPUT`으로 두고 “단순 다음 작업만으로 임의 정책을 확정하지 않는다”는 문장이 추가됐다.
3. 다음 session이 더 최근 문장을 사용자 입력이 필요한 blocking gate로 읽어, 기존 standing instruction과 사용자의 실행 지시를 승인 요청으로 되돌렸다.

### 고정한 재발 방지

- 이름 있는 첫 미완료 제품 Task가 하나면 “다음 작업 시작”을 `roadmapSequenceMatch=true`의 명시적 실행 지시로 기록한다.
- `DEFERRED / POLICY_INPUT`의 비차단 선택은 Fable 2-pass 권장안으로 확정하며 사용자 승인 대기로 표시하지 않는다.
- 중단은 canonical purpose ambiguity, Repository source 충돌, 보안·권한 불변조건 위반, fast-track 제외 경계에 한정한다.
- Roadmap·원장·change 갱신이 standing instruction을 약화하거나 재승인 요구를 다시 만들지 못하게 Root 지침과 template에 함께 기록한다.

## 4. 포함·제외 범위

### 포함

- `AGENTS.md`
- `docs/00-product-roadmap.md`
- `docs/12-task-completion-policy.md`
- `docs/27-experiment-task-ledger.md`
- `tasks/_templates/task-identity-gate-template.md`
- `tasks/gov-codex-002.md`
- `tasks/gov-codex-002-implementation-report.md`
- 이 change

### 제외

- Backend·Frontend·API·DB·migration·dependency·runtime·provider
- 대표 repo·`main`·push·PR·merge·worktree 정리
- 제품 Task 자체의 기획·구현 결과. 이 change commit 뒤 별도 canonical 제품 Task로 진행한다.

## 5. 승인 상태

- policyApproved: `true`
- implementationApproved: `true`
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 6. 검증 기준

- “다음 작업”과 비차단 Fable 권장안 자동 채택이 Root 지침·Roadmap·원장·종료 정책에서 같은 의미
- 완료 Task 재선택 금지와 실제 안전 차단 경계 유지
- 일반 branch 승인 계약과 `main` merge 승인 3회 유지
- Markdown local link·duplicate heading·privacy/secret·`git diff --check`
- 제품 source·migration·dependency·runtime diff `0`

## 7. 종료 상태

- 사용자 정책 승인: 완료
- 문서 구현: 완료
- 자동 검증: 변경 문서 `8`, local link missing `0`, duplicate heading `0`, `git diff --check` PASS, 제품 source·migration·dependency·runtime diff `0`
- 사용자 검수: 문서화 요청 자체로 정책 방향 확인 완료
- Git: local experiment commit만 승인. push·PR·merge 미승인
