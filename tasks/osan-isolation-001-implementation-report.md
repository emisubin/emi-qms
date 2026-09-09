# TASK-OSAN-ISOLATION-001 — 사업부 데이터 분리 구현 보고

## 1. 해결한 문제와 현재 상태

같은 PostgreSQL 서버에서 공통 directory, 청주 업무 DB, 오산 업무 DB를 별도 데이터베이스로 분리하고, 인증 identity를 directory에서 확인한 뒤 선택한 사업부의 local profile과 권한만 읽도록 구현했다. 기존 단일 DB 모드는 명시적으로 보존한다.

- 승인 근거: `TASK-OSAN-ISOLATION-001 Change 001`의 사용자 승인
- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION`
- 구현 branch: `feat/task-osan-isolation-001-db-boundaries`
- 기준 SHA: `574cea66f602eb65eb1d110801d331151731b0a6`
- 구현 workspace: `OSAN_ISOLATION_TEMP_WORKTREE`
- 구현 모델 요청: `gpt-5.6-sol/xhigh`
- 구현 모델 관측: `NOT_REPORTED`
- 제품 코드·migration·격리 harness: 구현 완료
- Release build·집중 격리 검증: PASS
- 전체 Backend PostgreSQL 회귀: PASS 575/575
- 격리 Full-Stack 회귀: 1차 63/64, 실패 시나리오 새 DB 재실행 1/1 PASS
- production image packaging rehearsal: PASS
- 기존 Task 1 제품 구현 38파일: Change 002 전후 fingerprint 동일
- Change 002 test-only 보정: 기존 Frontend·Full-Stack 테스트 3파일
- Change 003 패키징 검사 lifecycle: 코드 구현·정적 검사·fresh GPT-6 High 코드 검토 통과, Docker 동적 검증은 도구 승인 차단
- fresh GPT-6 High 독립 검증: IMPLEMENTATION_AND_EVIDENCE_CLEARANCE, 사용자 검수 인계 가능
- 사용자 검수: COMPLETE — `USER_EXPLICIT_APPROVAL_2026-09-06`; 사용자가 2026-09-06 “승인.”으로 직전에 제시된 8개 오산 검수 항목을 승인
- 기존 자동 테스트 자원 cleanup: DB·실행 container·network 완료, legacy 검사 container·local image 2개 자동 검토 차단으로 보존
- 실제 Azure DB·Persistent UAT·실제 provider: 미실행·미승인
- Commit·Push·PR·Merge: 미수행·미승인

## 2. 기술적 결정과 검토한 대안

업무 DB 두 개만 두고 로그인 전에 청주 DB를 기본 조회하는 방법은 미소속 사용자 생성과 장애 fallback을 막을 수 없어 제외했다. 선택 상태를 singleton이나 `AsyncLocal`에 저장하는 방법도 병렬 요청과 background 작업에서 context가 섞일 수 있어 제외했다. 승인안대로 최소 directory DB를 두고 request는 immutable `HttpContext` feature, worker는 explicit target 인자로 전달한다.

모든 connection을 정상 API 설정에 넣는 방법은 privileged secret 상시 배포가 되어 제외했다. Runtime, migration, membership backfill과 bootstrap이 각 목적에 필요한 connection만 검증하도록 나눴다. 사업장 이름만 설정으로 신뢰하는 방법도 제외하고 실제 DB identity marker와 허용된 migration 원장를 매 경계에서 확인한다.

### 요청·인증·권한 경계

`BusinessUnits:Enabled=true`일 때 인증 provider와 external subject를 먼저 directory에서 조회한다. 활성 membership이 없으면 어느 업무 DB에도 연결하지 않고 `/api/me`의 pending-safe 응답만 반환한다. 둘 이상의 membership을 선택할 수 있는 주체는 directory에 별도로 지정한 overall administrator뿐이며, 실제 API 권한은 선택한 업무 DB의 local role·permission으로 계속 판단한다.

요청별 선택은 immutable `HttpContext` feature에 저장한다. Singleton 전역 현재 사업부나 `AsyncLocal` 전파를 사용하지 않는다. 인증된 요청도 trusted feature와 selected local-profile claim이 모두 일치해야 업무 API를 통과한다. local UUID가 같아도 Entra oid 또는 개발 identity key가 다르면 local profile pending으로 처리하고 profile photo 조회·수정·삭제를 포함한 업무 DB 접근을 차단한다.

오산에서는 현재 Task 1의 안전한 공통 경로만 허용한다. 기존 프로젝트·export/download·pending·G2·hold/cancel과 후속 제조·품질·물류 mutation은 후속 Task가 오산 의미를 구현할 때까지 403으로 닫힌다.

### DB·migration·role 경계

Directory는 `database/directory-migrations/`의 독립 ledger를 사용하고, 두 업무 DB는 기존 연속 ledger와 신규 `0086_business_unit_database_identity`를 사용한다. 요청, worker, seed, backfill, health와 ReviewSafe는 실제 DB 이름, identity marker와 허용된 원장(업무 DB의 승인 legacy 호환성 포함)를 확인한다. 다중 DB 모드에는 implicit default나 장애 fallback이 없다.

정상 API는 runtime connection만 요구한다. Migration, membership backfill과 role bootstrap은 실행 목적에 필요한 privileged connection만 검증한다. 모든 target은 같은 server endpoint를 사용하고 database 이름, runtime/migrator role과 역할 범주가 서로 달라야 한다. 운영 작업에서는 VerifyFull, 관리 password 길이와 bootstrap credential 분리도 검사한다.

Role bootstrap은 DB별 runtime·migrator role을 bounded 속성으로 만들고 다른 target DB의 CONNECT를 회수한다. Directory runtime은 membership·overall administrator를 읽기만 할 수 있다. 업무 runtime은 business table을 사용할 수 있지만 migration ledger, global audit infrastructure와 database identity metadata를 변경할 수 없다. `qms_database_identity`는 migration 소유 metadata이므로 기존 94개 business row-audit 대상에는 추가하지 않고 명시적 infrastructure exclusion으로 분류했다.

Bootstrap은 제한 역할이 다른 PostgreSQL 역할의 member이면 어떤 role ALTER나 privilege grant보다 먼저 실패한다. 관리자가 제한 역할을 부여받는 반대 방향은 제한 역할 자체의 권한을 넓히지 않으므로 차단 대상이 아니다. 기존 membership을 자동 회수하지 않고 운영자가 원인을 확인하도록 fail-closed 처리한다.

기존 청주 DB의 0001~0085 schema와 사용자 ID·role을 보존한 채 0086을 additive 적용한다. 이미 데이터가 있는 unbound DB는 오산으로 자동 결속하지 않는다. Directory backfill은 명시적으로 승인한 active 청주 사용자 ID만 처리하고 overall administrator는 그 부분집합만 허용한다. ReviewSafe에서는 backfill entry에서 DB 연결 전에 중단한다.

Migration runner는 target advisory lock을 얻은 직후 read-only preflight로 configured DB name과 기존 identity를 확인한다. 오산 target에 사용자·프로젝트 업무 행이 있는데 identity가 없거나 identity가 다른 사업부를 가리키면 `schema_migrations` 생성, 0086 DDL 또는 ledger 기록 전에 중단한다. 기존 ledger는 catalog의 정확한 앞부분이어야 하며, business DB에서는 canonical compatibility policy의 승인된 0020 marker, 0023 successor와 실제 notification channel schema probe도 그대로 인정한다. 업무 행이 없는 신규 오산 DB에서 일부 migration만 commit된 상태는 같은 작업의 안전한 재시도로 허용한다. 기존 청주의 승인된 populated unbound upgrade도 허용한다.

### Background 작업과 provider 경계

알림 delivery, escalation, scheduled deletion은 각 target을 명시적으로 받는다. 각 worker는 target identity와 ledger를 확인하고 target별 실패를 모은 뒤 다른 허용 target 처리를 계속한다. 오산은 external notification enqueue와 dispatch를 application과 DB trigger 양쪽에서 차단하며 counting provider test의 실제 handler 호출 수는 0이다. Scheduled deletion은 한 target의 contract failure가 다른 target의 purge를 건너뛰거나 실패 DB로 우회하지 않는다.

### Legacy·ReviewSafe·packaging

`BusinessUnits:Enabled=false`는 기존 `QmsDatabase`와 `DATABASE_HOST/PORT/NAME/USER/PASSWORD` fallback을 유지하며 legacy manual delivery의 explicit target도 같은 connection을 사용한다. ReviewSafe는 각 DB에 read-only session, 고정 application name, identity와 허용된 원장(업무 DB의 승인 legacy 호환성 포함)를 모두 검사한다. Production Dockerfile은 business migration과 함께 directory migration catalog를 published runtime image에 포함한다.

## 3. 변경 경로

### 신규 파일

- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitConfiguration.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitRequestContext.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitDirectoryStore.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitResolver.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitCapabilityMiddleware.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitMembershipBackfillRunner.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitDirectoryMigrationCatalog.cs`
- `backend/src/Emi.Qms.Api/appsettings.BusinessUnits.example.json`
- `database/directory-migrations/0001_business_unit_directory.sql`
- `database/migrations/0086_business_unit_database_identity.sql`
- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`
- `scripts/test-business-unit-isolation.sh`
- `scripts/test-business-unit-production-image.sh` — 패키징 검사 생성·검사·owned cleanup wrapper
- `tasks/osan-isolation-001-implementation-report.md`

### 수정 파일

- `backend/Dockerfile.production`
- `backend/src/Emi.Qms.Api/Program.cs`
- `backend/src/Emi.Qms.Api/DatabaseConnectionStringProvider.cs`
- `backend/src/Emi.Qms.Api/DatabaseHealthChecker.cs`
- `backend/src/Emi.Qms.Api/DatabaseMigrationRunner.cs`
- `backend/src/Emi.Qms.Api/DatabaseRoleBootstrapper.cs`
- `backend/src/Emi.Qms.Api/DatabaseRuntimePrivilegeManager.cs`
- `backend/src/Emi.Qms.Api/Authorization/DevelopmentAuthenticationHandler.cs`
- `backend/src/Emi.Qms.Api/Authorization/EntraClaimsTransformation.cs`
- `backend/src/Emi.Qms.Api/Authorization/QmsClaimTypes.cs`
- `backend/src/Emi.Qms.Api/Identity/DevelopmentIdentitySeeder.cs`
- `backend/src/Emi.Qms.Api/Identity/HybridIdentityStore.cs`
- `backend/src/Emi.Qms.Api/Identity/IdentityEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/Identity/UserAdministrationStore.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryStore.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDispatcher.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationEscalationService.cs`
- `backend/src/Emi.Qms.Api/Notifications/WorkItemEscalationStore.cs`
- `backend/src/Emi.Qms.Api/Admin/AdminScheduledDeletionService.cs`
- `backend/src/Emi.Qms.Api/ReviewSafe/ReviewSafeStatusService.cs`
- `backend/src/Emi.Qms.Api/ReviewSafe/MigrationLedgerInspector.cs`
- `backend/src/Emi.Qms.Api/Security/DatabaseOperationSecurityPolicy.cs`
- `backend/src/Emi.Qms.Api/Security/ProductionSecurityPolicy.cs`
- `backend/tests/Emi.Qms.Api.Tests/AuditInfrastructureTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
- `database/README.md`
- `scripts/test-production-migration-image.sh` — wrapper 호출 시 migration container name·label·trap cleanup 적용
- `frontend/tests/App.test.tsx` — 독립 route 계약을 세 테스트로 분리
- `frontend/tests/G2Navigation.test.tsx` — 첫 G2 제목의 국소 5초 대기
- `frontend/e2e/full-stack/project-registration.full-stack.spec.ts` — 재활성 POST·상세 GET 성공 뒤 badge 확인

