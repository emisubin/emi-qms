# TASK-AUDIT-001 구현 보고서

- taskType: `APPROVED_FEATURE_IMPLEMENTATION / UAT_RUNTIME`
- implementation status: `MAIN_MERGED / AZURE_RELEASE_COMPLETE / USER_VALIDATION_PENDING`
- release candidate: `N/A — production deployed`
- current gate: `공개 사용자 직접 검수 → 운영 aggregate 관찰`
- user validation: `자동·독립 검증 완료 / 사용자 직접 검수 대기`
- current user action: `공개 관리자 감사 화면 직접 검수`
- publication: `Commit·Push·PR #111·Merge·Azure release 완료 / local Persistent UAT 미승인·미실행`
- implementation branch: `feat/task-audit-001-access-change-audit`
- implementation base SHA: `7371d9e7224c3786f9b0efe3b2b88dfe9b88cd50`
- release identity: `운영 source exact main SHA 6713e5974ad5262d87d7cc2332b27486d2487ccd`
- validation target: base 대비 source/test `29` files, SHA-256 `1f344c6a446a508882a6bcab69f00482d08204b4d3bf5397380bb08d29dcc6f9`, `2026-08-28T02:00:05Z`
- migration: `0083_global_access_change_audit` — schema-additive, runtime-behavior-changing, forward-only
- planning: [audit-001-planning.md](audit-001-planning.md)
- Codex review: [audit-001-review.md](audit-001-review.md)
- approved changes: [audit-001-change-001.md](audit-001-change-001.md), [audit-001-change-002.md](audit-001-change-002.md), [audit-001-change-003.md](audit-001-change-003.md)
- coverage registry: [audit-001-coverage-registry.md](audit-001-coverage-registry.md)

## 1. 해결한 업무 문제

기존 시스템은 Azure 요청 수와 일부 업무별 이력만으로 사용자의 로그인 사실과 실제 데이터 변경을 한 흐름으로 확정할 수 없었다. 이번 구현은 배포 이후, 고정 registry에 포함된 인증 사용자 endpoint와 relation에서 다음 근거를 append-only 전역 원장으로 남긴다. 대화형 login correlation이 있는 경우에만 로그인과 mutation을 연결하며, 이를 모든 Microsoft 세션의 최초 로그인 증명으로 해석하지 않는다.

- 앱이 확정할 수 있는 대화형 Microsoft 로그인과 명시적 로그아웃
- 인증 사용자가 실제로 commit한 업무·관리 데이터 변경
- validation과 409/412 충돌로 거절된 저장 시도. 현재 중복 409도 `Conflict`로 보수 분류한다.
- 기존 `authorization_audit_events`의 권한 거부를 같은 관리자 화면에서 통합 조회
- 변경 field의 고정 scalar before/after 또는 자유문 원문 없는 길이 metadata

System Administrator는 `관리자 → 전체 감사 이력`에서 최근 30일을 기본으로 요약·필터·목록·상세를 확인하고 선택한 행을 Excel로 보존할 수 있다.

## 2. 포함·제외 범위

### 포함

- G2를 포함한 인증 사용자의 durable 업무·관리 mutation
- 일반 저장, 상태 전이, bulk/import apply, 첨부 metadata, 직접 관리자 삭제·복구
- 대화형 `LOGIN_SUCCESS` Redirect/Popup, 앱 접근 결과 `Allowed|ApprovalPending|Inactive`, 명시적 logout
- 성공 변경의 effective actor와 실제 대리 실행 actor snapshot
- 선택 Excel과 기존 권한 거부 원장의 read-only 통합

### 제외

- Entra 내부 비밀번호·MFA 실패와 sign-in log 연동
- silent token 갱신·cached account 복원
- worker·scheduler·자동 만료·provider 내부 처리
- 조회·preview·알림 읽음·web-push 기기 상태·404·5xx
- request/response body, exception message, header, cookie, token, 첨부 binary
- 과거 전용 원장의 소급 합성, 신규 알림 채널, 권한 확대, audit purge/archive
- 구현·격리 검증 단계의 Persistent UAT, 운영 DB migration, 실제 provider와 Azure 공개배포. 운영 DB migration과 Azure 공개배포는 이후 Change 003 승인으로 실행했다.

Coverage 수치는 다음처럼 서로 다른 집합을 센다. 상세 계약은 coverage registry에 있다.

| 분류 대상 | 전체 | 포함·추적 | 명시적 제외 |
| --- | ---: | ---: | ---: |
| 인증 mutation endpoint | `185` | `156` | `29` |
| schema relation | `145` | `94` | `51` |

