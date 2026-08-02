# TASK-AZURE-DEPLOY-001 Change 003 — Azure 신원·PostgreSQL 실행 역할 최소 권한 분리

## 승인과 분류

- taskType: `SECURITY_HARDENING`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- taskIdentityGate: `PASS_REUSE`
- canonicalTask: `TASK-AZURE-DEPLOY-001`
- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_RESOLVE_ALL_P1`
- 승인일: 2026-08-02
- 기준 SHA: `4d15b7cee0d97f1846a1838500f9c9edf11b68bf`
- 작업 branch: `fix/task-azure-deploy-001-p1-hardening`

## Root Finding

### `AZURE-IDENTITY-001` (P1)

Frontend, Backend, migration job이 하나의 user-assigned managed identity를 공유하고 그 identity가 Key Vault 전체 범위의 `Key Vault Secrets User`를 가진다. 공개 Frontend 또는 한 workload가 침해되면 해당 workload에 필요하지 않은 DB·Gmail·Teams secret까지 읽을 수 있어 blast radius가 불필요하게 크다.

### `AZURE-DB-ROLE-001` (P1)

Azure 배포 절차는 PostgreSQL 관리자 연결 문자열 하나만 수집하고 application과 migration이 같은 DB identity를 사용한다. 공개 API가 schema 변경·role 관리가 가능한 계정으로 실행될 수 있어 application compromise가 schema와 migration ledger 손상으로 확대될 수 있다.

## 승인된 수정 범위

1. Backend, Frontend, migration, database bootstrap을 서로 다른 user-assigned managed identity로 분리한다.
2. ACR pull은 각 workload identity에 개별 부여한다.
3. Key Vault 전체 범위 secret read를 제거하고 각 secret resource 범위에만 `Key Vault Secrets User`를 부여하는 별도 post-secret 배포 단계를 추가한다.
   - 기존 단일 runtime identity를 실제 배포한 이력이 있으면 incremental deployment가 과거 role assignment를 삭제하지 않으므로 운영 절차에서 명시적으로 제거하고 0건을 검증한다.
4. PostgreSQL 연결을 관리자, migration(`pms_migrator`), runtime(`pms_app`) 3종으로 분리한다.
5. 수동 database bootstrap job이 두 제한 role을 idempotent하게 만들고 DB·schema·기존/default object privilege를 최소 권한으로 설정한다.
6. migration job은 `pms_migrator`로만 schema를 변경하고 완료 후 `pms_app`의 기존 object 권한을 재조정한다.
7. Backend는 `pms_app` 연결만 읽고 schema 생성, migration ledger 변경, role·database 관리 권한을 갖지 않는다.
8. 배포 순서를 Foundation → secret 입력 → secret-scope RBAC → DB role bootstrap → migration → restore → serving workload → edge로 보정한다.

## 보존할 불변조건

- Frontend만 public Container Apps ingress를 사용한다.
- Backend와 ClamAV는 internal ingress를 유지한다.
- PostgreSQL public network access는 계속 `Disabled`이고 TLS `VerifyFull`을 강제한다.
- Front Door ID와 별도 origin token의 이중 검증을 유지한다.
- 업로드 malware scan은 fail-closed를 유지한다.
- migration은 manual one-shot, advisory lock, transaction과 Exact ledger 검증을 유지한다.
- 실제 hostname, email, tenant/client identifier, password, token과 connection string은 tracked 파일에 기록하지 않는다.
- Azure resource 생성, role assignment 적용, DB role 생성, migration 실행, traffic·provider 활성화는 이 local 변경에 포함하지 않는다.

## 완료 기준과 검증

- Bicep compile과 static invariant 검증이 네 배포 template 모두 통과한다.
- vault-scope `Key Vault Secrets User` assignment가 0이고 secret-scope assignment만 존재한다.
- 각 workload가 자기 identity와 필요한 secret만 참조함을 자동 test가 확인한다.
- PostgreSQL 통합 test에서 runtime role은 업무 CRUD와 migration ledger 조회만 가능하고 schema 생성·ledger 변경·role 생성은 거부된다.
- migration role은 전체 migration과 Exact ledger 검증을 완료하며 runtime object grant를 재조정한다.
- Backend 집중 보안 test와 전체 회귀가 통과한다.
- current diff와 tracked/history secret scan에서 실제 secret이 0이다.
- Open P0/P1/P2가 0일 때만 게시 가능 판정을 `GO`로 바꾼다. 실제 Azure 적용과 public traffic은 기존 비용·pre-traffic gate를 계속 따른다.
