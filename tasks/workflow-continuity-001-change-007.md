# TASK-WORKFLOW-CONTINUITY-001 Change 007 — 구매 변경 상세 인계와 자재 입고 확정 단순화

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

- 업무 목표: 구매품 신규·변경 내용을 자재 담당자가 알림과 내 업무에서 바로 이해하고, 프로젝트별 입고 화면에서 도착 등록→IQC→입고 확정을 별도 마감 없이 끝내게 한다.
- Root Finding: 구매→자재 인계는 품목 버전만 구분하고 변경 필드 상세를 버렸으며, IQC 합격 업무는 자재 정 담당자에게만 생성됐다. 또한 품목 완료값은 수량 충족과 무관하게 별도 `품목 입고 마감`에 의존했고 자재 화면은 품목 대형 카드를 최상위로 배치했다.
- 변경·검증 경계: 구매품 변경 상세 payload, 자재 정·부 IQC 합격 인계, 수량 기반 자동 입고 완료, 프로젝트 우선 자재 입고 UI만 변경한다.
- 보존할 불변조건: 구매팀의 발주 수량 소유권, 도착분별 IQC, 부적합 Pending 차단, 멱등 알림·업무, 권한·감사·transaction 경계를 유지한다.
- 예상 산출물: API 회귀 테스트, compact 프로젝트/품목 행 UI, desktop/mobile 고정 runtime 검수와 구현 보고.

## 구현 계약

1. 구매품 신규 등록은 입력된 주요 필드, 변경은 실제로 바뀐 필드만 안정된 순서로 알림 메시지와 자재 정·부 내 업무 설명에 포함한다.
2. 빈 값은 `-`, 날짜는 `M/d`, 수량은 단위와 함께 표시하며 동일 품목 버전 재시도는 기존 멱등키를 재사용한다.
3. 자재 입고 관리는 프로젝트 한 행을 최상위로 표시하고 프로젝트를 열면 구매품을 한 행씩 표시한다.
4. 각 구매품 행의 `도착입력`은 수량·도착일·비고와 저장·취소만 제공하며 저장 성공 시 기존 transaction으로 IQC 회차·정/부 업무·알림을 함께 만든다.
5. IQC 합격 시 자재 정·부 각각에게 입고 확정 내 업무를 만들고 두 담당자 모두에게 알림을 보낸다.
6. 입고 확정 시 누적 확정 수량이 발주·제공 예정 수량에 도달하면 품목 입고를 자동 마감하고 완료 projection을 갱신한다.
7. 누적 확정 수량이 예정 수량보다 적으면 구매정보의 입고 확정 상태를 `부분 입고`로 표시하고 추가 도착 등록을 계속 허용한다.
8. 정상 흐름에서 별도 `품목 입고 마감` UI를 제거하되 기존 API는 하위 호환을 위해 유지한다.

## 실행 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
