# TASK-AZURE-DEPLOY-001 Change 031 — 오산 1단계 등록 전용 공개 배포

## 상태

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- roadmapOverrideSource: `USER_EXPLICIT_2026-09-07_OSAN_PHASE1_AZURE_DEPLOY`
- gateStatus: `PASS_REUSE`
- sourceTasks: `TASK-OSAN-ISOLATION-001`, `TASK-OSAN-ACCESS-001`, `TASK-OSAN-PROJECT-001`
- sourceBranch: `feat/task-osan-project-001-project-registration`
- sourceBaseline: `574cea66f602eb65eb1d110801d331151731b0a6`
- initialSourceHead: `b62e5aebdb1b857c11c25f10ae708c869bfdd2ac`
- productionDeploymentApproved: `true`
- gitPublicationApproved: `true`
- mainMergeApproved: `false`
- selectorUserValidation: `PENDING`
- status: `LOCAL_VALIDATION_COMPLETE_AWAITING_DRAFT_PR_CI`

## 승인과 목적

사용자는 2026-09-07 오산 Task 1~3의 로그인·사업부 해석, 소속 없음·local profile pending gate, 총괄 소속 관리, 선택 사업부 local 역할 관리와 오산 프로젝트 등록·목록·상세를 Azure 공개 환경에 배포하도록 명시했다. 이 승인은 read-only 사전 점검, 배포용 코드·기록 작성, 로컬 검증·commit, remote branch·PR·CI 준비와 승인된 Azure 배포 조작을 포함한다. 대표 `main`의 exact merge는 별도 명시 승인이 없으므로 현재 Root 지침의 마지막 병합 gate로 남긴다.

Roadmap의 다음 제품 순서는 Task 4이지만 사용자가 Task 4·5보다 먼저 Task 1~3만 공개 배포하도록 명시해 이번 Change에 한해 partial sequence override를 적용한다. 별도 rollout Task를 만들지 않고 기존 `TASK-AZURE-DEPLOY-001`을 재사용한다.

## 알려진 제약과 사용자 검수 상태

- 일반 사용자가 청주·오산 소속을 동시에 부여받으면 사업부를 전환할 수 없는 문제는 사용자가 이번 배포에서 보정을 보류했다. 1단계 운영에서는 일반 사용자에게 active membership을 정확히 한 곳만 부여한다. 총괄 관리자의 다중 소속은 기존 계약대로 허용한다.
- `TASK-OSAN-ACCESS-001 Change 002`의 selector 숨김 보정은 자동 test와 desktop/mobile 시각 증빙이 완료됐지만 사용자 검수 완료 기록은 없다. 상태를 `USER_VALIDATION_PENDING`으로 유지한다.
- `TASK-OSAN-PROJECT-001 Change 005` 사용자 검수는 완료됐다.

## Implementation Direction Brief

### 현재 동작과 root cause

운영 PostgreSQL에는 기존 단일 업무 DB만 있고 Directory·Osan DB가 없다. 운영 workload는 legacy `QmsDatabase` connection과 DB secret 3개만 연결하며 migration·role bootstrap job도 단일 DB 설정이다. 새 제품 코드는 세 논리 DB와 각각 분리된 runtime/migrator 역할·secret을 요구하므로 현재 배포 정의로 image만 교체하면 Production startup 또는 migration이 fail-closed된다.

### 권장 구현과 순서

