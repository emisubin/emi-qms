# TASK-AZURE-DEPLOY-001 Change 010 — 배포 상태 동기화와 최신 main 이미지 게시

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `migration 0069·Backend revision → DNS/TLS → Teams provider 검수`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `기존 Change 007 immutable image 게시 절차 재사용`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_DOCS_SYNC_AND_LATEST_MAIN_IMAGE_PUBLISH`
- 승인일: 2026-08-05
- 기준 원격 main: `f9523c84b72b626a53776150b525e53e51cb012b`
- Change 009 게시 상태: PR #70 merge 완료
- Azure serving image 기준: `7467122057af397dbbe14d299f5f6f63c1f90e36`
- Azure DB 기준: migration `0068 Exact`
- 공개 traffic·actual provider: 비활성 유지

## Purpose identity

- 업무 목표: Change 009의 실제 원격 merge와 Azure·DNS·Teams 상태를 canonical 문서에 맞춘 뒤, 그 문서 변경까지 포함한 최종 `main` Backend·Frontend 이미지를 ACR에 게시한다.
- Root Finding: 원격 main은 Change 009를 포함하지만 Roadmap·SOP·Implementation report·checklist는 PR merge 전 또는 Change 006/007 이전 상태를 일부 유지하고 있다. Azure serving image도 Change 009 이전 `main`을 사용한다.
- 변경 경계: 문서 상태 동기화, 문서 전용 검증, commit·push·PR·merge, 기존 `Azure Pilot Images (Manual)` workflow를 통한 최종 main SHA의 Backend·Frontend immutable image 게시와 ACR tag 확인을 포함한다.
- 보존할 불변조건: `main` 직접 수정 금지, mutable `latest` tag 금지, Azure revision·migration `0069`·Front Door·traffic·actual provider 변경 금지, secret·identifier·DNS validation token 비추적.
- 예상 산출물: Change 010 문서, 실제 상태와 일치하는 Roadmap·Implementation report·SOP·checklist, 최종 main SHA의 Backend·Frontend ACR image 2개.

## 실행 전 privacy-safe 상태

- 원격 main Change 009 merge: `PASS`
- Change 009 main SHA의 ACR tag: Backend `0`, Frontend `0`
- serving workload: Backend·Frontend·ClamAV `3/3 Running`, Change 009 이전 image
- migration job: 마지막 실행 `Succeeded`, DB `0068 Exact`
- DNS: 권한 네임서버 `3/3`, 공용 resolver `4/4`에서 validation TXT 일치
- Front Door CNAME 진단: `validated=true`
- Front Door domain/TLS: `Pending / NotStarted`
- Teams 1.0.4: 관리자 승인 요청 제출 / 승인·catalog·actual Activity 대기

## 변경 Allowlist

- `tasks/azure-deploy-001-change-010.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 완료 기준

- 문서가 PR #70 merge, Azure `0068`·현재 workload, 최신 image 미게시, DNS 정상·Front Door Pending, Teams 승인 대기를 서로 모순 없이 기록한다.
- 문서 link·heading·secret/PII·`git diff --check`와 changed-file allowlist 검증이 통과하고 코드·migration·dependency diff가 0이다.
- 문서 PR을 원격 main에 병합한 뒤 그 최종 40자리 main SHA로 수동 image workflow를 성공시킨다.
- ACR에 Backend·Frontend의 최종 main SHA tag가 각각 1개 존재하고 digest 형식이 유효하다.
- Container Apps revision, DB migration, Front Door, public traffic과 actual provider는 변경하지 않는다.
