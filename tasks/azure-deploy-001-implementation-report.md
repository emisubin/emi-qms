# TASK-AZURE-DEPLOY-001 Implementation Report — 20일 Azure 시범 배포

## 현재 상태

- Task 유형: `UAT_RUNTIME` / Change 003 `SECURITY_HARDENING`
- 기준 SHA: `4d15b7cee0d97f1846a1838500f9c9edf11b68bf`
- 작업 branch: `fix/task-azure-deploy-001-p1-hardening`
- Local deployment artifact: `Change 003 구현 및 자동 검증 완료`
- Azure resource와 비용 발생 작업: `사용자 실행 대기`
- Public traffic: `미전환`
- 사용자 검수: `Change 003 완료`
- Commit: `Change 003 미커밋`
- Push / PR / Merge: `사용자 승인 완료 / 미수행`

이 보고서는 비용이 발생하지 않는 local 준비 단계의 완료를 기록한다. Azure resource 생성, image push, migration·restore rehearsal, DNS·TLS, 실제 provider 발송과 public traffic 전환을 완료로 주장하지 않는다.

## Change 003 — P1 최소 권한 보정

### 해결한 Finding

| ID | 등급 | 상태 | Root cause | 해결 |
| --- | --- | --- | --- | --- |
| `AZURE-IDENTITY-001` | P1 | `RESOLVED_LOCAL` | 공개 Frontend, Backend와 migration이 하나의 managed identity와 vault-scope secret read를 공유 | 네 identity로 분리하고 Key Vault read를 secret resource scope 10개로 제한. vault-scope workload assignment 제거 |
| `AZURE-DB-ROLE-001` | P1 | `RESOLVED_LOCAL` | Backend와 migration이 관리자급 PostgreSQL 연결을 공유할 수 있었고 별도 runtime role 생성·검증 단계가 없음 | 관리자·`pms_migrator`·`pms_app` 연결 분리, manual bootstrap job과 최소 권한 reconciler, Production fail-closed connection policy 추가 |

### 실제 구현

1. Foundation이 Backend, Frontend, migration, database bootstrap user-assigned identity를 각각 만든다.
2. 신규 `identity-access.bicep`은 Backend 5개, Frontend 1개, migration 1개, database bootstrap 3개 조합에만 secret-scope `Key Vault Secrets User`를 부여한다.
3. Backend는 runtime DB와 실제 알림에 필요한 secret만, migration은 migration DB 하나만, Frontend는 origin token 하나만 참조한다.
4. `database-role-bootstrap` manual job은 관리자 DB secret을 사용해 두 제한 role을 idempotent하게 생성하고 password rotation을 반영한다.
5. `pms_migrator`는 DB connect·temporary, public schema create·usage와 migration object ownership을 가진다. role/database 관리 권한은 없다.
6. `pms_app`은 DB connect, public schema usage, 업무 table CRUD, sequence usage/select, trigger/function execute와 `schema_migrations` select만 가진다. schema·role·temporary table 생성과 ledger mutation은 거부된다.
7. migration 성공 뒤 migration role이 기존 table·sequence·자체 function과 default privilege를 runtime role에 다시 맞춘다.
8. Production database bootstrap/migration command는 세 연결의 TLS `VerifyFull`, 고정 username, 32자 이상·상호 다른 password와 동일 endpoint를 fail-closed로 검증한다.

### 자동 검증

| 검증 | 결과 |
| --- | --- |
| Bicep 4종 foundation/identity-access/workloads/edge compile | `PASS` |
| Azure artifact static invariant와 Teams package | `PASS` |
| Backend build, warning | `PASS`, `0` |
| P1 보안 집중 test | `42/42 PASS` |
| PostgreSQL 실제 role·권한 test | runtime 업무 CRUD 성공, schema·role·temporary·ledger mutation 거부 `PASS` |
| Backend 전체 격리 회귀 | `481/481 PASS`, 11분 45초 |
| Production Backend image | `PASS` |
| Production migration image fresh/existing | migration `67/67 Exact`, 두 실행 `PASS` |
| Backend NuGet known vulnerability | `0` |
| ShellCheck / Git whitespace | `PASS` |
| 변경·신규 파일 high-confidence secret pattern | `0` |
| 독립 diff 재검토: identity/secret matrix, API role fail-closed, migration/bootstrap 직렬화, 문서·parameter 정합성 | `PASS`, 신규 P0/P1/P2 `0` |
| 임시 DB·container·network cleanup | `PASS` |

