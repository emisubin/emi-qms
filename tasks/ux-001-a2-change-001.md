# TASK-UX-001 A2 Change 001 — experiment fast-track과 Task Identity Gate

- proposedTaskId: `TASK-UX-001 A2`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-UX-001 A2`
- roadmapNextGate: `TASK-UX-001 A2 FABLE_2_PASS_PLANNING`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UX-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: A1에서 만든 공통 action feedback 계약을 생산계획·구매·자재·패널·선택 Excel action으로 확대해 action 위치에서 처리 중·성공·부분 성공·실패와 복구 행동을 알 수 있게 한다.
- Root Finding 또는 정책 결정: A1은 완료된 slice이고, A2 화면에는 여전히 분산된 문자열 feedback·전역 message·field 오류 focus 부재가 남아 있다. A1을 다시 만들지 않고 기존 hook/component를 확장한다.
- 변경·검증 경계: Frontend 기존 화면과 공통 feedback/test/browser 증빙만 포함한다. 기존 API 계약을 대조하되 신규 업무 상태·권한·DB·migration·provider는 포함하지 않는다.
- 보존할 불변조건: Backend 권한·validation·상태 전이가 authoritative하다. 동일 action 중복 submit, bulk/row conflict, stale refresh가 사용자 context를 훼손하지 않아야 한다. desktop·390px overflow 0을 유지한다.
- 예상 산출물: A2 interview, Fable 1차 planning, Codex review, Fable 2차 planning, Frontend 구현·tests, desktop/mobile synthetic screenshots, implementation report와 5종 종료 산출물.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

검색 결과는 canonical `TASK-UX-001` 한 건이다. 기존 `experiment/task-ux-001-action-feedback` branch의 A1 commit은 현재 HEAD의 ancestor이고 별도 worktree·동일 목적 open PR은 없다. PR 검색의 문서-only 간접 언급 1건은 A2 목적 구현이 아니다. A1은 `EXPERIMENT_SLICE_COMPLETE`이므로 재구현하지 않는다.

## 실행·게시 경계

- 현재 branch: `experiment/task-home-002-personalized-shell`
- 시작 HEAD: `4c44a9c29eb660052e871e9a45d746a3a19d3a85`
- 대표 repo·`origin/main`: `b8f3e2104074d05c2e71999c08a7374e8729f68f`, 변경 금지
- 실험 standing rule: 사용자-facing interview와 중간 승인을 생략하고 Fable 권장안을 자동 채택한다.
- local commit: 승인됨
- push·PR·merge·Persistent UAT·runtime handover·실제 provider: 제외
- `main` merge 승인: `0/3`

## Fable 2차 기획 승인

- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/31-action-feedback-a2-plan.md`

## Claude 사용량 측정

| 시점 | 5시간 session | 주간 전체 모델 | 주간 Fable | 초기화 정보 |
| --- | --- | --- | --- | --- |
| Fable 1차 직전 | 사용 0% / 잔여 100% | 사용 14% / 잔여 86% | 사용 28% / 잔여 72% | session `05:49 Asia/Seoul`, 전체 `Jul 25 07:59 Asia/Seoul`, Fable reset TUI parse 불가 |
| Fable 1차 직후 | 사용 0% / 잔여 100% | 사용 14% / 잔여 86% | 사용 28% / 잔여 72% | session `05:49 Asia/Seoul`, 전체 `Jul 25 07:59 Asia/Seoul`, Fable reset TUI parse 불가 |
| Fable 2차 직전 | 사용 8% / 잔여 92% | 사용 29% / 잔여 71% | 사용 14% / 잔여 86% | session `05:49 Asia/Seoul`, 전체 `Jul 25 07:59 Asia/Seoul`, Fable reset TUI parse 불가 |
| Fable 2차 직후 | 사용 8% / 잔여 92% | 사용 29% / 잔여 71% | 사용 20% / 잔여 80% | session `05:49 Asia/Seoul`, 전체 `Jul 25 07:59 Asia/Seoul`, Fable reset TUI parse 불가 |