## 3. 전체 아키텍처와 영향

### Database·Migration

`0083_global_access_change_audit.sql`이 destructive DDL 없이 다음 자산을 추가한다. 다만 94개 relation trigger와 audit fail-closed로 포함 mutation의 성공 조건이 바뀌므로 runtime 영향이 없는 migration은 아니다.

- coverage 시작 시각 1건을 보존하는 `audit_coverage_state`
- Login, Logout, MutationSucceeded, MutationFailed parent인 `audit_events`
- row·field before/after projection child인 `audit_event_changes`
- runtime role의 audit table 직접 DML 차단과 security-definer append 함수
- 94개 업무 relation의 `AFTER INSERT/UPDATE/DELETE` capture trigger. 구현 후 coverage 재검토에서 누락됐던 구매 필수항목 template·row 2개도 포함한다.
- update/delete를 거부하는 append-only trigger와 조회 index
- 선택 export kind `AuditLedgerSelected`

업무 데이터와 성공 audit는 같은 DB transaction에서 확정된다. 실제 field 변경이 없으면 사건을 만들지 않고, rollback이면 audit도 사라진다. audit projection/insert 실패는 업무 transaction까지 rollback하는 fail-closed다. Application rollback 때 이 원장을 삭제하지 않고 forward-fix한다.

### Backend·API

- runtime endpoint catalog와 exact method+route registry를 startup에서 대조한다.
- authorization 뒤 공통 middleware가 포함 mutation에만 actor·actual actor·request/login correlation context를 연다.
- request별 DB session GUC를 사용하는 mutation 연결은 pooling을 끈다. request UUID마다 별도 pool이 생겨 첫 전체 회귀에서 연결 정체가 발생한 문제를 차단하며 read traffic은 기존 stable pool을 유지한다.
- DB trigger가 기존 store 변경을 같은 transaction에서 포착하므로 약 140개 transaction call site를 각각 수정하지 않는다.
- 실패 응답은 body를 읽지 않고 400/422, 409, 412만 fixed enum으로 별도 기록한다. Change 002에서 승인한 v1 계약에 따라 409는 endpoint 이름으로 중복을 추정하지 않고 모두 `Conflict`로 보수 분류한다. 401/403은 기존 권한 원장, 404/5xx는 제외한다.
- 기존 권한 거절 원장에도 대리 실행의 actual actor를 추가해 effective user와 실제 관리자를 함께 보여준다.
- 관리자 조회는 coverage 시작 이후 신규 global 원장과 기존 권한 거부 원장을 공통 projection으로 합친다.
- 상세는 field 변경과 연결된 Login의 시각·IP·browser/OS family를 보여준다.

신규 API:

- `POST /api/audit/sessions/interactive-login`
- `POST /api/audit/sessions/logout`
- `GET /api/admin/audit-events`
- `GET /api/admin/audit-events/{eventId}`

관리자 조회·Excel은 기존 `Audit.Read.All` / `QmsPolicies.AuditReadAll`만 사용한다. 로그인 endpoint는 승인 대기·비활성 identity까지 자기 사건만 만들 수 있는 기존 `AuthenticatedIdentity` 경계를 사용한다.

### Frontend·UI·UX

- MSAL event API의 Redirect/Popup `LOGIN_SUCCESS`만 pending interaction UUID를 만든다.
- 서버가 반환한 login correlation·receipt는 MSAL cache 선택과 같은 local/session storage에 account별로 보관한다.
- localStorage 공유 탭은 pending/session 변경을 구독해 다른 탭의 새 로그인 중에 이전 correlation을 쓰지 않고, 새 session 발급 뒤 최신 값으로 교체한다.
- business mutation에만 두 식별 header를 붙이며 GET, 로그인/로그아웃, admin test-user header와 분리한다.
- logout은 인증된 keepalive request 뒤 session을 지우며 audit 장애가 실제 logout을 막지 않는다.
- `/admin/system/audit-events` route, 관리자 메뉴, 요약·필터·페이지·선택 Excel·상세 panel을 추가했다.
- desktop은 표, 720px 이하에서는 의미 순서가 같은 card를 표시한다.
- 원문을 표시하지 않는 metadata field는 `N자`, exact fixed scalar만 before/after로 보인다.
- coverage 시작 시각과 correlation 부재를 사용자에게 명시한다.

### Workflow·권한·기존 원장

기존 업무 상태 전이, CAS, lock, 권한, 알림, 전용 audit/history writer는 변경하지 않았다. 권한 거부는 기존 원장, Excel 성공은 기존 `data_export_events`, 알림·관리자·프로젝트 전용 원장은 기존 기능을 위한 canonical source로 계속 보존한다. 배포 이후 로그인·성공 변경·실패 시도의 전체 조회만 신규 원장을 canonical source로 사용한다.