참조 overlay의 지침·Roadmap·상위 오산 문서는 parent 소유이며 위 제품 변경 목록에 포함하지 않는다. 이 목록의 제품 변경은 `OSAN_ISOLATION_TEMP_WORKTREE`의 구현 branch에만 있으며, 원래 canonical clone의 보존된 governance WIP와 섞지 않았다. Parent가 canonical clone으로 돌려보내는 보고서·상태 문서는 추적용이고 제품 source 반영을 뜻하지 않는다.

## 4. 검증 증거

| 검증 | 결과 | 범위 |
| --- | --- | --- |
| API Release build | PASS | 경고 0, 오류 0 |
| Test Release build | PASS | 경고 0, 오류 0 |
| `scripts/test-business-unit-isolation.sh` | PASS | 2/2, 실패 0, 건너뜀 0, 42초; owned tmpfs PostgreSQL에서 fresh 3 DB, 기존 청주 upgrade, roles, public API, workers, ReviewSafe |
| 전체 `scripts/e2e-backend-tests.sh` | PASS | 575/575, 실패 0, 건너뜀 0, 1시간 4분, exit 0; owned DB·container·network cleanup 완료 |
| 격리 `scripts/e2e-full-stack.sh` | 1차 63/64, 대상 재실행 PASS | 1차 63 PASS·1 timeout, 25.4분, exit 1. 실패한 기존 project registration 시나리오를 새 격리 DB에서 재실행해 1/1 PASS, test 13.6초·전체 19.9초, exit 0; 두 실행 모두 cleanup 완료 |
| Production image build/catalog/rehearsal | PASS | image build exit 0; directory 0001이 원본과 byte·SHA-256 일치; packaged image에서 legacy business migration 86개 fresh·existing 반복 적용과 exact ledger PASS, exit 0; rehearsal DB·container·network cleanup 완료 |
| Frontend typecheck | PASS | 기존 frontend source, 오류 0 |
| Frontend unit | 초기 248/250, 대상 재실행 PASS | 최초 실행은 33 files 중 31 PASS·2 FAIL, 250 tests 중 248 PASS·2 FAIL, 135초. 실패한 `App` route와 `G2 navigation` 두 파일을 single-worker로 격리 재실행해 2/2 files, 88/88 tests PASS, 80.17초 |
| Change 002 Frontend 안정성 | PASS | 변경 전 paired 10회 중 9회 PASS·1회 실패로 변동성 재현. 보정 후 선택 4사례 32회씩 128/128, 두 파일 90/90, 전체 33 files·252/252 PASS; typecheck와 변경 3파일 lint PASS |
| Change 002 Full-Stack 대상 | PASS | 확인 클릭 전에 재활성 POST·프로젝트 상세 GET 대기를 등록해 두 응답 성공과 최종 badge를 확인. fresh synthetic 환경 3/3 PASS, 49.9초, exit 0; DB·container·network cleanup 완료 |
| Change 003 shell 정적 검사 | PASS | macOS `/bin/bash` 3.2 syntax, `shellcheck -x` 두 스크립트, `git diff --check` 통과; mode `755`, staged 0 |
| Change 003 Docker lifecycle | DEFERRED_TO_TASK_OSAN_VALIDATION_001 | 사용자 재승인 뒤에도 첫 Docker 사전 조회가 process 시작 전에 자동 정책으로 거부됐다. 정상·주입 실패·TERM과 잔여 0 검증은 성공으로 간주하지 않고 통합 검증 Task로 이관 |
| Change 003 fresh GPT-6 High 코드 검토 | CODE_REVIEW_CLEARANCE | 앞선 claim window·조회 UNKNOWN·child container·Compose override·Bash 3.2 빈 배열·ID 형식 Finding 6건 해소, 신규 P0/P1/P2/P3 없음; 동적 검증 완료 판정은 보류 |
| `git diff --check` | PASS | whitespace 오류 0 |
| fresh GPT-6 High verifier | IMPLEMENTATION_AND_EVIDENCE_CLEARANCE | 앞선 frozen manifest 62 정적 검토의 5개 P2 해소에 이어 최종 종료 집계·보고서 대조 통과. 현재 구현 38파일 검토 전후 동일, 추가 P0/P1/P2 없음; 사용자 검수·cleanup·게시·운영 적용은 별도 |

