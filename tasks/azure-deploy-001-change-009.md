# TASK-AZURE-DEPLOY-001 Change 009 — 공개 Teams 알림 유형과 자동 발송 연결

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `Edge·DNS·TLS·Teams catalog·provider 검수`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- overrideSource: `USER_EXPLICIT_PUBLIC_TEAMS_MANIFEST_AND_BACKEND_IMPLEMENTATION`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `기존 Roadmap 6.5 수신자·발송 시점 재사용, 에스컬레이션 세부 정책만 후속`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_PUBLIC_TEAMS_MANIFEST_AND_BACKEND_IMPLEMENTATION`
- 승인일: 2026-08-05
- 기준 commit: `7467122057af397dbbe14d299f5f6f63c1f90e36` (`origin/main`과 동일)
- 작업 branch: `fix/task-azure-deploy-001-teams-manifest-schema`
- 선행 WIP: Change 008의 v1.19 `packageName` 제거를 보존하고 그 위에 누적한다.
- 실제 Teams catalog 게시·provider 발송: `제외`
- Azure runtime·DB·DNS·TLS mutation: `제외`

## Purpose identity

- 업무 목표: 공개 운영 Teams app manifest가 실제 업무 알림 유형을 선언하고, Backend가 기존 인앱 알림의 확정 수신자와 발생 시점에 Activity Feed delivery를 자동 생성하게 한다.
- Root Finding: 현재 manifest와 renderer는 6개 type만 선언하고, 자동 event는 단계 업무 일부·에스컬레이션 일부 외에 TeamsActivity delivery로 연결되지 않는다. 프로젝트 생성·납기 변경·상태 변경은 공개 운영 요구에 필요한 전용 activity type과 자동 연결이 없다.
- 변경 경계: manifest 10개 type, Backend type/renderer, 기존 인앱 원본 event에 대한 TeamsActivity delivery 연결, 프로젝트 납기·상태 변경 인앱 원본 생성, 공개 Azure의 개인 채널 전략 설정과 자동 검증을 포함한다.
- 보존할 불변조건: 인앱이 공식 원본이고 외부 provider 실패는 업무 transaction을 되돌리지 않는다. 기존 recipient·work item·Pending·프로젝트 권한을 확대하지 않는다. 일일 요약은 Mail 전용으로 유지한다. 에스컬레이션 L0~L3 조건·대상 확대·기한 정책은 변경하지 않는다. 기존 notification·delivery row와 source kind는 보존한다.
- 예상 산출물: Teams v1.19 공개 package `1.0.4`, 자동 Activity Feed delivery와 renderer, 회귀 테스트, 운영 적용 순서.

## 수신자와 발송 시점

| event | 수신자 | 발송 시점 |
| --- | --- | --- |
| 프로젝트 생성 | 기존 프로젝트 생성 참조 대상인 8개 운영 부서 활성 사용자 | 생성 workflow event 성공 직후 |
| 프로젝트 납기 변경 | 활성 영업 담당자와 현재 프로젝트 담당자 | 납기 변경 transaction 성공 직후 |
| 프로젝트 상태 변경 | 활성 영업 담당자와 현재 프로젝트 담당자 | 보류·재개·취소·재활성 transaction 성공 직후 |
| 일반 단계 업무 생성 | 생성된 work item의 정담당자 | work item 생성 직후 |
| 긴급/차단 | 기존 긴급 인앱 수신자 개인 + 기존 통합 Teams 채널 | Pending/차단 알림 생성 직후 |
| 재검사 요청 | 기존 재검사 work item 알림 수신자 | 재검사 요청 transaction 성공 직후 |
| 예정일 임박·초과 | 기존 L0~L2 개인 수신자 | 기존 에스컬레이션 발생 시점 |
| 프로젝트 완료 | 활성 영업 담당자 | 영업 정산 완료 transaction 성공 직후 |

## 승인된 수정 범위

1. manifest activity type을 `projectCreated`, `projectDeliveryDateChanged`, `projectStatusChanged`, `workItemAssigned`, `urgentPending`, `reinspectionRequested`, `deadlineApproaching`, `deadlineOverdue`, `projectCompleted`, `generalNotification`으로 고정한다.
2. Teams에서 사용하지 않는 `dailyDigest` activity type을 manifest에서 제거하고 수동 일반 알림은 `generalNotification`으로 렌더링한다.
3. Backend가 기존 `notification_recipients`와 `notification_deliveries` 원장을 재사용해 자동 TeamsActivity delivery를 만든다. additive migration `0069`는 `notifications.source_kind` 허용 목록에 새 event 5개만 추가한다.
4. 프로젝트 납기·상태 변경은 같은 transaction에서 인앱 원본과 수신자를 생성하며, actor·권한·감사 이력 계약은 유지한다.
5. 프로젝트 생성·재검사·완료 notification에 안정된 `source_kind`를 기록해 제목 문자열 추정 없이 event를 분류한다.
6. 공개 Azure Backend는 일반 개인 Teams 채널 전략만 `TeamsActivity`로 설정한다. 에스컬레이션 worker와 L0~L3 정책은 현재 비활성·기존값을 유지한다.
7. package·Backend 집중 테스트, 전체 관련 회귀, Azure 정적 artifact와 whitespace 검증을 수행한다.

## 제외 범위

- L0~L3 조건·기간·수신자 확대, 부서장·경영진 에스컬레이션
- 사용자별 알림 preference taxonomy·UI 변경
- 일일 요약 Teams 발송
- 새 DB table/column, 기존 row backfill·삭제·수정
- 실제 Teams catalog 업로드, Graph actual 발송, Azure revision 교체
- Frontend 업무 화면 변경

## 변경 Allowlist

- `infrastructure/teams/manifest.template.json`
- `infrastructure/azure-pilot/workloads.bicep`
- `infrastructure/azure-pilot/workloads.json`
- `backend/src/Emi.Qms.Api/Notifications/`
- `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`
- `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`
- `backend/src/Emi.Qms.Api/Sales/SalesSettlementStore.cs`
- `backend/src/Emi.Qms.Api/QualityInspections/QualityInspectionStore.cs`
- `backend/src/Emi.Qms.Api/Materials/MaterialsStore.cs`
- `backend/src/Emi.Qms.Api/appsettings.json`
- `backend/src/Emi.Qms.Api/appsettings.Development.json`
- `backend/tests/Emi.Qms.Api.Tests/`
- `database/migrations/0069_teams_activity_event_source_kinds.sql`
- `scripts/test-teams-manifest-package.sh`
- `tasks/azure-deploy-001-change-009.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 완료 기준

