# TASK-AZURE-DEPLOY-001 SOP — 20일 시범 배포

## 1. 현재 판정

- Deployment source: Change 026 PR #108 원격 `main` 병합·CI 완료 / merge SHA `51aba7e97a2d1fee0f9ee4b82a3f89d514171acf` / 운영 release run `32197298425` 성공
- Portal ARM JSON 4개: 실제 Foundation·identity-access·inactive/active workload 배포에 사용
- GitHub 웹 수동 image 게시 workflow: Change 013이 포함된 최종 main Backend·Frontend immutable image 게시 완료
- Azure resource: Foundation·secret-scope RBAC·workload·DB 생성 완료
- DB role bootstrap·migration: 기존 migration 기준선 유지. Change 026은 migration diff가 없어 실행 생략
- PITR restore rehearsal: 60분 목표 이내 성공 / 임시 restore resource 정리 완료
- Active workload: Backend·Frontend latest revision ready·Running / exact Change 026 main image digest 적용 / ClamAV unchanged
- Teams·PWA: 제공 EMI 원본 기반 PWA와 Web Push 운영 반영 완료 / 실제 iPhone·Android PWA 수신·알림 상세 이동 확인 / 직원 설치·알림 허용은 자율 / 공개 Teams `1.0.4` 관리자 승인·사용자 설치 보고 완료 / synthetic actual Activity Graph `204`·Teams web 표시 / Change 017 worker actual 활성화·최신 Teams Activity `6/6 Sent`
- DNS·Front Door: domain validation·deployment·provisioning 완료 / managed certificate·TLS 1.2·hostname 검증 완료 / 공개 root·PWA `200`, direct origin 업무 route `403`
- 공개 traffic: HTTP→HTTPS, 익명 비브라우저 root·asset·PWA·API `401`, 브라우저는 PMS shell·bundle 없는 Easy Auth 인증 화면, `/health/live` `200` / Dispatcher·Teams Activity·Mail·Web Push actual 활성화
- 로그인·권한: 현재 비상 관리자 계정의 Entra 로그인과 관리자 전용 메뉴·사용자 관리 화면 접근 확인. bootstrap 목록 순서는 권한 우선순위가 아니므로 secret 재정렬은 하지 않음
- Frontend 사전 인증: Change 015 운영 적용·실제 계정 로그인 검수 완료. `/health/live` 외 shell·bundle·PWA·API proxy는 Entra 인증 전 제공하지 않음
- 실제 Teams·Gmail: 최신 수동 Teams Activity `6/6 Sent`, Mail `3/3 Sent`, Open Pending/Processing/Failed `0` / 사용자 client·메일함 실제 수신 확인 완료
- 비용 발생 단계: 사용자 승인·시작 완료

## 2. 비용 Gate

사용자는 Azure Portal에서 아래 항목을 확인한 뒤 실제 생성 여부를 결정한다.

| 서비스 | 시범 사양 | 필요한 이유 |
| --- | --- | --- |
| Front Door Standard | 1 profile, custom rate limit | 최종 hostname·managed TLS·origin 보호와 단일 진입점 |
| Container Apps | Consumption, Front 0.25/0.5, API 1/2, ClamAV 2/4 | 세 프로젝트의 작은 사용량에서 idle 비용을 줄이고 ClamAV 메모리를 보장 |
| PostgreSQL | Burstable B2s, 32 GB, PITR 14일, HA 없음 | 시범 입력량에는 충분하고 1시간 복구 rehearsal로 HA 비용을 보류 |
| ACR | Basic | Backend·Frontend image digest 보관 |
| Azure Files | 5 GB | ClamAV signature 재시작 보존 |
| Key Vault | Standard | DB·Gmail·Teams secret 비추적 주입 |
| Log Analytics | 1 GB/day cap, 30일 | Container와 보안 로그 수집 비용 상한 |
| Application Insights | workspace based | 추후 application telemetry 연결 지점 |

20일 예상 비용과 무료 credit 잔액 확인, Budget 알림 설정은 완료됐다. 시범 기간에는 실제 사용량을 매일 확인한다.

## 2.1 Teams·PWA 브랜드 handover

1. Teams package는 Repository의 `infrastructure/teams/assets/color.png`와 `outline.png`를 사용한다.
2. 실제 package 생성 시 최종 hostname, 기존 Teams manifest ID, activity client ID·resource와 증가된 SemVer를 builder argument로만 전달한다. 실제 identifier는 tracked 파일이나 보고에 기록하지 않는다.
3. 공개 운영 package는 v1.19 schema와 10개 Activity type을 통과한 `1.0.4`다. 사용자가 Teams Admin Center 승인 요청을 제출했으며 승인 뒤 조직 catalog·설치·actual Activity를 검수한다.
4. PWA manifest와 icon은 Frontend image의 Vite build output에 자동 포함된다. 별도 파일 업로드는 하지 않는다.
5. PWA Service Worker는 알림 수신과 알림 선택 이동만 담당한다. offline cache·background sync는 제공하지 않는다.
6. 최종 hostname에서 manifest·icon HTTP `200`, install name·icon·standalone launch와 iPhone·Android 실제 푸시 수신을 확인했다.
7. 직원별 PWA 설치와 알림 허용은 사용자가 직접 선택한다. 미설치·미허용 사용자는 인앱 알림을 계속 사용하며, 나중에 허용하면 그 이후 새 인앱 알림부터 PWA로 받는다.