집중 검증은 실제 synthetic DB에서 다음 경계를 확인했다.

- fresh directory·청주·오산 migration과 0001~0085 기존 청주 upgrade, 기존 사용자 ID·system administrator role 보존
- runtime role의 `NOSUPERUSER/NOCREATEDB/NOCREATEROLE/NOREPLICATION/NOBYPASSRLS`, 다른 사업부 DB CONNECT 거부
- 오산 migrator를 청주 runtime에 membership으로 부여하고 `NOINHERIT`로 바꾼 합성 상태에서 bootstrap이 role ALTER 전에 거부하며 기존 속성을 바꾸지 않음
- directory runtime의 membership·overall administrator·identity 쓰기 거부와 business runtime의 identity 변경·role 생성 거부
- 같은 사용자 UUID를 가진 청주·오산 profile의 서로 다른 이름, 선택별 조회, selected DB만 포함하는 관리자 사용자 snapshot
- 같은 project ID에 서로 다른 청주·오산 title/status를 두고 청주 조회·hold는 성공, 오산 조회·hold는 403이며 오산 status·audit aggregate는 불변
- 같은 notice/attachment ID에 서로 다른 바이트를 두고 청주 download는 정확한 청주 바이트, 오산 download는 403이며 오산 저장 바이트는 불변
- header 위조, 미소속, membership 회수, 비활성 unit, directory ledger 누락·연결 장애, business identity·ledger mismatch의 fail-closed 동작
- Entra oid·Dev subject 충돌 시 local-profile pending과 profile photo GET/PUT/DELETE 거부
- 병렬 20개 선택의 교차 context 0과 취소 전파
- 오산 project/export/G2/pending/hold/cancel 403, delivery/escalation enqueue 0, DB trigger 차단, provider handler 호출 0
- 청주 worker target failure 뒤 오산 scheduled deletion 처리와 최종 generic failure
- ReviewSafe backfill의 directory audit 변화 0
- 정상 runtime 설정에서 migration/admin secret 미요구, 목적별 connection·role·password 검증, legacy env-only connection 유지
- wrong-bound 오산과 application schema가 있는 unbound 오산에서 migration이 ledger·schema·project marker를 전혀 바꾸지 않고 실패
- 신규 오산 DB의 0023까지 commit된 partial prefix에서 재실행해 0086까지 완료하고, ledger 중간 누락은 어떤 재적용도 하기 전에 거부
- 승인된 legacy 0020 marker와 0023 successor/schema가 있는 partial prefix는 성공하고, successor 누락·unknown marker·ledger hole·0074 이후 WebPush schema mismatch는 ledger 불변으로 거부

