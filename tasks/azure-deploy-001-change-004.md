# TASK-AZURE-DEPLOY-001 Change 004 — Portal ARM JSON과 수동 GitHub 이미지 게시

## 승인과 분류

- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- taskIdentityGate: `PASS_REUSE`
- canonicalTask: `TASK-AZURE-DEPLOY-001`
- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_WEB_ONLY_DEPLOYMENT_PREPARATION`
- 승인일: 2026-08-02
- 사용자 검수: `완료 — 2026-08-02`
- Git 게시·main 병합 승인: `USER_EXPLICIT_MERGE_APPROVAL — 2026-08-02`
- 기준 SHA: `5ce3957382a4e8fee19a3619f46e4d5b638c8e98`
- 작업 branch: `feat/task-azure-deploy-001-web-release`

## Root Finding

### `AZURE-PORTAL-ARTIFACT-001`

현재 Azure 배포 원본은 Bicep 4개뿐이다. Azure Portal의 사용자 지정 템플릿 편집기는 ARM JSON 업로드를 기준으로 하므로 사용자가 터미널 없이 `로드 파일`로 진행할 고정 산출물이 없다.

### `AZURE-WEB-IMAGE-PUBLISH-001`

현재 GitHub Actions에는 일반 CI만 있고, 승인된 `main` commit의 Backend·Frontend Production image를 ACR에 게시할 수동 workflow가 없다. 사용자가 터미널 없이 배포하려면 장기 Azure credential 없이 웹에서 실행할 수 있는 별도 게시 경로가 필요하다.

## 승인된 수정 범위

1. `foundation`, `identity-access`, `workloads`, `edge` Bicep과 generator metadata를 제외한 구조적 동등성을 검증할 추적 ARM JSON 4개를 생성한다.
2. ARM JSON을 Azure Portal `사용자 지정 템플릿 배포 → 편집기 → 로드 파일`에서 사용할 수 있게 문서화한다.
3. GitHub `workflow_dispatch`에서만 실행되는 Azure pilot image 게시 workflow를 추가한다.
4. GitHub OIDC와 Azure workload identity federation을 사용하며 장기 Azure client secret을 만들지 않는다.
5. source SHA는 40자리 commit이고 `origin/main`에 포함된 경우만 허용한다.
6. image push 전에 사용자가 boolean 확인 입력을 선택해야 하며, GitHub Environment protection을 적용한다.
7. Backend·Frontend image는 mutable `latest` 없이 source SHA tag로 ACR에 push하고 digest를 결과로 남긴다.
8. 실제 Azure identifier·hostname은 GitHub Environment secret에서만 읽고 로그·tracked 파일에 원문을 남기지 않는다.
9. workflow action은 full commit SHA로 pin하고 권한은 `contents: read`, `id-token: write`로 제한한다.
10. Bicep/ARM drift, workflow trigger·permission·secret 경계와 negative path를 로컬에서 검증한다.

## 제외 범위

- Azure resource provider 등록, resource group·Foundation·RBAC·workload·edge 생성
- Azure OIDC application 또는 federated credential의 실제 생성
- ACR role assignment와 실제 Azure 로그인
- ACR image push와 Container Apps revision 변경
- Key Vault secret 입력, database bootstrap, migration, PITR restore
- DNS, TLS, public traffic, Teams·Gmail actual 발송
- Git commit, push, PR와 merge

## 보존할 불변조건

- 실제 Azure 생성과 비용 발생 action은 사용자가 웹 화면에서 직접 실행한다.
- Frontend만 public ingress이고 Backend·ClamAV는 internal ingress를 유지한다.
- Front Door ID와 별도 origin token의 이중 검증을 유지한다.
- Key Vault vault-scope workload secret read는 0이며 secret resource scope만 허용한다.
- Backend는 `pms_app`, migration은 `pms_migrator`, bootstrap만 관리자 연결을 사용한다.
- migration Exact와 PITR restore Gate 전에는 active workload·edge·external notification을 허용하지 않는다.
- public repository build provenance에 Frontend build argument 원문을 포함하지 않는다.
- 실제 tenant/client/object ID, hostname, email, token, password와 connection string은 tracked 파일과 검증 출력에 기록하지 않는다.

## 변경 Allowlist

- `.github/workflows/azure-pilot-images.yml`
- `infrastructure/azure-pilot/foundation.json`
- `infrastructure/azure-pilot/identity-access.json`
- `infrastructure/azure-pilot/workloads.json`
- `infrastructure/azure-pilot/edge.json`
- `infrastructure/azure-pilot/README.md`
- `scripts/validate-azure-pilot-artifacts.sh`
- `scripts/validate-azure-image-publish-inputs.sh`
- `scripts/test-azure-image-publish-inputs.sh`
- `tasks/azure-deploy-001-change-004.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 완료 기준과 검증

- Bicep 4개를 다시 빌드한 결과가 compiler version·template hash metadata를 제외하고 tracked ARM JSON 4개와 구조적으로 동일하다.
- ARM JSON 4개가 JSON parse와 synthetic parameter contract를 통과하고 secret 원문을 포함하지 않는다.
- workflow는 `workflow_dispatch` 외 trigger가 없고 Environment gate, explicit push confirmation, main ancestry guard와 concurrency를 가진다.
- Azure login은 OIDC만 사용하고 client secret 입력이 없으며 Azure CLI 기본 출력을 끈다.
- image tag는 검증된 40자리 source SHA이고 digest output을 생성한다.
- Frontend build provenance는 `mode=min`으로 제한해 build argument 원문을 포함하지 않는다.
- shell syntax, actionlint, Git whitespace, secret/PII와 changed-file allowlist 검증이 통과한다.
- 실제 Azure API call, resource mutation과 image push가 0이다.
