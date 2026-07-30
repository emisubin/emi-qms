# TASK-QUALITY-OPERATING-MODEL-001 Change 003

## Task Identity Gate

- proposedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- taskType: `HOUSEKEEPING`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapExpectedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- roadmapNextGate: `사용자 검수 뒤 게시·merge 별도 승인`
- roadmapSequenceMatch: true
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: false
- gateStatus: `PASS_REUSE`

## 사용자 검수와 게시 승인

- userValidationCompleted: true
- userValidationCompletedAt: `2026-07-31`
- userValidationSource: `USER_EXPLICIT_CONFIRMATION`
- mainMergeApproved: true
- mainMergeApprovedAt: `2026-07-31`
- mainMergeApprovalSource: `USER_EXPLICIT_MERGE_REQUEST`
- approvalCountRequired: 1
- approvalCountSatisfied: 1
- pushApproved: false
- persistentUatApproved: false
- externalProviderApproved: false

사용자는 `TASK-QUALITY-OPERATING-MODEL-001`의 사용자 검수를 완료했다고 명시하고 현재 변경을 `main`에 병합하도록 승인했다. 이 승인은 local `main` 병합과 그에 필요한 선택 staging·commit·통합 검증을 포함하며, remote push·Persistent UAT migration·runtime handover·실제 외부 provider 활성화는 포함하지 않는다.

## 병합 범위

1. `TASK-WORKFLOW-CONTINUITY-001` Change 017과 migration `0065`
2. `TASK-ATTACHMENT-001`과 migration `0066`
3. `TASK-QUALITY-OPERATING-MODEL-001`과 migration `0067`
4. 위 세 범위의 Backend·Frontend·tests·Roadmap·완료 원장·Task 종료 산출물

과거 Task의 재생성된 screenshot, 기존 runtime artifact와 승인되지 않은 운영 설정은 병합 범위에서 제외한다.

## 병합 검증 계약

- 승인 범위만 개별 경로로 stage한다.
- staged 파일에 secret·PII·runtime artifact·과거 screenshot 변경이 없는지 확인한다.
- 최신 `main`에서 별도 promotion branch를 만들고 실험 commit을 적용한다.
- migration `0065`~`0067`의 순서·fresh DB 적용과 전체 Backend 회귀를 검증한다.
- Frontend lint·typecheck·unit·production build를 검증한다.
- Open P0/P1/P2가 없을 때만 local `main`에 병합한다.
