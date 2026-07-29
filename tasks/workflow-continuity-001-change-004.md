# TASK-WORKFLOW-CONTINUITY-001 Change 004 — 발주 수량 책임과 구매→자재→IQC 실사용 복구

## 1. Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001 Change 004`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `23eed3b1a2f21c5450e0c56a5479c5f5ecf9b05e`
- applicableInstructions: Root `AGENTS.md`, `frontend/AGENTS.md`, `backend/AGENTS.md`, `scripts/AGENTS.md`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `FINAL_BATCHED_USER_VALIDATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- overrideSource: 사용자가 실제 검수 실패 1~3번을 같은 수정 범위로 즉시 구현하라고 명시했다.
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-WORKFLOW-CONTINUITY-001`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 발주 수량·단위 입력 책임을 구매팀으로 고정하고, 구매 저장의 자재 인계와 도착분의 IQC 생성·검사·Pending·재검사·합격 전 경로를 실제 담당자 관점에서 복구한다.
- Root Finding: 대표 `main`의 5174/5081 실행본이 실험 branch와 분리돼 최신 인계·복구 endpoint가 반영되지 않았고, 자재 담당자 fallback이 권한 보유자를 사용자 ID순으로 골라 관리자 계정에 업무를 배정했으며, 자재 도착 API가 구매 수량을 대신 입력하도록 허용해 책임 경계가 뒤섞였다.
- 변경·검증 경계: 기존 구매·자재·IQC API/UI와 workflow 완료 계산을 보정하고 isolated PostgreSQL·Frontend·Full-Stack·desktop/mobile 화면으로 검증한다.
- 보존할 불변조건: 구매 품목 identity, optimistic concurrency, 부서별 mutation permission, 정·부 담당자, 알림 idempotency, 도착과 IQC의 단일 transaction, Pending 재검사 계약과 18단계 전진 흐름을 유지한다.
- 예상 산출물: Change 계약, Backend·Frontend 구현과 회귀 테스트, 실험 runtime 화면 증빙, Implementation report, Roadmap·완료 원장 동기화, experiment local commit.

## 2. 사용자 수정필요사항별 구현 계약

1. 발주 수량·단위는 구매 화면에서만 입력한다. 자재 도착 화면과 요청 계약에서 발주 수량 입력을 제거한다. 발주 수량·단위가 없는 구매품은 자재가 임의 보완하지 못하고 구매팀 입력 필요 안내와 함께 도착을 차단한다. 구매 완료 계산에도 도급 구매품 수량·단위를 포함한다.
2. 구매품 신규·실제 변경 시 프로젝트 자재 정·부 담당자에게 내 업무와 인앱 알림을 생성한다. 프로젝트 담당자가 없으면 실제 `MaterialReceipt.Update` 권한 보유자를 fallback으로 사용하며, 담당자도 fallback도 없으면 저장 성공으로 위장하지 않고 구매 화면에 자재 담당자 지정 필요 오류를 반환한다.
3. 도착 등록은 receipt·IQC attempt·품질 정·부 내 업무·인앱 알림을 같은 transaction에서 보장한다. 품질 담당자가 검사함을 열면 기존 누락 도착과 누락 업무·알림을 멱등 복구한다. IQC 상세 입력, 합격, 부적합 Pending, 조치 완료 재검사와 재검사 합격까지 전체 경로를 회귀 검증한다.

## 3. 실행·게시 경계

- implementationApproved: `true`
- userValidationCompleted: `false` — 이번 검수 실패 보정 후 마지막 일괄 검수 대기
- fableInvocationRequired: `false`
- fableInvocationCount: `0`
- commitApproved: `true` — experiment local commit만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
