# 20일 Azure 시범 배포

이 폴더는 비용이 발생하지 않는 로컬 준비와 사용자가 직접 실행할 Azure 배포 경계를 분리한다. 실제 hostname, email, tenant/client identifier, password, token과 connection string은 tracked 파일에 넣지 않는다.

## 구성

| 파일 | 역할 | 실행 순서 |
| --- | --- | ---: |
| `foundation.bicep` | VNet, Container Apps environment, PostgreSQL, ACR, Azure Files, Key Vault, Log Analytics, Application Insights, Front Door profile·endpoint·custom rate limit | 1 |
| `identity-access.bicep` | Backend·Frontend·migration·DB bootstrap identity가 필요한 Key Vault secret 하나씩만 읽도록 secret-scope RBAC 부여 | 3 |
| `workloads.bicep` | ClamAV, Backend, DB role bootstrap job, migration job, Frontend. 기본값은 `activateWorkloads=false`라 minimum replica가 0 | 5, 8 |
| `edge.bicep` | Front Door origin·custom domain·managed TLS·route·WAF association | 9 |
| `nginx.conf.template` | Front Door ID와 별도 origin verification header를 함께 검사하고 직접 origin 요청을 403으로 차단 | image build |
| `*.parameters.example.json` | 실제 값이 없는 입력 예시. `.local.json` 복사본만 실제 배포에 사용 | 배포 전 |

## 터미널 없는 웹 배포 준비물

각 Bicep 원본과 같은 폴더의 `foundation.json`, `identity-access.json`, `workloads.json`, `edge.json`은 Azure Portal의 **사용자 지정 템플릿 배포 → 편집기에서 사용자 고유의 템플릿 빌드 → 로드 파일**에서 사용하는 ARM JSON이다. 생성기 version과 template hash를 제외한 실제 template 구조가 Bicep 원본과 같은지는 `scripts/validate-azure-pilot-artifacts.sh --compile`이 검사한다.

Portal 업로드 순서는 다음과 같다.

1. `foundation.json`
2. Key Vault에 secret 8개 직접 입력
3. `identity-access.json`
4. GitHub Actions에서 Backend·Frontend image 게시
5. `workloads.json`을 `activateWorkloads=false`, `enableExternalNotifications=false`로 배포
6. DB role bootstrap, migration과 PITR restore 검증
7. `workloads.json`을 검증된 restore 시각과 `activateWorkloads=true`로 다시 배포
8. `edge.json`

ARM JSON에 실제 값을 직접 적어 다시 저장하지 않는다. Portal이 표시하는 parameter 입력란 또는 GitHub Environment secret을 사용한다. `검토 + 만들기`의 최종 `만들기`는 실제 Azure resource 또는 사용량을 만들 수 있으므로 사용자가 비용을 확인한 뒤 직접 누른다.

## GitHub 웹 화면에서 image 게시 준비

`.github/workflows/azure-pilot-images.yml`은 자동 실행되지 않는다. `main`에 포함된 full commit SHA와 명시적인 비용 확인을 입력하고 GitHub Environment 승인을 통과해야 Backend·Frontend image 두 개를 ACR에 게시한다. Azure client secret 대신 GitHub OIDC의 짧은 수명 token을 사용한다.

### 1. GitHub Environment

GitHub Repository의 **Settings → Environments → New environment**에서 `azure-pilot-image-publish`를 만든다.

- Required reviewers에 시범 배포 책임자를 지정한다.
- Deployment branches and tags는 `main`만 허용한다.
- 아래 값은 Repository secret이 아니라 이 Environment의 secret으로 등록한다.

| Environment secret | 의미 |
| --- | --- |
| `AZURE_CLIENT_ID` | image 게시 전용 Entra application client identifier |
| `AZURE_TENANT_ID` | Azure tenant identifier |
| `AZURE_SUBSCRIPTION_ID` | 시범 resource가 있는 subscription identifier |
| `AZURE_ACR_NAME` | Foundation이 만든 ACR resource 이름 |
| `AZURE_ACR_LOGIN_SERVER` | Foundation output의 ACR login server |
| `PMS_PUBLIC_HOSTNAME` | 최종 PMS hostname. `https://`는 제외 |
| `ENTRA_API_CLIENT_ID` | 운영 API app identifier |
| `ENTRA_SPA_CLIENT_ID` | 운영 SPA app identifier |
| `ENTRA_API_SCOPE` | `api://<API app identifier>/access_as_user` 형식의 scope |

실제 값은 issue, PR, commit, workflow input, screenshot과 문서에 넣지 않는다.

### 2. Azure Portal OIDC 신뢰

Azure Portal의 **Microsoft Entra ID → 앱 등록**에서 image 게시 전용 application을 만든다. 그 application의 **인증서 및 비밀 → 페더레이션된 자격 증명 추가**에서 GitHub Actions 시나리오를 선택한다.

- Organization: 이 Repository의 GitHub organization 또는 owner
- Repository: 이 Repository
- Entity type: `Environment`
- Environment: `azure-pilot-image-publish`
- Audience: Azure가 권장하는 기본 token exchange audience

