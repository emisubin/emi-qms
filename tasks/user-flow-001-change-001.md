# TASK-USER-FLOW-001 Change 001 — Fable 원문 preview와 GPT-5.6 SOL review

## 1. 사용자 정정

원래 계획은 Fable 5가 `docs/13-user-flow-baseline.md` preview 전문을 직접 작성하고 GPT-5.6 SOL이 그 파일을 사후 review하는 것이다. Codex가 Fable 초안을 다듬거나 대신 파일에 반영하는 방식은 승인 의도와 다르다.

Interview 질문도 Fable 원문을 Codex가 요약·재작성하지 않고 그대로 전달해야 한다. Codex가 Fable 질문의 순서·표현·선택지·권장안을 바꾸거나 질문을 합치고 나누는 것도 금지한다.

## 2. 승인된 실행 계약

1. Fable 5가 preview Markdown 전문을 작성한다.
2. Runner가 Fable stdout을 byte-for-byte로 `docs/13-user-flow-baseline.md`에 직접 기록한다.
3. 기계적 contract·privacy guard는 위반 시 저장을 거부할 뿐 문장을 수정하지 않는다.
4. GPT-5.6 SOL이 read-only로 preview를 검토하고 `tasks/user-flow-001-preview-review.md`에 Finding을 기록한다.
5. Codex는 Fable 원문 파일을 patch하지 않는다.
6. 이 항목의 자동 재작성 계약은 Change 002로 대체됐다. Codex review 뒤 Fable을 자동 재호출하지 않으며, 사용자가 새 전문을 명시적으로 요청한 경우에만 별도 change와 승인 상태를 기록하고 Fable이 전문 전체를 다시 작성한다.
7. 사용자가 preview를 검수하기 전 `docs/02-business-flow.md`와 `docs/04-permission-matrix.md`는 변경하지 않는다.
8. Runner의 contract·privacy 검사는 `거부/통과`만 판정하며 Fable 문장을 교정하거나 대체하지 않는다.
9. Codex 설명이 꼭 필요하면 Fable 질문 원문과 별도 구역에 표시하고 원문 안에 삽입하지 않는다.

## 3. 포함·제외 범위

### 포함

- Fable 5 direct preview 전문
- GPT-5.6 SOL 별도 review
- Planning·review·Roadmap·Implementation report 상태 추적
- Markdown·Mermaid·privacy·source-contract 자동 검증

### 제외

- Codex의 preview 원문 편집
- 기존 `docs/02`·`docs/04` 변경
- Frontend·Backend·API·DB·migration·dependency·runtime·provider 변경
- commit·push·PR·merge와 worktree 정리

## 4. 승인 상태

- planningApproved: true
- reviewResolutionApproved: true
- fableDirectDraftApproved: true
- gpt56SolReviewApproved: true
- phaseAPreviewExecutionApproved: true
- phaseAPreviewUserValidationComplete: false
- phaseBApproved: false
- publishingApproved: false
- mergeApproved: false

## 5. 후속 변경

Change 002가 최신 사용자 의도를 반영한다. 현재 Fable 원문은 보존하고 Codex 내용 review 한 번으로 이번 기획 작성 흐름을 종료한다. 이 Change의 과거 draft·review·revise 반복 기록은 실행 이력일 뿐 향후 기본 절차가 아니다.
