# DESIGN-000 Change 006 — Graphite 프론트엔드 디자인 승격

## Task Identity Gate

- proposedTaskId: `DESIGN-000 Change 006`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `fix/design-000-graphite-promotion`
- baselineHead: `4d15b7cee0d97f1846a1838500f9c9edf11b68bf`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `AZURE_COST_GATE_PENDING`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `DESIGN-000`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true` — 사용자가 Azure Gate를 보류하고 DESIGN-000 Change 006 승격을 먼저 진행하도록 명시했다.
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 독립 Graphite 실험에서 검증한 흑백 wireframe과 프론트엔드 구성 통일을 최신 `main`에 선택적으로 이식해 투박하고 촌스러운 인상을 줄이고, 표 밀도·열 정렬·부서 탐색을 일관된 계약으로 만든다.
- Root Finding:
  - `DESIGN-GRAPHITE-VISUAL-COHESION` — 기존 흑백 wireframe의 굵은 대비·간격·표면 조합이 화면별로 다르고 시각적으로 거칠다.
  - `DESIGN-GRAPHITE-TABLE-DENSITY` — 생산계획·제조 투입의 행 높이와 내 업무·알림의 열 너비 계약이 서로 달라 정보 밀도와 정렬이 불안정하다.
  - `DESIGN-GRAPHITE-DEPARTMENT-NAVIGATION` — 부서별 다중 업무를 별도 선택 화면에만 의존해 반복 진입이 필요하며 desktop sidebar와 mobile drawer의 탐색 규칙이 일치하지 않는다.
- 변경·검증 경계: Graphite CSS foundation, 공통 React primitive와 직접 소비 화면, 생산계획·제조 투입·내 업무·알림 table contract, desktop/mobile 부서 disclosure navigation, 업무 선택 전용 화면 제거와 legacy 부서 root 호환 redirect, 관련 Frontend unit·mock E2E·browser smoke와 Task 산출물.
- 보존할 불변조건: 실제 child route·URL·deep link·업무/권한/API·DB·workflow·상태 의미색·mobile card branch를 유지한다. Backend·DB·migration·배포·실제 provider와 기존 runtime을 변경하지 않는다.
- 예상 산출물: 최신 main 기반 promotion diff, Frontend 전체 검증, desktop·390px privacy-safe 검수, implementation report와 사용자 검수 checklist.

## Source와 fixed allowlist

승격 source는 독립 Graphite 실험 저장소의 검증된 로컬 구현이다. 실험 brief·Fable 계획/보고서·screenshot과 실험 Git history는 승격하지 않는다.

- `frontend/src/App.tsx`
- `frontend/src/DepartmentWorkHub.tsx` — 삭제
- `frontend/src/HomePage.tsx`
- `frontend/src/MaterialsWorkspace.tsx`
- `frontend/src/NotificationPreferenceAuditPage.tsx`
- `frontend/src/OperationalProjectDashboard.tsx`
- `frontend/src/PendingPage.tsx`
- `frontend/src/PendingTypeManagementPage.tsx`
- `frontend/src/design-system/components.tsx`
- `frontend/src/design-system/index.ts`
- `frontend/src/design-system/wireframe.css`
- `frontend/e2e/mock-ui/panel-kitting-smoke.spec.ts`
- `frontend/tests/App.test.tsx`
- `frontend/tests/App.navigation.test.tsx`
- `frontend/tests/design-system-state.test.tsx`
- `frontend/tests/graphite-table-contracts.test.ts`
- `tasks/design-000-change-006.md`
- `tasks/design-000-change-006-implementation-report.md`
- `docs/00-product-roadmap.md`
- `docs/27-experiment-task-ledger.md`

## 구현 계약

1. Graphite 흑백 wireframe은 상태 의미색을 보존하면서 surface·type·spacing·border·control 계층을 공통 token과 primitive로 통일한다.
2. 생산계획·제조 투입 desktop grid header는 36~40px, 단일 행은 44~52px 범위로 유지하고 건강한 다른 업무 table template은 변경하지 않는다.
3. 내 업무·알림 desktop table은 wrapper 100%와 fixed-layout 계약을 사용하고, detail 열이 남는 폭을 흡수하며 action이 있는 일반 행은 48~52px를 유지한다. Mobile card branch는 보존한다.
4. 카드·배너·상태·hero·선택 행의 장식용 왼쪽 강조선, 왼쪽 inset shadow와 `::before` rail을 제거한다. 의미 있는 timeline connector, table/grid divider와 Gantt axis는 보존한다.
5. 기존 `operationalHubConfigs`를 단일 source로 desktop sidebar와 390px drawer에 부서 disclosure navigation을 제공한다. 부서 행 전체가 하위 업무를 여닫고 별도 generic chevron target은 두지 않으며, 처음에는 모두 접혀 있고 한 번에 한 부서만 연다.
6. 업무 선택 전용 화면과 그 화면으로 돌아가는 일반 사용자 흐름을 삭제한다. 부서 child는 기존 실제 업무 URL로 직접 이동하고 mobile drawer는 child 선택 뒤 닫힌다. 기존 부서 root deep link는 shell 준비 뒤 첫 실제 업무로 `replace` redirect해 호환한다.
7. 생산계획↔제조 투입 같은 sibling route 전환에서 이전 화면의 선택·펼침 state가 다음 화면 동작을 뒤집지 않으며 child `aria-current`, 권한, detail/back 흐름을 보존한다.

## 검증 계약

- `corepack pnpm --dir frontend run lint`
- `corepack pnpm --dir frontend run typecheck`
- `corepack pnpm --dir frontend test`
- `corepack pnpm --dir frontend run build`
- mock UI E2E와 관련 navigation/CSS contract test
- desktop 1440px와 390×844에서 대표 route, drawer, overflow, console/request failure 확인
- `git diff --check`, allowlist, dependency·Backend·DB·migration diff 0, 개인정보·secret·generated artifact 검사

## 게시 경계

- 최신 `origin/main` 기반 bounded promotion worktree에서만 구현·검증한다.
- 원본 dirty checkout과 기존 runtime은 변경하지 않는다.
- 사용자는 2026-08-01 사용자 검수를 완료하고 local commit과 local `main` merge를 승인했다. 원격 push·PR·배포는 포함하지 않는다.
- Azure 비용 Gate는 보류 상태로 유지하고 이 변경에서 Azure resource·배포 설정을 수정하지 않는다.
