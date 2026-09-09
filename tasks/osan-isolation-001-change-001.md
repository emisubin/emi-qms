# TASK-OSAN-ISOLATION-001 Change 001 — 승인된 사업부 데이터 분리 구현

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- status: `USER_VALIDATION_PENDING`
- implementationApproved: true
- isolatedTestRuntimeApproved: true
- productionRuntimeMutationApproved: false
- gitPublicationApproved: false
- approvalSource: 사용자에게 공통 소속 DB를 포함한 3개 DB 구조와 로그인·데이터 접근·자동 작업·격리 테스트 범위를 제시한 뒤 받은 “승인.”
- approvalDate: 2026-09-06

## 승인 계약

같은 PostgreSQL 서버에 청주 업무 DB, 오산 업무 DB, 최소 공통 소속 DB를 둔다. 공통 DB는 인증 identity·사업부 membership·총괄 지정·소속 변경 감사만 소유한다. 업무 데이터와 업무 권한은 각 업무 DB에 남긴다. 이 기술 권장안은 이번 승인으로 확정됐으며 이전 기획 승인으로 소급하지 않는다.

신뢰된 인증 → 소속 확인 → 허용 사업부 선택 → 해당 DB의 local 권한 순서로 연결한다. 미소속 사용자 자동 청주 배정과 장애 시 다른 DB fallback을 금지한다. 기존 청주 사용자 ID·이력·권한·G2, ReviewSafe와 provider 안전 계약을 보존한다. 로그인·요청과 worker·migration·bootstrap·health의 연결을 명시적으로 구분한다. 오산 외부 알림은 enqueue와 dispatch 모두 차단한다.

이 승인은 Task 1 구현·격리 테스트만 포함한다. 후속 프로젝트/진행 Task의 review resolution, 관리자 UI, 8개 입력·7단계 화면·대시보드 구현을 함께 승인한 것으로 기록하지 않는다. 실제 Azure DB·운영 credential 생성, Persistent UAT 변경, 실제 provider, commit·push·PR·merge·기존 WIP 정리는 포함하지 않는다.

## 착수 gate와 기준선