## 2.2 터미널 없는 웹 실행 Gate

사용자가 Terminal 또는 Cloud Shell을 사용하지 않는 경우 다음 두 웹 화면만 사용한다.

1. Azure Portal `사용자 지정 템플릿 배포 → 편집기 → 로드 파일`
2. GitHub `Actions → Azure Pilot Release (Manual) → Run workflow`

Portal에는 `foundation.json → identity-access.json → workloads.json → edge.json` 순으로 업로드한다. 각 JSON은 같은 이름의 Bicep 원본에서 생성되며 generator metadata를 제외한 구조 동등성을 자동 검사한다.

현재 운영 identity-access 재배포는 기존 Frontend access-gate와 Web Push 두 secret role assignment 이름을 비추적 local parameter로 전달해 수동 생성 역할을 인수한다. 역할을 먼저 삭제하지 않으며 `what-if`에서 role assignment Create/Delete `0/0`을 확인한 뒤 실행한다. 새 환경은 세 선택 입력을 빈 값으로 두어 결정적 이름을 생성한다.

GitHub 운영 release 전 `azure-pilot-image-publish` Environment, `main` branch 제한, Environment secret·비식별 resource variable, federated credential을 구성한다. OIDC service principal에는 ACR 한 개의 `AcrPush`, Backend·Frontend 각 한 개의 `Container Apps Contributor`, migration job 한 개의 `Container Apps Jobs Contributor`만 exact resource 범위로 부여한다. Client secret과 subscription/resource group 범위 `Contributor`는 사용하지 않는다.

현재 public Repository에서도 `azure-pilot-image-publish` Environment 승인 Gate를 유지한다. Workflow는 작업자가 `main`을 선택하고 실행 시점 최신 full SHA, image 게시 확인과 운영 migration·앱 교체 확인을 모두 제출하고 Environment 승인을 통과한 경우에만 Azure OIDC login을 시작한다. Backend·Frontend는 source SHA tag와 digest만 사용하고 `latest` tag를 만들지 않는다.

Image 게시 뒤에는 현재 Backend·Frontend가 single revision이고 migration job이 manual인지 확인한다. 현재 앱과 공개 보안 기준선이 정상일 때 migration을 먼저 실행하며, 성공 전에는 앱을 변경하지 않는다. 이후 Backend, Frontend 순서로 digest를 교체하고 각 latest revision의 exact `Healthy`와 `Running` 또는 `RunningAtMaxScale`, 공개 health·익명 인증 차단을 확인한다. `Stopped`, `ScaleToZero`, `Degraded`, `Unknown`과 빈 값은 준비되지 않은 상태로 차단한다. 앱 또는 공개 검사 실패 시 직전 image로 rollback을 시도하며 migration 자체는 additive forward-fix 원칙을 유지한다.

최초 ARM JSON 배포와 GitHub Actions image 게시 실행은 사용자 확인 아래 완료됐다. Change 018 source 게시는 workflow를 사용할 수 있게 할 뿐 실제 운영 release를 자동 실행하지 않는다. 첫 실제 run은 정상 `RunningAtMaxScale` 판정 누락으로 mutation 전에 안전 중단됐다. Change 019를 원격 `main`에 게시한 뒤 최신 main full SHA로 다시 실행해 migration·Backend·Frontend 교체와 공개 보안 검사를 모두 통과했다.

## 3. 사용자 입력값

실제 값은 tracked 문서가 아니라 Portal과 ignored `*.local.json`에만 둔다.

- 최종 public hostname
- Entra tenant, API client, SPA client, API audience와 verified domain
- Frontend 사전 인증 전용 Entra web client와 Key Vault client secret
- Teams activity client, catalog app, manifest external identifier
- 비상 관리자 두 명
- 경보 수신자 alias
- PostgreSQL 관리자 password
- `pms_migrator` 전용 32자 이상 password
- `pms_app` 전용 32자 이상 password
- Front Door origin random token
- Gmail username/app password
- Teams activity client secret
- Web Push VAPID public/private key

## 4. 배포 Gate

Git에 검증된 배포 코드를 먼저 merge한다. 이 merge는 Azure resource 생성이나 public traffic 활성화를 의미하지 않는다.