세 번의 이전 전체 회귀는 최종 실행으로 계산하지 않는다. 첫 실행은 신규 0086에 따른 기존 expected latest/classification assertion을 찾은 뒤 stale compiled run으로 중단했고, 두 번째는 승인된 `UserAdministrationStore` 경계 보정을 반영하기 전에 중단했다. 세 번째는 독립 검토에서 역할 상속·entity evidence·migration preflight 보정이 확인되어 중단했다. 모두 exit 130의 정상 interrupt와 harness EXIT cleanup으로 owned database, container와 network를 제거했다.

최종 Backend 전체 회귀는 runner의 개별 test 진행 출력이 없어 약 1시간 시점에 정체 가능성을 별도로 확인했다. Owned PostgreSQL network aggregate가 60초 동안 변하지 않는 동안 test process의 native stack은 user-code loop가 아니라 .NET reflection invocation의 JIT compile phase에 있었고, 실행을 임의 중단하지 않았다. 곧이어 575/575로 정상 종료했다. 진단은 데이터 행·connection 값·credential을 읽지 않았다.

Full-Stack 1차 실행의 유일한 실패는 기존 `project registration, permissions, status, and panel count` 시나리오가 재활성화 뒤 `진행` status badge를 5초 안에 찾지 못해 전체 30초 timeout이 된 건이다. 나머지 63개는 통과했고 harness cleanup 뒤 같은 시나리오를 새 DB·새 browser에서 재실행해 13.6초에 통과했다. 1차 실행을 깨끗한 64/64로 바꾸어 기록하지 않는다. Frontend unit도 최초 248/250과 실패 두 파일의 single-worker 88/88 재실행을 각각 보존한다.