1. 기존 단일 업무 DB를 청주 canonical DB로 그대로 사용한다. DB rename·copy나 기존 데이터 이동을 하지 않는다.
2. 같은 Flexible Server에 빈 Directory·Osan DB를 추가한 뒤 세 DB마다 admin/migration/runtime connection을 별도 Key Vault secret으로 둔다. 기존 DB secret 3개는 청주 연결로 재사용해 직전 legacy image rollback 경로를 보존한다.
3. workload와 secret-scope RBAC를 세 DB에 맞게 확장한다. Backend는 runtime 3개, migration job은 migration 3개, role bootstrap은 9개를 읽는다. membership backfill job은 Directory·Cheongju migration connection과 private 승인 ID 목록만 읽는다.
4. role bootstrap → Directory `0001/0002`와 Cheongju·Osan business `0001..0087` migration → 승인 계정 Cheongju membership backfill → Backend digest → Frontend digest 순서를 release script에서 fail-closed로 고정한다.
5. public 활성 전 세 DB identity·exact ledger, bounded role negative probe, backup/restore readiness, Backend readiness를 확인한다. 외부 provider는 청주 기존 설정을 유지하되 Osan provider·escalation·deletion worker는 비활성으로 유지한다.
6. 공개 전후 익명 `200/401/401`, 청주 회귀, 제한된 오산 계정과 등록 준비를 확인한다. 승인된 정정 경로가 없으므로 fake 운영 프로젝트는 만들지 않는다.

### exact allowlist

