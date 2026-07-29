# DESIGN-000 Change 005 — 좁은 입력 패널과 흑백 대비 보정

## Task Identity Gate

- proposedTaskId: `DESIGN-000 Change 005`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baselineHead: `a7651b5c266d73be48e76861a02910435c1371fe`
- roadmapExpectedTaskId: `DESIGN-000`
- roadmapNextGate: `완료 foundation의 사용자 검수 실패를 기존 change로 보정`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `DESIGN-000`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true` — 사용자가 확인된 남은 문제 1·2와 검은 배경 글자 가독성 수정을 명시함
- gateStatus: `PASS_REUSE`
- mainMergeApprovalCount: `0/3`

## Purpose identity

- 업무 목표: 물류와 영업 정산의 좁은 입력 영역에서도 공통 입력 제목·설명·3단계 안내가 정상적으로 읽히고, 검은 배경을 사용하는 모든 활성·주요 UI에서 글자가 충분한 대비로 보이게 한다.
- Root Finding:
  - `DESIGN-INPUT-FLOW-NARROW-COLLAPSE` — `DsInputFlow`의 최소 290px 단계 안내와 2열 header가 약 310px 물류 action panel 및 정산 form의 단일 열 안에서 제목 폭을 없앤다.
  - `DESIGN-WIREFRAME-DARK-SURFACE-TEXT-CONTRAST` — 전역 wireframe cascade가 일부 검은 표면의 배경만 강제하고 내부 글자색 계약은 구성요소별 selector에 남겨 낮은 대비 조합이 발생할 수 있다.
- 변경·검증 경계: 공통 `DsInputFlow`의 container-aware compact layout, 물류 action panel과 정산 form의 grid 배치, 검은 표면의 descendant 글자 대비, 관련 Frontend test와 desktop·390px browser 검증.
- 보존할 불변조건: API·DB·권한·업무 상태 전이·입력값·저장 action·URL·상태 의미색은 변경하지 않는다. 흑백·무그림자·사각형 foundation을 유지한다.
- 예상 산출물: CSS 보정, 대비 회귀 test, 물류·정산 desktop/mobile 시각 검증, Implementation report.

## 변경 allowlist

- `frontend/src/design-system/wireframe.css`
- `frontend/src/styles.css`
- 관련 Frontend unit test
- `tasks/design-000-change-005.md`
- `tasks/design-000-change-005-implementation-report.md`
- `docs/00-product-roadmap.md`
- `docs/27-experiment-task-ledger.md`

## 검증 계약

- 물류 action panel과 정산 입력 form에서 `DsInputFlow` header의 제목 영역이 0보다 충분히 넓고 3단계 안내가 header 안에 유지된다.
- 검은 표면의 visible text 대비를 합성 browser 환경에서 검사하고 낮은 대비 조합을 0건으로 만든다.
- Frontend lint, typecheck, unit, build를 실행한다.
- Desktop 1440px와 390px에서 물류·정산 입력의 수평 overflow, header collapse, console error를 확인한다.
- 기존 미커밋 WIP를 보존하고 `git diff --check`, allowlist와 개인정보·secret을 검사한다.

## 게시 경계

- 현재 실험 worktree에만 변경한다.
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 제외한다.
- local commit은 이번 사용자 요청에 포함되지 않는다.