Change 002에서는 제품 source를 바꾸지 않고 세 테스트의 동기화만 보정했다. App의 독립 route 계약을 분리하고, G2의 동일한 첫 제목을 국소적으로 기다리며, 재활성 확인 클릭 전에 해당 POST·GET 응답 대기를 등록한 뒤 두 응답 성공과 최종 badge를 검증한다. 변경 전 paired 반복 10회 중 1회 실패로 변동성을 재현했고, 보정 후 Frontend 전체 252/252와 대상 Full-Stack 3/3이 통과했다. 최초 개별 실패의 환경 원인을 모두 확정한 것은 아니며 최초 결과를 삭제하거나 250/250·64/64로 바꾸지 않는다.

Change 003은 하네스 밖의 일회성 catalog 검사에서 image와 never-started container가 남은 절차를 보정한다. 새 wrapper는 canonical E2E Compose, 고유 run ID·name·label과 owner 검증을 사용해 build, 두 migration catalog의 재귀 exact 비교, fresh/existing migration과 cleanup을 한 실행에 묶는다. 하위 migration container도 선택적 managed mode로 같은 scope에 포함하고 Docker 조회 실패는 `UNKNOWN`으로 실패 처리한다. 기존 legacy 자원을 삭제 대상으로 넣지 않았다. 정적 검사와 최종 코드 검토는 통과했지만 Docker lifecycle은 실행 전 정책 차단으로 검증하지 못했다.

Change 001 Full-Stack suite는 기존 사용자 검수 screenshot 경로도 다시 생성했다. 종료 뒤 실제 파일 기준 117개를 고정 목록으로 확인했고 당시 frozen manifest 62와 참조 overlay 24에 모두 포함되지 않음을 검증했다. 그중 tracked rewrite 112개만 HEAD로 복원하고 해당 실행이 만든 untracked screenshot 5개만 삭제했다. 해당 screenshot diff는 0이며 Change 002도 신규 screenshot·trace·video를 남기지 않았다.

## 5. Finding과 제한

| Finding | 심각도 | 상태 | 해소 또는 후속 |
| --- | --- | --- | --- |
| OSAN-ISO-001 — local subject 불일치 뒤 profile photo 접근 | P1 | RESOLVED | middleware가 selected local-profile claim도 요구하고 Entra·Dev GET/PUT/DELETE 회귀 추가 |
| OSAN-ISO-002 — 정상 API의 privileged secret 요구 | P1 | RESOLVED | runtime과 operation별 connection validation 분리 |
| OSAN-ISO-003 — 비활성 unit membership·directory/runtime metadata 쓰기 | P1 | RESOLVED | active join과 migration 이후 명시적 revoke, 실제 역할 테스트 추가 |
| OSAN-ISO-004 — health와 별개로 wrong identity/ledger DB 접근 | P1 | RESOLVED | local profile 전, seed/backfill과 각 worker target 실행 전에 actual boundary validation |
| OSAN-ISO-005 — 관리자 목록의 전역 개발 계정 혼입 | P1 | RESOLVED | 선택 DB의 local Dev/Entra snapshot만 사용, 청주·오산 교차 이름 테스트 |
| OSAN-ISO-006 — ReviewSafe backfill write 가능성 | P1 | RESOLVED | runner entry에서 connection 전 중단, directory audit before/after 변화 0 테스트 |
| OSAN-ISO-007 — runtime image의 directory migration catalog 누락 | P1 | RESOLVED | build/runtime COPY 구현, image 내부 0001 원본 byte·SHA-256 일치와 packaged migration 반복 rehearsal 통과 |
| OSAN-ISO-008 — 0086 추가 뒤 latest migration·relation classification 회귀 | P2 | RESOLVED | expected latest를 0086으로 갱신하고 identity를 runtime-write-protected metadata로 분류; business 감사 대상 94개 유지 |
| OSAN-ISO-009 — 제한 역할의 inherited membership으로 교차 DB 권한 재획득 | P2 | RESOLVED | 제한 역할이 다른 역할의 member이면 어떤 bootstrap mutation보다 먼저 거부; 자동 membership 정리 없음 |
| OSAN-ISO-010 — 동일 entity·attachment 실제 데이터 경계 증거 누락 | P2 | RESOLVED | 같은 project/notice/attachment ID의 상이한 데이터로 청주 성공, 오산 403과 counterpart 불변 확인 |
| OSAN-ISO-011 — migration이 identity 거부 전에 0086·ledger를 기록 | P2 | RESOLVED | advisory lock 아래 read-only preflight를 첫 DDL 전에 수행하고 wrong-bound/unbound populated no-mutation 회귀 추가 |
| OSAN-ISO-012 — 공용 table 존재만으로 중단된 신규 오산 migration 재시도 차단 | P2 | RESOLVED | 실제 user/project 행과 허용된 ledger prefix를 구분하고 0023까지의 partial prefix 재시도·ledger gap 거부 회귀 추가 |
| OSAN-ISO-013 — known-prefix preflight가 승인된 legacy migration marker를 거부 | P2 | RESOLVED | canonical compatibility policy와 schema probe 재사용; approved marker 성공과 missing successor/unknown/hole/schema mismatch 선행 거부 회귀 추가 |
| OSAN-TEST-TIMING — 최초 UI 검증과 격리 재실행 결과 차이 | P3 | RESOLVED | Change 002에서 테스트 실행 단위와 비동기 응답 대기를 보정했다. 기존 assertion을 유지한 선택 사례 128/128, Frontend 전체 252/252와 대상 Full-Stack 3/3이 통과했다. 최초 개별 실패의 환경 원인을 모두 확정한 것은 아니다. |
| OSAN-003-CODE-FINDINGS — packaging lifecycle 정적 안전 경계 6건 | P2/P3 | RESOLVED | 생성 전 claim, Docker 조회 `UNKNOWN`, child container 회수, canonical Compose, Bash 3.2 빈 배열과 residual full ID를 보정하고 fresh GPT-6 High가 코드 해소를 확인했다. |
| OSAN-003-RUNTIME-VALIDATION — packaging lifecycle 실제 검증 미완료 | P2 | USER_ACCEPTED_DEFERRED_TO_TASK_OSAN_VALIDATION_001 | 사용자가 실행환경에서 불가능한 자동 정리 검증은 추적만 하도록 정정했다. 실제 Docker 증거 부재를 유지하고 Task 6 격리 통합 검증·Azure/Persistent UAT 개통 전에 정상·실패·TERM과 잔여 0을 재검토한다. |
| OSAN-LOCAL-ARTIFACT-CLEANUP — 검사 자원 2개 정리 미완료 | P3 | USER_MANUAL_ACTION_PLANNED | 사용자가 legacy 컨테이너와 이미지를 직접 삭제하겠다고 했으며 결과 보고 전에는 완료로 표시하지 않는다. 이 정리는 Change 003 동적 검증을 대체하지 않는다. |

