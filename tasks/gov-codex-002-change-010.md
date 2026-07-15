# TASK-GOV-CODEX-002 Change 010 — Fable 단일 초안과 Codex 내용 Review

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 010`
- taskType: `POLICY_DECISION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-USER-FLOW-001`
- roadmapNextGate: `CONTENT_REVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: Fable과 Codex의 기획 산출물 왕복을 없애고 `Fable primary draft 1회 → Codex 내용 review 1회 → 종료`로 단순화한다.
- Root Finding: 기존 Change 009는 정확성 Finding을 Fable revise로 반복 해소해 draft·review round가 세 차례 이어졌고, Codex review가 기획 품질보다 코드 정합성 검증에 치우쳤다.
- 변경 경계: Root/Fable 지침, Fable runner, governance 산출물, Roadmap과 현재 USER-FLOW review 계약.
- 보존할 불변조건: Fable 원문 무편집, 질문 원문 전달, read-only Fable 도구, 제품 구현·runtime·DB·provider·Git 게시 경계.
- 예상 산출물: 단일 초안·내용 review 정책, runner planning direct write와 explicit-redraft gate, USER-FLOW 내용 review.

## 2. 사용자 결정

- Fable은 primary draft 전문을 한 번 작성한다.
- Codex가 내용·제품 방향 review를 한 번 작성하면 기획 작성 흐름은 끝난다.
- Codex review를 이유로 Fable 수정·재검토 round를 자동 반복하지 않는다.
- 사용자가 review 뒤 새 전문을 명시적으로 요청한 경우에만 별도 change를 만들고 Fable redraft를 한 번 실행할 수 있다.
- Review의 중심은 코드 오류가 아니라 개발 방향, 사용자 가치, 기능 필요성, 우선순위, 누락 기능, 과도한 범위와 trade-off다.
- Code 대조는 구현 가능성과 기존 계약 충돌을 판단하는 보조 근거로만 사용한다.

## 3. Review 고정 관점

1. 이 기획이 사용자가 해결하려는 문제에 직접 도움이 되는가
2. 제품 방향과 Roadmap에 맞는가
3. 각 기능이 필요한가, 불필요하거나 너무 이른 기능은 무엇인가
4. 지금 유지·추가·보류·제거할 기능은 무엇인가
5. 누락된 사용자 흐름·운영 기능·성공 기준은 무엇인가
6. 기능 간 의존성과 권장 개발 순서는 무엇인가
7. 1인 개발 속도에 비해 문서·운영·기술 범위가 과도하지 않은가
8. 더 단순한 대안과 명확한 trade-off가 있는가

## 4. 구현 범위

### 포함

- Root `AGENTS.md` 단일 초안·내용 review 계약
- `CLAUDE.md` single primary draft 계약
- Runner `planning` stdout의 planning target byte-identical direct write
- 기존 planning·draft target의 atomic no-overwrite와 symlink 차단
- `revise`의 사용자 명시적 redraft 승인 marker와 one-time private receipt 강제
- USER-FLOW 현재 문서의 GPT-5.6 SOL 내용 review 1회
- Task·Implementation report·Roadmap 상태 정렬

### 제외

- Fable 원문 수정
- Codex review 뒤 자동 Fable 재작성
- Frontend·Backend·API·DB·migration·dependency·runtime·provider 변경
- commit·push·PR·merge와 worktree cleanup

## 5. 승인 상태

- policyApproved: true
- implementationApproved: true
- contentReviewApproved: true
- automaticFableRevisionApproved: false
- runtimeMutationApproved: false
- publishingApproved: false
- mergeApproved: false

## 6. 완료 Gate

- Fable planning direct-write와 existing-target failure 검증
- Fable revise user-approval failure 검증
- 기존 target 경쟁 생성 no-overwrite와 redraft approval 재사용 차단 검증
- Root·Fable 지침의 single-pass·content-review contract 확인
- USER-FLOW Fable 원문 source modification 0
- GPT-5.6 SOL 내용 review 1회 완료
- 제품 source·runtime·DB·provider mutation 0
- 사용자 검수와 별도 게시 승인
