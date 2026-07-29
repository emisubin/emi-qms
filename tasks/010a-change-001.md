# TASK-010A Change 001 — Codex review 기반 Fable 2차 기획과 실험 구현 승인

## 1. 사용자 요청 source

사용자는 이 실험 branch의 신규 기능을 `Fable 1차 기획 → Codex review → review 내용을 반영한 Fable 2차 기획 → 2차 기획 기준 Codex 구현` 순서로 진행하도록 명시했다. 인터뷰·채택·중간 확인을 다시 묻지 않고 권장안을 적용해 코드·검증·페이지별 screenshot·local commit까지 완료하며, 대표 repo와 GitHub `main`에는 반영하지 않는다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-010A`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-010a-panel-kitting`
- interviewSource: `tasks/010a-interview.md`
- firstPlanningSource: `tasks/010a-planning.md`
- codexReviewSource: `tasks/010a-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/17-panel-kitting-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 직접 완전히 읽고 review의 유지·추가·보류·제거 판단과 모든 resolution을 authoritative implementation contract로 통합한다.

## 3. 2차 기획 필수 반영사항

- 부분 키팅은 프로젝트의 활성 패널 선택 단위 완료, BOM·패널 내부 자재 allocation 제외
- active procurement item 1건 이상 + 전체 derived `receipt_completed=true`의 공통 readiness predicate
- `ProjectRead`·`MaterialReceiptUpdate`와 project access scope 결합
- operationId 기반 batch identity: 동일 operation+payload 성공 replay, operation 재사용+다른 payload conflict
- project row lock + panel lock + all-or-nothing transaction
- panel completion·batch·제조 work item·마지막 stage event·묶음 인앱 notification의 exactly-once
- generic `WorkflowStore.CompleteStageAsync` 호출 금지와 project-level 제조 업무 중복 방지
- 제조 assignee 해석 실패 시 completion 포함 전체 rollback
- Confirm·CloseArrivals readiness transition hook과 existing ready queue의 업무 유무 비의존성
- soft-delete·취소에서 완료 보존, approved permanent purge 회귀 정합, 취소 패널의 open 제조 업무 취소 또는 안전 차단
- 전용 `PanelKittingStore`/contracts/endpoints와 `PanelKittingPage`/type/API helper
- 모바일 좌측 상단 global menu 유지, compact card 선택, desktop 별도 composition
- 기존 0030~0032·panel workflow stage·제조 체크리스트·Persistent UAT·provider·대표 repo 불변

## 4. 구현·Git 승인 경계

- planningApproved: `true` — 2차 Fable 기획이 Codex review resolution을 모두 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/17-panel-kitting-plan.md`의 최소 계약, experiment branch 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 구현·검증·screenshot·종료 산출물 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 5. Fable 사용량 기록

Claude `/usage` 퍼센트는 정수 반올림 값이다.

| 측정 시점 | 전체 모델 사용 | 전체 모델 잔여 | Fable 사용 | Fable 잔여 |
| --- | ---: | ---: | ---: | ---: |
| 1차 planning 직전 | 18% | 82% | 35% | 65% |
| 1차 planning 직후 | 18% | 82% | 35% | 65% |
| 2차 planning 직전 | 19% | 81% | 37% | 63% |
| 2차 planning 직후 | 20% | 80% | 40% | 60% |

1차 planning은 preflight 1초·model 582초·postflight 0초가 걸렸다. 표시 사용량은 정수 반올림 범위 안에서 변하지 않았다.
2차 planning은 기존 session artifact를 재사용해 preflight 0초·model 368초·postflight 0초가 걸렸고, 전체 모델 사용량은 1%p·Fable 사용량은 3%p 증가했다.

## 6. 완료 조건

- 2차 Fable 기획 `openBlockingDecisionCount: 0`
- additive migration과 Backend·Frontend·isolated E2E 구현·검증
- `/materials/kitting` queue·선택·완료와 제조 내 업무의 desktop·390px screenshot
- implementation report·user validation checklist·Roadmap experiment 상태 갱신
- privacy/secret·diff·Finding gate 통과
- current experiment branch local commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
