# TASK-E2E-FULL-SUITE-001 Change 011 — Pending 인계 검증의 프로젝트 범위 고정

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001 Change 011`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `fix/task-azure-deploy-001-release-workflow-018`
- baselineHead: `4d2336d51b0ce45cb3504980ecffb9c83599d605`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `CHANGE_018_REMOTE_MAIN_PUBLICATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-E2E-FULL-SUITE-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## 승인과 Purpose identity

- approvalSource: `USER_EXPLICIT_RECOMMENDED_FIX_AND_MAIN_MERGE`
- 업무 목표: PR #75 전체 Full-Stack E2E에서 반복 실패한 Pending 인계 확인을 생성한 프로젝트 범위에 고정해 suite 누적 데이터와 무관하게 검증한다.
- Root Finding: `E2E-PENDING-GLOBAL-PAGINATION-COUPLING` — 테스트가 프로젝트별 인계를 확인하면서도 전역 Pending dashboard의 최대 100개 프로젝트 목록에 새 프로젝트가 반드시 포함된다고 가정했다.
- 변경·검증 경계: 해당 spec의 Pending 진입 URL과 화면 준비 assertion만 프로젝트별 canonical route로 바꾸고 targeted 반복 실행과 PR 전체 CI를 확인한다.
- 보존할 불변조건: 제품 UI·API·DB·migration·dependency·runtime·Persistent UAT·Azure 운영 release와 실제 provider를 변경하지 않는다. E2E는 전용 disposable PostgreSQL과 provider 비활성 경계를 유지한다.
- 예상 산출물: test-only 수정, 자동 검증 결과, Roadmap·Implementation report와 PR #75 원격 `main` 게시.

## 확인된 원인

- PR #75의 Full-Stack E2E는 같은 위치에서 두 번 실패했고, 나머지 시나리오는 각각 `55`개가 통과했다.
- 실패 위치는 전역 `/pending` dashboard에서 방금 생성한 프로젝트 button을 찾는 assertion이었다.
- 전역 Pending 화면은 프로젝트 목록을 `pageSize: 100`으로 읽는다. 전체 suite는 이 테스트 전에 100개가 넘는 합성 프로젝트를 누적하므로 새 프로젝트가 전역 첫 페이지에서 제외될 수 있다.
- 해당 spec만 분리한 실행은 `1/1 PASS`였다. 제품별 Pending route `/pending?projectId=<id>`와 프로젝트 제목 heading은 다른 canonical E2E에서도 사용 중이다.

## 변경 allowlist

- `frontend/e2e/full-stack/workflow-continuity-change-003.full-stack.spec.ts`
- `tasks/e2e-full-suite-001-change-011.md`
- `tasks/e2e-full-suite-001-change-011-implementation-report.md`
- `docs/00-product-roadmap.md`

PR #75의 필수 CI를 막는 기존 회귀 결함이므로 사용자의 명시 승인에 따라 Azure Change 018 게시 branch에 이 Change를 함께 싣는다. 별도 제품 기능이나 Azure 실행 범위로 확대하지 않는다.

## 구현 계약

1. `/pending` 전역 dashboard 진입을 `/pending?projectId=${projectId}`로 바꾼다.
2. 전역 dashboard의 프로젝트 button 대신 프로젝트별 Pending 화면의 제목 heading을 기다린다.
3. 기존 screenshot 위치와 이후 자재·IQC 인계 시나리오는 유지한다.
4. test-only diff를 확인하고 Frontend 기본 검증, targeted 반복 E2E와 PR 전체 CI를 통과한 뒤에만 merge한다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | `tasks/e2e-full-suite-001-change-011-implementation-report.md` |
| SOP | report에 포함 | Implementation report의 검증·rollback 절 |
| User manual | `N/A` | 제품 사용법과 화면 동작이 바뀌지 않는 test-only 수정 |
| Roadmap update | 작성됨 | `docs/00-product-roadmap.md` |
| User validation checklist | report에 포함 | 자동 검증과 Git 게시 상태를 분리 기록 |
