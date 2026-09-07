# TASK-OSAN-ACCESS-001 Change 002 — 실제 전환 가능한 사용자에게만 사업부 선택 UI 표시

## 1. 승인·Gate·기준선

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_AWAITING_USER_VALIDATION`
- canonicalTask: `TASK-OSAN-ACCESS-001`
- canonicalChange: `TASK-OSAN-ACCESS-001 Change 002`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- roadmapExpectedTaskId: `TASK-OSAN-PROGRESS-001`
- roadmapNextGate: `TASK-OSAN-PROGRESS-001_IMPLEMENTATION_APPROVAL`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- implementationApprovalSource: `USER_EXPLICIT_2026-09-07_SELECTOR_VISIBILITY_FIX`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH_ONLY`
- implementationOwnerObserved: `NOT_REPORTED`
- gpt6ReviewRequested: false
- gpt6ReviewProhibitedByUser: true
- implementationBranch: `feat/task-osan-project-001-project-registration`
- implementationBaseline: `d7401610311638317a2606416845492f03ae58fb`
- originMainBaseline: `574cea66f602eb65eb1d110801d331151731b0a6`
- gitPublicationApproved: false
- persistentRuntimeMutationApproved: false
- providerOperationApproved: false

이 요청을 “active membership과 active business unit에서 계산된 `allowedBusinessUnits`가 실제 두 곳 이상일 때만 사업부 이동 UI를 표시하고, 단일 청주·단일 오산·미소속 사용자는 총괄 직함 여부와 관계없이 선택 control과 label을 전혀 보지 않게 한다”로 이해했다. 사용자는 현재 Roadmap의 Task 4보다 이 검수 결함 보정을 먼저 구현하라고 명시했으므로 기존 Task 2의 다음 Change를 재사용한다. 사용자 지시는 필요한 제품·테스트·Task·Roadmap 기록과 local commit을 승인하며 push·PR·merge·`main`·Persistent UAT·실제 provider·운영 적용 권한은 확대하지 않는다.

## 2. Purpose identity와 검색 결과