1. Foundation 생성과 Key Vault 9개 secret 입력
2. Backend·Frontend·migration·DB bootstrap identity의 secret-scope RBAC 11개 적용 및 전파 확인. 이전 단일 runtime identity를 배포한 적이 있으면 incremental deployment에 남은 vault-scope role assignment를 Portal에서 제거하고 0건을 확인
3. image digest 고정
4. Inactive workload와 두 manual job 생성
5. DB role bootstrap job 성공: `pms_migrator`·`pms_app` 생성과 최소 권한 probe
6. migration job Exact와 runtime object privilege 재조정
7. PITR restore 60분 이내와 aggregate·ledger 검증
8. Active workload readiness
9. DNS·TLS·Front Door origin 차단
10. Entra login과 관리자 승인 workflow
11. Teams manifest 조직 catalog update와 개인 설치
12. actual Teams·Gmail smoke

Teams smoke는 운영 Dispatcher를 켜지 않고 `scripts/smoke-azure-teams-activity.sh`의 명시 confirmation guard로 실행한다. 현재 Azure 로그인 사용자가 bootstrap 관리자와 일치하고 Dispatcher·Teams Activity·Mail이 기존 disabled/dry-run 상태일 때만 동일 운영 credential로 synthetic `generalNotification` 1건을 Graph에 직접 보낸다. 호출은 재시도 없이 1회다. HTTP `204` 뒤에는 `--inspect-installation`으로만 개인 설치 목록을 읽기 전용 진단할 수 있으며 권한 부족이면 권한을 자동 추가하지 않는다. Teams web과 desktop client 표시는 각각 확인하고, desktop에만 보이지 않으면 먼저 Activity 화면 재진입·새로고침·완전 종료 후 재실행으로 client 동기화를 확인한다.

Change 017 운영 활성화는 `scripts/activate-azure-external-notifications.sh --preflight`로 단일 Backend·provider secret·Gmail SMTP/TLS·안전 기준선을 확인한 뒤 명시 confirmation flag로만 실행한다. 활성화 뒤 latest revision Ready와 Open Pending/Processing/Failed `0`을 확인한다. Provider 장애 시 전체 또는 해당 채널을 disabled/dry-run으로 되돌리되 이미 `Sent`인 외부 메시지는 취소하지 않는다. 관리자 `Dismissed` delivery는 worker가 자동 해제하지 않는다.

Change 015 운영 사전 인증은 single-tenant Entra web application, 공개 hostname의 `/.auth/login/aad/callback`, Key Vault `entra-access-gate-client-secret`과 Frontend secret-scope RBAC를 먼저 준비한다. `RedirectToLoginPage`, proxy convention `Standard`, HTTPS 필수와 `/health/live` 단일 제외 경로를 적용한다. 익명 비브라우저 root·asset·manifest·API가 `401`, 브라우저는 PMS shell·bundle 없는 인증 화면, health만 `200`인지 확인한 뒤 실제 계정 로그인을 검수한다. 문제가 생기면 auth platform을 비활성화하고 `AllowAnonymous`로 되돌려 기존 app-level 로그인으로 복구한다.

한 단계가 실패하면 다음 단계로 넘어가지 않는다.

Active workload에서 Backend probe `400` 또는 Frontend Nginx map hash 시작 실패가 확인되면 Change 005가 반영된 `main` image·ARM template인지 먼저 확인한다. Backend 세 HTTP probe는 public Host header를 사용해야 하며 Frontend template은 64자 origin token을 수용하는 map hash bucket을 가져야 한다. 이 두 조건과 세 replica readiness가 모두 확인되기 전에는 Edge를 배포하지 않는다.

Frontend proxy가 Backend 내부 FQDN을 HTTP Host로 전달하므로 Backend `AllowedHosts`에는 public hostname과 `backend.internal.<managed environment defaultDomain>` 두 exact host가 모두 필요하다. wildcard나 전체 environment domain을 허용하지 않는다. 공개 API가 `400`이면 Nginx route보다 먼저 latest Backend revision의 이 exact 2-host 계약을 확인하고, 안정 API version의 Container Apps REST update로 forward-fix한다.

DB 역할 검수에서는 Backend 연결 사용자가 `pms_app`, migration 연결 사용자가 `pms_migrator`인지 확인한다. `pms_app`은 업무 CRUD는 성공해야 하지만 `CREATE TABLE`, `CREATE ROLE`, `schema_migrations` INSERT와 database temporary privilege는 모두 거부돼야 한다. Key Vault 검수에서는 vault scope role assignment가 0이고 각 identity가 접근표에 없는 secret을 읽지 못해야 한다.

DB 복구, edge·인증과 actual provider smoke는 `PRE_TRAFFIC_GATE`다. Git merge 후에 실행하되, 세 Gate가 모두 PASS가 되기 전에는 public traffic과 external notification을 활성화하지 않는다.

## 5. 개인정보 안전 증빙

보고에는 다음 aggregate만 남긴다.

- resource 종류와 개수
- 배포 성공/실패
- image digest 존재 여부
- migration expected/applied count와 Exact 여부
- restore 소요시간과 aggregate 일치 여부
- HTTP status와 security header 존재 여부
- 로그인 성공 사용자 수, 승인 대기 사용자 수
- Teams·Gmail test 성공/실패

hostname, email, tenant/client identifier, token, secret, connection string, 실제 업무명과 첨부 원문은 기록하지 않는다.
