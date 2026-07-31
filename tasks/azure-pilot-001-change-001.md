# TASK-AZURE-PILOT-001 Change 001 — 서비스 중립 공개 파일럿 준비

## 승인과 분류

- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- implementationApproved: `true`
- approvalSource: `USER_EXPLICIT_P1_REMEDIATION_APPROVAL`
- 승인일: 2026-07-31

## 포함 범위

1. 아직 GitHub `main`에 없는 승인 완료 제품 commit을 포함하는 배포 후보 branch를 만든다.
2. Entra SPA와 API app registration identifier를 Production에서도 분리하고 잘못된 단일 app 설정을 시작 전에 거절한다.
3. 앱 시작과 분리된 one-shot migration 실행·ledger 검증 경로를 만든다.
4. 특정 hosting을 선택하기 전에도 실행 가능한 Production Compose·image·설정 사전점검을 보강한다.

## 제외 범위

- 사용자 검수 전 실제 사용자 traffic 전환
- 실제 Teams·메일 provider 발송
- 기존 Development UAT·experiment DB·runtime 변경
- 기존 PostgreSQL `bytea` 첨부를 Blob Storage로 이관하는 P2 capacity 개선
- Azure hosting·managed DB·WAF·SIEM 서비스 선정, IaC와 실제 cloud mutation
- GitHub Actions OIDC와 provider별 release·rollback workflow
- `main` merge와 branch 정리

## 완료 기준

- `OPS-PILOT-003`과 독립 migration P1을 실제 근거로 `RESOLVED`한다. `OPS-PILOT-001`은 Draft PR 게시 후보를 만들되 GitHub `main` merge 전까지 `OPEN`을 유지한다. `OPS-PILOT-002`와 `OPS-PILOT-004`는 사용자의 서비스 선정 보류 결정과 후속 Gate를 기록한다.
- Backend·Frontend 전체 회귀, isolated Full-Stack E2E, Production Compose·image 검증과 migration fresh/existing apply가 통과한다.
- Fresh DB는 ledger `Exact`, 기존 DB는 `Exact` 또는 Repository가 승인한 historical-compatible 상태와 schema compatibility를 만족한다.
- hosting·managed DB·WAF·SIEM·restore·rollback 실제 검증 전에는 공개 배포 `GO`로 표시하지 않는다.
- 사용자 검수 대기 중 PR은 Draft로 유지한다.