- `.github/workflows/azure-pilot-images.yml`
- `.github/workflows/ci.yml`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitConfiguration.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitMembershipBackfillRunner.cs`
- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/PublicDeploymentSecurityTests.cs`
- `frontend/e2e/full-stack/business-unit-access.full-stack.spec.ts`
- `frontend/e2e/full-stack/iqc-digital-report.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-adaptive-navigation.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-compact-workspaces.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-first-experience.full-stack.spec.ts`
- `frontend/e2e/full-stack/osan-project-registration.full-stack.spec.ts`
- `frontend/e2e/full-stack/pending-list.full-stack.spec.ts`
- `frontend/e2e/full-stack/project-registration.full-stack.spec.ts`
- `frontend/e2e/mock-ui/osan-project-registration.spec.ts`
- `frontend/playwright.full-stack.config.ts`
- `frontend/src/App.tsx`
- `frontend/src/QrScanLandingPage.tsx`
- `infrastructure/azure-pilot/identity-access.bicep`
- `infrastructure/azure-pilot/identity-access.json`
- `infrastructure/azure-pilot/identity-access.parameters.example.json`
- `infrastructure/azure-pilot/workloads.bicep`
- `infrastructure/azure-pilot/workloads.json`
- `infrastructure/azure-pilot/workloads.parameters.example.json`
- `infrastructure/azure-pilot/README.md`
- `scripts/deploy-azure-pilot-release.sh`
- `scripts/test-azure-pilot-release.sh`
- `scripts/validate-azure-pilot-artifacts.sh`
- `docs/00-product-roadmap.md`
- `tasks/azure-deploy-001-change-031.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `tasks/osan-pilot-001-rollout-handoff.md`
- `tasks/osan-isolation-001-implementation-report.md`
- `tasks/osan-access-001-implementation-report.md`
- `tasks/osan-project-001-implementation-report.md`

### 불변조건과 제외 범위

- Directory/Cheongju/Osan은 같은 server·서로 다른 database·runtime role·migration role·secret을 사용하고 다른 DB로 fallback하지 않는다.
- Business identity 계약은 `0086_business_unit_database_identity`, Directory identity 계약은 `0001_business_unit_directory`로 유지하고 최신 ledger 번호와 섞지 않는다.
- 기존 청주/G2 데이터·provider 설정·public security·single revision·Backend 최대 replica 1을 보존한다.
- 오산 progress mutation·auto-complete·dashboard·Pending/hold/cancel/deleted/Excel과 외부 provider·worker를 활성화하지 않는다.
- migration은 additive이며 down migration·운영 DB overwrite·fake 업무 레코드를 금지한다.
- `latest` tag를 만들지 않고 exact current-main SHA와 immutable image digest만 사용한다.
- Persistent UAT, 실제 외부 provider 시험, Task 4·5 제품 구현, 일반 사용자 dual-membership 결함 보정은 제외한다.

### 관찰 가능한 완료 조건

- 세 DB가 각각 기대 이름·역할·identity로 결속되고 Directory ledger `0001..0002`, 두 business ledger `0001..0087`이 Exact다.
- 기존 청주 aggregate와 G2 기능이 보존되고, 오산 DB의 실제 업무 프로젝트 수는 초기 `0`이다.
- 승인된 현재 계정은 Directory에 연결되고 총괄 또는 정확히 한 사업부 membership과 선택 사업부 local role로 접근한다.
- 오산에서 프로젝트 create/list/detail만 사용할 수 있고 이후 단계 기능은 노출·mutation되지 않는다.
- Backend·Frontend가 새 digest에서 Ready이고 공개 health `200`, 익명 root/API `401/401`이다.
- 직전 Backend·Frontend immutable image가 기록돼 application rollback 가능하며 additive DB 변경은 forward-fix 대상으로 남는다.

### 검증과 증거

- Backend 전체 Release suite, 다중 DB isolation/access/project 집중 suite, migration `0087` upgrade·repeat, public deployment security suite.
- Frontend 전체 test·typecheck·lint·production build, selector desktop/mobile evidence의 기존 artifact와 상태 대조.
- Azure artifact static validation, Bicep compile·tracked ARM 동등성, release script 정상·각 stage 실패·rollback mock.
- PR required CI와 exact head 확인. merge 뒤 main CI와 release run의 exact SHA/digest를 기록한다.
- Azure mutation 전 server/PITR/network/DB 목록, 앱/job mode, secret reference·RBAC projection, 기존 revision/digest를 privacy-safe로 기록한다.
- 실제 실행 후 DB별 identity·ledger·role negative probe와 aggregate, public security, Cheongju/Osan 접근을 privacy-safe count/status로만 남긴다.
- Microsoft 365 실제 사용자 검수와 첫 실제 프로젝트 등록을 직접 관찰하지 못하면 완료로 쓰지 않는다.

### parent 반환 조건

source-of-truth 충돌, destructive operation, 기존 데이터 불일치, 실제 provider 권한 필요, branch protection/environment gate 또는 exact `main` merge 승인이 필요하면 의존 stage를 중단하고 현재 recoverable state와 필요한 한 가지 결정을 보고한다. 한 stage가 실패하면 downstream을 중단하고 Osan을 disabled/isolated로 두며 청주 health를 재확인한다.

## 로컬 검증 결과

- Backend Release 전체 suite `582/582 PASS`, 실패·skip `0`.
- Frontend unit `297/297 PASS`, mock Chromium `13/13 PASS`, typecheck·production build PASS, lint error `0`과 기존 warning `1`.
- Full-Stack은 단일 업무 DB 회귀 `64/64 PASS`, Directory·Cheongju·Osan 전용 business-unit access `1/1 PASS`, Osan create/list/detail·Cheongju 불변 `1/1 PASS`로 최종 `66/66 PASS`다. 각 전용 실행에서 세 DB, 여섯 bounded role, exact migration과 자원 cleanup을 확인했다.
- Bicep compile·tracked ARM 구조 동등성·Azure artifact 정적 검증, release 정상/실패/DB-only/rollback mock, workflow actionlint, shell syntax, main PR CI·CI gate·change scope와 diff check가 모두 PASS다.
- 첫 `55/66` 진단 실행은 서로 다른 DB topology를 같은 harness에 넣은 문제, 개발 사용자 전환 뒤 shell remount를 반영하지 못한 mobile test, QR resolve가 runtime mode 확정 전에 시작되는 실제 race를 드러냈다. 특수 두 시나리오는 전용 3-DB harness로 분리하고 CI에서 별도로 실행했으며, stale UI test를 현재 공용 화면·selector 계약에 맞췄다. QR 화면은 runtime mode가 준비된 뒤에만 resolve하도록 제품 코드 두 파일을 보정했다.
- 운영 Azure mutation, image push, DB/secret/RBAC 변경, migration과 app revision 교체는 `0`건이다. 다음 단계는 Draft PR 필수 CI이며, selector 사용자 검수와 exact `main` merge 승인 전에는 운영 mutation을 시작하지 않는다.
