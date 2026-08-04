# TASK-AZURE-DEPLOY-001 SOP — 20일 시범 배포

## 1. 현재 판정

- Deployment source: Change 006 Teams·PWA 브랜드 자산 PR #68 원격 main 병합·CI 완료
- Portal ARM JSON 4개: 실제 Foundation·identity-access·inactive/active workload 배포에 사용
- GitHub 웹 수동 image 게시 workflow: Backend·Frontend 최초 image 게시 완료
- Azure resource: Foundation·secret-scope RBAC·workload·DB 생성 완료
- DB role bootstrap·migration: 기존 Azure `67 Exact` / 최신 main `0068` handover 대기
- PITR restore rehearsal: 60분 목표 이내 성공 / 임시 restore resource 정리 완료
- Active workload: Change 005 image·revision ready / 최신 main `496b88b` image 게시 승인·revision 적용 대기
- Teams·PWA: 제공 EMI 원본 기반 Teams `1.0.2` package와 PWA manifest·icon 생성 완료 / Azure image·catalog 반영 대기
- 공개 traffic: 전환 안 함
- 실제 Teams·Gmail: 발송 안 함
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
3. 기존 외부 package version은 `1.0.1`이며 신규 브랜드·유효 JSON package는 `1.0.2`다. Teams Admin Center update는 public Frontend와 TLS 검증 뒤 수행한다.
4. PWA manifest와 icon은 Frontend image의 Vite build output에 자동 포함된다. 별도 파일 업로드는 하지 않는다.
5. PWA는 standalone install metadata만 제공하며 Service Worker·offline cache는 현재 범위에 없다.
6. 최종 hostname에서 manifest·icon HTTP `200`, install name·icon·standalone launch를 확인한 뒤 완료로 기록한다.

## 2.2 터미널 없는 웹 실행 Gate

사용자가 Terminal 또는 Cloud Shell을 사용하지 않는 경우 다음 두 웹 화면만 사용한다.

1. Azure Portal `사용자 지정 템플릿 배포 → 편집기 → 로드 파일`
2. GitHub `Actions → Azure Pilot Images (Manual) → Run workflow`

Portal에는 `foundation.json → identity-access.json → workloads.json → edge.json` 순으로 업로드한다. 각 JSON은 같은 이름의 Bicep 원본에서 생성되며 generator metadata를 제외한 구조 동등성을 자동 검사한다.

GitHub image 게시 전 `azure-pilot-image-publish` Environment, `main` branch 제한, Environment secret, federated credential과 ACR resource 범위 `AcrPush`를 구성했다. Client secret과 subscription/resource group `Contributor`는 사용하지 않는다.

Workflow는 full 40자리 source SHA가 `origin/main`에 포함됐는지 검증하고, 비용 확인 checkbox가 선택된 경우에만 Azure OIDC login과 ACR push를 실행한다. Backend·Frontend는 source SHA tag와 digest만 사용하고 `latest` tag를 만들지 않는다. Workflow 자체는 Container Apps deployment, revision activation과 traffic 전환을 수행하지 않는다.

최초 ARM JSON 배포와 GitHub Actions image 게시 실행은 사용자 확인 아래 완료됐다. 후속 Change는 동일한 비용·공개 traffic Gate와 검증된 `main` source 계약을 유지한다.

## 3. 사용자 입력값

실제 값은 tracked 문서가 아니라 Portal과 ignored `*.local.json`에만 둔다.

- 최종 public hostname
- Entra tenant, API client, SPA client, API audience와 verified domain
- Teams activity client, catalog app, manifest external identifier
- 비상 관리자 두 명
- 경보 수신자 alias
- PostgreSQL 관리자 password
- `pms_migrator` 전용 32자 이상 password
- `pms_app` 전용 32자 이상 password
- Front Door origin random token
- Gmail username/app password
- Teams activity client secret

## 4. 배포 Gate

Git에 검증된 배포 코드를 먼저 merge한다. 이 merge는 Azure resource 생성이나 public traffic 활성화를 의미하지 않는다.

1. Foundation 생성과 Key Vault 8개 secret 입력
2. Backend·Frontend·migration·DB bootstrap identity의 secret-scope RBAC 10개 적용 및 전파 확인. 이전 단일 runtime identity를 배포한 적이 있으면 incremental deployment에 남은 vault-scope role assignment를 Portal에서 제거하고 0건을 확인
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

한 단계가 실패하면 다음 단계로 넘어가지 않는다.

Active workload에서 Backend probe `400` 또는 Frontend Nginx map hash 시작 실패가 확인되면 Change 005가 반영된 `main` image·ARM template인지 먼저 확인한다. Backend 세 HTTP probe는 public Host header를 사용해야 하며 Frontend template은 64자 origin token을 수용하는 map hash bucket을 가져야 한다. 이 두 조건과 세 replica readiness가 모두 확인되기 전에는 Edge를 배포하지 않는다.

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