- 생성 package가 v1.19 schema와 10개 exact activity type을 통과하고 `dailyDigest`·`packageName`을 포함하지 않는다.
- 프로젝트 생성·납기 변경·상태 변경·업무 생성·긴급·재검사·프로젝트 완료 event가 기존 수신자 기준으로 중복 없는 TeamsActivity delivery를 만든다.
- 각 delivery renderer가 manifest에 선언된 type과 필요한 template parameter만 생성한다.
- TeamsActivity 비활성·dry-run 테스트에서는 실제 Graph 호출이 없다.
- migration `0069`가 fresh DB와 기존 `0068` ledger upgrade에서 기존 row를 보존하고 exact 적용된다.
- Backend 관련/전체 테스트, package·Azure artifact validation과 `git diff --check`가 통과한다.
- 실제 catalog 게시·Graph actual 수신은 별도 운영 검수로 남긴다.

## 구현 상태

- localImplementationStatus: `COMPLETE`
- automatedValidation: `PASS`
- backendFullRegression: `485/485 PASS`
- teamsPackage: `/Users/parksubin/Downloads/emi-qms-teams-1.0.4-public-notifications.zip`
- teamsCatalogApproval: `SUBMITTED_BY_USER`
- catalogAndActualProviderValidation: `APPROVAL_AND_VALIDATION_PENDING`
- gitPublication: `USER_APPROVED`
