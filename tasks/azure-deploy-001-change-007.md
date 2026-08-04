# TASK-AZURE-DEPLOY-001 Change 007 — 문서 상태 동기화와 최신 main 이미지 게시

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `최신 main image 게시 → migration 0068·revision handover → Edge·TLS·provider 검수`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_SYNC_DOCS_AND_PUBLISH_LATEST_MAIN_IMAGES`
- 승인일: 2026-08-05
- 기준 branch: `origin/main`
- 기준 SHA: `496b88b793c0514fefdc3ee7a09c252201b8eda9`
- 작업 branch: `fix/task-azure-deploy-001-doc-state-sync`
- ACR image push: `승인`
- Container Apps revision 교체: `제외`
- migration `0068` 실행: `제외`
- public traffic·actual provider 발송: `제외`

## Purpose identity

- 업무 목표: Change 006의 실제 원격 병합 상태를 canonical 문서와 동기화한 뒤, 동기화 문서가 포함된 최신 원격 `main`의 Backend·Frontend 이미지를 ACR에 immutable SHA tag와 digest로 게시한다.
- Root Finding: Roadmap·SOP·Implementation report·사용자 검수 checklist가 Change 006를 로컬 검증·게시 대기로 표시하지만 PR #68과 원격 `main` 병합·CI는 완료됐고, Azure와 ACR에는 해당 최신 `main` 이미지가 아직 없다.
- 변경·실행 경계: 문서 상태 동기화, 문서 PR의 CI·main 병합, GitHub `Azure Pilot Images (Manual)` 실행과 ACR의 Backend·Frontend SHA tag 존재 확인만 포함한다.
- 보존할 불변조건: `main` 직접 push 금지, source SHA의 `main` 포함 검증, OIDC·Environment gate, mutable `latest` 금지, workload·DB·Edge·traffic·provider 무변경, privacy-safe aggregate 증빙.

## 실행 전 상태

- 원격 `main`: Change 006 PR #68 squash merge와 main CI 성공
- Azure Container Apps: 기존 Change 005 revision 3/3 ready, 최신 `main` image 적용 0/3
- ACR: Backend·Frontend repository 2개, 최신 `main` SHA tag 0/2
- manual job: bootstrap·migration 마지막 실행 성공, 최신 `main` image 적용 0/2
- DB: Azure `67 Exact`, Repository expected `0068`
- DNS: CNAME·TXT와 Azure validation token 일치
- Front Door: custom domain validation `Pending`, route·TLS deployment `NotStarted`
- public traffic·external notification: 비활성

## 변경 Allowlist

- `docs/00-product-roadmap.md`
- `tasks/azure-deploy-001-change-007.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`

## 완료 기준

1. 문서 네 곳이 Change 006 원격 병합 완료와 최신 Azure image 미게시 상태를 일치하게 표시한다.
2. 문서 변경만 존재하고 코드·migration·dependency·runtime 설정 변경은 0건이다.
3. 문서 검증·PR CI·원격 `main` 병합이 성공한다.
4. 병합으로 생성된 최신 원격 `main` full SHA를 수동 image workflow의 `source_sha`로 사용한다.
5. workflow가 성공하고 ACR의 Backend·Frontend repository 각각에 source SHA tag가 존재한다.
6. Container Apps revision, migration `0068`, Edge, public traffic과 actual provider는 변경되지 않는다.