Client secret은 만들지 않는다. ACR의 **액세스 제어(IAM) → 역할 할당 추가**에서 이 application의 service principal에 해당 ACR resource 범위의 `AcrPush`만 부여한다. Subscription 또는 resource group `Contributor`는 부여하지 않는다.

### 3. GitHub Actions 실행

Foundation과 ACR이 실제로 생성되고 비용 실행을 결정한 뒤에만 진행한다.

1. GitHub Repository의 **Actions**를 연다.
2. **Azure Pilot Images (Manual)**을 선택한다.
3. **Run workflow**에서 `main`을 선택한다.
4. `source_sha`에 `main`에 포함된 full 40자리 commit SHA를 입력한다.
5. ACR image 두 개가 게시되어 비용이 발생할 수 있음을 확인하는 checkbox를 선택한다.
6. **Run workflow**를 누른 뒤 Environment reviewer가 배포를 승인한다.
7. 완료된 run의 Summary에서 Backend·Frontend digest가 각각 `sha256:` 형식인지 확인한다.

Workflow는 입력 SHA가 `origin/main`의 조상이 아니면 Azure 로그인 전에 실패한다. `latest` tag를 만들지 않고 SHA tag만 push하며, 결과 digest를 `workloads.json`의 Backend·Frontend image parameter에 `registry/repository@sha256:...` 형식으로 사용한다. Workflow는 Container Apps를 배포하거나 활성화하지 않는다.

## 왜 네 단계인가

Foundation이 identity와 Key Vault를 만든 뒤 사용자가 secret을 입력하고, `identity-access.bicep`이 secret 하나 단위의 read 권한만 연결한다. 그 다음 inactive workload를 만든다. DB role bootstrap, migration job, PostgreSQL restore rehearsal과 Backend readiness가 확인되기 전에는 앱 minimum replica와 public route를 활성화하지 않는다. Migration이 실패해도 기존 application traffic이나 schema를 임의로 되돌리지 않고 additive forward-fix를 적용한다.

## 보안 경계

- Frontend만 external Container Apps ingress를 사용한다.
- Backend와 ClamAV ingress는 environment 내부 전용이다.
- PostgreSQL은 delegated subnet과 private DNS만 사용하며 public network access는 꺼진다.
- Front Door Standard가 `X-Azure-FDID`를 추가하고 rule set이 별도 origin verification token을 추가한다.
- Frontend Nginx는 두 값이 모두 맞지 않으면 health endpoint를 제외한 모든 요청을 403으로 차단한다.
- Frontend가 Backend 내부 ingress로 전달하는 HTTP Host를 수용하도록 Backend `AllowedHosts`는 public hostname과 managed environment에서 산출한 exact `backend.internal.<defaultDomain>`만 허용한다. wildcard는 사용하지 않는다.
- Backend는 Container Apps infrastructure subnet CIDR만 trusted proxy network로 사용한다.
- Key Vault secret은 tracked template이 만들지 않는다. 사용자가 Portal에서 직접 입력한 뒤 workload별 identity가 자기 secret resource만 읽는다. Key Vault 전체 범위 secret read는 없다.
- Backend, Frontend, migration과 DB bootstrap은 서로 다른 user-assigned managed identity를 사용한다.
- ACR admin user와 anonymous pull은 꺼지고 네 managed identity의 개별 `AcrPull`만 허용한다.
- Backend는 `pms_app`, migration job은 `pms_migrator`로 접속한다. 관리자 연결은 public ingress가 없는 수동 DB bootstrap job에서만 사용한다.
- `pms_app`은 업무 table CRUD, sequence 사용, trigger/function 실행과 migration ledger 조회만 가능하다. database/schema/role 생성, 임시 table, migration ledger 변경은 허용하지 않는다.

## Key Vault에 사용자가 넣을 secret 이름

| 이름 | 값 |
| --- | --- |
| `database-admin-connection-string` | Foundation의 PostgreSQL 관리자 연결. DB role bootstrap job만 읽음 |
| `database-migration-connection-string` | `Username=pms_migrator`, 별도 32자 이상 password, `SSL Mode=VerifyFull` |
| `database-runtime-connection-string` | `Username=pms_app`, migration과 다른 32자 이상 password, `SSL Mode=VerifyFull` |
| `bootstrap-administrator-emails` | 비상 관리자 두 명의 email을 세미콜론으로 구분 |
| `front-door-origin-verify-token` | Foundation secure parameter와 동일한 64자 이상 random 값 |
| `gmail-username` | 발송 계정 |
| `gmail-app-password` | Gmail app password |
| `teams-activity-client-secret` | Teams activity용 Entra application secret |

세 DB 연결은 같은 host, port와 database를 가리켜야 하고 세 username·password는 서로 달라야 한다. Secret 원문은 CLI 인자, shell history, deployment output, screenshot과 문서에 남기지 않는다. Portal의 Key Vault secret 입력 화면을 사용하고, 만료일을 설정한다.

## Managed identity와 secret 접근표

