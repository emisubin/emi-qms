# TASK-HOME-002 Change 003 — 자재 Home 사급 제공 지연 KPI

## Task Identity Gate

- proposedTaskId: `TASK-HOME-002 Change 003`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-HOME-002`
- roadmapNextGate: `BATCHED_FINAL_USER_VALIDATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-HOME-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-HOME-002`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 자재 사용자가 Home 진입 즉시 고객 사급품의 제공 예정일 초과·미도착 잔량 위험을 발견하고 해당 목록으로 바로 이동한다.
- Root Finding 또는 정책 결정: `MATERIAL-HOME-KPI-OMITS-CUSTOMER-SUPPLY-RISK` P2. 사급 12 EA 중 6 EA가 미도착이고 제공 예정일이 지났지만 Home KPI는 도착·IQC·키팅의 내부 처리 상태만 집계해 모두 0으로 보였다.
- 변경·검증 경계: 기존 `TASK-008B`의 `CustomerSupplied + expected date 경과 + 미도착 잔량 > 0` derived 계약을 Home query와 자재 화면 초기 filter에 재사용한다. 신규 상태·알림·worker·migration은 추가하지 않는다.
- 보존할 불변조건: 부서별 Home metric 최대 3개, project access scope, 자재 mutation 권한, 일반 구매품 호환성, 사급 잔량의 단위별 원장, 대표 repo·`main`·Persistent UAT·provider 불변.
- 예상 산출물: 사급 지연 Home metric, 사급·제공 지연 목록 deep link, Backend fresh PostgreSQL query test, Frontend route/filter test, desktop/mobile screenshot과 implementation report 갱신.

## 구현·게시 경계

- implementationApproved: `true` — 사용자의 남은 P2 연속 처리 지시
- localCommitApproved: `false` — 이번 요청에서 commit을 별도로 지시하지 않음
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 완료 기준

- 제공 예정일이 지났고 미도착 잔량이 있는 사급 품목만 `사급 제공 지연`으로 집계한다.
- 취소 receipt는 도착량에서 제외하고, 전량 도착 뒤 IQC·입고 확정 대기는 고객 제공 지연으로 세지 않는다.
- Home metric 선택 시 자재 입고 화면의 `사급·제공 지연` filter가 적용된다.
- 기존 도착 등록 대기는 자재 화면 summary에서 유지하고 Home 최대 3개 KPI는 `사급 제공 지연`, `IQC 판정 대기`, `키팅 대기 패널` 순으로 표시한다.