## 4. 개인정보·secret 검토

### 허용 projection

- exact: bool/numeric/date/timestamp/UUID, `table.column` 단위 고정 allowlist의 status/code/type 등, attachment relation allowlist의 filename/MIME/byte size
- metadata-only: 일반 text/json의 before/after 길이
- identity snapshot: 표시명·부서만
- login-only: trusted proxy 처리 뒤 IP와 fixed browser/OS family

### 절대 제외

`password`, `token`, `authorization`, `cookie`, `secret`, `payload`, request/response/exception/raw body, binary/content/data/hash 계열 field를 trigger에서 제외한다. `notice_posts.body`와 `pending_comments.body` 같은 업무 본문은 원문 대신 bounded 길이만 남긴다. 실패 원인은 fixed enum과 fixed 서버 문구만 저장한다. Request/response/exception/header를 audit object로 직렬화하는 fallback은 없다.

검증 fixture에서 secret 형태 자유문이 exact before/after에 나타나지 않고 길이만 저장되는지, 문자열 exact projection이 suffix 추정이 아니라 고정 `table.column` allowlist인지, migration source에 request/response body field가 없는지, Excel formula marker가 기존 builder로 text 처리되는지 확인했다. 표시명·부서는 행위자 식별을 위한 명시적 snapshot 예외다. 실제 사용자·고객·프로젝트 원문은 구현 증빙에 사용하지 않았다. UI 기본 최근 30일은 조회 범위일 뿐 보존 기간이 아니며, 사용자 결정대로 v1 원장은 기한 없이 보존하고 archive/purge는 별도 Task로만 변경한다.

## 5. Excel·PDF·첨부 영향

- Excel: `admin-audit-events` selected export screen과 `AuditLedgerSelected` kind를 추가했다. 최대 선택 행, server column allowlist, 공식 행 순서, formula prefix 방어, concurrency gate, `data_export_events` 기록을 그대로 재사용한다.
- PDF: 생성·양식 변경 없음. PDF 재처리 endpoint는 provider 내부 처리로 신규 mutation audit에서 제외한다.
- 첨부: 기존 upload/download/binary 저장을 바꾸지 않는다. attachment relation allowlist에서 filename/MIME/byte size와 insert/delete/update field만 기록하며 body/hash/content는 제외한다.

## 6. 변경 파일과 역할

### 신규 핵심 파일

- `database/migrations/0083_global_access_change_audit.sql`: schema, append 함수, projection trigger, 94 relation, export kind
- `backend/src/Emi.Qms.Api/Audit/AuditContracts.cs`: 사건·조회·상세·login context 계약
- `backend/src/Emi.Qms.Api/Audit/AuditRequestContext.cs`: AsyncLocal request mutation context
- `backend/src/Emi.Qms.Api/Audit/AuditMutationRegistry.cs`: 전체 mutation endpoint 185개의 exact 분류, 포함 156·제외 29
- `backend/src/Emi.Qms.Api/Audit/AuditMutationMiddleware.cs`: actor/correlation, 성공 context, 실패 classifier
- `backend/src/Emi.Qms.Api/Audit/AuditStore.cs`: login/logout/failure append, 통합 조회·상세
- `backend/src/Emi.Qms.Api/Audit/AuditEndpointExtensions.cs`: 로그인 lifecycle과 관리자 API
- `frontend/src/audit.ts`, `frontend/src/AuditPage.tsx`: 화면 model·관리자 UI
- `backend/tests/Emi.Qms.Api.Tests/AuditInfrastructureTests.cs`: privacy·context·family unit contract
- `backend/tests/Emi.Qms.Api.Tests/AuditMutationCoverageTests.cs`: runtime route catalog exact coverage
- `frontend/tests/AuditPage.test.tsx`: desktop/mobile record와 연결 로그인 상세

### 수정 영역

- `Program.cs`, `DatabaseConnectionStringProvider.cs`, `DatabaseRuntimePrivilegeManager.cs`: middleware/API 등록, request DB context, least privilege
- `AuthorizationAuditLogger.cs`, `AdminScheduledDeletionService.cs`: 권한 거절 actual actor 보존과 사용자 purge reference preflight
- `DataExports/*`, selected export registry tests: 전체 감사 선택 Excel
- `PostgreSqlMigrationTests.cs`: fresh/upgrade, commit/no-op/rollback, privacy, append-only, runtime role, login/session/query/detail
- `App.tsx`, `api.ts`, `auth.ts`, `main.tsx`, `styles.css`, `auth.test.tsx`: route/menu/session header/logout/responsive UI와 회귀
- `tasks/audit-001-*`, Product Roadmap: 승인 계약·coverage·구현·검수·운영 handoff