독립 검토의 `OSAN-VERIFY-ROLE-INHERITANCE`, `OSAN-VERIFY-ENTITY-EVIDENCE`, `OSAN-VERIFY-MIGRATION-PREFLIGHT`, `OSAN-VERIFY-MIGRATION-RETRY`, `OSAN-VERIFY-LEGACY-COMPATIBILITY`는 각각 OSAN-ISO-009~013에 대응한다. 검토 기준선은 62파일 manifest SHA-256 `deeaf2234573639f38c0b025f2dac260172b766880252fdcc15fd6a1425ece4b`다.

최종 verifier 판정은 구현·자동 검증 증빙 검토 통과, 사용자 검수 인계 가능이다. 기존 Task 1 구현 38파일 fingerprint는 Change 002 전후 `c1231495fb4e181b64558d9461dbd376fe6cdb803e97f3321f6f9a32ec973a46`로 동일했다. Change 002의 별도 GPT-6 High 검토도 세 test-only 보정을 PASS로 판정했고 신규 P0/P1/P2/P3는 없다. 과거 62파일 manifest의 최종 직접 재대조는 읽기 명령 자동 거부로 미실행이며 우회하지 않았다. 독립 검토자는 테스트를 다시 실행하지 않고 전달된 종료 집계와 실제 diff를 대조했다. 요청 모델은 `gpt-6-astra/high`, 관측 모델은 `NOT_REPORTED`다. 이번 parent session은 GPT-5였으며 지정된 Sol 구현자와 별도 GPT-6 검증자를 사용했다. 이 판정은 게시 가능 판정이나 사용자 검수 완료가 아니다.

Directory 장애는 전체 로그인 routing을 fail-closed로 막는 의도된 가용성 의존성이다. Task 2 전에는 membership 관리 UI가 없으며 승인된 초기 청주 identity만 one-time backfill로 연결한다. Task 3/4 전에는 오산 업무 API를 의도적으로 닫는다. 실제 Azure role/credential, 운영 데이터, Persistent UAT와 실제 provider는 이번 검증 결과로 대체하지 않는다.

## 6. 시행착오 및 폐기한 접근

첫 boundary 검사는 health에만 identity와 ledger를 확인해 실제 요청과 worker가 잘못 결속된 DB를 열 여지가 있었다. Health 결과를 routing 권한으로 간주하지 않고 요청의 local profile 조회 전과 각 explicit 작업 target에서 다시 확인하도록 바꿨다.

첫 pending 처리는 selected feature만 남긴 채 local profile claim을 pending으로 바꿨다. 이 상태에서는 profile photo endpoint가 directory UUID로 다른 local row를 읽을 수 있어 feature와 claim을 함께 요구하도록 수정했다. Entra oid와 Dev key 충돌을 각각 실제 인증 경로에서 검증한다.

