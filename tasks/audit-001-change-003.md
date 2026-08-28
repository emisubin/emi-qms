# TASK-AUDIT-001 Change 003 — 원격 main 병합과 Azure 공개배포 승인

- taskType: `UAT_RUNTIME`
- changeStatus: `PUBLICATION_AND_AZURE_DEPLOYMENT_APPROVED`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapSequenceMatch: true
- gateStatus: `PASS_REUSE`
- userInstructionDate: 2026-08-28
- canonicalTaskId: `TASK-AUDIT-001`
- approvalSource: `USER_EXPLICIT`
- userApprovalExact: `원격 메인에 병합하고 공개배포 해`
- sourceCommit: `1e55337870d348d69ac4a12f73c15e8c20eb1bcc`
- sourceBranch: `feat/task-audit-001-access-change-audit`
- pushApproved: true
- pullRequestApproved: true
- mergeApproved: true
- productionMigrationApproved: true
- azureDeploymentApproved: true
- persistentLocalUatApproved: false
- externalProviderSendApproved: false
- userValidationStatus: `PENDING_AFTER_DEPLOYMENT`

## 1. 승인된 게시 범위

- 승인된 `TASK-AUDIT-001` 구현과 Change 002 acceptance 보정을 원격 Task branch에 push한다.
- Ready PR의 변경 인지형 필수 `CI Gate`를 통과한 뒤 원격 `main`에 병합한다.
- 병합된 exact 40자 최신 `main` SHA로 `Azure Pilot Release (Manual)`을 실행한다.
- 변경 분류는 migration·Backend·Frontend를 모두 대상으로 해야 한다.
- additive migration `0083_global_access_change_audit.sql` 성공 뒤 Backend·Frontend revision을 교체하고 공개 security smoke를 확인한다.

## 2. 보존·제외 경계

- 기존 운영 인증, Front Door 익명 차단, Teams·메일·Web Push 활성 설정, Key Vault 참조와 기존 업무 데이터를 보존한다.
- 운영 migration은 destructive rollback을 하지 않고 additive forward-fix 원칙을 유지한다.
- 공개배포 과정에서 실제 Teams·메일·Web Push 시험 발송을 새로 만들지 않는다.
- 로컬 Persistent UAT DB·5081/5174 runtime handover는 공개 Azure release와 별개이므로 실행하지 않는다.
- 사용자 직접 검수는 배포 뒤 관리자의 bounded 실제 흐름으로 수행하며, 배포 성공만으로 검수 완료로 기록하지 않는다.

## 3. 실행 Gate

1. source/test digest와 Open P0/P1/P2 `0/0/0`, clean branch를 확인한다.
2. 승인 문서만 추가 반영해 commit·push하고 PR changed-file allowlist와 개인정보·secret 부재를 확인한다.
3. 필수 PR CI가 모두 성공한 경우에만 squash merge하고 exact 최신 `main` SHA를 확인한다.
4. main CI 상태와 Azure release input gate를 확인한 뒤 두 confirmation을 포함해 수동 release를 실행한다.
5. migration 성공 전 application revision을 바꾸지 않는다.
6. Backend·Frontend latest revision ready와 공개 health `200`, 익명 root·API `401/401`을 확인한다.
7. 실제 결과를 implementation report와 Roadmap에 privacy-safe projection으로 후속 기록한다.

## 4. Rollback

- application 또는 공개 smoke 실패 시 release workflow가 직전 immutable Backend·Frontend image로 rollback한다.
- migration `0083`은 신규 audit schema와 원장을 삭제하지 않고 보존한다. 결함은 다음 additive migration과 application forward-fix로 보정한다.
- audit append·DB 권한·capacity 장애가 포함 mutation을 막으면 배포와 해당 mutation을 중단하고 aggregate·fixed code만 수집한다.

## 5. 다음 Gate

원격 branch push → Ready PR 필수 CI → squash merge → exact latest `main` SHA 확인 → Azure migration·Backend·Frontend release → public security smoke → 사용자 직접 검수 순서로 진행한다.

## 6. 게시 전 확인

- 마지막 성공 Azure release source와 현재 원격 `main`: `7371d9e7224c3786f9b0efe3b2b88dfe9b88cd50` 일치
- 변경 분류: `cross-layer`, changed files `42`, Backend·Frontend·Full-Stack·Azure validation 대상
- 운영 범위: `deploy_backend=true`, `deploy_frontend=true`, `run_migration=true`, `fail_safe=false`
- Azure Bicep compile·Portal template·static validation·release rollback mock: `PASS`
