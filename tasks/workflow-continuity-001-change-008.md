# TASK-WORKFLOW-CONTINUITY-001 Change 008 — 알림 상세 분리와 부분 입고 표시 보정

## Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `83662623e32becfc4f41b085642af747c18f2ac3`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 기존 알림·내 업무 제목을 보존하면서 PC 표 맨 오른쪽 `상세 내용` 열에 기존 두 번째 줄과 변경값·Pending 사유를 표시하고, 자재 입고의 부분 확정 수량·이력 탐색을 실제 업무 의미에 맞춘다.
- Root Finding: 상세 변경을 기존 본문에 단순 연결했고 내 업무 desktop에는 설명이 보이지 않았다. 잔여 수량은 확정량이 아니라 도착량을 차감했으며, 과거 부분 입고는 projection 메모가 없으면 구매 화면에서 표시되지 않았다. 도착·IQC 이력은 작은 summary control에 숨겨져 있었다.
- 변경·검증 경계: 구매·IQC·Pending 알림 및 내 업무 presentation, 부분 입고 조회 projection, 자재 입고 행 이력 interaction만 변경한다.
- 보존할 불변조건: 기존 알림 제목·요약, 수신자·멱등키·권한·감사, 도착분별 IQC, 입고 확정 transaction과 자동 마감 조건은 유지한다.

## 구현 계약

1. 구매 신규·변경 알림과 내 업무는 기존 제목을 유지하고 PC 표 맨 오른쪽 `상세 내용` 열에 기존 본문과 변경 필드를 표시한다. 본문 아래 별도 상세 박스는 만들지 않는다.
2. 값 변화 구분자는 문자 `->`가 아니라 실제 화살표 `→`를 사용한다.
3. IQC 합격 알림 문구는 유지하고 `7/23 도착분이 IQC 합격했습니다. 입고 확정을 진행해 주세요.`를 포함한 기존 본문을 `상세 내용` 열에 표시한다.
4. Pending 알림과 내 업무의 사유도 같은 `상세 내용` 열에 표시한다.
5. 자재 잔여 수량은 `발주·제공 예정 수량 - 입고 확정 수량`으로 계산한다. 고객사 사급 지연 여부는 기존대로 미도착 수량을 사용한다.
6. 부분 입고 projection 메모가 없는 기존 데이터도 구매 조회 시 확정 집계를 계산해 `부분 입고 확정/전체 단위`로 표시한다.
7. 자재 품목 행 자체를 click·Enter·Space로 열고 도착·IQC 이력을 바로 아래에 표시한다. 별도 이력 summary 버튼은 제거하며 행 내부 입력·선택 control은 이력 toggle을 유발하지 않는다.

## 실행 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