### 미실행과 경계

- 실제 Azure identity, role assignment와 Key Vault access probe는 resource가 아직 없어 실행하지 않았다.
- 실제 Azure PostgreSQL role bootstrap, migration, PITR와 serving session user 확인은 비용·runtime Gate로 남는다.
- 이 변경은 Azure resource 생성, image push, DNS, traffic 또는 실제 Teams·Gmail 발송을 수행하지 않았다.
- 실제 배포에서는 Foundation → 8개 secret 입력 → secret-scope RBAC → inactive workload → DB role bootstrap → migration → restore → active workload → edge 순서를 지켜야 한다.

## 1. 해결한 업무 문제

20일 동안 세 프로젝트를 최종 hostname으로 시범 운영하기 위해 기존 service-neutral Production 준비물을 Azure의 실제 서비스 경계로 옮길 수 있는 배포 artifact가 필요했다. 동시에 사용자가 비용 관련 실행을 직접 담당하므로, local 준비와 Azure 생성 사이에 명시적인 중단선을 두어야 했다.

이번 변경으로 다음을 준비했다.

1. Front Door 뒤에서만 Frontend origin을 사용할 수 있는 이중 origin 검증
2. Container Apps의 동적 private network를 제한된 CIDR로 신뢰하는 Backend proxy 정책
3. Foundation, inactive workload, migration, restore, active workload와 edge를 분리한 Azure Bicep
4. migration-only job과 serving API의 restore verification gate 분리
5. PMS용 Teams manifest template과 재현 가능한 package builder
6. 실제 hostname, 계정, identifier와 secret을 Git에 남기지 않는 parameter·Key Vault 경계

## 2. 목적, 포함 범위와 제외 범위

### 포함

- Azure Front Door Standard, custom rate limit와 managed TLS 배포 정의
- Container Apps Consumption profile의 Frontend, Backend/worker와 ClamAV 정의
- PostgreSQL Flexible Server Burstable B2s, private network, 32 GB, auto-grow와 PITR 14일 정의
- ACR Basic, Azure Files 5 GB, Key Vault, Log Analytics와 workspace-based Application Insights 정의
- one-shot migration job과 traffic activation gate
- Azure 전용 Frontend image와 origin protection
- Backend trusted proxy network와 migration-only production preflight 보정
- Teams manifest, icon과 ZIP package 생성·검증
- local build, static validation, Bicep compile, migration image와 Backend 전체 회귀 검증

### 제외

- Azure provider 등록, resource group와 실제 resource 생성
- budget과 alert 생성
- ACR push와 실제 secret 입력
- 실제 PostgreSQL migration과 PITR restore rehearsal
- DNS, certificate, public traffic과 조직 Teams catalog 변경
- 실제 Teams·Gmail 발송
- Blob Storage 분리와 정식 운영 HA·Premium WAF 결정
- Git push, PR와 merge

제외 항목은 실패나 누락이 아니라 사용자가 직접 수행하기로 한 비용·운영 경계다.

## 3. 전체 아키텍처와 영향

### Frontend와 Edge

- `frontend/Dockerfile.azure`가 static Frontend를 빌드해 Nginx 8080으로 제공한다.
- `/health/live`만 origin health probe에 허용하고, 나머지는 Front Door ID와 별도 random origin token이 모두 일치해야 한다.
- Front Door가 전달한 client IP를 Backend rate-limit 판단에 유지한다.
- 기존 local·Docker-host Frontend image와 Compose 경로는 변경하지 않았다.

### Backend, API, 권한과 Workflow

- `ReverseProxy:KnownNetworks`를 추가해 bounded CIDR만 trusted proxy로 허용한다.
- `/0`, loopback과 잘못된 network 설정은 Production startup을 중단한다.
- migration-only mode는 초기 배포 전에 불가능한 restore rehearsal 확인만 건너뛴다. Entra, HTTPS, DB, malware scan 등 다른 Production security gate는 유지한다.
- API, 업무 권한, 상태 전이, 알림 수신자 정책과 사용자 Workflow는 변경하지 않았다.