첫 설정 검증은 다중 DB 모드의 모든 connection 값을 startup에서 요구했다. 정상 API가 migration/admin secret을 보유하게 되므로 목적별 검증으로 폐기했다. 초기 directory privilege reconciliation의 실제 table 이름 불일치와 business identity metadata 재부여 가능성도 실제 role 테스트 전에 바로잡았다.

초기 role bootstrap은 제한 역할 속성과 직접 CONNECT만 다시 설정했다. 제한 역할이 다른 migrator 역할을 상속하면 `SET ROLE`과 inherited CONNECT로 경계를 넘을 수 있으므로, 무관한 기존 membership을 자동 회수하는 방식 대신 mutation 전 명시적 거부로 바꿨다. 초기 migration 순서는 0086을 commit한 뒤 identity를 결속했기 때문에 잘못 지정한 populated 오산 DB에 DDL과 ledger가 남을 수 있었다. Identity·실제 user/project 행과 ledger prefix를 먼저 읽는 preflight로 순서를 고쳤다. 공용 table 존재 자체를 populated로 판단한 첫 보정은 일부 migration이 commit된 신규 DB의 재시도를 막아 폐기했다. Exact prefix만 인정한 다음 보정도 기존 승인 legacy marker 계약을 깨므로 canonical policy와 실제 schema probe를 재사용하도록 고쳤다.

전체 회귀의 첫 실행에서 0086 추가로 기존 latest migration 기대값과 exhaustive relation 분류가 오래된 사실을 확인했다. 대상 assertion만 신규 ledger와 metadata 분류에 맞추고, 기존 94개 business row-audit 대상은 변경하지 않았다. 수정 전 compiled run과 승인된 관리자 snapshot 보정 전 run은 최종 증거로 사용하지 않고 정상 cleanup으로 종료했다.

## 7. 운영 SOP와 사용자 안내

1. 같은 PostgreSQL server에 directory, 청주, 오산 DB를 이름이 겹치지 않게 준비한다. 기존 populated DB는 청주로만 사용할 수 있으며 새 오산 DB는 비어 있어야 한다.
2. 설정 예시의 target code, expected DB name, expected schema version과 DB별 runtime/migrator role 이름을 고정한다. 정상 API 배포에는 runtime connection 세 개만 제공한다.
3. Bootstrap 작업에만 administrator, migration, runtime connection을 주고 `--bootstrap-database-roles`를 실행한다. 성공 뒤 privileged connection을 정상 API 환경에서 제거한다.
4. Migration 작업에만 migration connection 세 개를 주고 `--migrate-only`를 실행한다. Directory 0001의 exact 원장과 두 업무 DB 0086까지의 승인 호환 원장·identity를 확인한다.
5. 기존 청주 사용자를 연결할 때 검토한 ID만 `ApprovedUserIds`에 넣는다. 필요한 overall administrator만 그 부분집합에 넣고 `--backfill-business-unit-memberships`를 한 번 실행한다. ReviewSafe에서는 실행하지 않는다.
6. 정상 runtime connection으로 `/health/ready`를 확인한다. 한 target이라도 identity, ledger, role 또는 연결이 다르면 개통하지 않는다.
7. Task 1은 backend의 `X-Qms-Business-Unit` selector header와 `/api/me` pending contract만 제공한다. Membership이 하나인 요청은 backend가 자동 확정하고, 여러 membership의 overall administrator 요청은 허용된 header를 검증한다. 실제 사용자-facing 사업부 선택·membership·pending 화면은 Task 2 범위이며 아직 구현되지 않았다.

오류 복구 시 실패 target의 설정·role·identity·ledger를 먼저 고치고 같은 명시적 작업을 재실행한다. 다른 DB로 connection string을 바꾸거나 fallback을 추가하지 않는다. 이미 결속된 populated DB의 identity row를 수동 변경하지 않는다. 신규 다중 DB runtime을 시작하지 않았다면 `BusinessUnits:Enabled=false`로 기존 단일 DB 배포를 유지할 수 있다.

## 8. 사용자 검수 결과와 남은 항목

사용자 검수 결과는 `COMPLETE`이며 source는 `USER_EXPLICIT_APPROVAL_2026-09-06`이다. 사용자가 2026-09-06 “승인.”으로 직전에 제시된 다음 8개 오산 계약 확인 항목을 명시적으로 승인했다.

- [x] 같은 EMI PMS를 사용하면서 청주·오산 업무 데이터는 분리한다.
- [x] 오산 프로젝트 등록은 합의한 8개 필드를 사용한다.
- [x] 입력 수량만큼 진행 대상 item을 생성한다.
- [x] 모든 대상은 고정 7단계를 순서대로 거치며 단계를 건너뛰지 않는다.
- [x] 진행 처리는 개별 처리와 일괄 처리를 모두 제공한다.
- [x] 모든 대상의 마지막 포장이 끝나면 프로젝트가 자동 완료된다.
- [x] 권한 있는 사용자가 전체 현황을 확인하는 대시보드를 제공한다.
- [x] 오산 흐름에는 중단·펜딩 상태를 두지 않는다.