| Identity | 허용 secret |
| --- | --- |
| Backend | runtime DB, 비상 관리자 목록, Gmail 계정·app password, Teams activity client secret |
| Frontend | Front Door origin verification token |
| Migration | migration DB |
| Database bootstrap | admin DB, migration DB, runtime DB |

`identity-access.bicep`은 위 10개 조합을 secret resource scope로만 만든다. vault scope의 `Key Vault Secrets User`는 금지한다.

이전 단일 `runtime` identity 배포를 실제 Azure에 한 적이 있다면 incremental Bicep은 삭제된 role assignment를 자동 제거하지 않는다. Key Vault의 **액세스 제어(IAM)**에서 그 identity의 vault-scope `Key Vault Secrets User`를 먼저 제거하고, 더 이상 workload가 참조하지 않는 것을 확인한 뒤 기존 identity도 정리한다. vault-scope workload assignment가 0이 아니면 migration·serving workload를 실행하지 않는다.

## 비용 발생 전 반드시 중단하는 지점

다음은 실제 Azure resource 또는 사용량을 만들 수 있으므로 Codex가 자동 실행하지 않는다.

1. Resource provider 등록과 resource group 생성
2. Budget alert 생성
3. `foundation.bicep` deployment
4. ACR image push
5. `identity-access.bicep`와 `workloads.bicep` deployment, DB role bootstrap·migration job 실행
6. PostgreSQL point-in-time restore rehearsal
7. `edge.bicep`, DNS, managed TLS와 traffic 활성화
8. Teams·Gmail actual smoke

사용자는 위 단계 전에 이번 배포의 서비스·사양·20일 예상 비용을 다시 확인한다.

## 비용 없는 로컬 검증

```bash
scripts/validate-azure-pilot-artifacts.sh
scripts/test-teams-manifest-package.sh
dotnet test backend/tests/Emi.Qms.Api.Tests/Emi.Qms.Api.Tests.csproj -c Release --filter FullyQualifiedName~PublicDeploymentSecurityTests
```

Bicep compiler가 준비된 환경에서는 다음을 추가한다.

```bash
scripts/validate-azure-pilot-artifacts.sh --compile
```

## 실제 배포 순서

1. 사용자가 budget 알림을 먼저 만든다.
2. Foundation local parameter 파일을 만들고 Azure resource를 생성한다.
3. Key Vault에 위 8개 secret을 직접 입력한다.
4. `identity-access.bicep`을 적용하고 10개 role assignment가 secret scope인지 확인한다. RBAC 전파가 끝나기 전에는 다음 단계로 가지 않는다.
5. 같은 Git commit에서 Backend·Frontend image를 build하고 ACR에 push한 뒤 digest를 고정한다.
6. `activateWorkloads=false`, `enableExternalNotifications=false`로 workload와 두 manual job을 배치한다.
7. `database-role-bootstrap` job을 한 번 실행해 `pms_migrator`와 `pms_app`을 만들고 권한 probe를 통과시킨다.
8. migration job을 한 번 실행하고 migration ledger가 Exact인지 확인한다. 이 job이 신규 DB object의 runtime 권한도 재조정한다.
9. PostgreSQL PITR restore rehearsal을 수행하고 1시간 안에 복구·연결·ledger 검증이 되는지 확인한다. 임시 restore server는 사용자 비용 경계에서 정리한다.
10. 성공 시각을 `restoreVerifiedAtUtc`에 넣고 `activateWorkloads=true`로 workload를 다시 배치한다.
11. Backend `/health/ready`가 성공한 뒤 edge를 배치하고 DNS TXT/CNAME, managed TLS를 확인한다. 공개 API가 `400`이면 Backend latest revision의 `AllowedHosts`가 public hostname과 exact internal Backend hostname 두 개를 포함하는지 확인한다.
12. Front Door 주소는 200, Frontend Container App 원본 주소는 403인지 확인한다.
13. Entra redirect URI와 Teams manifest를 최종 주소로 갱신한다.
14. 실제 provider smoke가 성공한 뒤에만 `enableExternalNotifications=true`로 바꾼다.

## Rollback

- Edge 이상: Front Door route를 비활성화하고 직접 origin은 계속 403으로 유지한다.
- Application 이상: 직전 검증 image digest로 `workloads.bicep`을 다시 적용한다.
- DB role bootstrap 이상: serving workload와 migration을 시작하지 않고 세 connection string의 endpoint·username 분리와 secret-scope RBAC를 수정한 뒤 bootstrap job을 재실행한다.
- Migration 실패: application을 활성화하지 않고 additive migration으로 forward-fix한다.
- Data 이상: 검증된 restore point에서 새 PostgreSQL server를 만든 뒤 aggregate·ledger 검증 후 connection secret을 새 version으로 교체한다.
- 외부 알림 이상: `enableExternalNotifications=false`로 되돌려 in-app 알림만 유지한다.

Resource 삭제, PITR server 삭제와 scale 변경도 비용·복구에 영향을 주므로 사용자가 직접 수행한다.
