# TASK-WORKFLOW-CONTINUITY-001 Change 005 — Pending 활동·재검사 순환 복구

## 1. Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001 Change 005`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `895de8d8666bc588c634ac8bdcb9612f26326335`
- applicableInstructions: Root `AGENTS.md`, `frontend/AGENTS.md`, `backend/AGENTS.md`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `FINAL_BATCHED_USER_VALIDATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-WORKFLOW-CONTINUITY-001`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: Pending 상세의 코멘트와 상태 이력을 하단 전체 폭 활동 영역으로 통합하고, 조치 완료→품질 재검사→불합격 재조치 또는 합격 종결의 반복 순환을 업무·알림·코멘트까지 끊김 없이 복구한다.
- Root Finding: 통합 타임라인 데이터는 구현됐지만 desktop 배치가 오른쪽 보조 열에 남았고, 조치 완료 시 조치 업무가 완료 상태로 인계되지 않았으며, 재검사 불합격 시 기존 Pending 업무는 재개하지만 담당자 알림을 다시 발행하지 않았다. 품질 검사 화면에도 Pending 조치 활동과 재검사 코멘트를 함께 확인·작성할 연결 UI가 없었다.
- 변경·검증 경계: 기존 Pending 상세 UI, 자재 IQC·패널 품질 검사 UI, Pending 상태·업무·알림 동기화와 회귀 테스트만 보정한다.
- 보존할 불변조건: 하나의 품질 부적합은 하나의 Pending을 재사용하고, 조치 완료와 재검사 업무·알림 생성은 같은 transaction에서 처리하며, 재검사 불합격은 새 Pending을 만들지 않고 기존 조치 업무를 재개한다. 합격은 Pending과 검사 차단을 함께 종결한다.
- 예상 산출물: 하단 전체 폭 통합 활동 UI, 품질 재검사 조치 맥락·코멘트 UI, 조치/재검사 업무·알림 순환 보정, Backend·Frontend 회귀 테스트, 화면 증빙과 Implementation report.

## 2. 사용자 수정필요사항별 구현 계약

1. Pending 상세의 코멘트와 상태 변경 이력을 하나의 `코멘트와 처리 이력` 영역으로 유지하되 오른쪽 열을 제거하고, 발생 내용·조치 영역 아래 전체 폭으로 크게 배치한다. 코멘트 입력은 목록 위에 두어 바로 보이게 한다.
2. Pending 조치 완료 시 기존 조치 업무는 완료 처리하고 같은 transaction에서 품질 정·부 담당자의 재검사 업무와 인앱 알림을 생성한다. 동일 전이 재시도는 기존 멱등키로 중복 생성하지 않는다.
3. IQC와 패널 품질 재검사 화면에서 연결 Pending의 조치 코멘트·상태 이력을 확인하고 재검사 코멘트를 추가할 수 있게 한다. 판정 사유도 재검사 상태에서는 `재검사 코멘트`로 명확히 표시한다.
4. 재검사 불합격 시 새 Pending을 만들지 않고 같은 Pending을 `조치 중`으로 되돌리고 기존 정 담당자 업무를 재개하며 정·부 담당자에게 재조치 인앱 알림을 다시 보낸다. 다시 조치 완료하면 다음 재검사 업무가 생성된다.
5. 재검사 합격 시 연결 Pending을 종결하고 기존 조치 업무와 품질 검사 차단을 함께 완료한다.
6. `재검사 요청` 상태에서 품질 담당자가 실제 업무와 해제 경로를 발견할 수 있도록 Pending 상세에 정확한 검사 바로가기를 제공하고, 내 업무·알림·IQC 검사함에 Pending 번호·품목·수량·검사 차수를 표시한다. IQC 검사함은 Pending 재검사와 일반 IQC를 분리하며 최종 판정 전 미완료 조건과 비활성화 사유를 한곳에 노출한다.
7. 상세 IQC 재검사는 직전 실패 회차에서 실제 `부적합`이었던 항목만 표시·저장·최종화 대상으로 삼는다. 재검사 항목은 `적합/부적합`으로만 판정하고 `해당없음`은 허용하지 않으며, 이전 부적합 근거와 Pending 조치 완료 내용을 판정 영역 바로 위에서 함께 비교한다. 대상 항목에 사진 의무가 없으면 사진 단계를 생략한다.

## 3. 실행·게시 경계

- implementationApproved: `true`
- userValidationCompleted: `false` — 이번 검수 실패 보정 후 마지막 일괄 검수 대기
- fableInvocationRequired: `false`
- fableInvocationCount: `0`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
