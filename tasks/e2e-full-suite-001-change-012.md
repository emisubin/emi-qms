# TASK-E2E-FULL-SUITE-001 Change 012 — Pending 프로젝트 전용 링크 메타데이터 복구

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001 Change 012`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `fix/task-e2e-full-suite-001-pending-scope-012`
- baselineHead: `7a8d241d56e2f94b33c3125dd34d95ef4a7158f0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `CHANGE_018_SOURCE_MERGED_OPERATIONAL_RELEASE_SEPARATE`
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
- 업무 목표: 프로젝트별 Pending URL이 전역 최근 100개 프로젝트 목록과 Pending 보유 여부에 관계없이 지정된 프로젝트의 제목과 코드를 정확히 표시하게 한다.
- Root Finding: `PENDING-SCOPED-DEEP-LINK-METADATA-FALLBACK` — 프로젝트별 Pending API는 정확히 필터링하지만 화면 제목은 전역 `pageSize: 100` 프로젝트 목록 또는 첫 Pending에 의존한다. 대상 프로젝트가 목록 밖이고 Pending이 0건이면 일반 제목으로 대체된다.
- 변경·검증 경계: 프로젝트별 Pending 진입 시 기존 프로젝트 상세 API로 정확한 프로젝트 메타데이터를 읽고, loading·error·retry를 명시한다. 전역 Pending dashboard와 등록 dialog의 프로젝트 목록은 기존 동작을 유지한다.
- 보존할 불변조건: Pending 상태·권한·필터·등록·조치·재검사·종결, 전역 dashboard 정렬과 100개 조회, API·DB·migration·dependency·Azure runtime·실제 provider를 변경하지 않는다.
- 예상 산출물: Frontend 최소 수정, 목록 밖·Pending 0건 회귀 test, 변경 spec과 전체 Frontend·Full-Stack E2E·CI 검증, 원격 `main` 병합.

## 변경 allowlist

- `frontend/src/PendingPage.tsx`
- `frontend/tests/App.test.tsx`
- `frontend/e2e/full-stack/workflow-continuity-change-003.full-stack.spec.ts` — 기존 Change 011 회귀를 재사용하며 추가 수정은 원인 대조 뒤에만 허용
- `tasks/e2e-full-suite-001-change-011-implementation-report.md`
- `tasks/e2e-full-suite-001-change-012.md`
- `tasks/e2e-full-suite-001-change-012-implementation-report.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `docs/00-product-roadmap.md`

## 구현 계약

1. 프로젝트별 Pending route는 `initialProjectId`의 프로젝트 상세를 직접 조회한다.
2. 정확한 프로젝트 메타데이터를 읽는 동안 loading을 표시하고, 실패하면 일반 제목으로 조용히 대체하지 않고 error와 retry를 제공한다.
3. 대상 프로젝트가 전역 100개 목록 밖이고 Pending이 0건이어도 실제 제목·코드를 표시한다.
4. 전역 Pending dashboard는 기존 목록·정렬·KPI를 유지한다.
5. Change 011의 생성 프로젝트별 route 검증은 유지하고 전체 suite 누적 데이터에서도 통과해야 한다.

## 게시 경계

- 사용자는 구현·검증·commit·push·PR·`main` 병합까지 승인했다.
- 실제 Azure 운영 release 실행은 포함하지 않는다. Change 018 source 병합과 운영 release 실행은 계속 분리한다.
- branch·worktree 정리, DB·migration과 외부 provider 변경은 포함하지 않는다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 구현 뒤 작성 | `tasks/e2e-full-suite-001-change-012-implementation-report.md` |
| SOP | report에 포함 | 검증·rollback 절 |
| User manual | report에 포함 | 프로젝트별 Pending 진입 동작 |
| Roadmap update | 구현 뒤 작성 | `docs/00-product-roadmap.md` |
| User validation checklist | report에 포함 | desktop·390px·자동·CI·merge 상태 분리 |
