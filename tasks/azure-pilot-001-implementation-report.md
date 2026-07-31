# TASK-AZURE-PILOT-001 Implementation Report

## 1. 목적

회사 한 달 파일럿 공개 전에 특정 Azure 서비스를 선택하지 않아도 닫을 수 있는 P1을 해결한다. 대상은 GitHub 게시 후보 정리, Production Entra API·SPA 분리, 애플리케이션 기동과 분리된 database migration, 설정 사전점검이다.

Azure hosting, managed database, WAF, SIEM, domain과 실제 rollback 수단 선정은 사용자 결정에 따라 이번 Task에서 제외한다.

## 2. 승인과 기준선

- Task 유형: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- Gate: `PASS_CREATE`
- 구현 승인: 2026-07-31 사용자 명시 승인
- 범위 보정: 2026-07-31 사용자가 Azure 서비스 선정이 필요한 작업을 후속으로 보류
- 기준 SHA: `07b1507822fa5e07a9bb22631b40bf0877750843`
- 작업 branch: `feat/task-azure-pilot-001-operations`

## 3. 해결한 업무 문제

1. Production Compose가 하나의 Entra client identifier를 화면과 API에 함께 사용해 잘못된 app registration 구성을 통과시킬 수 있었다.
2. 운영 Backend는 startup migration을 끈 상태였지만 release 전에 실행할 독립 migration entry point와 동시 실행 방어선이 없었다.
3. 운영 설정을 실제 기동 전에 값 원문 없이 검사할 표준 preflight가 없었다.
4. 승인 완료 local `main`의 제품 commit이 GitHub `main`에는 아직 없어 파일럿 후보가 원격에서 추적되지 않았다.

## 4. 구현 내용

### Entra app 분리

- `ENTRA_API_CLIENT_ID`와 `ENTRA_SPA_CLIENT_ID`를 별도 필수값으로 사용한다.
- Frontend build는 tenant·API·SPA identifier, API scope, HTTPS redirect를 검증한다.
- API와 SPA identifier가 같거나 API scope가 API app과 일치하지 않으면 build/preflight가 실패한다.
- Backend Production startup도 별도 `AzureAd:SpaClientId`가 없거나 API client와 같으면 실패한다.
- legacy `ENTRA_CLIENT_ID`가 남아 있으면 preflight가 모호한 구성으로 거절한다.

### 독립 migration

- Production Backend image에 canonical migration 67개를 포함한다.
- `--migrate-only` 실행은 web listener와 background worker를 시작하지 않고 migration과 ledger 검증만 수행한다.
- PostgreSQL advisory lock으로 두 migration runner를 직렬화한다.
- 각 SQL file과 `schema_migrations` 기록을 같은 transaction에서 확정한다.
- 완료 후 migration catalog, ledger와 schema compatibility를 다시 검사한다.
- Production Compose의 `operations` profile에 one-shot `migration` service를 추가했다.

### 배포 전 사전점검

- env file을 shell source/eval 없이 읽는다.
- Entra API·SPA 분리, legacy identifier 부재, delegated scope와 public host를 검사한다.
- Production Compose가 확장되고 migration job이 존재하는지 확인한다.
- 선택적으로 TLS certificate hostname·만료·key 일치를 기존 validator로 확인한다.
- 출력은 boolean과 stable failure code만 사용하고 identifier·secret 원문을 출력하지 않는다.

## 5. 서비스 선정 보류 범위

다음 항목은 구현 실패가 아니라 사용자의 명시적 정책 보류다.

- Azure hosting product와 실행 architecture
- managed PostgreSQL product, network와 identity 방식
- public domain, certificate 발급·갱신 주체
- WAF, DDoS, rate-limit edge 배치
- SIEM/log 수집 product와 alert receiver
- container registry, GitHub Actions OIDC, provider release·rollback workflow
- 실제 backup/restore rehearsal과 traffic cutover

이 항목이 정해지기 전 공개 배포 판정은 `NO_GO_EXTERNAL`이다.

## 6. 수정 파일

- Backend: Production image, migration runner/entry point, Production security policy와 tests
- Frontend: Production image Entra split validation
- Infrastructure: Production Compose, env example과 운영 README
- Scripts: Production preflight, preflight negative tests, Production migration image fresh/existing apply test
- Governance: Task identity/change, 이 report, SOP, user manual, user validation checklist와 Product Roadmap

Migration SQL, application API, 업무 workflow, 권한, dependency와 lockfile은 변경하지 않았다.

## 7. 자동 검증