- 업무 목표: 실제 다른 사업부로 이동할 수 있는 사용자에게만 사업부 선택 UI를 표시한다.
- Root Finding: 정상 shell은 `allowedBusinessUnits.length > 1`을 검사하지만 업무 진입 전 gate는 `length > 0`을 검사해 단일 소속 총괄에게도 한 옵션뿐인 이동 버튼을 표시했다.
- 변경·검증 경계: Frontend 가시성 판정과 unit/browser 회귀, canonical Task/report/Roadmap 및 Task 3 사용자 검수 완료 기록만 변경한다.
- 보존할 불변조건: Backend membership·overall 권한 검사, active membership/business-unit filtering, 탭별 선택, 다중 사업부 전환, mutation 중 잠금, 소속 회수·오류 fail-closed, 청주·오산 업무 기능과 데이터 경계를 유지한다.
- 예상 산출물: 공통 가시성 판정, single Cheongju/Osan·multi·미소속·loading/error·desktop/mobile 검증, Change/report/Task/Roadmap 기록과 local commit.
- 검색 범위: `tasks/`의 관련 Task·planning·review·change·report, Roadmap 실행 큐·추적 99·Decision Log, local/remote branch·worktree와 open/merged PR을 확인했다.
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-OSAN-ACCESS-001`
- 새 Task·branch·worktree·planning은 만들지 않는다.

## 3. Implementation Direction Brief

### 현재 동작과 원인

Backend의 `BusinessUnitDirectoryStore`는 active identity에 대해 active membership과 active business unit만 결합하고 known code를 중복 제거해 `allowedBusinessUnits`를 만든다. Frontend header의 desktop/mobile selector는 이미 총괄이면서 이 collection이 두 곳 이상일 때만 보였다. 반면 `BusinessUnitAccessGate`는 총괄이면서 한 곳 이상이면 이동 버튼 목록을 보여 단일 소속 사용자가 선택할 수 없는 UI를 보게 했다.

### 구현 방식과 순서

1. `BusinessUnitAccess`를 받아 총괄 여부와 `allowedBusinessUnits.length > 1`을 함께 판정하는 공통 helper를 둔다.
2. desktop header, mobile header와 access gate가 모두 이 helper를 사용한다.
3. 단일 청주·단일 오산의 gate와 정상 shell에서 selector·이동 button이 없고, 기존 다중 전환과 mutation 잠금이 유지되는지 component/browser에서 검증한다.
4. Task 3 Change 005의 사용자 검수 완료와 이번 보정의 별도 사용자 검수 상태를 canonical 기록에 분리한다.

### Exact allowlist

- `frontend/src/App.tsx`
- `frontend/tests/BusinessUnitAccess.test.tsx`
- `frontend/e2e/mock-ui/business-unit-access.spec.ts`
- `tasks/osan-access-001-change-002.md`
- `tasks/osan-access-001.md`
- `tasks/osan-access-001-implementation-report.md`
- `tasks/osan-project-001-change-005.md`
- `tasks/osan-project-001.md`
- `tasks/osan-project-001-implementation-report.md`
- `tasks/osan-pilot-001.md`
- `tasks/osan-pilot-001-implementation-report.md`
- `docs/00-product-roadmap.md`

### 제외 범위와 반환 조건

API·DB·migration·authorization 능력, membership 관리 정책, 총괄 designation UI, 사업부 수 확대, 다른 오산 업무 기능은 변경하지 않는다. Source-of-truth 충돌, 새 데이터 계약, destructive operation, 실제 provider·runtime·게시 권한이 필요하면 그 의존 작업만 중단해 보고한다.

## 4. 완료 조건과 검증 계획

- 단일 청주·단일 오산의 총괄과 비총괄 사용자는 정상 shell과 진입 gate에서 selector, 단일 이동 button과 빈 label을 보지 않는다.
- 미소속, selection denied, inactive/unavailable membership이 제외된 결과와 loading/error 상태에서 선택 UI가 나타나지 않는다.
- active 목적지가 두 곳 이상인 총괄은 기존 desktop/mobile 선택, 탭별 context, mutation 중 disabled와 전환 후 요청 무효화 동작을 유지한다.
- Frontend 집중·전체 test, lint, typecheck, build, mock Chromium desktop·390px, `git diff --check`, privacy/secret/generated artifact와 문서 링크 검사를 통과한다.
- 변경 화면을 직접 확인하고 open P0/P1/P2를 0으로 닫는다. 사용자의 지시에 따라 GPT-6 review는 실행하지 않는다.

## 5. 구현·검증 결과

- `canSwitchBusinessUnit`이 `isOverallAdministrator`와 active `allowedBusinessUnits.length > 1`을 함께 검사한다. 정상 shell의 desktop/mobile header와 access gate가 같은 판정을 사용한다.
- 단일 청주·단일 오산 총괄은 local profile pending gate와 selected shell 모두에서 selector, 단일 이동 button과 selector label을 보지 않는다. 총괄 designation 자체로 가시성을 열지 않는다.
- 복수 active 목적지 총괄의 기존 선택, 탭별 context, 저장 중 selector disabled와 전환은 그대로 유지한다.
- Backend·API·DB·migration은 변경하지 않았다. 기존 `BusinessUnitDirectoryStore`가 active identity, active membership과 active business unit만 결합하고 known code를 중복 제거한 collection을 그대로 신뢰한다.

검증 결과:

- `corepack pnpm exec vitest run tests/BusinessUnitAccess.test.tsx`: `1 file / 18 tests PASS`.
- `corepack pnpm exec vitest run`: `36 files / 297 tests PASS`.
- `corepack pnpm exec playwright test e2e/mock-ui/business-unit-access.spec.ts`: `3/3 PASS`. 기존 복수 사업부 전환·탭별 선택과 단일 소속 desktop/mobile gate·shell을 함께 검증했다.
- `bash scripts/test-business-unit-isolation.sh`: 실제 PostgreSQL 3-DB 경계 `2/2 PASS`. inactive membership·inactive business unit, 총괄/일반 권한과 소속 회수 경계를 포함한다.
- `corepack pnpm run lint`: 오류 0, 기존 `frontend/src/main.tsx` Fast Refresh warning 1.
- `corepack pnpm run typecheck`: `PASS`.
- `corepack pnpm run build`: 399 modules, `PASS`; 기존 500 kB chunk warning만 남았다.
- 1440×900·390×844의 단일 오산 gate와 shell screenshot 4개를 직접 확인했다. selector와 빈 label이 없고 390px horizontal overflow는 0이다.
- 변경 문서 local Markdown link 227개를 확인해 누락 0건, 추가 diff의 non-fixture email/private key/bearer/client secret 0건, tracked/staged browser·build artifact 0건과 `git diff --check` PASS를 확인했다.
- 제품·test·문서 self-review의 open P0/P1/P2는 `0/0/0`이다. 사용자 지시대로 GPT-6 review는 요청하거나 실행하지 않았다.

## 6. 사용자 검수·게시 경계

Task 3 Change 005 현재 화면은 사용자가 이 요청에서 검수 완료했다. 이번 selector 가시성 보정은 자동·시각 검증 완료 뒤 별도 사용자 검수 대상으로 남긴다. Local commit은 승인됐고 push·PR·merge·`main`·Persistent UAT·실제 provider·운영 적용은 승인되지 않았다.
