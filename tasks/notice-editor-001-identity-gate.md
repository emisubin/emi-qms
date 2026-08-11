# TASK-NOTICE-EDITOR-001 — Task Identity Gate

- proposedTaskId: `TASK-NOTICE-EDITOR-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `OPERATING_OBSERVATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-NOTICE-EDITOR-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 공지 본문 일부를 굵게 표시하고 작성자가 기존 공지를 수정하며, 첨부파일을 추가하고 조회 가능한 사용자가 내려받게 한다.
- Root Finding 또는 정책 결정: 기존 `TASK-NOTICE-BOARD-001`은 수정과 첨부를 후속 `NEW_FEATURE`로 제외했다. 사용자는 이번 요청에서 해당 후속 능력을 한 Task로 기획·구현하고, Fable 대신 Codex가 직접 기획하도록 명시했다.
- 변경·검증 경계: 공지 API·DB·Frontend·migration과 격리된 synthetic 검증만 포함한다. 대표 Repository `main`, Persistent UAT, 실제 provider, push·PR·merge는 제외한다.
- 보존할 불변조건: 모든 승인된 active 사용자의 공지 조회, 작성자 본인만 변경·삭제, soft delete, 작성자 snapshot, 알림·내 업무·외부 발송 미생성, 기존 Home 상단·중앙 widget과 프로젝트 병목 계산 무변경.
- 예상 산출물: Codex planning, additive migration, Backend/Frontend 구현, 자동 테스트, desktop·390px 검수 화면, implementation report와 user validation checklist.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 검색 결과와 판정

- `TASK-NOTICE-BOARD-001`은 공지 작성·조회·작성자 soft delete의 기존 기능이며, 해당 기획이 수정과 첨부를 별도 신규 기능으로 명시했다.
- `TASK-ATTACHMENT-001`은 Pending 조치 사진과 재검사 근거 기능으로 업무 목표·데이터·화면이 다르다.
- `notice`, `attachment`, `editor` 목적의 local/remote branch와 GitHub PR에서 같은 목적 후보는 확인되지 않았다.
- Roadmap의 현재 기본 Next Gate는 운영 관찰이지만, 사용자가 공지 기능을 지금 기획·구현하라고 명시해 순서 변경과 구현 경계를 승인했다.
- 따라서 새 canonical Task `TASK-NOTICE-EDITOR-001`을 `PASS_CREATE`로 확정한다.

## 격리 기준선

- branch: `feat/task-notice-editor-001`
- base: `origin/main` SHA `9a25157f0b8d1e78ad5392acf336ebf3c0f61b64`
- worktree: Task 전용 임시 worktree
- 사유: canonical clone과 기존 privacy 후보 worktree의 사용자 소유 WIP를 보존하고, 신규 migration·Backend runtime 검증을 분리한다.
- 예상 종료: 구현·자동 검증·사용자 검수 서버 handoff까지. Commit·push·PR·merge와 worktree 정리는 별도 승인 대상이다.