Fable 원문 `tasks/audit-001-planning.md`와 raw interview round는 수정하지 않았다.

## 7. 실행한 검증과 결과

| 검증 | 결과 |
| --- | --- |
| Backend 전체 | `567/567` 통과, 19분 42초 |
| PostgreSQL migration class | `59/59` 통과, 55초 |
| Change 002 집중 검증 | failure reason·migration privacy·audit append 실패 rollback `14/14` 통과 |
| audit 집중 unit/integration | route catalog, privacy, migration, store, runtime privilege 모두 통과 |
| Backend build | warning `0`, error `0` |
| Frontend 전체 | `235/235` 통과 |
| Frontend typecheck | 통과 |
| Frontend lint | error `0`, 기존 `main.tsx` fast-refresh warning `1` |
| Frontend production build | 통과, 기존 대형 bundle warning 유지 |
| selected Excel registry | `5/5` 통과 |
| `git diff --check` | 통과 |
| final privacy/secret scan | credential signature 감지 `0`; audit code의 request/response/body/exception 직렬화 `0`; 식별 UUID header 2개만 허용 |
| 독립 read-only 재검증 | `PASS`, Open P0/P1/P2 `0/0/0`, local release candidate `READY` |
| Change 003 Azure release preflight | 마지막 성공 release와 원격 main SHA 일치; `cross-layer`; migration·Backend·Frontend `true`; Bicep·Portal template·static·release mock `PASS` |
| 구현 PR·필수 CI | PR `#111` squash merge 완료; PR CI run `33136383870` `PASS` |
| main CI | 운영 source exact SHA `6713e5974ad5262d87d7cc2332b27486d2487ccd`; run `33137735821` `PASS` |
| Azure 운영 release | run `33137792491`; migration·Backend·Frontend·public security smoke 모두 `PASS` |
| release workflow 밖 별도 공개 endpoint | health `200`, 익명 root `401`, 익명 API `401` |
| Fable private session cleanup | `FABLE_TASK_SESSION_CLEANED`, session/transcript 각 `5` 정리 |
| local full-stack visual | desktop list/detail/login context 확인; narrow viewport `375=375`, card `3`, desktop table hidden, horizontal overflow `0` |
| local 동시 mutation | non-pooled 연결로 시작한 50개 동시 저장, 업무 row `50`, 성공 parent `50`, field child 연결 `50` 통과 |

외부 기술 계약도 공식 문서와 대조했다.