| 검증 | 적용 | 결과 | 근거 |
| --- | --- | --- | --- |
| Backend Release 전체 | 적용 | PASS | 469/469 |
| 동시 migration 집중 검증 | 적용 | PASS | 1/1, 두 runner ledger Exact |
| Production security 집중 검증 | 적용 | PASS | 30/30 |
| Frontend lint | 적용 | PASS_WITH_EXISTING_WARNING | error 0, Fast Refresh warning 1 |
| Frontend typecheck | 적용 | PASS | error 0 |
| Frontend unit | 적용 | PASS | 144/144 |
| Frontend production build | 적용 | PASS_WITH_EXISTING_WARNING | build 성공, large chunk warning |
| Mock UI | 적용 | PASS | 4/4 |
| Isolated Full-Stack E2E | 적용 | PASS | 55/55, cleanup 완료 |
| Production preflight | 적용 | PASS | 4/4 |
| Production migration image | 적용 | PASS | fresh apply, existing apply, 67 ledger Exact |
| Production image build | 적용 | PASS | Backend/Frontend ARM64·AMD64 |
| Production image vulnerability | 적용 | PASS | ARM64·AMD64 Backend/Frontend Critical 0, High 0 |
| Shell syntax/shellcheck | 적용 | PASS | 3개 신규 script |
| 실제 Azure runtime | 미적용 | N/A | 서비스 미선정으로 사용자 보류 |
| 실제 managed DB restore | 미적용 | N/A | DB 서비스 미선정으로 사용자 보류 |
| 사용자 검수 | 적용 | PENDING | 자동 검증과 분리 |

Full-Stack와 migration image 검증은 실행별 전용 PostgreSQL container/network/tmpfs를 사용했고 종료 후 모두 삭제했다. Persistent UAT와 기존 Development runtime은 변경하지 않았다.

## 8. Finding

| ID | 심각도 | 상태 | 원인·영향 | 해소 또는 후속 |
| --- | --- | --- | --- | --- |
| `OPS-PILOT-001` | P1 | OPEN | 승인 완료 local 제품 commit이 GitHub `main`에 아직 없어 원격 배포 기준선이 뒤처짐 | 이 branch를 Draft PR로 게시한다. 사용자 검수와 merge 승인 뒤 GitHub `main` 반영 시 RESOLVED |
| `OPS-PILOT-002` | P1 | OPEN | 실제 hosting·release·rollback 서비스와 자동화가 미선정 | `DEFERRED_USER_DECISION`; 서비스 선정 후 provider-specific 운영 전환 Task에서 해결 |
| `OPS-PILOT-003` | P1 | RESOLVED | Production이 SPA/API client를 하나로 사용 | 분리 identifier와 build/preflight/startup fail-closed 적용 |
| `OPS-PILOT-004` | P1 | OPEN | 실제 domain·managed DB·restore·SIEM·WAF 미선정/미검증 | `DEFERRED_USER_DECISION`; 서비스 선정과 실제 증빙 후 해결 |
| `OPS-PILOT-MIGRATION-001` | P1 | RESOLVED | startup과 분리된 migration/ledger gate와 동시 실행 방어 부재 | one-shot entry point, advisory lock, transaction, ledger inspection과 image E2E 적용 |
| `OPS-PILOT-P2-ATTACHMENT` | P2 | BACKLOG | 첨부가 PostgreSQL `bytea`에 있어 파일럿 증가 시 DB 용량·backup 시간이 커질 수 있음 | 파일럿 용량 상한·보존기간을 측정한 뒤 object storage 정책 Task |
| `OPS-PILOT-P2-FRONTEND-BUNDLE` | P2 | BACKLOG | Frontend main chunk가 500 kB를 넘음 | route/code splitting 성능 Task에서 해결 |
| `SEC-PUBLIC-014` | P3 | BACKLOG | 이전 scanner의 base-image Low finding 재확인 필요 | 이번 Docker Scout ARM64·AMD64는 전 심각도 0. 최종 provider architecture와 scanner 확정 뒤 다시 검사 |

## 9. 보안·개인정보

- 실제 tenant/client ID, email, token, password, connection string, certificate와 업무 원문을 문서·Git에 기록하지 않았다.
- 검증에는 synthetic identifier와 disposable DB만 사용했다.
- Production image는 non-root/read-only/cap-drop 계약을 유지한다.
- External Teams/Mail actual provider를 호출하지 않았다.
- 테스트가 재생성한 tracked screenshot 109개와 untracked screenshot 1개는 이번 실행 소유 산출물로 확인한 뒤 원상복구/삭제했고 게시 범위에 포함하지 않았다.

## 10. Rollback과 복구

- 사용자 검수 전에는 이 branch commit을 폐기하면 된다.
- merge 후 application rollback은 이전 verified image로 전환하되 migration을 되돌리지 않는다.
- migration 실패 시 새 application을 시작하지 않고 원인을 수정한 additive migration으로 forward-fix한다.
- DB restore/point-in-time recovery와 traffic rollback은 managed DB/hosting 서비스가 선택된 뒤 별도 SOP로 확정한다.

## 11. 산출물 상태

- Implementation report: 이 문서
- SOP: [TASK-AZURE-PILOT-001 SOP](azure-pilot-001-sop.md)
- User manual: [TASK-AZURE-PILOT-001 User Manual](azure-pilot-001-user-manual.md)
- Roadmap update: [Product Roadmap](../docs/00-product-roadmap.md#task-azure-pilot-001-서비스-중립-공개-파일럿-준비)
- User validation checklist: [Checklist](azure-pilot-001-user-validation-checklist.md)

상태는 `자동 검증 완료 / 사용자 검수 대기 / Draft PR 대상 / 실제 공개 배포 NO_GO_EXTERNAL`이다.
