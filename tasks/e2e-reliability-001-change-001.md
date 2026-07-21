# TASK-E2E-RELIABILITY-001 Change 001 — 구매정보 초기 load 동작 잠금

## Task Identity Gate

- proposedTaskId: `TASK-E2E-RELIABILITY-001 Change 001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-E2E-RELIABILITY-001`
- roadmapNextGate: `BATCHED_FINAL_USER_VALIDATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-E2E-RELIABILITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 구매정보 수정 초기 자료가 확정되기 전에 행 추가·저장·Excel 동작을 시작할 수 없게 해 사용자 입력이 늦은 load로 사라지는 틈을 닫는다.
- Root Finding: `PROCUREMENT-INITIAL-LOAD-ACTION-UNLOCKED` P2.
- 기존 Task와의 관계: 본 Task의 request-id guard는 stale 응답은 무시하지만 최신 load 완료 전에 상단 action이 활성화된 경계는 다루지 않았다.
- 변경 경계: `ProcurementEditPage` 초기 readiness·안내 문구와 deterministic frontend regression, 격리 화면 증빙.
- 보존 불변조건: 구매 입력·저장·Excel API, 권한, workflow, DB·migration, 기존 request-id guard를 바꾸지 않는다.

## 승인·운영 경계

- investigationApproved: `true`
- implementationApproved: `true` — 사용자가 남은 P2 자동 진행을 명시
- Fable: `NOT_APPLICABLE` — 기존 확정 계약의 결함 보정
- localCommitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 권장 최소안과 완료 기준

1. project·procurement 둘 다 `ready`일 때만 행 추가·저장·Excel 양식·업로드를 활성화한다.
2. 불러오는 동안 입력 잠금 사유를 `role=status`로 표시한다.
3. stale 첫 응답만 도착해도 action은 계속 잠기고, 최신 응답 완료 후에만 편집 table과 action이 열린다.
4. Backend·API·DB·migration·Persistent UAT·실제 provider·`main`은 변경하지 않는다.