- PostgreSQL row trigger는 원래 statement와 같은 transaction에서 실행되며 어느 쪽이든 오류가 나면 둘 다 rollback된다는 계약을 [PostgreSQL Trigger Behavior](https://www.postgresql.org/docs/current/trigger-definition.html)에서 확인했다.
- Npgsql은 기본적으로 pooling을 사용하며 connection string에서 이를 끌 수 있다는 계약을 [Npgsql Connection String Parameters](https://www.npgsql.org/doc/connection-string-parameters)에서 확인했다. Request별 pool 증가 자체는 첫 전체 회귀의 연결 정체와 수정 뒤 전체 회귀 통과로 검증했다.
- MSAL Browser의 `LOGIN_SUCCESS`가 `Popup` 또는 `Redirect` interaction으로 발생하고 silent token 사건과 분리된다는 계약을 [Microsoft MSAL Browser Events](https://learn.microsoft.com/en-us/entra/msal/javascript/browser/events)에서 확인했다.

전체 Backend 첫 실행에서 request별 audit GUC가 connection string pool key를 바꿔 연결 pool이 증가하는 결함을 발견했다. 해당 실행을 중단하고 audit mutation connection에 `Pooling=false`를 적용했으며 unit guard를 추가했다. Change 002 독립 검증 보정까지 모두 반영한 최종 전체 Backend `567/567`은 연결 정체 없이 통과했다.

종료 전 검증 설계 재검토에서 포함 endpoint인 구매 필수항목 설정이 쓰는 `procurement_required_item_templates`와 `procurement_required_item_template_rows`가 relation trigger 목록에서 빠진 것을 발견했다. 두 relation을 추가해 `94/94`로 보정하고 실제 parent·exact item code·metadata-only item name을 DB test로 고정했다. 이어 non-pooled 연결 50개를 동시에 시작해 업무 row와 성공 parent·child가 각각 50건으로 일치하는지 확인했다. Change 002 보정까지 포함한 전체 Backend·migration 검증 결과는 각각 `567/567`, `59/59`이다.

Change 002 이후 첫 Backend 전체 재실행은 일부 기존 test fixture가 고정 포트 `5432`를 사용하는데 임시 PostgreSQL을 `55439`에 연 설정 오류로 즉시 중단했다. Task 전용 PostgreSQL을 기존 회귀가 기대하는 `5432`에 다시 열고 처음부터 재실행해 `567/567`을 통과했다. 이 중단 실행은 제품 결함이나 통과 수치로 계산하지 않았다.

로컬 full-stack은 임시 PostgreSQL·Development identity·synthetic holiday 1건과 validation 실패 1건만 사용했다. 종료 뒤 Vite, Backend와 Task 소유 `--rm` PostgreSQL container를 모두 종료했고 Task 소유 잔존 container가 없음을 확인했다. 별도 작업의 `valhalla-walking-engine-1`은 수정하지 않았다. 이 로컬 검증에서는 Persistent UAT와 Azure 운영 데이터를 변경하지 않았다.

## 8. 시행착오 및 폐기한 접근

### 업무 store마다 recorder를 직접 삽입

기획 권장안은 store transaction 주입형 recorder였다. 실제 Repository에는 direct transaction call site가 약 140개이고 변경 경로가 계속 늘어나 누락 위험이 컸다. PostgreSQL request context + fixed relation trigger를 사용해 기존 transaction 자체를 source로 삼았고 route/relation registry를 fail-closed로 고정했다.

### request별 audit context가 포함된 pooled connection string

초기 구현은 연결 시점 GUC를 정확히 전달했지만 request UUID마다 별도 pool을 만들었다. 전체 회귀에서 연결 부족 대기가 재현되어 폐기했다. Mutation request만 non-pooled short-lived connection으로 바꾸고 read 요청은 기존 pool을 유지했다. 동시접속 50명 규모에서 correctness와 bounded connection lifecycle을 우선하며, 향후 성능 실측에서 필요하면 공통 connection initializer 구조를 별도 최적화한다.

### 데스크톱 표만 responsive scroll로 사용

기획의 390px 의미 순서 보존을 충족하지 못해 폐기했다. 실제 mobile card를 추가하고 desktop table을 breakpoint에서 숨겼다.

### 변경 상세에서 correlation UUID만 제공

관리자가 UUID를 보고 로그인 맥락을 판단할 수 없으므로 폐기했다. 서버가 correlation 소유 Login 사건을 다시 조회해 시각·IP·fixed browser/OS를 상세에 제공한다.

## 9. SOP — 운영 적용·관찰·복구

### 배포 전

1. Change 002 완화 계약의 독립 재검증 PASS와 exact release SHA의 최신 Backend 전체, Frontend `235/235`, build, migration fresh/upgrade와 50개 동시 mutation 결과를 다시 확인한다.
2. 운영 DB backup/restore readiness와 migration/runtime 분리 role을 확인한다. Secret 원문은 증빙에 적지 않는다.
3. `0083` 적용 전 현재 latest migration이 `0082`이고 원격 release branch가 승인된 exact SHA인지 확인한다.

### 적용 순서

1. migration role로 additive `0083` 적용
2. runtime role의 audit table direct DML 거부·append function execute·select 권한 확인
3. Backend 배포 및 ready 확인
4. Frontend 배포 및 ready/public security smoke 확인
5. 관리자 bounded synthetic 검수 후 coverage 시작 시각 이후 count/fixed enum만 privacy-safe evidence로 기록

### 운영 확인

- 관리자 1명이 대화형 로그인 1건, 성공 저장 1건, validation 실패 1건을 확인한다.
- 성공 변경 상세의 연결 로그인과 자유문 `N자`, fixed scalar before/after를 확인한다.
- 일반 역할의 감사 API 접근이 403이고 기존 권한 거부 원장에 남는지 확인한다.
- 선택 Excel의 행 수·필수 컬럼·formula text 처리를 확인한다.
- DB connection·latency·audit row 증가량을 aggregate로 관찰한다. 원문을 로그나 증빙에 복사하지 않는다.

### 장애·rollback

- Backend/Frontend 회귀에는 application rollback을 사용할 수 있지만, 이전 application은 audit request context를 만들지 않아 신규 원장 coverage가 중단된다. 따라서 이전 application으로 내릴 때는 포함 mutation을 중단하고 신규 audit schema를 보존한 채 forward-fix한다.
- DB trigger·privilege·audit insert·capacity 장애는 application rollback만으로 쓰기 복구를 보장하지 않는다. 배포와 포함 mutation을 중단하고 승인된 forward-fix migration/application으로 보정한다.
- audit append가 business mutation을 5xx로 막으면 affected route/action fixed code와 aggregate count만 수집하고 배포를 중단한다.
- login/failure best-effort append 장애는 사용자 operation을 막지 않지만 구조화 server error를 기준으로 운영 alert를 처리한다.
- 장기 보존량·archive·purge는 현재 범위 밖이며 운영 관찰 후 별도 Task로만 결정한다.

## 10. User manual — 전체 감사 이력 사용법

1. System Administrator로 로그인한 뒤 `관리자 → 전체 감사 이력`을 연다.
2. 상단 카드에서 전체, 로그인, 저장 완료, 저장 실패·권한 거절 수를 확인한다.
3. 시작일·종료일, 사건, 업무영역, 실패 종류, 행동, 사용자·부서·대상 검색을 지정하고 `조회`를 누른다.
4. 발생일시 또는 mobile의 `상세 보기`를 눌러 사용자·업무·대상·연결 로그인과 field 변경을 확인한다.
5. `변경 전/후`에 값이 보이면 고정 scalar이고, `N자`로 보이면 자유 입력 원문을 보존하지 않은 길이 정보다.
6. `로그인 연결 없음`은 오류가 아니라 대화형 로그인 correlation이 없던 세션의 변경이다.
7. 필요한 행을 선택하고 컬럼을 확인한 뒤 `선택 Excel 내보내기`를 누른다.
8. 화면의 “이 시각 이후 전체 기록” 이전 데이터는 이 화면에서 전체 이력으로 추정하지 않는다.

일반 사용자에게는 메뉴와 API가 제공되지 않는다. 감사 원장은 화면에서 수정·삭제할 수 없다.

## 11. 사용자 검수 체크리스트

상태: `자동·독립 검증 완료 / 사용자 직접 검수 대기`

### 일반 자동 회귀·대표 계약 검증 완료

- [x] Change 002 독립 Finding 보정 후 전체 Backend `567/567`·migration `59/59`
- [x] Frontend `235/235` 회귀·typecheck·build
- [x] migration fresh/upgrade와 runtime role 권한
- [x] commit/no-op/rollback/append-only/privacy adversarial fixture
- [x] 대화형 login 중복 방지·session owner·logout 1회
- [x] 실패 fixed reason·기존 권한 거부 통합 query
- [x] selected Excel registry와 formula-safe builder
- [x] desktop·375px list/detail/login context·overflow

### 공개 운영 사용자 검수 대기

- [ ] 본인의 새 대화형 로그인 1건이 한 번만 보인다.
- [ ] 승인된 bounded 업무 데이터 1건을 바꾼 뒤 사용자·시각·대상·field before/after가 맞다.
- [ ] validation 실패 1건이 입력 원문 없이 실패 종류로 보인다.
- [ ] 변경 상세의 연결 로그인 시각·IP·browser/OS family가 맞다.
- [ ] 자유문은 원문 대신 길이만 보이고 첨부 body는 보이지 않는다.
- [ ] 선택 Excel의 행·필수 컬럼·한글 시각이 화면과 맞다.
- [ ] 일반 역할은 메뉴/API 접근이 차단된다.
- [ ] 과거 소급 없음과 coverage 시작 안내를 이해했다.

이 체크리스트는 존재만으로 완료되지 않는다. Change 002 독립 재검증은 PASS했으며, 사용자가 직접 확인하기 전까지는 `사용자 검수 대기`다.

## 12. Finding, known issue와 잔여 위험

### Resolved

- `AUDIT-IMPL-F01` P1 `RESOLVED`: request별 GUC connection string이 pool key를 증가시켜 전체 회귀에서 연결 대기가 발생. audit mutation connection pooling을 비활성화하고 full regression으로 재검증.
- `AUDIT-IMPL-F02` P2 `RESOLVED`: audit timestamp가 Npgsql에서 `DateTime`으로 반환되어 조회 cast 실패. UTC normalization helper와 integration test 추가.
- `AUDIT-IMPL-F03` P2 `RESOLVED`: mobile card 미렌더링. 실제 card와 390px overflow 검증 추가.
- `AUDIT-IMPL-F04` P2 `RESOLVED`: 상세에서 연결 로그인 환경 미표시. linked login projection과 UI/test 추가.
- `AUDIT-IMPL-F05` P3 `RESOLVED`: PostgreSQL inet 표시가 `/32` suffix를 노출. `host(inet)`으로 일반 IP 표시 정규화.
- `AUDIT-IMPL-F06` P1 `RESOLVED`: 포함된 구매 필수항목 설정 endpoint의 두 기준정보 relation이 trigger registry에서 누락. 두 relation을 추가하고 실제 DB parent·field projection 회귀로 고정.
- `AUDIT-IMPL-F07` P1 `RESOLVED`: 문자열 exact projection이 field suffix 추정에 의존해 승인된 field별 allowlist보다 넓었음. 실제 스키마의 `table.column` 고정 목록으로 좁히고 자유문 secret fixture를 metadata-only로 검증.
- `AUDIT-IMPL-F08` P2 `RESOLVED`: 새 대화형 로그인 직후 신규 감사 세션 발급 전에는 저장된 이전 correlation이 잠시 복원될 수 있었음. pending login이 있으면 이전 session header를 사용하지 않고 신규 발급 전까지 null 연결로 유지하도록 unit contract를 추가.
- `AUDIT-IMPL-F09` P2 `RESOLVED`: 대리 실행의 actual actor snapshot은 DB·Excel에 있었지만 관리자 목록·상세에서 바로 보이지 않았음. effective actor 아래에 실제 사용자를 표시하고 desktop/mobile/detail 회귀로 고정.
- `AUDIT-IMPL-F10` P1 `RESOLVED`: 대리 실행의 403이 기존 권한 원장에 effective user로만 남아 실제 관리자를 잃었음. `actual_actor_user_id` schema·logger·통합 query와 실제 DB integration 검증으로 보정.
- `AUDIT-IMPL-F11` P2 `RESOLVED`: `Create/Register/Issue` endpoint 이름만으로 409를 `Duplicate`로 추정해 상태 충돌을 오분류. 이름 추정을 완전히 제거하고 409를 `Conflict`로 보수 분류하며 unknown route를 fail-closed로 고정.
- `AUDIT-IMPL-F12` P2 `RESOLVED`: 실제 첨부 schema가 쓰는 `normalized_mime`이 exact allowlist에 없었고 content-type 예외가 forbidden guard 뒤에 있어 도달할 수 없었음. 제한된 attachment relation의 MIME만 exact로 허용하고 content/content_hash 미기록을 실제 DB test로 고정.
- `AUDIT-IMPL-F13` P1 `RESOLVED`: 기존 권한 원장의 새 `actual_actor_user_id` FK가 사용자 purge reference preflight에 없어 예외로 rollback될 수 있었음. reference column을 추가하고 actual-actor-only fixture가 `PurgeBlocked`와 사용자 보존으로 끝나는지 실제 DB로 고정.
- `AUDIT-IMPL-F14` P2 `RESOLVED`: localStorage를 공유하는 다른 탭이 새 대화형 로그인을 시작해도 이미 열린 탭의 module session이 이전 correlation을 계속 사용할 수 있었음. account pending/session storage event를 구독해 pending 중 null, 신규 발급 뒤 최신 session으로 교체하는 multi-tab contract를 추가.
- `AUDIT-IMPL-F15` P1 `RESOLVED`: forbidden regex의 일반 `body`가 `notice_posts.body`·`pending_comments.body`·`notice_posts.body_format`까지 완전 제외해 metadata-only 계약을 깨뜨렸음. request/response/exception/raw body만 제외하고 업무 body는 길이만, `body_format`은 고정 enum으로 실제 DB 검증.
- `AUDIT-IMPL-F16` P1 `RESOLVED`: audit append 자체가 실패할 때 business mutation도 rollback된다는 직접 증빙이 없었음. `audit_events` insert를 통제된 trigger로 실패시키고 업무 row·audit parent·field child가 모두 `0`건인지 실제 PostgreSQL에서 검증.
- `AUDIT-IMPL-F17` P2 `RESOLVED`: Change 002가 모든 409를 `Conflict`로 확정했지만 `Duplicate`가 Backend allowlist·DB constraint/function·관리자 filter/label에 남아 있었음. 두 fixed reason만 남기고 Backend·migration·Frontend 부재 검증으로 고정.

### Open/Backlog

- 최종 독립 read-only 재검증 기준 Open P0/P1/P2: `0/0/0`.
- `AUDIT-COVERAGE-EXECUTION-GAP` P1 `RESOLVED_BY_CHANGE_002 / VERIFIED`: endpoint 분류 `185/185`(포함 156·제외 29), relation 분류 `145/145`(추적 94·제외 51), 중앙 PostgreSQL 성공·accepted no-op·caller rollback·audit append 실패 rollback·privacy·append-only·권한 대표 검증과 동시 mutation 50건을 v1 acceptance로 승인·검증했다. 156-route 1:1 matrix는 요구하지 않으며 모든 route의 개별 업무 규칙을 실행 증명했다고 주장하지 않는다.
- `AUDIT-DUPLICATE-CLASSIFICATION-GAP` P2 `RESOLVED_BY_CHANGE_002 / VERIFIED`: v1은 400/422 `Validation`, 409/412 `Conflict`만 사용한다. 중복도 `Conflict`로 보수 분류하며 endpoint·action 이름 추정은 금지한다. `Duplicate`는 Backend·DB·UI와 관리자 filter/label에서 제거했다.
- `AUDIT-BACKLOG-001` P3 `BACKLOG`: 무기한 append-only 저장량의 archive/purge 정책. 현재는 사용자 확정대로 purge 없음. 운영 row growth와 DB 비용 관찰 후 별도 Task ID를 Roadmap에서 확정한다.
- `AUDIT-BACKLOG-002` P3 `BACKLOG`: Entra 단계 실패 로그인과 sign-in log/Graph 연동. 앱 외부 신규 연동이므로 별도 NEW_FEATURE 후보다.
- `AUDIT-BACKLOG-003` P3 `BACKLOG`: local PostgreSQL에서 non-pooled 동시 mutation 50건의 업무/audit 일치와 오류 0은 확인했다. Azure 운영 DB의 connection ceiling·p95 latency·peak connection은 배포 후 aggregate로 관찰하고 필요할 때 stable pool + explicit per-connection initialization 최적화를 별도 Task로 검토한다.
- 기존 Frontend `main.tsx` fast-refresh warning 1건과 bundle size warning은 이번 기능의 오류가 아니며 기존 backlog 성격을 유지한다.

## 13. 미실행 검증

- 실제 Microsoft 365 interactive login: 배포는 완료했지만 사용자 직접 운영 검수 단계이므로 미실행. Frontend event contract와 local Dev identity로 검증했다.
- Persistent UAT migration/runtime handover: 승인 범위 밖이므로 미실행.
- 실제 운영 Excel 다운로드와 일반 역할 403: local contract·자동 검증까지만 완료, 공개 운영 사용자 checklist에서 대기.
- Azure 운영 환경의 실제 50명 동시 부하: local 전용 PostgreSQL의 50개 동시 mutation은 통과했지만 Azure network·DB tier를 포함한 부하는 미실행이다. 운영 aggregate 관찰을 P3로 추적한다.
- 포함 route 156개의 실제 성공·accepted no-op·rollback 1:1 execution matrix: 미실행이다. Change 002에서 v1 acceptance 제외를 승인했으며 모든 route의 개별 업무 규칙을 실행 증명했다고 주장하지 않는다.
- 중복과 상태 충돌을 구분하는 server-owned typed 409 signal: 미구현이다. Change 002에서 v1은 모든 409를 안전하게 `Conflict`로 기록하도록 승인했다.

## 14. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 이 문서 |
| SOP | 작성됨 | 이 문서 9장 |
| User manual | 작성됨 | 이 문서 10장 |
| Roadmap update | 작성됨 | `docs/00-product-roadmap.md` 실행 큐 `3.3L`·추적 `48`·Decision Log `2026-08-28` |
| User validation checklist | 자동·독립 검증 완료 / 사용자 직접 검수 대기 | 이 문서 11장 |

## 15. 사용자 검수 결과와 남은 항목

현재 local implementation·exact catalog·중앙 PostgreSQL 대표 transaction·local full-stack visual·Backend `567/567`·Frontend `235/235`·migration `59/59`·final privacy/secret scan을 포함한 일반 자동 회귀를 완료했다. 사용자는 Change 002에서 이를 v1 acceptance로 확정하고 모든 409를 `Conflict`로 보수 분류하도록 승인했다. 독립 검증에서 발견된 audit append 실패 rollback 증빙과 잔존 `Duplicate` 계약도 보정했고, 최종 read-only 재검증은 `PASS`, Open P0/P1/P2 `0/0/0`이다. Change 003에 따라 PR `#111`을 squash merge해 운영 source exact main SHA `6713e5974ad5262d87d7cc2332b27486d2487ccd`를 만들었고, main CI run `33137735821`과 Azure release run `33137792491`이 통과했다. Migration·Backend·Frontend·public security smoke가 모두 `PASS`이며 release workflow 밖 별도 HTTP 확인은 health `200`, 익명 root·API `401/401`이다. Local Persistent UAT handover와 실제 외부 알림 시험 발송은 제외했다. 공개 사용자 직접 검수와 운영 aggregate 관찰은 계속 대기 상태다.
