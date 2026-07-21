# TASK-WORKFLOW-CONTINUITY-001 Change 003 — 구매→자재→IQC 실제 인계와 프로젝트 우선 진입 보정

## 1. Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001 Change 003`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `1d83dd2680b71e4c88b2a23462e0c700ab727dac`
- applicableInstructions: Root `AGENTS.md`, `frontend/AGENTS.md`, `backend/AGENTS.md`, `scripts/AGENTS.md`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `FINAL_BATCHED_USER_VALIDATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- overrideSource: 사용자가 이번 수정필요사항 1~6을 한 Task로 즉시 구현하고 번호별 완료 결과를 보고하라고 명시했다.
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-WORKFLOW-CONTINUITY-001`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 구매품 저장 오류를 행·필드 단위로 설명하고 구매 변경을 자재 정·부 담당자의 알림·내 업무로 넘기며, 도착분마다 실제 IQC 업무·검사함 항목이 생성되는지 보장하고 운영 화면을 프로젝트 우선 진입 구조로 통일한다.
- Root Finding: 화면 오류가 서버 field와 구매 행에 연결되지 않았고, 도착 성공 문구가 실제 IQC attempt 생성 여부를 확인하지 않았으며, IQC 내 업무는 정담당자 1건만 생성해 부담당자의 내 업무 계약이 누락됐다. 전역 운영 메뉴도 프로젝트 선택 없이 첫 queue를 직접 열었다.
- 변경·검증 경계: 기존 구매·자재·IQC API와 React routing·운영 화면을 보정하고, isolated PostgreSQL·Frontend unit·Full-Stack E2E·desktop/mobile screenshot으로 검증한다.
- 보존할 불변조건: 동일 `project_procurement_items.id`, optimistic concurrency, 기존 mutation permission, 담당자 정·부 범위, 인앱 알림 idempotency, IQC evidence·Pending 계약과 18단계 workflow를 유지한다.
- 예상 산출물: Change 계약, Backend·Frontend 구현과 테스트, page별 screenshot, Implementation report, Roadmap·실험 완료 원장 동기화, experiment local commit.

## 2. 사용자 수정필요사항과 구현 계약

1. 도급 구매품 저장 오류는 오류 요약에 `공급 유형·품목명·행 번호·문제 필드·해결 방법`을 표시하고 해당 행과 필드를 강조·focus한다. 서버 validation도 가능한 경우 `Items[index].Field`로 반환한다.
2. 구매품 신규 추가 또는 실제 변경 시 해당 품목을 대상으로 자재 정·부 담당자 각각에게 인앱 알림과 내 업무를 idempotent하게 생성한다. 같은 저장 재시도는 중복 생성하지 않고 새 row version의 실제 변경은 새 업무로 기록한다.
3. 전역 자재 메뉴는 `입고 관리`와 `키팅`을 먼저 고른 뒤 프로젝트를 선택하게 한다. 프로젝트 상세와 내 업무 deep link는 기존처럼 정확한 작업 화면으로 바로 이동한다.
4. 도착 등록 transaction은 receipt, IQC attempt, 품질 정·부 내 업무와 인앱 알림을 함께 생성한다. Frontend는 응답의 `iqcAttemptId`와 `IqcRequested`를 확인한 뒤에만 성공으로 표시한다.
5. 자재·제조·품질·물류·Pending 전역 메뉴는 프로젝트 목록을 첫 화면으로 사용한다. 프로젝트 선택 뒤 해당 부서 작업을 조회·수정하며 deep link는 프로젝트 선택 단계를 우회한다.
6. 기존 `Arrived` 상태인데 IQC attempt가 없는 유효 도착분은 품질 담당자가 IQC 검사함을 열 때 idempotent reconciliation으로 복구한 뒤 검사함·내 업무·알림에 표시한다.

## 3. 권한·알림·데이터 경계

- 모든 운영 부서는 기존 `projects.read`/`Project.Read.All` 범위에서 프로젝트 목록을 조회한다.
- 입력은 기존 `ProcurementPlan.Update`, `MaterialReceipt.Update`, `manufacturing.update`, `quality.inspect`, `logistics.ship`, `Pending.Manage` 권한을 바꾸지 않는다.
- 구매 변경과 IQC 인계의 recipient는 프로젝트 담당자 `MaterialsPrimary/Secondary`, `QualityIQC/Secondary`를 우선한다. 정담당자가 없을 때만 기존 permission fallback을 사용한다.
- 실제 Teams·Mail provider는 호출하지 않고 기존 인앱 알림·내 업무만 생성한다.
- DB schema·migration은 추가하지 않는다. 누락 IQC 복구는 기존 table과 idempotency key를 사용한다.

## 4. 검증 계획

- Backend: 구매 신규·변경 시 자재 정·부 work/notification, 같은 version 중복 방지, 도착 시 품질 정·부 work/notification, orphan `Arrived` reconciliation, IQC queue 노출을 통합 검증한다.
- Frontend: 도급 오류의 행·필드 요약/focus, 자재 두 업무 선택, 5개 운영 메뉴의 프로젝트 우선 진입, 실제 IQC 응답 확인과 프로젝트별 IQC 필터를 검증한다.
- Full-Stack: 구매 오류 재현 → 정상 저장 → 자재 정·부 내 업무·알림 → 도착 등록 → 품질 정·부 내 업무·알림·IQC 검사함 → 프로젝트 우선 진입을 실제 API와 UI로 확인한다.
- desktop와 390px에서 프로젝트 목록, 자재 업무 선택, 구매 오류와 IQC 검사함을 screenshot으로 남긴다.

## 5. 실행·Git 경계

- implementationApproved: `true`
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- fableInvocationRequired: `false`
- fableInvocationCount: `0`
- commitApproved: `true` — experiment local commit만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 6. 종료 상태

- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- automatedValidationStatus: `PASS`
- privacySafeEvidenceStatus: `PASS`
- userValidationStatus: `PENDING_FINAL_BATCH`
- implementationReport: [Change 003 구현 보고서](workflow-continuity-001-change-003-implementation-report.md)
- localExperimentCommit: `승인됨 — 구현 보고서와 함께 생성`