이 완료 표시는 사용자가 Task 1 결과와 후속 오산 계약을 확인했다는 뜻이다. Task 2 UI 전에는 개발자 도구 또는 API client가 selector header를 보내야 하므로 일반 사용자가 화면에서 사업부를 바꿀 수 없다. Task 2~5 제품 기능, 실제 Azure DB·운영 credential·Persistent UAT·provider 개통과 Change 003 Docker 동적 검증은 완료로 표시하지 않는다.

남은 제품 작업은 Task 2의 사용자-facing 소속/선택/pending UI와 Task 3/4의 오산 업무 흐름이다. 이번 Task의 제품 구현·최종 evidence 독립 검토, 테스트 안정성 마무리와 사용자 검수는 통과했다. Docker lifecycle 동적 검증 P2는 성공 처리하지 않고 `TASK-OSAN-VALIDATION-001`로 이관했으며 Task 2의 선행 Gate에서는 내렸다. Legacy 검사 자원 2개 정리는 사용자가 나중에 직접 수행하는 P3 `USER_MANUAL_ACTION_PLANNED`로 추적한다.

Frontend source·화면 배치는 변경하지 않았다. Excel/PDF/export 양식 변경도 없으며, 첨부·다운로드는 기존 구현에 사업부 접근 경계가 적용된다. 신규 오산 화면의 desktop/mobile 시각 검수는 아직 해당 화면이 없는 Task 1에 적용되지 않는다. CI·게시 전 전체 파이프라인과 실제 운영 계정·Persistent UAT·provider 검증은 게시·운영 적용이 승인되지 않아 실행하지 않았다. 이미 수행한 Backend 전체·Frontend type/unit·격리 Full-Stack·migration/image 검증은 위 표의 실제 결과만 인정한다.

## 9. 5종 산출물 추적

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 최종 자동 검증 evidence 반영 완료 |
| SOP | 이 문서 7절, `database/README.md` | 작성, 실제 운영 실행 전 |
| User manual | 이 문서 7절의 backend header·pending 안내 | 작성, Task 2 UI 미구현 제한 명시 |
| Roadmap update | parent 소유 `docs/00-product-roadmap.md` 오산 Task 1 row | USER_VALIDATION_COMPLETE_RUNTIME_VALIDATION_DEFERRED |
| User validation checklist | 이 문서 8절 | COMPLETE — USER_EXPLICIT_APPROVAL_2026-09-06 |

## 10. Git·runtime 상태

Task 전용 임시 worktree와 branch에 미커밋 결과를 보존한다. Commit, stage, push, PR, merge와 worktree/branch cleanup은 수행하지 않는다. Backend·Full-Stack·기존 packaged migration harness가 만든 synthetic DB/container/network는 모두 제거됐다. 과거 read-only 확인에서 해당 네 실행의 running container와 network는 0이었다. Directory catalog 검사에 사용한 never-started container `emi-qms-osan-isolation-001-catalog-check`는 당시 상태 `Created`, local image tag `emi-qms-osan-isolation-001-test:manifest62`는 당시 image ID `sha256:78a050a43e7d259e76930b3193384a111e26d10c50db6fdafcde4c2ace5d8d78`, size 103,025,341 bytes였다. 2026-09-06 사용자 재승인 뒤에도 첫 Docker 사전 조회가 자동 정책의 `approval required by policy, but AskForApproval is set to Never`로 process 시작 전에 거부됐다. 정상·실패·TERM 실행, 자원 생성과 cleanup은 시작되지 않았고 다른 명령·API로 우회하지 않았다. 사용자는 legacy 2개 자원을 직접 삭제할 예정이며 결과 보고 전에는 완료로 기록하지 않는다.

Task 1의 다음 제품 Gate는 TASK-OSAN-ACCESS-001 구현 범위 승인이다. 제품 구현·evidence 독립 검토, Change 003 코드 검토와 사용자 검수는 통과했다. 동적 cleanup 검증은 `TASK-OSAN-VALIDATION-001`로 이관했고 legacy 잔여 자원 정리·Git 게시·운영 적용은 미완료다.

Full-Stack 검증이 생성한 스크린샷 변경은 파일 기준 117개였다. 고정 62파일과 참조 overlay 24파일에 겹치지 않고 검증 전 변경이 없음을 확인한 뒤, 승인된 cleanup 범위에서 tracked 112파일만 HEAD로 원복하고 새 untracked 5파일만 삭제했다. 잔여 screenshot diff는 0이며 제품·테스트·harness 기준선은 그대로다. Parent의 shell syntax와 diff check도 통과했다.

## Azure phase 1 승격 상태

2026-09-07 사용자 지시로 Task 1~3만 `TASK-AZURE-DEPLOY-001 Change 031`에 승격 준비 중이다. 기존 운영 DB는 Cheongju로 보존하고 Directory·Osan DB를 같은 server에 추가하며, DB/role/identity/ledger와 PITR rehearsal이 모두 통과하기 전에는 serving 연결을 켜지 않는다. 최종 local source는 Backend `582/582`, 3-DB 전용 access·Osan `2/2`와 일반 Full-Stack `64/64`를 통과했다. 이 기록은 실제 Azure DB 생성이나 migration 완료를 뜻하지 않는다.