### DB와 Migration

- 신규 application migration은 없다.
- 기존 migration ledger를 one-shot Azure Container Apps Job에서 Exact로 검증한다.
- 초기 migration 성공 후 PITR restore rehearsal을 통과하기 전 serving workload와 public traffic을 활성화하지 않는다.
- PostgreSQL은 private subnet만 사용한다.

### 첨부파일, Excel, PDF와 QR

- 시범 기간에는 기존 PostgreSQL 첨부 저장 계약을 유지한다.
- Excel, PDF와 QR 생성 코드는 변경하지 않았다.
- 업로드 malware scan은 ClamAV private TCP app에 연결하고 Production에서 fail-closed를 유지한다.

### UI·UX

- 제품 화면 변경은 없다.
- Teams 개인 tab은 최종 public hostname을 사용하는 새 PMS manifest로 package할 수 있다.

## 4. 기술적 결정과 검토한 대안

1. **Foundation / workload / edge 분리**
   - resource 생성, secret 입력, migration·restore와 traffic 전환을 한 번에 실행하지 않는다.
   - 실패 지점을 분리하고 비용 발생 전에 사용자가 각 단계를 확인할 수 있다.
2. **API replica 최대 1**
   - API process 안의 background worker 중복 실행을 방지한다.
   - replica 확장은 worker 분리 이후 정식 운영에서 다시 판단한다.
3. **ClamAV 2 vCPU / 4 GiB**
   - signature load와 동시 file scan의 memory 부족을 피하기 위한 시범 최소 여유다.
   - workload activation 전에는 replica 0으로 배포할 수 있다.
4. **Front Door Standard custom rate limit**
   - 외부 사용자가 없는 시범 기간에 Premium WAF 고정비를 보류한다.
   - managed WAF와 private origin은 정식 운영의 실측 기반 결정으로 남긴다.
5. **PostgreSQL B2s / HA 없음**
   - 세 프로젝트 시범량에 맞춘 사양이다.
   - 60분 복구 목표는 PITR rehearsal로 검증하며, 정식 운영 HA 선택은 실제 복구시간으로 결정한다.
6. **Application Insights resource와 connection string만 준비**
   - Container log는 Log Analytics에서 수집할 수 있다.
   - 현재 Backend에는 Application Insights SDK 기반 APM 계측이 없어 request trace 기능은 아직 활성화되지 않는다. 시범 운영 중 필요성이 확인되면 별도 P3 계측 작업으로 연결한다.

## 5. 시행착오 및 폐기한 접근

- Bicep의 최신 Container Apps schema에서 workload profile configuration 경로와 Front Door origin 속성이 달라 첫 compile 오류가 있었다. 현재 schema에 맞춰 수정하고 세 파일을 모두 다시 compile했다.
- 기본 local PostgreSQL에 직접 Backend 전체 test를 실행했을 때 local password 불일치로 test가 실패했다. application 결함으로 오판하지 않고 tmpfs PostgreSQL을 사용하는 격리 test runner로 재실행해 476개 전체 통과와 cleanup을 확인했다.
- 실제 Azure 상태를 확인하거나 provider를 등록하는 방식은 비용·운영 경계를 흐리므로 사용하지 않았다.

## 6. 실제 변경 파일

### Backend

- `backend/src/Emi.Qms.Api/Program.cs` — trusted network와 migration-only restore gate 연결
- `backend/src/Emi.Qms.Api/Security/ProductionSecurityPolicy.cs` — CIDR와 restore verification 정책
- `backend/src/Emi.Qms.Api/Security/TrustedProxyConfiguration.cs` — proxy network parsing
- `backend/src/Emi.Qms.Api/appsettings.json` — `KnownNetworks` 기본 구조
- `backend/tests/Emi.Qms.Api.Tests/PublicDeploymentSecurityTests.cs` — 정상·거부·migration-only security test

### Frontend와 Azure

