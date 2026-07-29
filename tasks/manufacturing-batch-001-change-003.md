# TASK-MANUFACTURING-BATCH-001 Change 003 — 모든 제조 단계 일괄 완료

## 1. Task Identity Gate

- proposedTaskId: `TASK-MANUFACTURING-BATCH-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-MANUFACTURING-BATCH-001`
- roadmapNextGate: `USER_VALIDATION_CORRECTION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-MANUFACTURING-BATCH-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-MANUFACTURING-BATCH-001`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 제조 담당자가 선택한 여러 패널에 대해 조립으로 지정된 한 단계가 아니라 제조 양식의 모든 단계 중 원하는 단계 한 건을 일괄 완료한다.
- Root Finding 또는 정책 결정: Change 002가 사용자의 “단계 한 건만 일괄 완료”를 “조립 의미 단계만 일괄 완료”로 잘못 좁혀 해석했다.
- 변경·검증 경계: 조립/일반 구분 제거, 선택 단계 일괄 완료 API·UI, 원자성·권한·멱등·audit 회귀와 양식 저장 검증을 포함한다.
- 보존할 불변조건: 선택한 단계 외 앞뒤 단계는 유지하고, 패널별 execution·step·actor/time audit, Pending 차단, expected version, project scope와 전부 성공/전부 실패를 보존한다.
- 예상 산출물: Backend·Frontend 구현, 자동 검증, desktop·390px 증빙, 구현 보고·검수 체크리스트·Roadmap·실험 완료 원장 갱신.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 2. 사용자 수정 지시와 원인

사용자는 일괄 처리가 조립 단계 전용이라는 의미가 아니며, 제조 양식의 각 단계 모두를 각각 일괄 완료할 수 있어야 한다고 정정했다. 조립 단계는 중요하지 않으므로 제조 항목의 `조립/일반` 구분도 삭제한다.

Change 002는 “제조 전체나 선행 단계까지 자동 완료하지 말고 한 단계만 완료”해야 한다는 부분은 맞게 고쳤지만, 그 한 단계를 고정된 `Assembly` 역할로 제한한 점이 잘못됐다.

## 3. 수정 계약

1. 제조 양식은 순서와 단계명만 관리한다. 사용자에게 `일반/조립` 구분을 입력·조회하게 하지 않는다.
2. 기존 DB의 `step_role` 컬럼은 과거 snapshot 호환을 위해 당장 파괴적으로 삭제하지 않고 내부 기본값 `General`로만 기록한다. 일괄 처리의 판정에는 사용하지 않는다.
3. 제조 작업의 선택 일괄 창은 선택 패널에 공통으로 존재하는 제조 단계 목록을 제공하고, 담당자가 완료할 단계 한 건을 선택한다.
4. 선택한 단계가 아직 미완료인 패널만 처리 대상으로 분류한다. 이미 완료했거나 단계 순번·이름이 다른 패널은 사유와 함께 제외한다.
5. 서버 요청은 단계 순번과 표시명을 함께 받고, 모든 payload 패널에 같은 순번·같은 표시명의 미완료 단계가 존재하는지 재검증한다.
6. 서버에 전달된 대상은 한 transaction에서 전부 성공 또는 전부 실패한다. 선택한 단계 앞뒤의 제조 단계는 변경하지 않는다.
7. 패널마다 `StepChecked` event 한 건과 execution version `+1`을 기록하고, 기존 batch operation replay·event correlation을 유지한다.
8. batch는 제조 실행 완료, 제조 업무 완료, LQC/OQC 인계, panel workflow stage를 직접 전진시키지 않는다.
9. 기존 프로젝트와 기존 실행의 제조 단계·완료 이력은 유지한다. 과거 `Assembly` 값은 표시·판정에서만 사용하지 않는다.

## 4. 검증 기준

- 제조 양식 화면과 저장 요청에서 `일반/조립` 구분이 사라진다.
- 1단계, 중간 단계, 마지막 단계 중 어느 단계든 선택해 2개 이상 패널에 일괄 완료할 수 있다.
- 한 번의 요청은 선택 단계만 각 패널 1건씩 확인하고 다른 단계는 그대로 둔다.
- 같은 순번의 단계명이 다른 실행, 이미 완료한 단계, 중단·stale 실행을 섞으면 서버 payload 전체를 rollback한다.
- 같은 operation id·payload 재시도는 event와 version을 중복 생성하지 않는다.
- Desktop·390px에서 단계 선택, 대상·제외 사유와 “다른 단계 유지” 안내가 명확하다.

## 5. 승인·게시 경계

- implementationApproved: `true` — 사용자 직접 수정 지시
- fableInvocation: `0` — 사용자가 기존 기능 정정을 Codex가 직접 수행하도록 한 계약 유지
- commitApproved: `false` — 이번 요청에 커밋 지시 없음
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