- proposedTaskId / canonicalTaskId: `TASK-OSAN-ISOLATION-001`
- instructionChainRead: true
- instructionConflictCount: 0
- planningOwner: `GPT_6_ASTRA_HIGH`
- implementationOwner: `GPT_5_6_SOL_XHIGH`
- verificationOwner: `FRESH_GPT_6_ASTRA_HIGH`
- roadmapExpectedTaskId: `TASK-OSAN-ISOLATION-001`
- roadmapNextGate: `IMPLEMENTATION_SCOPE_APPROVAL` → 이번 사용자 승인으로 충족
- roadmapSequenceMatch: true
- samePurposeMatchCount: 1
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: false
- experimentStandingInstructionApplies: false
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`
- baseSha: `574cea66f602eb65eb1d110801d331151731b0a6` — remote main과 local origin/main 일치 확인
- canonicalBranch: `fix/task-gov-codex-002-instruction-clarity`
- implementationBranch: `feat/task-osan-isolation-001-db-boundaries`
- 적용 지침: Root·Backend·Scripts AGENTS, Roadmap, 종료 정책, Validation Matrix, Privacy-safe Evidence, 상위 기획·review·보고 및 이 change. Frontend source 변경과 experiment 원장은 비적용.

Purpose identity는 사업부별 소속 판별과 DB/worker/migration 연결 경계다. tasks·Roadmap·Decision Log·local/remote refs·worktree·PR 목록을 대조했다. PR 22의 기존 TASK-E2E-ISOLATION-001은 테스트 자원 분리이며 제품의 사업부 분리와 다른 목적이다. 같은 목적 Task는 위 canonical Task 한 건이다. PR 상세 조회가 자동 승인 검토에서 거절돼 허용된 PR 목록의 제한된 metadata로 목적을 확인했다.

## 작업공간과 보존

공통 DB 신규 초기화, 기존 업무 schema 이전과 DB role 분리 rehearsal에 물리 격리가 필요하므로 Task 전용 임시 worktree를 사용한다. 기존 canonical clone의 37개 미커밋/미추적 파일을 보존하고 branch를 전환하지 않는다. 승인·상태 기록에 해당하는 오산 문서와 Roadmap의 오산 구역만 갱신한다.

- purpose: 사업부 DB 분리 구현·위험한 migration/bootstrap 격리 검증
- owner: TASK-OSAN-ISOLATION-001의 Sol implementer, parent 관리
- workspaceAlias: `OSAN_ISOLATION_TEMP_WORKTREE`
- 기준 SHA: 위 baseSha
- 예상 종료: 구현·독립 검증 후 사용자 검수 handoff. 미커밋 결과가 남아 있으면 보존한다.
- cleanup 경계: 이번 실행이 만든 synthetic DB/container/temp artifact만 소유 확인 후 정리한다. worktree·branch 삭제나 기존 WIP 정리는 실행하지 않는다.
- 기존 5081/5174/5082/5175 listener: 착수 시 각 0. 다른 runtime의 부재를 뜻하지 않으며 기존 process와 DB는 건드리지 않는다.

현재 미커밋 instruction chain과 오산 문서를 worktree에 참조용 복사한다. 복사된 기존 WIP는 구현 변경으로 계산하지 않고 implementer가 수정하지 않는다. Parent는 시작 digest 대비 구현 변경과 기존 문서 차이를 구분한다.

## 구현 allowlist와 인계

기존 파일의 초기 allowlist:

- backend/src/Emi.Qms.Api/Program.cs
- backend/src/Emi.Qms.Api/DatabaseConnectionStringProvider.cs
- backend/src/Emi.Qms.Api/DatabaseHealthChecker.cs
- backend/src/Emi.Qms.Api/DatabaseMigrationRunner.cs
- backend/src/Emi.Qms.Api/DatabaseRoleBootstrapper.cs
- backend/src/Emi.Qms.Api/DatabaseRuntimePrivilegeManager.cs
- backend/src/Emi.Qms.Api/Authorization/EntraClaimsTransformation.cs
- backend/src/Emi.Qms.Api/Authorization/AuthorizationServiceCollectionExtensions.cs
- backend/src/Emi.Qms.Api/Authorization/QmsClaimTypes.cs
- backend/src/Emi.Qms.Api/Authorization/AuthorizationAuditLogger.cs
- backend/src/Emi.Qms.Api/Identity/DbIdentityStore.cs
- backend/src/Emi.Qms.Api/Identity/HybridIdentityStore.cs
- backend/src/Emi.Qms.Api/Identity/DevelopmentIdentitySeeder.cs
- backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryWorker.cs
- backend/src/Emi.Qms.Api/Notifications/NotificationEscalationWorker.cs
- backend/src/Emi.Qms.Api/Notifications/NotificationDispatcher.cs
- backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryStore.cs
- backend/src/Emi.Qms.Api/Admin/AdminDeletionPurgeWorker.cs
- backend/src/Emi.Qms.Api/ReviewSafe/DatabaseMigrationCatalog.cs
- backend/src/Emi.Qms.Api/ReviewSafe/ReviewSafeStatusService.cs
- backend/src/Emi.Qms.Api/ReviewSafe/MigrationLedgerInspector.cs — 기존 승인 legacy schema probe를 사전 검사에서 재사용할 수 있게 하는 최소 공개 범위 변경. 기존 inspection·호환성 정책은 보존.
- backend/src/Emi.Qms.Api/Security/DatabaseOperationSecurityPolicy.cs
- backend/src/Emi.Qms.Api/Security/ProductionSecurityPolicy.cs
- backend/tests/Emi.Qms.Api.Tests/QmsWebApplicationFactory.cs
- backend/tests/Emi.Qms.Api.Tests/PostgreSqlTestDatabase.cs
- backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs
- backend/tests/Emi.Qms.Api.Tests/PublicDeploymentSecurityTests.cs
- backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj
- database/README.md

신규 BusinessUnits 파일, directory 전용 migration, 설정 예시, 격리 테스트·실행 harness는 구현자가 필요 경로를 열거하고 parent가 같은 승인 범위에 해당하는지 확인한 뒤 exact allowlist에 추가한다. 변경하지 않은 모든 DB 소비자도 connection을 매 호출에 해석하는지, singleton cache나 직접 연결 우회가 없는지 조사한다. 다른 기존 파일이 필요한 경우도 경로와 이유를 먼저 parent에게 전달한다. 이는 구현 범위 내 파일 선정이며 같은 사용자 승인을 재요청하지 않는다.

Parent 소유 문서는 이 change, 기존 Task/상위 Task/Roadmap의 오산 상태와 구현 보고다. Implementer는 `tasks/osan-isolation-001-implementation-report.md`를 작성할 수 있다. 기존 planning/review 원문과 지침 WIP는 수정하지 않는다.

### Parent가 확인한 추가 exact 경로

아래 경로는 기존 승인 범위 내 구현에 필요하다는 Sol의 조사 결과를 parent가 확인했다. 기존 migration은 0085까지이므로 0086을 신규로 사용한다. 파일 선정 확인은 새 업무 권한이나 운영 승인이 아니다.

- backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitConfiguration.cs
- backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitRequestContext.cs
- backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitDirectoryStore.cs
- backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitResolver.cs
- backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitCapabilityMiddleware.cs
- backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitMembershipBackfillRunner.cs
- backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitDirectoryMigrationCatalog.cs
- database/directory-migrations/0001_business_unit_directory.sql
- database/migrations/0086_business_unit_database_identity.sql
- backend/src/Emi.Qms.Api/appsettings.BusinessUnits.example.json
- backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs
- backend/tests/Emi.Qms.Api.Tests/AuditInfrastructureTests.cs — 신규 qms_database_identity를 migration 소유·runtime 쓰기 금지 metadata로 분류하고 제외 개수만 갱신. 기존 업무 감사 대상 94개는 보존.
- scripts/test-business-unit-isolation.sh
- backend/src/Emi.Qms.Api/Authorization/DevelopmentAuthenticationHandler.cs
- backend/src/Emi.Qms.Api/Identity/IdentityEndpointExtensions.cs
- backend/src/Emi.Qms.Api/Identity/UserAdministrationStore.cs — 분리 모드의 관리자 사용자 목록에서 전역 개발용 메모리 계정 혼입 방지. 기존 단일 DB 동작 보존.
- backend/src/Emi.Qms.Api/Notifications/WorkItemEscalationStore.cs
- backend/src/Emi.Qms.Api/Notifications/NotificationEscalationService.cs
- backend/src/Emi.Qms.Api/Admin/AdminScheduledDeletionService.cs
- backend/Dockerfile.production — directory migration을 build/runtime artifact에 포함하는 최소 packaging 변경. 이미지 게시·배포는 제외.

상세 책임: BusinessUnits는 신뢰된 요청 context·소속 조회·연결 설정·capability·명시적 기존 사용자 연결과 directory migration을 소유한다. 인증·identity 추가 경로는 업무 DB 조회 이전 소속 판별과 미소속 응답, worker/store 추가 경로는 대상별 실행과 발송 차단에 한정한다. 신규 테스트/harness는 이번 실행 소유의 synthetic 세 DB와 접속 role·실제 migration을 검증한다.

## 검증 계약

Backend Release build와 영향/전체 tests, 실제 격리 PostgreSQL의 fresh directory+두 업무 DB 및 기존 청주 additive 경로, 서로 같은 ID를 가진 DB의 교차 조회·쓰기·다운로드 거부, runtime role 교차 접속 거부, 소속/권한 없음·directory 장애·schema mismatch·요청 취소·동시성·worker 한쪽 실패를 검증한다. 오산 외부 provider 실제 호출은 0이어야 한다. ReviewSafe mutation 금지, legacy 단일 DB 모드와 청주 제조/G2 회귀도 확인한다.

공통 소속 관리의 초기 연결 절차는 synthetic fixture로 검증하고 실제 사용자 전체에 소속을 자동 부여하지 않는다. 기존 운영 데이터 복사·조회는 테스트 fixture의 대체물이 아니다. 정적 검사만으로 DB 격리 성공을 주장하지 않는다.

Sol은 재위임하지 않는다. 요청 모델 `gpt-5.6-sol/xhigh`, 실제 모델은 도구가 반환하지 않으면 `NOT_REPORTED`로 기록한다. Parent의 실제 diff/test 검토 후 fresh `gpt-6-astra/high` verifier가 미커밋 고정 digest로 독립 검증한다.

## 독립 구현 검토와 범위 내 보정

Fresh `gpt-6-astra/high` read-only verifier의 첫 정적 검토는 61개 파일의 고정 기준선에서 수행됐다. 요청 모델과 관측값 `NOT_REPORTED`를 구분한다. 검토 전후 파일 지문은 같았으며, 다음 P2 세 건을 기존 승인·allowlist 안에서 보정한다. 새 업무 기능·운영 권한을 추가하는 승인이 아니다.

- `OSAN-VERIFY-ROLE-INHERITANCE`: 기존 접속 역할에 남은 상속 membership은 직접 CONNECT 회수만으로 차단되지 않는다. 역할 변경 전 검사로 거부하고 synthetic 기존 역할에서 변경 없음까지 검증한다.
- `OSAN-VERIFY-ENTITY-EVIDENCE`: 같은 사용자 ID 검증에 더해, 같은 프로젝트 ID의 조회·변경·실제 첨부 다운로드와 상대 DB 불변을 검증한다. 오산 업무 API는 계속 차단한다.
- `OSAN-VERIFY-MIGRATION-PREFLIGHT`: migration 적용 후에야 잘못된 대상 DB를 거부하는 순서를 보정한다. maintenance lock 안에서 첫 DDL 전에 대상 identity·기존 데이터 검사를 수행하고, 잘못 지정된 DB의 schema·ledger·업무 데이터가 변하지 않는지 확인한다.

첫 정적 검토는 최종 PASS가 아니다. 보정 후 집중 검증과 고정 기준선 재검토를 먼저 수행하고, 최신 전체 회귀·packaging·최종 보고서 검증 결과를 구현 보고에 기록한다. 앞선 코드의 중단된 회귀 실행은 완료 결과로 계산하지 않는다.

보정 검토에서 부분 초기화 재시도 차단(`OSAN-VERIFY-MIGRATION-RETRY`, P2)과 승인 legacy 원장 호환성 차단(`OSAN-VERIFY-LEGACY-COMPATIBILITY`, P2)을 추가 확인했다. 업무 데이터가 없는 정상 초기화 중단 상태는 재시도를 허용하되 원장 순서·누락 검사를 유지하고, 업무 DB의 기존 `MigrationLedgerCompatibilityPolicy`에 정의된 successor·schema 조건만 재사용한다. Directory의 exact 원장 정책과 알 수 없는 marker 거부는 유지한다. 두 보정도 기존 청주 보존과 DB 분리 검증 승인 범위에 속한다.

다섯 P2는 보정 후 62파일 고정 기준선의 독립 정적 검토에서 해소됐다. 최신 Backend 전체 575/575·집중 2/2·image migration 86개 반복 적용 통과, UI 최초 실패와 대상 재실행 통과, 최종 evidence 및 cleanup 상태는 [구현 보고서](osan-isolation-001-implementation-report.md)를 따른다. 테스트가 만든 screenshot 117파일만 원복·삭제했으며 기존 WIP와 제품 변경은 보존했다. 검사 container 삭제는 자동 승인 검토에 거절돼 관련 local image와 함께 2개를 보존한다. 사용자 검수·Git 게시·운영 적용 승인은 변경하지 않았다.

최종 fresh GPT-6 High verifier는 구현·자동 검증 증빙 검토를 통과로 판정하고 사용자 검수 인계를 허용했다. 추가 P0/P1/P2는 없으며 UI 원인 미확정 P3와 검사 자원 정리 P3는 유지한다. 구현 38파일의 검토 전후 fingerprint는 동일했다. 과거 62파일 manifest의 최종 직접 재대조는 읽기 명령 자동 거부로 미실행이며, 실행 결과는 전달된 종료 집계와 보고서를 대조한 범위다. 사용자 검수·잔여 cleanup·게시·운영 적용의 완료나 승인을 뜻하지 않는다.