- `frontend/Dockerfile.azure` — Azure Production Frontend image
- `infrastructure/azure-pilot/foundation.bicep` — network, registry, storage, observability, PostgreSQL, Front Door foundation
- `infrastructure/azure-pilot/workloads.bicep` — inactive/active application workload와 migration job
- `infrastructure/azure-pilot/edge.bicep` — origin, custom domain, route, TLS와 security policy
- `infrastructure/azure-pilot/nginx.conf.template` — origin verification과 proxy
- `infrastructure/azure-pilot/*.parameters.example.json` — synthetic parameter 예시
- `infrastructure/azure-pilot/README.md` — 배포 operator manual과 rollback

### Teams와 도구

- `infrastructure/teams/manifest.template.json` — PMS Teams app manifest 원본
- `scripts/build-teams-manifest-package.sh` — icon·manifest·ZIP 생성
- `scripts/test-teams-manifest-package.sh` — package와 negative validation
- `scripts/build-azure-pilot-images.sh` — local Backend·Frontend image build
- `scripts/validate-azure-pilot-artifacts.sh` — static invariant와 선택적 Bicep compile

### Task와 governance

- `.gitignore` — local secret parameter와 generated output 제외
- `docs/00-product-roadmap.md` — Task 상태와 비용 Gate
- `tasks/azure-deploy-001-identity-gate.md`
- `tasks/azure-deploy-001-change-001.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `tasks/azure-deploy-001-implementation-report.md`

기존 Fable interview artifact는 사용자의 Codex-only 지시에 따라 이번 구현의 source로 사용하거나 수정하지 않았다.

## 7. 실행한 자동 검증

| 검증 | 결과 |
| --- | --- |
| Backend public deployment security tests | `37/37 PASS` |
| Backend 전체 격리 회귀 test | `476/476 PASS`, 14분 57초 |
| Backend Production image build | `PASS` |
| Production migration image fresh/existing apply | `PASS`, ledger `67 Exact` |
| Azure Frontend Production image build | `PASS` |
| Frontend origin smoke | health `200`, direct `403`, partial header `403`, trusted edge `200` |
| Bicep compile | foundation/identity-access/workloads/edge `PASS` |
| Azure artifact static validation | `PASS` |
| Teams manifest/package test | `2/2 PASS` |
| Shell syntax와 ShellCheck | `PASS` |
| Git whitespace validation | `PASS` |
| tracked 배포 산출물 email/private-key pattern scan | `0` |
| 격리 DB·container·network cleanup | `PASS` |

Frontend build에는 기존 large bundle warning이 있었으나 build 실패나 배포 artifact 오류는 없었다.

## 8. 미실행 검증

| 검증 | 상태와 이유 |
| --- | --- |
| Azure Bicep `what-if`와 실제 create | 비용 관련 Azure 동작은 사용자 실행 범위 |
| Actual migration job과 ledger | Azure PostgreSQL 생성 전 실행 불가 |
| PITR restore 60분 목표 | 실제 PostgreSQL backup 생성 후 검증 가능 |
| DNS, managed TLS와 Front Door route | resource와 DNS 변경 전 검증 불가 |
| Entra 실제 login·승인 workflow | public callback과 실제 app 설정 전 검증 불가 |
| Teams 조직 catalog·activity | 실제 package parameter와 admin 게시 전 검증 불가 |
| Gmail 실제 발송 | Key Vault secret과 public workload 활성화 전 검증 불가 |
| 사용자 세 프로젝트 UAT | public runtime 전환 전 검증 불가 |

## 9. 개인정보와 Secret 검토

- 실제 hostname, 사용자 이름, 회사 email, tenant/client/object identifier와 secret을 tracked 배포 파일에 기록하지 않았다.
- parameter 예시는 `example.*`, synthetic GUID와 placeholder만 사용한다.
- 실제 값은 ignored `*.local.json`, Azure Portal secure parameter와 Key Vault에만 둔다.
- 검증 로그는 status, count와 aggregate만 남기도록 구성했다.

## 10. Finding과 Pre-traffic Gate

### Finding

| ID | 등급 | 상태 | 내용과 영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `AZURE-IDENTITY-001` | P1 | `RESOLVED_LOCAL` | workload identity와 Key Vault 권한 공유로 침해 범위가 전체 secret으로 확대될 수 있었다. | 네 identity와 secret-scope assignment 10개로 분리. 실제 Azure access probe는 pre-traffic에서 검증 |
| `AZURE-DB-ROLE-001` | P1 | `RESOLVED_LOCAL` | API와 migration의 DB 관리자 권한 공유 가능성이 있었다. | `pms_app`·`pms_migrator`·admin 분리와 실제 PostgreSQL negative privilege test 통과 |
| `AZURE-COST-GATE-001` | External gate | `OPEN` | 실제 Azure resource가 없어 public pilot은 아직 시작되지 않았다. | 사용자가 비용·credit 확인과 budget 설정 후 foundation부터 직접 실행 |
| `AZURE-APM-001` | P3 | `BACKLOG` | Application Insights resource 정의는 있으나 Backend SDK APM 계측은 아직 없다. Log Analytics container log는 사용 가능하다. | 시범 운영에서 request trace 필요성을 확인한 뒤 별도 계측 |
| `FRONTEND-BUNDLE-001` | P3 | `BACKLOG` | Production build의 기존 large bundle warning이 유지된다. 기능 오류는 아니다. | 정식 운영 성능 점검에서 route chunk 분할 검토 |

Open P0/P1/P2 code Finding은 `0`이다. 두 P3는 Product Roadmap의 명시적 backlog에 연결한다.

### Pre-traffic operational gate

| ID | 상태 | Public 활성화 전 필수 검증 |
| --- | --- | --- |
| `AZURE-RESTORE-001` | `OPEN` | 실제 PITR restore가 60분 안에 완료되고 migration ledger·aggregate가 일치해야 함 |
| `AZURE-EDGE-AUTH-001` | `OPEN` | DNS·managed TLS·Entra callback·Front Door 200·origin 403을 실제 runtime에서 확인해야 함 |
| `AZURE-PROVIDER-001` | `OPEN` | Teams·Gmail을 각각 1건 actual smoke로 확인해야 함 |

이 세 Gate는 Change 002에 따라 Git merge를 막지 않지만 모두 PASS 전에 public traffic과 external notification 활성화를 금지한다.

## 11. Rollback과 복구

1. migration 또는 restore rehearsal 실패 시 workload를 inactive 상태로 유지하고 edge를 생성하지 않는다.
2. Frontend/API smoke 실패 시 새 image digest로 forward-fix하고 기존 digest를 보존한다.
3. Edge 전환 뒤 문제가 생기면 Front Door route를 비활성화하고 origin workload를 scale 0으로 내린다.
4. 데이터 손상이 의심되면 마지막 검증된 PITR restore server로 전환하며 원본 server는 즉시 삭제하지 않는다.
5. 시범 중단 시 비용 resource 삭제는 사용자가 usage, backup과 보존 정책을 확인한 뒤 수행한다.

## 12. 사용자 검수 결과와 남은 항목

- Checklist: `작성됨`
- 자동 검증: `완료`
- 사용자 검수: `Change 003 완료 — 2026-08-02`
- Azure resource와 public URL: `없음`
- 다음 Gate: Change 003 commit·게시·merge 뒤 사용자가 예상 비용, 무료 credit과 budget alert를 확인하고 Foundation을 생성한다.
- 비용 발생 전에 생성 대상, 사양, 예상 20일 비용과 삭제·rollback 영향을 다시 보고해야 한다.

## 13. 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/azure-deploy-001-implementation-report.md` | Change 003 반영 완료 |
| SOP | `tasks/azure-deploy-001-sop.md` | identity·DB role 분리 순서 반영 완료 |
| User manual | `infrastructure/azure-pilot/README.md` | identity 접근표·DB 역할 운영 절차 반영 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | Change 003 local 완료와 비용 Gate 반영 |
| User validation checklist | `tasks/azure-deploy-001-user-validation-checklist.md` | Change 003 사용자 검수 완료 / Azure 운영 검수 대기 |

## 14. Git와 게시 상태

- 변경사항: Change 003 검증본이 전용 Task branch에 미커밋 상태로 존재
- Commit: 미수행 — 사용자 요청 없음
- Push: 미수행 — 사용자 승인 완료
- PR: 미수행 — 사용자 승인 완료
- Merge: 미수행 — 사용자 승인 완료
- 실제 Azure 적용은 별도 비용 Gate와 사용자 직접 실행이 필요하다.
