# TASK-E2E-FULL-SUITE-001 Change 010 — 현재 UI 계약 회귀 기준선 복구

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001 Change 010`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baselineHead: `a7651b5c266d73be48e76861a02910435c1371fe`
- roadmapExpectedTaskId: `TASK-E2E-FULL-SUITE-001`
- roadmapNextGate: `완료 회귀 기준선의 사용자 검수 실패를 기존 change로 보정`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-E2E-FULL-SUITE-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true` — 사용자가 남은 문제 4 해결을 명시함
- gateStatus: `PASS_REUSE`
- mainMergeApprovalCount: `0/3`

## Purpose identity

- 업무 목표: 일반 1면과 12면 stress 실제 역할 lifecycle E2E가 현재 프로젝트 등록·업무 선택·프로젝트 펼침·파생 품질 판정·물류 1회 확정·정산 저장 UI를 그대로 사용해 수정 없이 통과하게 한다.
- Root Finding: `E2E-LIFECYCLE-UI-CONTRACT-DRIFT` — Change 008 이후 디자인·업무 화면이 갱신됐지만 두 canonical lifecycle spec은 삭제된 native select, 이전 메뉴·버튼·drawer·판정 dialog와 저장 문구를 기대한다.
- 변경·검증 경계: 두 lifecycle spec의 locator·입력 순서·기대 feedback만 현재 확정 UI에 맞춘다. 제품을 과거 test 계약으로 되돌리지 않는다.
- 보존할 불변조건: 실제 역할 UI 입력, disposable PostgreSQL, provider 비호출, 일반 18단계와 12면·분할입고·반복 Pending stress 시나리오, 최종 open Pending 0건과 프로젝트 완료를 유지한다.
- 예상 산출물: 현재 HEAD에서 별도 임시 patch 없이 통과하는 일반·stress E2E, Implementation report.

## 변경 allowlist

- `frontend/e2e/full-stack/project-lifecycle-user-validation.full-stack.spec.ts`
- `frontend/e2e/full-stack/project-lifecycle-stress-user-validation.full-stack.spec.ts`
- `tasks/e2e-full-suite-001-change-010.md`
- `tasks/e2e-full-suite-001-change-010-implementation-report.md`
- `docs/00-product-roadmap.md`
- `docs/27-experiment-task-ledger.md`

## 검증 계약

- 일반 실제 역할 lifecycle `1/1`: 프로젝트 생성부터 발행 확인·프로젝트 완료, workflow 18단계, open Pending 0건.
- stress lifecycle `1/1`: 12면, 사급 6회 분할 도착, 도급 1회 도착, 반복 Pending 6건, 품질·물류·정산 완료.
- E2E는 실행별 disposable PostgreSQL을 사용하고 container·network·DB를 종료 시 정리한다.
- 실제 Teams·Mail provider, Persistent UAT와 고정 사용자 검수 DB를 사용하지 않는다.
- `git diff --check`, 개인정보·secret·tracked browser artifact와 allowlist를 검사한다.

## 게시 경계

- 현재 실험 worktree에만 변경한다.
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 제외한다.
- local commit은 이번 사용자 요청에 포함되지 않는다.
